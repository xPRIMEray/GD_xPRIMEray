#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Godot;
using RendererCore.Fields;

/// <summary>
/// FIX-P1a qualification fixture: transport-relevant field-source content changes must invalidate
/// active Formal acquisitions and refuse reuse of a sealed acquisition, even when the process-local
/// field_source_epoch does not change. Drives the production Workbench lifecycle (FilmController
/// set_mode, the Formal pump, the contact-replay seal gate, region refinement and selected-probe
/// transport). Mutates only this throwaway process's in-memory scene and restores every value.
/// Args (user): --output=&lt;dir&gt;
/// </summary>
public partial class FieldContentInvalidationHeadless : Node
{
	private const int TimeoutFrames = 6000;
	// INSPECT-R0 regression references (output/inspect_r0_provenance_20261122_135222/data/cases.json).
	private const string R0BaselineQuery = "954b29ad2cac80cf1dedbb83cb693c9a0d5960f70c2971209cb2c50b4ef116d7";
	private const string R0EdgeAfterQuery = "942257ac";

	private GrinFilmCamera _film = null!;
	private Node _controller = null!;
	private FieldSource3D _source = null!;
	private string _outDir = string.Empty;
	private int _failures;
	private readonly StringBuilder _json = new();
	private bool _firstJson = true;
	private string _content0 = string.Empty;
	private long _epoch0;
	private string _baselineQuery = string.Empty;

	private sealed record FormalResult(
		string State,
		string Reason,
		bool ProbeViewAvailable,
		string Query,
		string FrozenContent,
		string SealedContent,
		long EpochAtEnd,
		bool Mutated,
		int ProcessedAtMutation,
		string ContentBeforeMutation,
		string ContentAfterMutation,
		long EpochBeforeMutation,
		long EpochAfterMutation);

