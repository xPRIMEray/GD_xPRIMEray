#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Godot;
using RendererCore.Fields;

/// <summary>
/// FIX-P1a.1 qualification fixture: selected-probe transport must consume the exact field snapshot that
/// its content check verified, never the optional field-source cache. Drives the production Workbench
/// lifecycle (FilmController set_mode, Formal acquisition with the cache off and on, region refinement and
/// the public selected-probe transport API against the sealed context key). The stale cache is populated
/// through a real cache-on Formal acquisition, not written directly. Mutates only this throwaway process's
/// in-memory scene and GrinFilmCamera exports, and restores every value.
/// Args (user): --output=&lt;dir&gt;
/// </summary>
public partial class FieldCacheStaleHeadless : Node
{
	private const int TimeoutFrames = 6000;
	private const int PixelStride = 4;
	private const int StaleCacheIntervalFrames = 1_000_000;
	// INSPECT-R0 / FIX-P1a references (field A = canonical Workbench, field B = CanonicalEdgeSoftness 0.5).
	private const string QueryA = "954b29ad2cac80cf1dedbb83cb693c9a0d5960f70c2971209cb2c50b4ef116d7";
	private const string QueryBPrefix = "942257ac";

	private GrinFilmCamera _film = null!;
	private Node _controller = null!;
	private FieldSource3D _source = null!;
	private string _outDir = string.Empty;
	private int _failures;
	private readonly StringBuilder _json = new();
	private bool _firstJson = true;
	private string _contentA = string.Empty;
	private string _contentB = string.Empty;
	private float _edgeA;
	private int[] _pixels = Array.Empty<int>();

	private sealed record Selected(string Status, bool Completed, int CompletedCount, string ResultsSha, string ConsumedContent, double ElapsedMs, ProbeSelectedIndexResult[] Results);

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

		bool cache0 = _film.UseFieldSourceCache;
		int interval0 = _film.FieldSourceRefreshIntervalFrames;
		_edgeA = _source.CanonicalEdgeSoftness;
		_contentA = _film.CurrentFieldSourceContentSha256;
		_source.CanonicalEdgeSoftness = 0.5f;
		_contentB = _film.CurrentFieldSourceContentSha256;
		_source.CanonicalEdgeSoftness = _edgeA;
		await Frames(3);
		Info($"field A content={_contentA} edge={_edgeA:R}; field B content={_contentB} edge=0.5; cache0={cache0} interval0={interval0}");
		Expect(!cache0, "canonical Workbench runs with the field-source cache off");
		Expect(_contentA.Length == 64 && _contentB.Length == 64 && _contentA != _contentB, "fields A and B have distinct content digests");
		Expect(CacheContent() == "uncached", "field-source cache is empty before the test populates it");

		// ---- R1. Reference: sealed A, cache OFF. Selected transport over a fixed pixel set, then a region refinement.
		Expect(await Formal("ref_A_cache_off", expectState: "complete", expectQuery: QueryA, expectFrozen: _contentA), "reference Formal A (cache off)");
		int total = _film.CathedralSnapshotTotalPixelCount;
		var pixels = new List<int>();
		for (int i = 0; i < total; i += PixelStride)
			pixels.Add(i);
		_pixels = pixels.ToArray();
		Info($"selected pixel set: {_pixels.Length} of {total} (stride {PixelStride})");
		string fpA = SealedFingerprint();
		Selected refA = await DirectSelected("ref_A_selected_cache_off", null);
		Expect(refA.Status == "completed" && refA.CompletedCount == _pixels.Length, $"reference selected transport completes (got {refA.Status} {refA.CompletedCount})");
		Expect(refA.ConsumedContent == _contentA, "reference selected transport consumed field A");
		Selected refARepeat = await DirectSelected("ref_A_selected_cache_off_repeat", null);
		Expect(refARepeat.ResultsSha == refA.ResultsSha, $"selected transport is repeatable under identical state (got {Short(refARepeat.ResultsSha)} vs {Short(refA.ResultsSha)})");
		Info($"collider_id_tie_diffs ref_A_repeat_vs_ref_A={ColliderIdDiffs(refA.Results, refARepeat.Results)} of {_pixels.Length} (process-local id, excluded)");
		Expect(SealedFingerprint() == fpA, "selected transport (no refinement) leaves the sealed evidence unchanged");
		string refRefine = await RefineAndWait("ref_A_refinement_cache_off", null);
		Expect(refRefine == "accepted:", $"reference region refinement accepted and completed (got '{refRefine}')");
		string fpARefined = SealedFingerprint();
		Expect(fpARefined != fpA, "reference refinement was applied to the sealed evidence (fingerprint is sensitive)");
		Expect(CacheContent() == "uncached", "cache-off lifecycle never populated the field-source cache");

