#nullable enable

using System;
using System.Reflection;
using Godot;

/// <summary>
/// Isolated regression fixture for request-owned Formal policy provenance.
/// It temporarily replaces the two catalog records with synthetic definitions
/// whose FormalPolicyId values differ, then drives the production controller
/// request and seal path. The catalog is restored before this process exits.
/// </summary>
public partial class FormalPolicyRequestOwnershipHeadless : Node
{
	private const int SettleFrames = 8;
	private const int TimeoutFrames = 6000;
	private const string AlphaPolicy = "synthetic.formal.alpha.v1";
	private const string BetaPolicy = "synthetic.formal.beta.v1";

	private enum Stage
	{
		Settling,
		WaitingForAlpha,
		WaitingForBeta,
		WaitingForMismatchInitialization,
		WaitingForMismatchSealGate,
		WaitingForUnavailableFailure,
		Finished
	}

	private Stage _stage = Stage.Settling;
	private int _frames;
	private Node? _workbench;
	private GrinFilmCamera? _film;
	private Node? _controller;
	private ProbeProfileDefinition[]? _catalog;
	private ProbeProfileDefinition[]? _originalProfiles;
	private string _alphaPolicySha256 = string.Empty;
	private string _betaPolicySha256 = string.Empty;
	private int _betaGeneration;
	private ProbeContextKey _mismatchFrozenContext;
	private ProbeContextKey _mismatchInjectedContext;

	public override void _Ready()
	{
		PackedScene? scene = GD.Load<PackedScene>("res://ObservatoryWorkbench.tscn");
		if (scene == null)
		{
			Finish(false, "Workbench scene unavailable");
			return;
		}

		_workbench = scene.Instantiate();
		AddChild(_workbench);
	}

	public override void _Process(double delta)
	{
		if (_stage == Stage.Finished)
			return;

		try
		{
			_frames++;
			switch (_stage)
			{
				case Stage.Settling when _frames >= SettleFrames:
					BeginAlphaRequest();
					break;
				case Stage.WaitingForAlpha:
					PollAlphaCompletion();
					break;
				case Stage.WaitingForBeta:
					PollBetaCompletion();
					break;
				case Stage.WaitingForMismatchInitialization:
					PollMismatchInitialization();
					break;
				case Stage.WaitingForMismatchSealGate:
					PollMismatchSealGate();
					break;
				case Stage.WaitingForUnavailableFailure:
					PollUnavailableFailure();
					break;
			}
		}
		catch (Exception exception)
		{
			Finish(false, $"{exception.GetType().Name}: {exception.Message}");
		}
	}

	private void BeginAlphaRequest()
	{
		if (_workbench == null)
			throw new InvalidOperationException("Workbench instance unavailable");

		Node chamber = _workbench.GetNode("PlayableWorld/TransportChamberWorld");
		_film = chamber.GetNode<GrinFilmCamera>("GrinFilmCamera");
		_controller = chamber.GetNode("FilmController");

		FieldInfo? profilesField = typeof(ProbeProfileCatalog).GetField(
			"Profiles",
			BindingFlags.Static | BindingFlags.NonPublic);
		_catalog = profilesField?.GetValue(null) as ProbeProfileDefinition[];
		if (_catalog == null || _catalog.Length != 2)
			throw new InvalidOperationException("Probe profile catalog shape changed");

		_originalProfiles = (ProbeProfileDefinition[])_catalog.Clone();
		_catalog[0] = new ProbeProfileDefinition(
			ProbeProfileId.Cathedral,
			"synthetic-alpha",
			"Synthetic Alpha",
			"accurate",
			AlphaPolicy);
		_catalog[1] = new ProbeProfileDefinition(
			ProbeProfileId.Motion,
			"synthetic-beta",
			"Synthetic Beta",
			"cheap-motion",
			BetaPolicy);

		_film.SetProbeProfileByIndex(0);
		if (!_controller.Call("EnterFormalSnapshotFromInput").AsBool())
			throw new InvalidOperationException("Alpha Formal request rejected");
		if (_film.CathedralRequestedProbeProfileStableId != "synthetic-alpha")
			throw new InvalidOperationException("Alpha request did not freeze selected profile metadata");

		string requestedPolicy = ReadRequestedPolicy();
		if (!string.Equals(requestedPolicy, AlphaPolicy, StringComparison.Ordinal))
			throw new InvalidOperationException($"Alpha request policy mismatch: {requestedPolicy}");

		// This is the production P mutation seam. Selection changes immediately,
		// while the active request must continue to own Alpha provenance.
		_film.SetProbeProfileByIndex(1);
		if (_film.ProbeProfileStableId != "synthetic-beta")
			throw new InvalidOperationException("Synthetic Beta was not selected during Alpha acquisition");
		if (!string.Equals(ReadRequestedPolicy(), AlphaPolicy, StringComparison.Ordinal))
			throw new InvalidOperationException("P relabeled the active Alpha request");

		_frames = 0;
		_stage = Stage.WaitingForAlpha;
	}