	public override void _Ready()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--output=", StringComparison.Ordinal))
				_outDir = arg.Substring("--output=".Length);
		}
		Run();
	}

	private async void Run()
	{
		try
		{
			await RunCore();
		}
		catch (Exception exception)
		{
			Fail($"exception {exception.GetType().Name}: {exception.Message}");
		}
		Finish();
	}

	private async Task RunCore()
	{
		if (string.IsNullOrEmpty(_outDir))
			throw new InvalidOperationException("--output=<dir> required");
		DirAccess.MakeDirRecursiveAbsolute(_outDir);
		Node root = GD.Load<PackedScene>("res://ObservatoryWorkbench.tscn").Instantiate();
		AddChild(root);
		await Frames(30);
		Node chamber = root.GetNode("PlayableWorld/TransportChamberWorld");
		_film = chamber.GetNode<GrinFilmCamera>("GrinFilmCamera");
		_controller = chamber.GetNode("FilmController");
		_source = chamber.GetNode<FieldSource3D>("GalleryFieldSource");
		chamber.GetNode("TransportChamberPlayer").SetPhysicsProcess(false);
		await Frames(5);

		_content0 = _film.CurrentFieldSourceContentSha256;
		_epoch0 = _film.CurrentFieldSourceEpoch;
		Info($"initial content={_content0} epoch={_epoch0} edge={_source.CanonicalEdgeSoftness:R}");
		Expect(_content0.Length == 64, "initial field content digest available");
		Expect(_film.CurrentFieldSourceContentSha256 == _content0, "content digest is stable under repeated evaluation");

		// ---- 1. Unchanged field content: acquisition completes; frozen = sealed = current; canonical evidence.
		FormalResult baseline = await RunFormal("baseline", null, null);
		_baselineQuery = baseline.Query;
		Expect(baseline.State == "complete" && baseline.ProbeViewAvailable, "baseline Formal sealed complete");
		Expect(baseline.FrozenContent == _content0 && baseline.SealedContent == _content0, "baseline frozen and sealed content = scene content");
		Expect(baseline.Query == R0BaselineQuery, $"baseline query dataset = INSPECT-R0 baseline {R0BaselineQuery[..8]} (got {Short(baseline.Query)})");
		Expect(baseline.EpochAtEnd == _epoch0, "baseline epoch unchanged");

		// ---- 4. Sealed reuse. Refusals first (they leave the sealed regions intact); the positive control
		// runs last because an applied refinement rewrites the region labels it selects from.
		// Post-seal change: epoch does NOT change (the P1 gap, FIX-P1b), content does; refinement refused.
		float edge0 = _source.CanonicalEdgeSoftness;
		_source.CanonicalEdgeSoftness = 0.5f;
		await Frames(3);
		Record("post_seal_edge_change", new Dictionary<string, string>
		{
			["epoch_before"] = _epoch0.ToString(), ["epoch_after"] = _film.CurrentFieldSourceEpoch.ToString(),
			["content_sealed"] = _film.SealedFrameFieldSourceContentSha256, ["content_now"] = _film.CurrentFieldSourceContentSha256,
			["lifecycle"] = _film.CathedralSnapshotLifecycleStateName, ["probe_view_available"] = _film.ProbeViewAvailable.ToString()
		});
		Expect(_film.CurrentFieldSourceEpoch == _epoch0, "post-seal CanonicalEdgeSoftness change leaves field_source_epoch unchanged (documented P1b gap)");
		Expect(_film.CurrentFieldSourceContentSha256 != _film.SealedFrameFieldSourceContentSha256, "post-seal CanonicalEdgeSoftness change is detected by the content digest");
		Expect(_film.CathedralSnapshotLifecycleStateName == "complete" && _film.ProbeViewAvailable, "sealed plate itself is retained (P3: no post-seal relabel in this fix)");
		string refused = await RefineAndWait("reuse_after_change", null);
		Expect(refused == "refused:field_source_content_mismatch", $"refinement after a field-content change is refused (got '{refused}')");
		Expect(OwnerName() == "Idle", "refused refinement holds no acquisition ownership");

		// Restoring equivalent content re-enables reuse (content identity, not history).
		_source.CanonicalEdgeSoftness = edge0;
		await Frames(3);
		Expect(_film.CurrentFieldSourceContentSha256 == _content0, "restoring CanonicalEdgeSoftness restores the identical content digest");
		// Execution-time guard: accepted request, field changed before the transport pump runs.
		string pumped = await RefineAndWait("reuse_change_before_pump", () => _source.CanonicalEdgeSoftness = 0.5f);
		Expect(pumped == "accepted:field_source_content_mismatch", $"selected-probe transport refuses at execution when content changed after the request (got '{pumped}')");
		Expect(_film.LastSelectedProbeTransportStatus == "field_source_content_mismatch", $"selected transport status (got '{_film.LastSelectedProbeTransportStatus}')");
		Expect(OwnerName() == "Idle", "execution-time refusal released acquisition ownership");
		_source.CanonicalEdgeSoftness = edge0;
		await Frames(3);
		Expect(_film.CurrentFieldSourceContentSha256 == _content0, "content restored after execution-time case");
		Expect(_film.CathedralSnapshotLifecycleStateName == "complete" && _film.ProbeViewAvailable, "refused reuse left the sealed acquisition intact");
		// Positive control: equivalent (restored) content -> refinement accepted, executed and applied.
		string control = await RefineAndWait("reuse_control_equivalent_content", null);
		Expect(control == "accepted:", $"refinement with field content equal to the sealed content is accepted and completes (got '{control}')");
		Expect(_film.LastSelectedProbeTransportStatus == "completed", $"selected transport executed (got '{_film.LastSelectedProbeTransportStatus}')");
		Expect(OwnerName() == "Idle", "refinement released acquisition ownership");

		// ---- 3. Change during an active acquisition (transport phase and the contact-replay seal gate).
		FormalResult during = await RunFormal("edge_during_transport", () => _source.CanonicalEdgeSoftness = 0.5f, null);
		ExpectInvalidatedByContent(during, "CanonicalEdgeSoftness during transport");
		Expect(during.EpochAfterMutation == during.EpochBeforeMutation, "mid-capture CanonicalEdgeSoftness change leaves the epoch unchanged (so only the content check can catch it)");
		await Restore(() => _source.CanonicalEdgeSoftness = edge0);

		FormalResult gate = await RunFormal("edge_at_replay_gate", () => _source.CanonicalEdgeSoftness = 0.5f, ReplayPending);
		ExpectInvalidatedByContent(gate, "CanonicalEdgeSoftness after transport, before contact replay/seal");
		await Restore(() => _source.CanonicalEdgeSoftness = edge0);

		// Other transport-consumed parameters the epoch does not hash, and inert controls.
		await MidCaptureCase("gamma_during", () => _source.CanonicalGamma = 2.0f, () => _source.CanonicalGamma = 1.0f);
		await MidCaptureCase("beta_scale_during", () => _source.CanonicalBetaScale = 1.5f, () => _source.CanonicalBetaScale = 1.0f);
		bool overrideBeta = _source.CanonicalOverrideBetaScale;
		await MidCaptureCase("override_beta_during", () => _source.CanonicalOverrideBetaScale = !overrideBeta, () => _source.CanonicalOverrideBetaScale = overrideBeta);
		await MidCaptureCase("curve_type_during", () => _source.CurveType = FieldCurveType.Linear, () => _source.CurveType = FieldCurveType.Power);
		await MidCaptureCase("curve_a_during", () => _source.CurveA = 2.0f, () => _source.CurveA = 1.0f);
		await MidCaptureCase("sigma_during", () => _source.Sigma = 2.0f, () => _source.Sigma = 1.0f);
		await MidCaptureCase("shape_box_during", () => _source.ShapeType = FieldShapeType.BoxVolume, () => _source.ShapeType = FieldShapeType.SphereRadial);
		Vector3 extents0 = _source.BoxExtents;
		await MidCaptureCase("box_extents_during_sphere", () => _source.BoxExtents = new Vector3(3f, 3f, 3f), () => _source.BoxExtents = extents0);
		await MidCaptureCase("mode_flags_during", () => _source.ModeFlags = 1u, () => _source.ModeFlags = 0u);
		await MidCaptureCase("inner_radius_during", () => { _source.CanonicalEnableInnerRadius = true; _source.RInner = 0.5f; },
			() => { _source.CanonicalEnableInnerRadius = false; _source.RInner = 0f; });
		// Controls already covered by the epoch: they keep their existing reason.
		await MidCaptureCase("amp_during_control", () => _source.Amp = 1.5f, () => _source.Amp = 1.0f);
		Vector3 position0 = _source.Position;
		await MidCaptureCase("move_during_control", () => _source.Position = position0 + new Vector3(0f, 0f, 0.25f), () => _source.Position = position0);

		// CustomCurve edited IN PLACE (same resource reference): only a content comparison can see it.
		var curve = new Curve();
		curve.AddPoint(new Vector2(0f, 1f));
		curve.AddPoint(new Vector2(1f, 0f));
		_source.CurveType = FieldCurveType.CustomCurve;
		_source.CustomCurve = curve;
		await Frames(3);
		string curveContent = _film.CurrentFieldSourceContentSha256;
		FormalResult inPlace = await RunFormal("custom_curve_in_place_during", () => curve.SetPointValue(1, 0.5f), null);
		ExpectInvalidatedByContent(inPlace, "CustomCurve point edited in place");
		Expect(inPlace.ContentBeforeMutation == curveContent && inPlace.ContentAfterMutation != curveContent, "in-place curve edit changes the content digest while the resource reference is unchanged");
		await Restore(() => { _source.CurveType = FieldCurveType.Power; _source.CustomCurve = null!; });

		// ---- 2/5. A Formal acquired WITH the changed value: completes, its evidence differs, the
		// frozen content records the change, and the epoch still does not (P1b, unchanged here).
		_source.CanonicalEdgeSoftness = 0.5f;
		await Frames(3);
		string contentEdge = _film.CurrentFieldSourceContentSha256;
		FormalResult edgeSealed = await RunFormal("edge_sealed_with_change", null, null);
		Expect(edgeSealed.State == "complete" && edgeSealed.ProbeViewAvailable, "Formal acquired with CanonicalEdgeSoftness=0.5 completes");
		Expect(edgeSealed.FrozenContent == contentEdge && contentEdge != _content0, "its frozen content is the changed content");
		Expect(edgeSealed.Query.StartsWith(R0EdgeAfterQuery, StringComparison.Ordinal), $"its query dataset = INSPECT-R0 edge_after {R0EdgeAfterQuery} (got {Short(edgeSealed.Query)})");
		Expect(edgeSealed.EpochAtEnd == _epoch0, "its field_source_epoch equals the baseline epoch (documented P1b attribution gap)");
		await Restore(() => _source.CanonicalEdgeSoftness = edge0);

		FormalResult restored = await RunFormal("restored_baseline", null, null);
		Expect(restored.State == "complete" && restored.Query == _baselineQuery && restored.FrozenContent == _content0,
			$"restored field reproduces the baseline acquisition (query {Short(restored.Query)}, content equal {restored.FrozenContent == _content0})");

		Info($"DETERMINISM content0={_content0} content_edge={contentEdge} content_curve={curveContent} baseline_query={_baselineQuery} edge_query={edgeSealed.Query}");
		WriteJson();
	}

	private async Task MidCaptureCase(string name, Action mutate, Action restore)
	{
		FormalResult r = await RunFormal(name, mutate, null);
		bool contentChanged = r.ContentAfterMutation != r.ContentBeforeMutation;
		bool epochChanged = r.EpochAfterMutation != r.EpochBeforeMutation;
		if (!r.Mutated)
		{
			Fail($"{name}: acquisition ended before the mutation point");
		}
		else if (epochChanged)
		{
			Expect(r.State == "invalidated" && r.Reason == "field_source_epoch_changed" && !r.ProbeViewAvailable,
				$"{name}: epoch-covered change keeps its existing reason (got {r.State}/{r.Reason})");
		}
		else if (contentChanged)
		{
			ExpectInvalidatedByContent(r, name);
		}
		else
		{
			Expect(r.State == "complete" && r.ProbeViewAvailable && r.Query == _baselineQuery,
				$"{name}: transport-inert change (content digest unchanged) completes with baseline evidence (got {r.State}/{r.Reason} {Short(r.Query)})");
		}
		await Restore(restore);
	}

	private void ExpectInvalidatedByContent(FormalResult r, string what)
	{
		Expect(r.Mutated, $"{what}: mutation applied during the acquisition");
		Expect(r.ContentAfterMutation != r.ContentBeforeMutation, $"{what}: content digest changed");
		Expect(r.State == "invalidated" && r.Reason == "field_source_content_changed",
			$"{what}: acquisition invalidated with field_source_content_changed (got {r.State}/{r.Reason})");
		Expect(!r.ProbeViewAvailable, $"{what}: no sealed plate is presented as current");
	}

	private async Task Restore(Action restore)
	{
		restore();
		await Frames(3);
		Expect(_film.CurrentFieldSourceContentSha256 == _content0, "field content restored to the initial digest");
		Expect(_film.CurrentFieldSourceEpoch == _epoch0, "field epoch restored");
	}

	private bool ReplayPending() =>
		_film.CathedralSnapshotProcessedPixelCount == _film.CathedralSnapshotTotalPixelCount &&
		_film.CathedralSnapshotTotalPixelCount > 0 &&
		ReadPrivate<bool>("_cathedralContactReplayPending");

	// Runs at the start of every physics tick. This node is the Workbench's parent, so in each tick it
	// runs before GrinFilmCamera._PhysicsProcess (which pumps the Formal acquisition and the contact
	// replay/seal). Several physics ticks can run per rendered frame, so a per-frame hook cannot reliably
	// land between the end of transport and the seal.
	private Action? _physicsHook;

	public override void _PhysicsProcess(double delta) => _physicsHook?.Invoke();

	private async Task<FormalResult> RunFormal(string name, Action? mutate, Func<bool>? trigger)
	{
		_controller.Call("set_mode", 0);
		await Frames(5);
		_controller.Call("set_mode", 1);
		await Frames(2);
		bool mutated = false;
		int processedAt = -1;
		string before = string.Empty, after = string.Empty;
		long epochBefore = 0, epochAfter = 0;
		if (mutate != null)
		{
			_physicsHook = () =>
			{
				if (mutated || !_film.CathedralSnapshotLifecycleActive)
					return;
				int processed = _film.CathedralSnapshotProcessedPixelCount;
				int total = _film.CathedralSnapshotTotalPixelCount;
				bool fire = trigger != null ? trigger() : processed > 0 && processed < total;
				if (!fire)
					return;
				before = _film.CurrentFieldSourceContentSha256;
				epochBefore = _film.CurrentFieldSourceEpoch;
				mutate();
				after = _film.CurrentFieldSourceContentSha256;
				epochAfter = _film.CurrentFieldSourceEpoch;
				mutated = true;
				processedAt = processed;
			};
		}
		try
		{
			for (int i = 0; i < TimeoutFrames; i++)
			{
				await Frames(1);
				if (!_film.CathedralSnapshotLifecycleActive && i > 3)
					break;
			}
		}
		finally
		{
			_physicsHook = null;
		}
		var r = new FormalResult(
			_film.CathedralSnapshotLifecycleStateName,
			_film.CathedralSnapshotLifecycleReasonName,
			_film.ProbeViewAvailable,
			_film.CathedralProbeQueryDatasetSha256,
			_film.CathedralFrozenFieldSourceContentSha256,
			_film.SealedFrameFieldSourceContentSha256,
			_film.CurrentFieldSourceEpoch,
			mutated,
			processedAt,
			before,
			after,
			epochBefore,
			epochAfter);
		Record(name, new Dictionary<string, string>
		{
			["lifecycle"] = r.State, ["reason"] = r.Reason, ["probe_view_available"] = r.ProbeViewAvailable.ToString(),
			["query_sha"] = r.Query, ["frozen_content"] = r.FrozenContent, ["sealed_frame_content"] = r.SealedContent,
			["epoch_at_end"] = r.EpochAtEnd.ToString(), ["mutated"] = r.Mutated.ToString(), ["processed_at_mutation"] = processedAt.ToString(),
			["total_pixels"] = _film.CathedralSnapshotTotalPixelCount.ToString(),
			["content_before_mutation"] = before, ["content_after_mutation"] = after,
			["epoch_before_mutation"] = epochBefore.ToString(), ["epoch_after_mutation"] = epochAfter.ToString()
		});
		return r;
	}

	/// <summary>Requests refinement of the smallest refinable region; optional action runs right after acceptance.</summary>
	private async Task<string> RefineAndWait(string name, Action? afterAccept)
	{
		ushort[] labels = ReadPrivate<ushort[]>("_regionLabels");
		int total = _film.CathedralSnapshotTotalPixelCount;
		var counts = new SortedDictionary<ushort, int>();
		for (int i = 0; i < total && i < labels.Length; i++)
		{
			if (labels[i] != 0)
				counts[labels[i]] = counts.TryGetValue(labels[i], out int c) ? c + 1 : 1;
		}
		var ordered = new List<KeyValuePair<ushort, int>>(counts);
		ordered.Sort((a, b) => a.Value != b.Value ? a.Value.CompareTo(b.Value) : a.Key.CompareTo(b.Key));
		string outcome = "refused:no_region";
		foreach ((ushort regionId, int pixels) in ordered)
		{
			bool accepted = _film.TryRequestCathedralProbeRegionRefinement(regionId, 1, out ProbeRefinementSummary summary);
			if (!accepted && (summary.FailureReason == "selected_pixel_capacity_exceeded" || summary.FailureReason == "requested_refinement_level_invalid"))
				continue;
			if (!accepted)
			{
				outcome = $"refused:{summary.FailureReason}";
				break;
			}
			afterAccept?.Invoke();
			for (int i = 0; i < TimeoutFrames && ReadPrivateObject("_activeRegionProbeRefinementRequest") != null; i++)
				await Frames(1);
			outcome = $"accepted:{_film.LastProbeRefinementFailureReason}";
			Record(name, new Dictionary<string, string>
			{
				["region_id"] = regionId.ToString(), ["region_pixels"] = pixels.ToString(), ["outcome"] = outcome,
				["selected_transport_status"] = _film.LastSelectedProbeTransportStatus, ["owner_after"] = OwnerName()
			});
			return outcome;
		}
		Record(name, new Dictionary<string, string> { ["outcome"] = outcome, ["owner_after"] = OwnerName() });
		return outcome;
	}

	private string OwnerName() => ReadPrivate<ObservationAcquisitionOwnership>("_observationAcquisition").Owner.ToString();

	private T ReadPrivate<T>(string name) => (T)(ReadPrivateObject(name) ?? throw new InvalidOperationException($"{name} is null"));

	private object? ReadPrivateObject(string name)
	{
		FieldInfo field = typeof(GrinFilmCamera).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException(typeof(GrinFilmCamera).FullName, name);
		return field.GetValue(_film);
	}

	private async Task Frames(int count)
	{
		for (int i = 0; i < count; i++)
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private static string Short(string s) => s.Length >= 8 ? s[..8] : s;

	private void Expect(bool condition, string message)
	{
		if (condition)
			GD.Print($"FIXP1A_OK {message}");
		else
			Fail(message);
	}

	private void Fail(string message)
	{
		_failures++;
		GD.Print($"FIXP1A_FAIL {message}");
	}

	private static void Info(string message) => GD.Print($"FIXP1A_INFO {message}");

	private void Record(string name, Dictionary<string, string> fields)
	{
		var line = new StringBuilder($"FIXP1A_CASE case={name}");
		foreach ((string k, string v) in fields)
			line.Append(' ').Append(k).Append('=').Append(v);
		GD.Print(line.ToString());
		_json.Append(_firstJson ? "\n" : ",\n").Append(" {\"case\": \"").Append(name).Append('"');
		foreach ((string k, string v) in fields)
			_json.Append(", \"").Append(k).Append("\": \"").Append(v.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
		_json.Append('}');
		_firstJson = false;
	}

	private void WriteJson()
	{
		using FileAccess? f = FileAccess.Open(_outDir.PathJoin("fix_p1a_cases.json"), FileAccess.ModeFlags.Write);
		f?.StoreString("[" + _json + "\n]\n");
	}

	private void Finish()
	{
		if (_film != null && GodotObject.IsInstanceValid(_film))
		{
			Info($"shutdown_drain {_film.DrainManagedFinalizersForTesting()}");
			Info($"shutdown_drain {_film.DrainManagedFinalizersForTesting()}");
		}
		GD.Print(_failures == 0 ? "FIXP1A PASS" : $"FIXP1A FAILED failures={_failures}");
		GetTree().Quit(_failures == 0 ? 0 : 1);
	}
}

#nullable restore