		// ---- R2. Field B. Sealed B, cache OFF: selected transport differs from A (the test can see field B).
		_source.CanonicalEdgeSoftness = 0.5f;
		await Frames(3);
		Expect(await Formal("ref_B_cache_off", expectState: "complete", expectQuery: QueryBPrefix, expectFrozen: _contentB), "reference Formal B (cache off)");
		Selected refB = await DirectSelected("ref_B_selected_cache_off", null);
		Expect(refB.Status == "completed" && refB.ConsumedContent == _contentB, $"selected transport under sealed+live B consumed B (got {refB.Status})");
		Expect(refB.ResultsSha != refA.ResultsSha, "selected-transport results under B differ from A (field B is observable in these pixels)");

		// ---- C1. Populate the cache with B through a REAL cache-on Formal acquisition (also: cache on = off for B).
		_film.UseFieldSourceCache = true;
		_film.FieldSourceRefreshIntervalFrames = interval0;
		Expect(await Formal("populate_cache_B_cache_on", expectState: "complete", expectQuery: QueryBPrefix, expectFrozen: _contentB),
			"cache-on Formal B seals the same evidence as cache-off Formal B");
		Expect(CacheContent() == _contentB, $"field-source cache now holds field B (got {Short(CacheContent())})");
		// Keep the cache from expiring for the rest of the adversarial sequence (no transform/identity change follows).
		_film.FieldSourceRefreshIntervalFrames = StaleCacheIntervalFrames;
		Selected onB = await DirectSelected("selected_B_cache_on_fresh", null);
		Expect(onB.Status == "completed" && onB.ResultsSha == refB.ResultsSha && onB.ConsumedContent == _contentB,
			"cache-on selected transport under B equals the cache-off B results");

		// ---- C2. Restore the live scene to A (parameter only: no transform/identity change), seal A with the
		// cache OFF (the cache-off path does not touch the cache, so it keeps B), then re-enable the cache.
		_film.UseFieldSourceCache = false;
		_source.CanonicalEdgeSoftness = _edgeA;
		await Frames(3);
		Expect(_film.CurrentFieldSourceContentSha256 == _contentA, "live scene restored to field A");
		Expect(await Formal("seal_A_cache_off_with_stale_cache", expectState: "complete", expectQuery: QueryA, expectFrozen: _contentA), "Formal A sealed (cache off)");
		string fpA2 = SealedFingerprint();
		Expect(fpA2 == fpA, "resealed A reproduces the reference sealed evidence");
		_film.UseFieldSourceCache = true;
		Expect(CacheContent() == _contentB, "the cache still holds stale field B");
		Expect(CachePathReturnsWithoutRefresh(out string cachePathContent) && cachePathContent == _contentB,
			$"PRECONDITION: the optional cache path would return stale B now, without refreshing (got {Short(cachePathContent)})");
		Record("adversarial_state", new Dictionary<string, string>
		{
			["sealed_content"] = _film.SealedFrameFieldSourceContentSha256, ["live_content"] = _film.CurrentFieldSourceContentSha256,
			["cache_content"] = CacheContent(), ["cache_enabled"] = _film.UseFieldSourceCache.ToString(),
			["refresh_interval"] = _film.FieldSourceRefreshIntervalFrames.ToString(), ["epoch"] = _film.CurrentFieldSourceEpoch.ToString()
		});