	private void PollAlphaCompletion()
	{
		if (_film == null || _controller == null)
			throw new InvalidOperationException("Alpha fixture ownership unavailable");
		if (!_film.ProbeViewAvailable)
		{
			if (_frames >= TimeoutFrames)
				throw new TimeoutException("Alpha acquisition did not seal");
			return;
		}

		if (_film.CathedralSealedProbeProfileStableId != "synthetic-alpha")
			throw new InvalidOperationException("Alpha sealed profile was relabeled by later P selection");
		if (_film.CathedralFormalPolicyId != AlphaPolicy)
			throw new InvalidOperationException("Alpha sealed Formal policy is not request-owned");
		_alphaPolicySha256 = _film.CathedralFormalPolicySha256;
		if (string.IsNullOrWhiteSpace(_alphaPolicySha256))
			throw new InvalidOperationException("Alpha sealed Formal policy hash unavailable");

		if (!_controller.Call("HandleProbeViewInput", false, true).AsBool())
			throw new InvalidOperationException("Beta replacement request rejected");
		if (_film.CathedralRequestedProbeProfileStableId != "synthetic-beta")
			throw new InvalidOperationException("Beta request did not freeze selected profile metadata");
		if (!string.Equals(ReadRequestedPolicy(), BetaPolicy, StringComparison.Ordinal))
			throw new InvalidOperationException("Beta request policy mismatch");

		_frames = 0;
		_stage = Stage.WaitingForBeta;
	}

	private void PollBetaCompletion()
	{
		if (_film == null)
			throw new InvalidOperationException("Beta fixture ownership unavailable");
		if (!_film.ProbeViewAvailable)
		{
			if (_frames >= TimeoutFrames)
				throw new TimeoutException("Beta acquisition did not seal");
			return;
		}

		if (_film.CathedralSealedProbeProfileStableId != "synthetic-beta")
			throw new InvalidOperationException("Beta sealed profile mismatch");
		if (_film.CathedralFormalPolicyId != BetaPolicy)
			throw new InvalidOperationException("Beta sealed Formal policy mismatch");
		_betaPolicySha256 = _film.CathedralFormalPolicySha256;
		if (string.Equals(_alphaPolicySha256, _betaPolicySha256, StringComparison.Ordinal))
			throw new InvalidOperationException("Distinct Formal policy IDs produced interchangeable policy hashes");
		_betaGeneration = _film.CathedralSnapshotGeneration;

		BeginCompletionMismatchRequest();
	}

	private void BeginCompletionMismatchRequest()
	{
		if (_film == null || _controller == null)
			throw new InvalidOperationException("Mismatch fixture ownership unavailable");

		_film.SetProbeProfileByIndex(0);
		if (!_controller.Call("HandleProbeViewInput", false, true).AsBool())
			throw new InvalidOperationException("Alpha mismatch replacement request rejected");
		if (_film.CathedralSnapshotGeneration != _betaGeneration + 1)
			throw new InvalidOperationException("Mismatch request did not acquire exactly one generation");
		if (!string.Equals(ReadRequestedPolicy(), AlphaPolicy, StringComparison.Ordinal))
			throw new InvalidOperationException("Mismatch request did not freeze Alpha policy");

		_frames = 0;
		_stage = Stage.WaitingForMismatchInitialization;
	}

