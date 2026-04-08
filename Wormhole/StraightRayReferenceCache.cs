using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class StraightRayReferenceCache : Node
{
	public readonly struct BaselineSnapshot
	{
		public readonly bool Valid;
		public readonly Texture2D Texture;
		public readonly ulong StateHash;
		public readonly int RefreshCount;
		public readonly bool RefreshInFlight;
		public readonly string Status;

		public BaselineSnapshot(
			bool valid,
			Texture2D texture,
			ulong stateHash,
			int refreshCount,
			bool refreshInFlight,
			string status)
		{
			Valid = valid;
			Texture = texture;
			StateHash = stateHash;
			RefreshCount = refreshCount;
			RefreshInFlight = refreshInFlight;
			Status = status ?? string.Empty;
		}
	}

	private readonly struct FieldSourceState
	{
		public readonly FieldSource3D FieldSource;
		public readonly bool Enabled;

		public FieldSourceState(FieldSource3D fieldSource, bool enabled)
		{
			FieldSource = fieldSource;
			Enabled = enabled;
		}
	}

	private GrinFilmCamera _sourceFilmCamera;
	private RayBeamRenderer _rayBeamRenderer;
	private Node _scratchRoot;
	private TextureRect _hiddenFilmView;
	private ImageTexture _baselineTexture;
	private bool _refreshInFlight;
	private bool _pendingRefresh;
	private ulong _pendingStateHash;
	private ulong _completedStateHash;
	private int _refreshCount;
	private string _lastStatus = "idle";

	public void Configure(GrinFilmCamera sourceFilmCamera, RayBeamRenderer rayBeamRenderer)
	{
		_sourceFilmCamera = sourceFilmCamera;
		_rayBeamRenderer = rayBeamRenderer;
	}

	public bool HasBaselineTexture => _baselineTexture != null;
	public bool RefreshInFlight => _refreshInFlight;
	public string LastStatus => _lastStatus;

	public bool TryGetBaselineSnapshot(out BaselineSnapshot snapshot)
	{
		snapshot = new BaselineSnapshot(
			_baselineTexture != null,
			_baselineTexture,
			_completedStateHash,
			_refreshCount,
			_refreshInFlight,
			_lastStatus);
		return _baselineTexture != null;
	}

	public void RequestRefresh(ulong stateHash, float reducedResolutionScale)
	{
		_pendingRefresh = true;
		_pendingStateHash = stateHash;
		if (_refreshInFlight)
			return;

		_ = RefreshDeferredAsync(reducedResolutionScale);
	}

	private async Task RefreshDeferredAsync(float reducedResolutionScale)
	{
		if (_refreshInFlight)
			return;

		_refreshInFlight = true;
		try
		{
			while (_pendingRefresh)
			{
				_pendingRefresh = false;
				await RenderBaselineAsync(_pendingStateHash, reducedResolutionScale);
			}
		}
		finally
		{
			_refreshInFlight = false;
		}
	}

	private void EnsureScratchNodes()
	{
		if (_scratchRoot != null && GodotObject.IsInstanceValid(_scratchRoot)
			&& _hiddenFilmView != null && GodotObject.IsInstanceValid(_hiddenFilmView))
		{
			return;
		}

		if (_scratchRoot != null && GodotObject.IsInstanceValid(_scratchRoot))
		{
			_scratchRoot.QueueFree();
		}

		_scratchRoot = new Node
		{
			Name = "DualRealityBaselineScratch"
		};
		AddChild(_scratchRoot);

		_hiddenFilmView = new TextureRect
		{
			Name = "BaselineHiddenFilmView",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_scratchRoot.AddChild(_hiddenFilmView);
	}

	private static void RestoreFieldSources(IReadOnlyList<FieldSourceState> fieldStates)
	{
		for (int i = 0; i < fieldStates.Count; i++)
		{
			FieldSourceState state = fieldStates[i];
			if (state.FieldSource != null && GodotObject.IsInstanceValid(state.FieldSource))
			{
				state.FieldSource.Enabled = state.Enabled;
			}
		}
	}

	private List<FieldSourceState> DisableFieldSources()
	{
		List<FieldSourceState> states = new();
		SceneTree tree = GetTree();
		if (tree == null)
			return states;

		foreach (Node node in tree.GetNodesInGroup("field_sources"))
		{
			if (node is FieldSource3D fieldSource)
			{
				states.Add(new FieldSourceState(fieldSource, fieldSource.Enabled));
				fieldSource.Enabled = false;
			}
		}

		return states;
	}

	private GrinFilmCamera CreateBaselineCamera(float reducedResolutionScale)
	{
		if (_sourceFilmCamera == null || !GodotObject.IsInstanceValid(_sourceFilmCamera))
			return null;

		if (_sourceFilmCamera.Duplicate() is not GrinFilmCamera baselineCamera)
			return null;

		baselineCamera.Name = "StraightReferenceFilmCamera";
		baselineCamera.FilmViewPath = _hiddenFilmView != null ? _hiddenFilmView.GetPath() : new NodePath("");
		baselineCamera.FilmOverlayPath = new NodePath("");
		baselineCamera.RayBeamRendererPath = _rayBeamRenderer != null && GodotObject.IsInstanceValid(_rayBeamRenderer)
			? _rayBeamRenderer.GetPath()
			: baselineCamera.RayBeamRendererPath;
		baselineCamera.UpdateEveryFrame = false;
		baselineCamera.UpdateEveryFrameBudgetMs = 1000f;
		baselineCamera.UpdateEveryFrameMaxRowsPerStep = Math.Max(64, baselineCamera.UpdateEveryFrameMaxRowsPerStep);
		baselineCamera.RenderStepMaxMs = Math.Max(1000, baselineCamera.RenderStepMaxMs);
		baselineCamera.RenderStepMaxPixelsPerFrame = 0;
		baselineCamera.RenderStepMaxSegmentsPerFrame = 0;
		baselineCamera.EnableFramePerf = false;
		baselineCamera.RenderStepPhaseLog = false;
		baselineCamera.RenderStepBandLog = false;
		baselineCamera.DebugSnapshotLog = false;
		baselineCamera.DebugProbeLog = false;
		baselineCamera.DebugGeomPruneAuditEnabled = false;
		baselineCamera.WormholePortalSectorDiagnosticsEnabled = false;
		baselineCamera.WormholePortalSectorRepresentativeOverlapEnabled = false;
		baselineCamera.RuntimeMacroHotkeysEnabled = false;
		baselineCamera.RuntimeMacroModeSwitchingEnabled = false;
		baselineCamera.RuntimeMacroShowHudStatus = false;
		baselineCamera.FilmResolutionScale = Mathf.Clamp(reducedResolutionScale, 0.12f, baselineCamera.FilmResolutionScale);
		return baselineCamera;
	}

	private async Task RenderBaselineAsync(ulong stateHash, float reducedResolutionScale)
	{
		if (_sourceFilmCamera == null || !GodotObject.IsInstanceValid(_sourceFilmCamera))
		{
			_lastStatus = "missing_source_film";
			return;
		}

		EnsureScratchNodes();
		bool previousMainUpdateEveryFrame = _sourceFilmCamera.UpdateEveryFrame;
		List<FieldSourceState> fieldStates = DisableFieldSources();
		GrinFilmCamera baselineCamera = null;
		try
		{
			_lastStatus = "refreshing";
			_sourceFilmCamera.UpdateEveryFrame = false;

			baselineCamera = CreateBaselineCamera(reducedResolutionScale);
			if (baselineCamera == null)
			{
				_lastStatus = "duplicate_failed";
				return;
			}

			_scratchRoot.AddChild(baselineCamera);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

			baselineCamera.ResetFilmPassManual();
			bool sawProgress = false;
			bool completed = false;
			GrinFilmCamera.FilmCaptureDiagnosticsSnapshot latestDiagnostics = default;

			for (int i = 0; i < 96; i++)
			{
				baselineCamera.RenderStep();
				if (!baselineCamera.TryGetFilmCaptureDiagnosticsForTesting(out latestDiagnostics))
					continue;

				if (latestDiagnostics.RowCursor > 0)
					sawProgress = true;

				if (sawProgress && latestDiagnostics.RowCursor == 0)
				{
					completed = true;
					break;
				}
			}

			if (!completed)
			{
				_lastStatus = $"incomplete row_cursor={latestDiagnostics.RowCursor}";
				return;
			}

			if (!baselineCamera.TryCopyFilmImageForTesting(out Image image) || image == null)
			{
				_lastStatus = "missing_baseline_image";
				return;
			}

			if (_baselineTexture == null
				|| _baselineTexture.GetSize() != image.GetSize())
			{
				_baselineTexture = ImageTexture.CreateFromImage(image);
			}
			else
			{
				_baselineTexture.Update(image);
			}

			_completedStateHash = stateHash;
			_refreshCount++;
			_lastStatus = "ready";
		}
		catch (Exception ex)
		{
			_lastStatus = $"error:{ex.GetType().Name}";
			GD.PushWarning($"[DualReality] baseline_refresh_failed error={ex.GetType().Name} detail={ex.Message}");
		}
		finally
		{
			if (baselineCamera != null && GodotObject.IsInstanceValid(baselineCamera))
			{
				baselineCamera.QueueFree();
			}

			RestoreFieldSources(fieldStates);
			_sourceFilmCamera.UpdateEveryFrame = previousMainUpdateEveryFrame;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}
}