		// ---- A1. THE FAILURE: sealed A, live A, cache B. Execution must consume verified A, never cached B.
		Selected adv = await DirectSelected("adversarial_selected_sealedA_liveA_cacheB", null);
		Expect(adv.Status == "completed" && adv.CompletedCount == _pixels.Length, $"selected transport accepted and executed (got {adv.Status})");
		Expect(adv.ConsumedContent == _contentA, $"execution consumed the verified field A snapshot (got {Short(adv.ConsumedContent)})");
		Expect(adv.ResultsSha == refA.ResultsSha, $"results are bit-identical to the cache-off field-A results (got {Short(adv.ResultsSha)}, A {Short(refA.ResultsSha)}, B {Short(refB.ResultsSha)})");
		Expect(adv.ResultsSha != refB.ResultsSha, "results are not the field-B results");
		Info($"collider_id_tie_diffs adversarial_vs_ref_A={ColliderIdDiffs(refA.Results, adv.Results)} ref_B_vs_ref_A={ColliderIdDiffs(refA.Results, refB.Results)} (process-local id, excluded)");
		Expect(CacheContent() == _contentB, "execution neither consulted nor refreshed the cache (it still holds B)");
		Expect(SealedFingerprint() == fpA, "sealed evidence unchanged by selected transport");

		// ---- A2. Live B vs sealed A (cache on): refused at request; nothing executed or mutated.
		_source.CanonicalEdgeSoftness = 0.5f;
		await Frames(3);
		Selected liveB = await DirectSelected("refusal_liveB_sealedA_cache_on", null);
		Expect(liveB.Status == "refused:field_source_content_mismatch", $"selected request with live B against sealed A is refused (got {liveB.Status})");
		Expect(liveB.ResultsSha == SentinelSha(), "refused request left the caller's result storage untouched");
		string refusedRefine = await RefineAndWait("refusal_refinement_liveB_cache_on", null);
		Expect(refusedRefine == "refused:field_source_content_mismatch", $"region refinement with live B is refused (got '{refusedRefine}')");
		Expect(OwnerName() == "Idle", "refusal holds no acquisition ownership");
		Expect(SealedFingerprint() == fpA, "refusal left the sealed evidence unchanged");

		// ---- A3. Field changes between request and execution (cache on): refused at execution.
		_source.CanonicalEdgeSoftness = _edgeA;
		await Frames(3);
		Selected between = await DirectSelected("refusal_change_between_request_and_execution", () => _source.CanonicalEdgeSoftness = 0.5f);
		Expect(between.Status == "field_source_content_mismatch" && between.Completed && between.CompletedCount == 0, $"execution-time refusal (got {between.Status})");
		Expect(between.ResultsSha == SentinelSha(), "execution-time refusal left the caller's result storage untouched");
		Expect(SealedFingerprint() == fpA, "execution-time refusal left the sealed evidence unchanged");
		_source.CanonicalEdgeSoftness = _edgeA;
		await Frames(3);
		string betweenRefine = await RefineAndWait("refusal_refinement_change_before_pump_cache_on", () => _source.CanonicalEdgeSoftness = 0.5f);
		Expect(betweenRefine == "accepted:field_source_content_mismatch", $"region refinement refused at execution (got '{betweenRefine}')");
		Expect(OwnerName() == "Idle", "execution-time refinement refusal released ownership");
		Expect(SealedFingerprint() == fpA, "execution-time refinement refusal left the sealed evidence unchanged");
		_source.CanonicalEdgeSoftness = _edgeA;
		await Frames(3);
		Expect(_film.CurrentFieldSourceContentSha256 == _contentA && CacheContent() == _contentB, "live A restored; cache still stale B");

		// ---- A4. Unchanged A (cache on, stale B): region refinement succeeds and equals the cache-off refinement.
		string okRefine = await RefineAndWait("refinement_unchanged_A_cache_on_stale_B", null);
		Expect(okRefine == "accepted:", $"region refinement with unchanged A is accepted and completes (got '{okRefine}')");
		Expect(_film.LastSelectedProbeTransportFieldSourceContentSha256 == _contentA, "refinement transport consumed field A");
		Expect(SealedFingerprint() == fpARefined, "refined sealed evidence is bit-identical to the cache-off refinement");
		Expect(OwnerName() == "Idle", "refinement released ownership");
		Expect(CacheContent() == _contentB, "refinement did not touch the cache");