	private void PollMismatchInitialization()
	{
		if (_film == null)
			throw new InvalidOperationException("Mismatch film unavailable");
		if (!ReadPrivate<bool>("_probeSnapshotLifecycleInitialized"))
		{
			if (_frames >= TimeoutFrames)
				throw new TimeoutException("Mismatch acquisition did not initialize");
			return;
		}

		_mismatchFrozenContext = ReadPrivate<ProbeContextKey>("_probeSnapshotLifecycleContextKey");
		if (!string.Equals(_mismatchFrozenContext.FormalPolicySha256, _alphaPolicySha256, StringComparison.Ordinal))
			throw new InvalidOperationException("Mismatch request did not initialize with request-owned Alpha policy hash");
		_mismatchInjectedContext = CopyWithFormalPolicySha(_mismatchFrozenContext, _betaPolicySha256);
		if (!ContextsDifferOnlyByFormalPolicy(_mismatchFrozenContext, _mismatchInjectedContext))
			throw new InvalidOperationException("Controlled mismatch changed more than Formal policy identity");
		WritePrivate("_probeSnapshotLifecycleContextKey", _mismatchInjectedContext);

		_frames = 0;
		_stage = Stage.WaitingForMismatchSealGate;
	}

	private void PollMismatchSealGate()
	{
		if (_film == null)
			throw new InvalidOperationException("Mismatch film unavailable");
		bool transportComplete = _film.CathedralSnapshotProcessedPixelCount == _film.CathedralSnapshotTotalPixelCount;
		bool replayComplete = ReadPrivate<bool>("_cathedralContactReplayComplete");
		bool summaryComplete = ReadPrivate<bool>("_probeSnapshotComplete");
		if (!transportComplete || !replayComplete || !summaryComplete)
		{
			if (_frames >= TimeoutFrames)
				throw new TimeoutException("Mismatch acquisition did not reach the final seal gate");
			return;
		}

		if (_film.ProbeViewAvailable)
			throw new InvalidOperationException("Policy-mismatched completion produced a valid seal");
		if (!_film.CathedralSnapshotLifecycleActive)
			throw new InvalidOperationException("Policy-mismatched completion failed before the final seal gate");
		ProbeContextKey currentExpected = ReadPrivate<ProbeContextKey>("_probeSnapshotLifecycleContextKey");
		if (currentExpected != _mismatchInjectedContext)
			throw new InvalidOperationException("Injected completion policy mismatch was not retained");

		// The production seal gate has now rejected the otherwise complete attempt.
		// Release this deliberately orphaned test acquisition using the existing
		// invalidation path before exercising request-time unavailable policy.
		_film.InvalidateCathedralProbeSnapshotForContextChange();
		if (_film.CathedralSnapshotLifecycleActive || _film.ProbeViewAvailable)
			throw new InvalidOperationException("Mismatch cleanup did not release authority without sealing");

		BeginUnavailablePolicyRequest();
	}

	private void BeginUnavailablePolicyRequest()
	{
		if (_film == null || _controller == null)
			throw new InvalidOperationException("Unavailable-policy fixture ownership unavailable");
		int generationBefore = _film.CathedralSnapshotGeneration;
		if (!_controller.Call("HandleProbeViewInput", false, false).AsBool())
			throw new InvalidOperationException("Unavailable-policy request setup was rejected");
		if (_film.CathedralSnapshotGeneration != generationBefore + 1)
			throw new InvalidOperationException("Unavailable-policy request advanced lifecycle incorrectly");

		WritePrivate("_requestedSnapshotFormalPolicyId", string.Empty);
		if (_film.TryBuildCurrentProbeContextKey(1, out _, out string reason) ||
			!string.Equals(reason, "request_formal_policy_unavailable", StringComparison.Ordinal))
		{
			throw new InvalidOperationException($"Unavailable policy branch not reached: {reason}");
		}

		_frames = 0;
		_stage = Stage.WaitingForUnavailableFailure;
	}