		// ---- E. Cache on vs off for equivalent effective inputs.
		// A cache-on Formal request that freezes the stale cache is invalidated (fail-closed, FIX-P1a).
		Expect(await Formal("cache_on_formal_freezes_stale_B", expectState: "invalidated", expectReason: "field_source_content_changed"),
			"cache-on Formal that froze stale B under live A is invalidated, never sealed");
		// With the cache allowed to refresh, cache-on Formal A seals exactly the cache-off evidence.
		_film.FieldSourceRefreshIntervalFrames = 1;
		Expect(await Formal("cache_on_formal_A_refreshed", expectState: "complete", expectQuery: QueryA, expectFrozen: _contentA),
			"cache-on Formal A (cache refreshed) seals the cache-off evidence");
		Expect(CacheContent() == _contentA, "cache refreshed to field A");
		Expect(SealedFingerprint() == fpA, "cache-on sealed A evidence is bit-identical to cache-off sealed A");
		Selected onA = await DirectSelected("selected_A_cache_on_fresh", null);
		Expect(onA.Status == "completed" && onA.ResultsSha == refA.ResultsSha && onA.ConsumedContent == _contentA,
			"cache-on selected transport under A equals the cache-off A results");

		// ---- Cost of the content digest (per-frame validation work added by FIX-P1a).
		MeasureDigestCost();

		_film.UseFieldSourceCache = cache0;
		_film.FieldSourceRefreshIntervalFrames = interval0;
		_source.CanonicalEdgeSoftness = _edgeA;
		await Frames(3);
		Expect(_film.CurrentFieldSourceContentSha256 == _contentA && !_film.UseFieldSourceCache, "scene and exports restored");
		Info($"DETERMINISM contentA={_contentA} contentB={_contentB} selA={refA.ResultsSha} selB={refB.ResultsSha} fpA={fpA} fpARefined={fpARefined}");
		WriteJson();
	}

	private void MeasureDigestCost()
	{
		const int n = 2000;
		for (int i = 0; i < 50; i++)
			_ = _film.CurrentFieldSourceContentSha256;
		long a0 = GC.GetAllocatedBytesForCurrentThread();
		var sw = System.Diagnostics.Stopwatch.StartNew();
		for (int i = 0; i < n; i++)
			_ = _film.CurrentFieldSourceContentSha256;
		sw.Stop();
		long a1 = GC.GetAllocatedBytesForCurrentThread();
		var swE = System.Diagnostics.Stopwatch.StartNew();
		for (int i = 0; i < n; i++)
			_ = _film.CurrentFieldSourceEpoch;
		swE.Stop();
		long a2 = GC.GetAllocatedBytesForCurrentThread();
		var nodes = GetTree().GetNodesInGroup("field_sources");
		Record("digest_cost", new Dictionary<string, string>
		{
			["iterations"] = n.ToString(), ["field_sources"] = nodes.Count.ToString(),
			["content_digest_us_per_call"] = (sw.Elapsed.TotalMilliseconds * 1000.0 / n).ToString("F2"),
			["content_digest_bytes_per_call"] = ((a1 - a0) / n).ToString(),
			["epoch_hash_us_per_call"] = (swE.Elapsed.TotalMilliseconds * 1000.0 / n).ToString("F2"),
			["epoch_hash_bytes_per_call"] = ((a2 - a1) / n).ToString()
		});
	}

	private async Task<bool> Formal(string name, string expectState, string? expectQuery = null, string? expectFrozen = null, string? expectReason = null)
	{
		_controller.Call("set_mode", 0);
		await Frames(5);
		var sw = System.Diagnostics.Stopwatch.StartNew();
		_controller.Call("set_mode", 1);
		await Frames(2);
		int frames = 2;
		for (int i = 0; i < TimeoutFrames; i++)
		{
			await Frames(1);
			frames++;
			if (!_film.CathedralSnapshotLifecycleActive && i > 3)
				break;
		}
		sw.Stop();
		string state = _film.CathedralSnapshotLifecycleStateName;
		string reason = _film.CathedralSnapshotLifecycleReasonName;
		string query = state == "complete" ? _film.CathedralProbeQueryDatasetSha256 : "(partial, not compared)";
		string frozen = _film.CathedralFrozenFieldSourceContentSha256;
		Record(name, new Dictionary<string, string>
		{
			["lifecycle"] = state, ["reason"] = reason, ["probe_view_available"] = _film.ProbeViewAvailable.ToString(),
			["query_sha"] = query, ["frozen_content"] = frozen, ["live_content"] = _film.CurrentFieldSourceContentSha256,
			["cache_enabled"] = _film.UseFieldSourceCache.ToString(), ["cache_content"] = CacheContent(),
			["frames"] = frames.ToString(), ["wall_ms"] = sw.Elapsed.TotalMilliseconds.ToString("F0")
		});
		bool ok = state == expectState;
		ok &= expectReason == null || reason == expectReason;
		ok &= expectQuery == null || query.StartsWith(expectQuery, StringComparison.Ordinal);
		ok &= expectFrozen == null || frozen == expectFrozen;
		if (expectState == "complete")
			ok &= _film.ProbeViewAvailable;
		else
			ok &= !_film.ProbeViewAvailable;
		if (!ok)
			Info($"{name}: state={state} reason={reason} query={Short(query)} frozen={Short(frozen)}");
		return ok;
	}

	/// <summary>Public selected-probe transport API against the sealed context key, over the fixed pixel set.</summary>
	private async Task<Selected> DirectSelected(string name, Action? afterAccept)
	{
		var key = (ProbeContextKey)(ReadPrivateObject("_probeFrameContextKey") ?? throw new InvalidOperationException("no sealed key"));
		MethodInfo build = typeof(GrinFilmCamera).GetMethod("BuildCathedralProbeRefinementPolicy", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException("BuildCathedralProbeRefinementPolicy");
		var policy = (ProbePolicy)build.Invoke(_film, new object[] { (byte)1, _film.CathedralSnapshotTotalPixelCount })!;
		var storage = new ProbeSelectedIndexResult[_pixels.Length];
		FillSentinel(storage);
		string before = _film.LastSelectedProbeTransportFieldSourceContentSha256;
		bool accepted = _film.TryRequestSelectedProbeTransport(_pixels, policy.RefinedStepsPerRay, policy.RefinedStepLength, in key, storage, out string reason);
		Selected result;
		if (!accepted)
		{
			result = new Selected($"refused:{reason}", false, 0, HashResults(storage), string.Empty, 0, storage);
		}
		else
		{
			afterAccept?.Invoke();
			bool completed = false; bool success = false; int count = 0; long ticks = 0; string status = string.Empty;
			for (int i = 0; i < TimeoutFrames; i++)
			{
				await Frames(1);
				_film.TryGetSelectedProbeTransportStatus(out completed, out success, out count, out ticks, out status);
				if (completed)
					break;
			}
			string consumed = status == "completed" || status == "partial" ? _film.LastSelectedProbeTransportFieldSourceContentSha256 : string.Empty;
			if (status != "completed" && status != "partial" && _film.LastSelectedProbeTransportFieldSourceContentSha256 != before)
				Fail($"{name}: a refused execution changed the consumed-content diagnostic");
			result = new Selected(status, completed, count, HashResults(storage), consumed, ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency, storage);
		}
		DumpResults(name, storage);
		Record(name, new Dictionary<string, string>
		{
			["status"] = result.Status, ["completed_count"] = result.CompletedCount.ToString(), ["pixels"] = _pixels.Length.ToString(),
			["results_sha"] = result.ResultsSha, ["consumed_content"] = result.ConsumedContent,
			["live_content"] = _film.CurrentFieldSourceContentSha256, ["sealed_content"] = _film.SealedFrameFieldSourceContentSha256,
			["cache_enabled"] = _film.UseFieldSourceCache.ToString(), ["cache_content"] = CacheContent(),
			["owner_after"] = OwnerName(), ["elapsed_ms"] = result.ElapsedMs.ToString("F1")
		});
		return result;
	}

	private void DumpResults(string name, ProbeSelectedIndexResult[] results)
	{
		var sb = new StringBuilder("pixel,outcome,surface,final_steps,numfail,found,maxsteps,stopped_early,contacts,first_contact,last_contact,geo_contact,bg_contact,collider,nx,ny,nz,normal_valid,policy_max_steps,effort_valid\n");
		foreach (ProbeSelectedIndexResult r in results)
			sb.Append(r.PixelIndex).Append(',').Append((int)r.Outcome).Append(',').Append((int)r.SurfaceClass).Append(',').Append(r.FinalStepCount).Append(',')
				.Append(r.HadNumericalFailure).Append(',').Append(r.Found).Append(',').Append(r.MaxStepsReached).Append(',').Append(r.StoppedEarly).Append(',')
				.Append(r.ContactCount).Append(',').Append(r.FirstContactStep).Append(',').Append(r.LastContactStep).Append(',')
				.Append(r.HadAnyGeometryContact).Append(',').Append(r.HadAnyBackgroundContact).Append(',').Append(r.NearestAcceptedColliderId).Append(',')
				.Append(BitConverter.SingleToInt32Bits(r.NearestAcceptedNormalX)).Append(',').Append(BitConverter.SingleToInt32Bits(r.NearestAcceptedNormalY)).Append(',')
				.Append(BitConverter.SingleToInt32Bits(r.NearestAcceptedNormalZ)).Append(',').Append(r.NormalValid).Append(',').Append(r.PolicyMaxSteps).Append(',')
				.Append(r.EffortValid).Append('\n');
		using Godot.FileAccess? f = Godot.FileAccess.Open(_outDir.PathJoin($"selected_{name}.csv"), Godot.FileAccess.ModeFlags.Write);
		f?.StoreString(sb.ToString());
	}

	private static void FillSentinel(ProbeSelectedIndexResult[] storage)
	{
		for (int i = 0; i < storage.Length; i++)
			storage[i] = new ProbeSelectedIndexResult { PixelIndex = -7, FinalStepCount = -7 };
	}

	private string? _sentinelSha;
	private string SentinelSha()
	{
		if (_sentinelSha == null)
		{
			var s = new ProbeSelectedIndexResult[_pixels.Length];
			FillSentinel(s);
			_sentinelSha = HashResults(s);
		}
		return _sentinelSha;
	}

	/// <summary>Number of pixels whose process-local NearestAcceptedColliderId differs (excluded from HashResults).</summary>
	private static int ColliderIdDiffs(ProbeSelectedIndexResult[] a, ProbeSelectedIndexResult[] b)
	{
		int n = 0;
		for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
			if (a[i].NearestAcceptedColliderId != b[i].NearestAcceptedColliderId)
				n++;
		return n;
	}

	// Every ProbeSelectedIndexResult field except NearestAcceptedColliderId, a process-local Godot object id.
	// dev3 showed that id alone varies between identical calls on a few tie pixels (same collider set, all other
	// fields equal): pre-existing physics-query tie ordering, independent of field input; recorded separately.
	private static string HashResults(ProbeSelectedIndexResult[] results)
	{
		using var ms = new MemoryStream();
		using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
		{
			foreach (ProbeSelectedIndexResult r in results)
			{
				w.Write(r.PixelIndex); w.Write((int)r.Outcome); w.Write((int)r.SurfaceClass); w.Write(r.FinalStepCount);
				w.Write(r.HadNumericalFailure); w.Write(r.Found); w.Write(r.MaxStepsReached); w.Write(r.StoppedEarly);
				w.Write(r.ContactCount); w.Write(r.FirstContactStep); w.Write(r.LastContactStep);
				w.Write(r.HadAnyGeometryContact); w.Write(r.HadAnyBackgroundContact);
				w.Write(BitConverter.SingleToInt32Bits(r.NearestAcceptedNormalX)); w.Write(BitConverter.SingleToInt32Bits(r.NearestAcceptedNormalY));
				w.Write(BitConverter.SingleToInt32Bits(r.NearestAcceptedNormalZ)); w.Write(r.NormalValid); w.Write(r.PolicyMaxSteps); w.Write(r.EffortValid);
			}
		}
		return Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
	}

	/// <summary>Digest of the sealed per-pixel evidence arrays (outcomes, refinement levels, region labels,
	/// sealed probe-view outcomes) and the sealed query dataset.</summary>
	private string SealedFingerprint()
	{
		using var ms = new MemoryStream();
		using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
		{
			foreach (string name in new[] { "_probeOutcomes", "_probeRefinLevel", "_regionLabels", "_sealedProbeViewOutcomes" })
			{
				var arr = (Array)(ReadPrivateObject(name) ?? Array.Empty<int>());
				w.Write(name); w.Write(arr.Length);
				foreach (object v in arr)
					w.Write(Convert.ToInt64(v));
			}
			w.Write(_film.CathedralProbeQueryDatasetSha256);
		}
		return Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
	}

	private string CacheContent()
	{
		if (ReadPrivate<int>("_fieldSourceLastRefreshFrame") <= -100000)
			return "uncached";
		var snaps = (RayBeamRenderer.FieldSourceSnap[])(ReadPrivateObject("_fieldSourceSnaps") ?? Array.Empty<RayBeamRenderer.FieldSourceSnap>());
		return RayBeamRenderer.ComputeFieldSourceContentSha256(snaps);
	}

	/// <summary>Reads the production cache path once (GetFieldSourceSnaps with the current effective config) and
	/// reports what it returns; fails if that read refreshed the cache (which would void the precondition).</summary>
	private bool CachePathReturnsWithoutRefresh(out string content)
	{
		MethodInfo resolve = typeof(GrinFilmCamera).GetMethod("ResolveEffectiveConfig", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException("ResolveEffectiveConfig");
		MethodInfo get = typeof(GrinFilmCamera).GetMethod("GetFieldSourceSnaps", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException("GetFieldSourceSnaps");
		object?[] rargs = { null };
		resolve.Invoke(_film, rargs);
		object?[] gargs = { rargs[0], ReadPrivate<int>("_frameIndex"), null, null };
		var snaps = (RayBeamRenderer.FieldSourceSnap[])get.Invoke(_film, gargs)!;
		content = RayBeamRenderer.ComputeFieldSourceContentSha256(snaps);
		bool refreshed = (bool)gargs[3]!;
		Info($"cache path read: content={content} refreshed={refreshed}");
		return !refreshed;
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
				["selected_transport_status"] = _film.LastSelectedProbeTransportStatus,
				["consumed_content"] = _film.LastSelectedProbeTransportFieldSourceContentSha256,
				["cache_enabled"] = _film.UseFieldSourceCache.ToString(), ["cache_content"] = CacheContent(), ["owner_after"] = OwnerName()
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
			GD.Print($"FIXP1A1_OK {message}");
		else
			Fail(message);
	}

	private void Fail(string message)
	{
		_failures++;
		GD.Print($"FIXP1A1_FAIL {message}");
	}

	private static void Info(string message) => GD.Print($"FIXP1A1_INFO {message}");

	private void Record(string name, Dictionary<string, string> fields)
	{
		var line = new StringBuilder($"FIXP1A1_CASE case={name}");
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
		using Godot.FileAccess? f = Godot.FileAccess.Open(_outDir.PathJoin("fix_p1a1_cache_cases.json"), Godot.FileAccess.ModeFlags.Write);
		f?.StoreString("[" + _json + "\n]\n");
	}

	private void Finish()
	{
		if (_film != null && GodotObject.IsInstanceValid(_film))
		{
			Info($"shutdown_drain {_film.DrainManagedFinalizersForTesting()}");
			Info($"shutdown_drain {_film.DrainManagedFinalizersForTesting()}");
		}
		GD.Print(_failures == 0 ? "FIXP1A1 PASS" : $"FIXP1A1 FAILED failures={_failures}");
		GetTree().Quit(_failures == 0 ? 0 : 1);
	}
}

#nullable restore