	private void PollUnavailableFailure()
	{
		if (_film == null)
			throw new InvalidOperationException("Unavailable-policy film unavailable");
		if (_film.CathedralSnapshotLifecycleActive)
		{
			if (_frames >= TimeoutFrames)
				throw new TimeoutException("Unavailable-policy request did not fail terminally");
			return;
		}

		if (!string.Equals(_film.CathedralSnapshotLifecycleStateName, "failed", StringComparison.Ordinal))
			throw new InvalidOperationException($"Unavailable policy did not fail lifecycle: {_film.CathedralSnapshotLifecycleStateName}");
		if (!string.Equals(_film.CathedralSnapshotLifecycleReasonName, "context_construction_failed", StringComparison.Ordinal))
			throw new InvalidOperationException($"Unavailable policy failure reason changed: {_film.CathedralSnapshotLifecycleReasonName}");
		if (_film.ProbeViewAvailable || !string.IsNullOrEmpty(_film.CathedralFormalPolicyId) ||
			!string.IsNullOrEmpty(_film.CathedralFormalPolicySha256))
		{
			throw new InvalidOperationException("Unavailable policy exposed sealed Formal provenance");
		}
		ObservationAcquisitionOwnership ownership = ReadPrivate<ObservationAcquisitionOwnership>("_observationAcquisition");
		if (ownership.Owner != ObservationAcquisitionOwner.Idle)
			throw new InvalidOperationException($"Unavailable policy retained acquisition owner {ownership.Owner}");

		Finish(
			true,
			$"alpha_policy={AlphaPolicy} alpha_sha={_alphaPolicySha256} " +
			$"beta_policy={BetaPolicy} beta_sha={_betaPolicySha256} " +
			"mismatch_seal=refused unavailable_reason=request_formal_policy_unavailable");
	}

	private static ProbeContextKey CopyWithFormalPolicySha(ProbeContextKey source, string policySha256)
	{
		return new ProbeContextKey(
			source.ObserverId,
			source.CameraOriginHash,
			source.CameraBasisHash,
			source.FovDeg,
			source.FilmWidth,
			source.FilmHeight,
			source.BaseStepsPerRay,
			source.BendScale,
			source.FieldStrength,
			source.FieldSourceEpoch,
			source.GeometryEpoch,
			source.BoundaryLayerEpoch,
			source.RefinementPolicyVersion,
			policySha256);
	}

	private static bool ContextsDifferOnlyByFormalPolicy(ProbeContextKey left, ProbeContextKey right)
	{
		return left.ObserverId == right.ObserverId &&
			left.CameraOriginHash == right.CameraOriginHash &&
			left.CameraBasisHash == right.CameraBasisHash &&
			left.FovDeg.Equals(right.FovDeg) &&
			left.FilmWidth == right.FilmWidth &&
			left.FilmHeight == right.FilmHeight &&
			left.BaseStepsPerRay == right.BaseStepsPerRay &&
			left.BendScale.Equals(right.BendScale) &&
			left.FieldStrength.Equals(right.FieldStrength) &&
			left.FieldSourceEpoch == right.FieldSourceEpoch &&
			left.GeometryEpoch == right.GeometryEpoch &&
			left.BoundaryLayerEpoch == right.BoundaryLayerEpoch &&
			left.RefinementPolicyVersion == right.RefinementPolicyVersion &&
			!string.Equals(left.FormalPolicySha256, right.FormalPolicySha256, StringComparison.Ordinal);
	}

	private T ReadPrivate<T>(string name)
	{
		if (_film == null)
			throw new InvalidOperationException("Film unavailable");
		FieldInfo field = typeof(GrinFilmCamera).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException(typeof(GrinFilmCamera).FullName, name);
		return (T)(field.GetValue(_film) ?? throw new InvalidOperationException($"Private field {name} is null"));
	}

	private void WritePrivate<T>(string name, T value)
	{
		if (_film == null)
			throw new InvalidOperationException("Film unavailable");
		FieldInfo field = typeof(GrinFilmCamera).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException(typeof(GrinFilmCamera).FullName, name);
		field.SetValue(_film, value);
	}

	private string ReadRequestedPolicy()
	{
		if (_film == null)
			return string.Empty;
		FieldInfo? field = typeof(GrinFilmCamera).GetField(
			"_requestedSnapshotFormalPolicyId",
			BindingFlags.Instance | BindingFlags.NonPublic);
		return field?.GetValue(_film) as string ?? string.Empty;
	}

	private void Finish(bool pass, string message)
	{
		if (_catalog != null && _originalProfiles != null)
			Array.Copy(_originalProfiles, _catalog, _catalog.Length);
		_stage = Stage.Finished;
		GD.Print($"PQG FORMAL POLICY OWNERSHIP {(pass ? "PASS" : "FAIL")} {message}");
		GetTree().Quit(pass ? 0 : 1);
	}
}

#nullable restore
