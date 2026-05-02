using Godot;
using RendererCore.SceneSnapshot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SceneSnapshotModel = RendererCore.SceneSnapshot.SceneSnapshot;
using NumericsVector3 = System.Numerics.Vector3;

namespace RendererCore.Scheduling;

public enum TransportAnchorMode
{
	Centroid = 0,
	AabbCorner = 1,
	PrincipalAxis = 2,
	PortalThroatCenter = 3,
	DomainBoundarySample = 4,
	EdgeMidpoint = 5
}

public enum TransportObserverKind
{
	GeometryObject = 0,
	Domain = 1,
	Portal = 2
}

public sealed class TransportAnchor
{
	public string AnchorId { get; init; } = "";
	public string StableObjectId { get; init; } = "";
	public TransportObserverKind ObserverKind { get; init; }
	public TransportAnchorMode Mode { get; init; }
	public Vector3 WorldPosition { get; init; }
	public Vector3 WorldNormalOrAxis { get; init; } = Vector3.Zero;
	public float InfluenceRadius { get; init; } = 1.0f;
}

public sealed class CharacteristicProbeResult
{
	public string AnchorId { get; init; } = "";
	public bool Hit { get; init; }
	public bool NearMiss { get; init; }
	public float EstimatedPathLength { get; init; }
	public ulong ColliderId { get; init; }
	public int DomainId { get; init; } = -1;
	public int PortalCrossingCount { get; init; }
	public int BoundaryEventCount { get; init; }
	public float HitDistance { get; init; }
	public Vector3 Normal { get; init; } = Vector3.Up;
	public Vector2I ProjectedPixel { get; init; } = new(-1, -1);
	public int ProjectedTileId { get; init; } = -1;
}

public sealed class DecisionRisk
{
	public double ColliderMismatch { get; init; }
	public double DomainMismatch { get; init; }
	public double PortalCrossingMismatch { get; init; }
	public double HitDistanceError { get; init; }
	public double NormalAngleError { get; init; }
	public double PathDeviation { get; init; }
	public double ResolverChanges { get; init; }
	public double BoundaryEventMismatch { get; init; }
	public double Total =>
		ColliderMismatch +
		DomainMismatch +
		PortalCrossingMismatch +
		HitDistanceError +
		NormalAngleError +
		PathDeviation +
		ResolverChanges +
		BoundaryEventMismatch;
}

public sealed class StepperPrecisionProfile
{
	public string RequiredPrecisionLabel { get; init; } = "reference";
	public double Epsilon { get; init; } = 0.05;
	public double CoarseRisk { get; init; }
	public double MediumRisk { get; init; }
	public double FineRisk { get; init; }
	public double ReferenceRisk { get; init; }

	public static StepperPrecisionProfile Estimate(
		CharacteristicProbeResult coarse,
		CharacteristicProbeResult medium,
		CharacteristicProbeResult fine,
		CharacteristicProbeResult reference,
		double epsilon)
	{
		double coarseRisk = CompareToReference(coarse, reference).Total;
		double mediumRisk = CompareToReference(medium, reference).Total;
		double fineRisk = CompareToReference(fine, reference).Total;
		string label = coarseRisk <= epsilon ? "coarse" : (mediumRisk <= epsilon ? "medium" : (fineRisk <= epsilon ? "fine" : "reference"));
		return new StepperPrecisionProfile
		{
			RequiredPrecisionLabel = label,
			Epsilon = Math.Max(0.0, epsilon),
			CoarseRisk = coarseRisk,
			MediumRisk = mediumRisk,
			FineRisk = fineRisk,
			ReferenceRisk = 0.0
		};
	}

	public static DecisionRisk CompareToReference(CharacteristicProbeResult candidate, CharacteristicProbeResult reference)
	{
		float candidatePath = Math.Max(0.0f, candidate.EstimatedPathLength);
		float referencePath = Math.Max(0.0f, reference.EstimatedPathLength);
		float pathScale = Math.Max(1.0f, referencePath);
		float hitScale = Math.Max(1.0f, Math.Max(Math.Abs(reference.HitDistance), referencePath));
		float normalDot = Mathf.Clamp(candidate.Normal.Normalized().Dot(reference.Normal.Normalized()), -1.0f, 1.0f);
		return new DecisionRisk
		{
			ColliderMismatch = candidate.ColliderId == reference.ColliderId ? 0.0 : 1.0,
			DomainMismatch = candidate.DomainId == reference.DomainId ? 0.0 : 1.0,
			PortalCrossingMismatch = candidate.PortalCrossingCount == reference.PortalCrossingCount ? 0.0 : 1.0,
			HitDistanceError = Math.Abs(candidate.HitDistance - reference.HitDistance) / hitScale,
			NormalAngleError = Math.Acos(normalDot) / Math.PI,
			PathDeviation = Math.Abs(candidatePath - referencePath) / pathScale,
			ResolverChanges = 0.0,
			BoundaryEventMismatch = candidate.BoundaryEventCount == reference.BoundaryEventCount ? 0.0 : 1.0
		};
	}
}

public sealed class ObjectTransportObserver
{
	public string StableObjectId { get; init; } = "";
	public TransportObserverKind Kind { get; init; }
	public Aabb3 Bounds { get; init; }
	public Vector3 Centroid { get; init; }
	public List<TransportAnchor> AnchorSamples { get; } = new();
	public List<CharacteristicProbeResult> LastProbeResults { get; } = new();
	public StepperPrecisionProfile PrecisionProfile { get; set; } = new();
	public double DecisionRiskScore { get; set; }
	public double ConfidenceScore { get; set; } = 1.0;
	public double ConfidenceDecay { get; set; }
	public List<int> ScreenSpaceInfluenceTiles { get; } = new();
	public string CacheVersionStamp { get; set; } = "";
	public string InvalidationReason { get; set; } = "fresh";
}

public sealed class SceneTransportFingerprint
{
	public string VersionStamp { get; private set; } = "";
	public List<ObjectTransportObserver> Observers { get; } = new();

	public void Rebuild(SceneSnapshotModel snapshot, int maxObservers)
	{
		Observers.Clear();
		int geometryCount = Math.Min(Math.Max(0, maxObservers), snapshot?.Geometry?.Count ?? 0);
		for (int i = 0; i < geometryCount; i++)
		{
			Aabb3 bounds = snapshot.Geometry.WorldBounds[i];
			Vector3 center = ToGodot(bounds.Center);
			string stableId = $"geometry:{(snapshot.Geometry.GodotInstanceIds.Length > i ? snapshot.Geometry.GodotInstanceIds[i] : i)}";
			var observer = new ObjectTransportObserver
			{
				StableObjectId = stableId,
				Kind = TransportObserverKind.GeometryObject,
				Bounds = bounds,
				Centroid = center,
				CacheVersionStamp = stableId + ":" + FormatVec(center)
			};
			observer.AnchorSamples.AddRange(TransportAnchorExtractor.BuildAnchorsForBounds(stableId, TransportObserverKind.GeometryObject, bounds));
			Observers.Add(observer);
		}

		int remaining = Math.Max(0, maxObservers - Observers.Count);
		int domainCount = Math.Min(remaining, snapshot?.Fields?.Count ?? 0);
		for (int i = 0; i < domainCount; i++)
		{
			Aabb3 bounds = snapshot.Fields.WorldBounds[i];
			Vector3 center = ToGodot(bounds.Center);
			string stableId = $"domain:{i}";
			var observer = new ObjectTransportObserver
			{
				StableObjectId = stableId,
				Kind = TransportObserverKind.Domain,
				Bounds = bounds,
				Centroid = center,
				CacheVersionStamp = stableId + ":" + FormatVec(center)
			};
			observer.AnchorSamples.AddRange(TransportAnchorExtractor.BuildAnchorsForBounds(stableId, TransportObserverKind.Domain, bounds));
			Observers.Add(observer);
		}

		VersionStamp = BuildVersionStamp(Observers);
	}

	private static string BuildVersionStamp(List<ObjectTransportObserver> observers)
	{
		var hash = new HashCode();
		hash.Add(observers.Count);
		for (int i = 0; i < observers.Count; i++)
		{
			hash.Add(observers[i].StableObjectId);
			hash.Add(observers[i].CacheVersionStamp);
		}
		return hash.ToHashCode().ToString("x8", CultureInfo.InvariantCulture);
	}

	internal static Vector3 ToGodot(NumericsVector3 v) => new(v.X, v.Y, v.Z);

	private static string FormatVec(Vector3 v) => $"{v.X:0.###},{v.Y:0.###},{v.Z:0.###}";
}

public static class TransportAnchorExtractor
{
	public static IEnumerable<TransportAnchor> BuildAnchorsForBounds(string stableObjectId, TransportObserverKind kind, Aabb3 bounds)
	{
		Vector3 min = SceneTransportFingerprint.ToGodot(bounds.Min);
		Vector3 max = SceneTransportFingerprint.ToGodot(bounds.Max);
		Vector3 center = SceneTransportFingerprint.ToGodot(bounds.Center);
		Vector3 extents = SceneTransportFingerprint.ToGodot(bounds.Extents);
		float radius = Math.Max(0.001f, extents.Length() * 0.5f);

		yield return new TransportAnchor
		{
			AnchorId = $"{stableObjectId}:centroid",
			StableObjectId = stableObjectId,
			ObserverKind = kind,
			Mode = TransportAnchorMode.Centroid,
			WorldPosition = center,
			InfluenceRadius = radius
		};

		int cornerIndex = 0;
		Vector3[] corners = new Vector3[8];
		for (int ix = 0; ix <= 1; ix++)
		for (int iy = 0; iy <= 1; iy++)
		for (int iz = 0; iz <= 1; iz++)
		{
			Vector3 corner = new Vector3(ix == 0 ? min.X : max.X, iy == 0 ? min.Y : max.Y, iz == 0 ? min.Z : max.Z);
			corners[cornerIndex] = corner;
			yield return new TransportAnchor
			{
				AnchorId = $"{stableObjectId}:corner:{cornerIndex++}",
				StableObjectId = stableObjectId,
				ObserverKind = kind,
				Mode = kind == TransportObserverKind.Domain ? TransportAnchorMode.DomainBoundarySample : TransportAnchorMode.AabbCorner,
				WorldPosition = corner,
				InfluenceRadius = radius * 0.35f
			};
		}

		int edgeIndex = 0;
		int[,] edgePairs =
		{
			{0, 1}, {0, 2}, {0, 4}, {1, 3}, {1, 5}, {2, 3},
			{2, 6}, {3, 7}, {4, 5}, {4, 6}, {5, 7}, {6, 7}
		};
		for (int i = 0; i < edgePairs.GetLength(0); i++)
		{
			Vector3 midpoint = (corners[edgePairs[i, 0]] + corners[edgePairs[i, 1]]) * 0.5f;
			yield return new TransportAnchor
			{
				AnchorId = $"{stableObjectId}:edge_midpoint:{edgeIndex++}",
				StableObjectId = stableObjectId,
				ObserverKind = kind,
				Mode = kind == TransportObserverKind.Domain ? TransportAnchorMode.DomainBoundarySample : TransportAnchorMode.EdgeMidpoint,
				WorldPosition = midpoint,
				InfluenceRadius = radius * 0.3f
			};
		}

		Vector3 axis = Vector3.Right;
		if (extents.Y >= extents.X && extents.Y >= extents.Z)
			axis = Vector3.Up;
		else if (extents.Z >= extents.X && extents.Z >= extents.Y)
			axis = Vector3.Forward;
		float half = Math.Max(0.001f, Math.Max(extents.X, Math.Max(extents.Y, extents.Z)) * 0.5f);
		yield return new TransportAnchor
		{
			AnchorId = $"{stableObjectId}:principal_axis:neg",
			StableObjectId = stableObjectId,
			ObserverKind = kind,
			Mode = TransportAnchorMode.PrincipalAxis,
			WorldPosition = center - axis * half,
			WorldNormalOrAxis = -axis,
			InfluenceRadius = radius * 0.5f
		};
		yield return new TransportAnchor
		{
			AnchorId = $"{stableObjectId}:principal_axis:pos",
			StableObjectId = stableObjectId,
			ObserverKind = kind,
			Mode = TransportAnchorMode.PrincipalAxis,
			WorldPosition = center + axis * half,
			WorldNormalOrAxis = axis,
			InfluenceRadius = radius * 0.5f
		};
	}
}

public sealed class NullGeodesicProbeCache
{
	private readonly Dictionary<string, CharacteristicProbeResult> _cache = new(StringComparer.Ordinal);
	public int HitCount { get; private set; }
	public int MissCount { get; private set; }

	public void Clear()
	{
		_cache.Clear();
		HitCount = 0;
		MissCount = 0;
	}

	public bool TryGet(string key, out CharacteristicProbeResult result)
	{
		if (_cache.TryGetValue(key, out result))
		{
			HitCount++;
			return true;
		}
		MissCount++;
		return false;
	}

	public void Store(string key, CharacteristicProbeResult result)
	{
		_cache[key] = result;
	}
}

public sealed class CacheInvalidationPolicy
{
	public float SlightCameraDelta { get; init; } = 0.05f;
	public float SignificantCameraDelta { get; init; } = 0.5f;
	public double LowConfidenceThreshold { get; init; } = 0.25;

	public string ResolveReason(float cameraDelta, bool versionChanged, bool stepperChanged)
	{
		if (stepperChanged)
			return "stepper_settings_changed";
		if (versionChanged)
			return "scene_fingerprint_changed";
		if (cameraDelta >= SignificantCameraDelta)
			return "camera_moved_significantly";
		if (cameraDelta >= SlightCameraDelta)
			return "camera_moved_slightly";
		return "cache_reused";
	}

	public double DecayConfidence(double confidence, float cameraDelta, bool versionUncertain)
	{
		double decay = 1.0 / (1.0 + Math.Max(0.0f, cameraDelta));
		if (versionUncertain)
			decay *= 0.5;
		return Math.Clamp(confidence * decay, 0.0, 1.0);
	}
}

public sealed class TransportRiskField
{
	public double[] TileRisk { get; private set; } = Array.Empty<double>();
	public double[] TileConfidence { get; private set; } = Array.Empty<double>();
	public int TileCount => TileRisk.Length;

	public void Reset(int tileCount)
	{
		int count = Math.Max(0, tileCount);
		if (TileRisk.Length != count)
		{
			TileRisk = new double[count];
			TileConfidence = new double[count];
		}
		else
		{
			Array.Clear(TileRisk);
			Array.Clear(TileConfidence);
		}
	}

	public void AddInfluence(int tileId, double risk, double confidence)
	{
		if ((uint)tileId >= (uint)TileRisk.Length)
			return;
		TileRisk[tileId] = Math.Max(TileRisk[tileId], Math.Max(0.0, risk));
		TileConfidence[tileId] = Math.Max(TileConfidence[tileId], Math.Clamp(confidence, 0.0, 1.0));
	}
}

public sealed class ObjectSeededTileScheduler
{
	public sealed class Options
	{
		public int FilmWidth { get; init; }
		public int FilmHeight { get; init; }
		public int TileWidth { get; init; } = 64;
		public int BandY { get; init; }
		public int BandHeight { get; init; }
		public int Seed { get; init; } = 1337;
		public int MaxObservers { get; init; } = 128;
		public int MaxProbes { get; init; } = 512;
		public double RiskEpsilon { get; init; } = 0.05;
		public double MinSeedConfidence { get; init; } = 0.25;
	}

	public sealed class BandScheduleResult
	{
		public int[] ExecutionOrder { get; init; } = Array.Empty<int>();
		public int ObserverCount { get; init; }
		public int AnchorCount { get; init; }
		public int ProbeCount { get; init; }
		public int SeededTileCount { get; init; }
		public int FallbackTileCount { get; init; }
		public int StaleObserverFallbackCount { get; init; }
		public int CacheHits { get; init; }
		public int CacheMisses { get; init; }
		public double MaxRisk { get; init; }
		public double MaxConfidence { get; init; }
		public string VersionStamp { get; init; } = "";
	}

	private readonly SceneTransportFingerprint _fingerprint = new();
	private readonly NullGeodesicProbeCache _probeCache = new();
	private readonly CacheInvalidationPolicy _invalidationPolicy = new();
	private readonly TransportRiskField _riskField = new();
	private Vector3 _lastCameraPosition = Vector3.Zero;
	private bool _hasLastCameraPosition;
	private string _lastVersionStamp = "";
	private BandScheduleResult _lastResult = new();

	public BandScheduleResult LastResult => _lastResult;
	public IReadOnlyList<ObjectTransportObserver> Observers => _fingerprint.Observers;

	public BandScheduleResult BuildBandSchedule(SceneSnapshotModel snapshot, Camera3D camera, Options options)
	{
		int tileCount = Math.Max(1, (Math.Max(1, options.FilmWidth) + Math.Max(1, options.TileWidth) - 1) / Math.Max(1, options.TileWidth));
		_riskField.Reset(tileCount);
		_fingerprint.Rebuild(snapshot, Math.Max(0, options.MaxObservers));

		float cameraDelta = 0.0f;
		Vector3 cameraPosition = camera?.GlobalPosition ?? Vector3.Zero;
		if (_hasLastCameraPosition)
			cameraDelta = cameraPosition.DistanceTo(_lastCameraPosition);
		_lastCameraPosition = cameraPosition;
		_hasLastCameraPosition = true;

		bool versionChanged = !string.IsNullOrEmpty(_lastVersionStamp) && !string.Equals(_lastVersionStamp, _fingerprint.VersionStamp, StringComparison.Ordinal);
		string invalidationReason = _invalidationPolicy.ResolveReason(cameraDelta, versionChanged, stepperChanged: false);
		if (versionChanged)
			_probeCache.Clear();
		_lastVersionStamp = _fingerprint.VersionStamp;

		int anchorCount = 0;
		int probeCount = 0;
		int staleFallbackCount = 0;
		double maxRisk = 0.0;
		double maxConfidence = 0.0;

		foreach (ObjectTransportObserver observer in _fingerprint.Observers)
		{
			observer.InvalidationReason = invalidationReason;
			observer.ConfidenceScore = _invalidationPolicy.DecayConfidence(1.0, cameraDelta, versionChanged);
			observer.ConfidenceDecay = 1.0 - observer.ConfidenceScore;
			if (observer.ConfidenceScore < options.MinSeedConfidence)
			{
				staleFallbackCount++;
				continue;
			}

			foreach (TransportAnchor anchor in observer.AnchorSamples)
			{
				if (probeCount >= Math.Max(0, options.MaxProbes))
				{
					observer.InvalidationReason = "probe_budget_exhausted";
					break;
				}
				anchorCount++;
				string key = $"{_fingerprint.VersionStamp}:{cameraPosition}:{options.TileWidth}:{anchor.AnchorId}";
				if (!_probeCache.TryGet(key, out CharacteristicProbeResult probe))
				{
					probe = CharacteristicProbeRunner.Probe(cameraPosition, anchor, camera, options.FilmWidth, options.FilmHeight, options.TileWidth);
					_probeCache.Store(key, probe);
				}
				probeCount++;
				observer.LastProbeResults.Add(probe);
				if (probe.ProjectedTileId >= 0 && (probe.Hit || probe.NearMiss))
					observer.ScreenSpaceInfluenceTiles.Add(probe.ProjectedTileId);

				CharacteristicProbeResult reference = probe;
				observer.PrecisionProfile = StepperPrecisionProfile.Estimate(probe, probe, probe, reference, options.RiskEpsilon);
				observer.DecisionRiskScore = Math.Max(observer.DecisionRiskScore, observer.PrecisionProfile.CoarseRisk);
				maxRisk = Math.Max(maxRisk, observer.DecisionRiskScore);
				maxConfidence = Math.Max(maxConfidence, observer.ConfidenceScore);

				if (probe.ProjectedTileId >= 0)
					_riskField.AddInfluence(probe.ProjectedTileId, Math.Max(0.01, observer.DecisionRiskScore), observer.ConfidenceScore);
			}
		}

		int[] order = BuildDeterministicOrder(_riskField, options.Seed);
		int seeded = 0;
		for (int i = 0; i < _riskField.TileCount; i++)
		{
			if (_riskField.TileConfidence[i] >= options.MinSeedConfidence)
				seeded++;
		}

		_lastResult = new BandScheduleResult
		{
			ExecutionOrder = order,
			ObserverCount = _fingerprint.Observers.Count,
			AnchorCount = anchorCount,
			ProbeCount = probeCount,
			SeededTileCount = seeded,
			FallbackTileCount = Math.Max(0, tileCount - seeded),
			StaleObserverFallbackCount = staleFallbackCount,
			CacheHits = _probeCache.HitCount,
			CacheMisses = _probeCache.MissCount,
			MaxRisk = maxRisk,
			MaxConfidence = maxConfidence,
			VersionStamp = _fingerprint.VersionStamp
		};
		return _lastResult;
	}

	public string BuildDiagnosticsJson()
	{
		var sb = new StringBuilder(2048);
		sb.Append("{");
		sb.Append("\"version_stamp\":\"").Append(Escape(_lastResult.VersionStamp)).Append("\",");
		sb.Append("\"observer_count\":").Append(_lastResult.ObserverCount).Append(",");
		sb.Append("\"anchor_count\":").Append(_lastResult.AnchorCount).Append(",");
		sb.Append("\"probe_count\":").Append(_lastResult.ProbeCount).Append(",");
		sb.Append("\"seeded_tile_count\":").Append(_lastResult.SeededTileCount).Append(",");
		sb.Append("\"fallback_tile_count\":").Append(_lastResult.FallbackTileCount).Append(",");
		sb.Append("\"stale_observer_fallback_count\":").Append(_lastResult.StaleObserverFallbackCount).Append(",");
		sb.Append("\"probe_cache_hits\":").Append(_lastResult.CacheHits).Append(",");
		sb.Append("\"probe_cache_misses\":").Append(_lastResult.CacheMisses).Append(",");
		sb.Append("\"max_risk\":").Append(Float(_lastResult.MaxRisk)).Append(",");
		sb.Append("\"max_confidence\":").Append(Float(_lastResult.MaxConfidence)).Append(",");
		sb.Append("\"observers\":[");
		int take = Math.Min(32, _fingerprint.Observers.Count);
		for (int i = 0; i < take; i++)
		{
			if (i > 0) sb.Append(",");
			ObjectTransportObserver o = _fingerprint.Observers[i];
			sb.Append("{");
			sb.Append("\"stable_object_id\":\"").Append(Escape(o.StableObjectId)).Append("\",");
			sb.Append("\"kind\":\"").Append(o.Kind).Append("\",");
			sb.Append("\"anchor_samples\":").Append(o.AnchorSamples.Count).Append(",");
			sb.Append("\"last_probe_results\":").Append(o.LastProbeResults.Count).Append(",");
			sb.Append("\"required_step_precision\":\"").Append(Escape(o.PrecisionProfile.RequiredPrecisionLabel)).Append("\",");
			sb.Append("\"decision_risk\":").Append(Float(o.DecisionRiskScore)).Append(",");
			sb.Append("\"confidence\":").Append(Float(o.ConfidenceScore)).Append(",");
			sb.Append("\"confidence_decay\":").Append(Float(o.ConfidenceDecay)).Append(",");
			sb.Append("\"screen_space_influence_tiles\":").Append(o.ScreenSpaceInfluenceTiles.Count).Append(",");
			sb.Append("\"cache_version_stamp\":\"").Append(Escape(o.CacheVersionStamp)).Append("\",");
			sb.Append("\"invalidation_reason\":\"").Append(Escape(o.InvalidationReason)).Append("\"");
			sb.Append("}");
		}
		sb.Append("]}");
		return sb.ToString();
	}

	private static int[] BuildDeterministicOrder(TransportRiskField riskField, int seed)
	{
		int count = riskField.TileCount;
		var items = new List<(int index, double confidence, double risk, uint hash)>(count);
		for (int i = 0; i < count; i++)
			items.Add((i, riskField.TileConfidence[i], riskField.TileRisk[i], StableHash(seed, i)));

		items.Sort((a, b) =>
		{
			bool aSeeded = a.confidence > 0.0;
			bool bSeeded = b.confidence > 0.0;
			if (aSeeded != bSeeded) return aSeeded ? -1 : 1;
			int cmp = b.risk.CompareTo(a.risk);
			if (cmp != 0) return cmp;
			cmp = b.confidence.CompareTo(a.confidence);
			if (cmp != 0) return cmp;
			cmp = a.hash.CompareTo(b.hash);
			if (cmp != 0) return cmp;
			return a.index.CompareTo(b.index);
		});

		int[] order = new int[count];
		for (int i = 0; i < count; i++)
			order[i] = items[i].index;
		return order;
	}

	private static uint StableHash(int seed, int value)
	{
		unchecked
		{
			uint x = (uint)seed ^ 0x9e3779b9u;
			x ^= (uint)value + 0x85ebca6bu + (x << 6) + (x >> 2);
			x ^= x >> 16;
			x *= 0x7feb352du;
			x ^= x >> 15;
			x *= 0x846ca68bu;
			x ^= x >> 16;
			return x;
		}
	}

	private static string Escape(string value) => (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
	private static string Float(double value) => double.IsFinite(value) ? value.ToString("0.######", CultureInfo.InvariantCulture) : "0";
}

public static class CharacteristicProbeRunner
{
	public static CharacteristicProbeResult Probe(Vector3 cameraPosition, TransportAnchor anchor, Camera3D camera, int filmWidth, int filmHeight, int tileWidth)
	{
		Vector3 toAnchor = anchor.WorldPosition - cameraPosition;
		float pathLength = toAnchor.Length();
		bool projected = TryProject(camera, anchor.WorldPosition, filmWidth, filmHeight, out Vector2I pixel);
		int tileId = projected ? Math.Clamp(pixel.X / Math.Max(1, tileWidth), 0, Math.Max(0, (filmWidth + Math.Max(1, tileWidth) - 1) / Math.Max(1, tileWidth) - 1)) : -1;
		return new CharacteristicProbeResult
		{
			AnchorId = anchor.AnchorId,
			Hit = projected,
			NearMiss = !projected && pathLength > 0.0f,
			EstimatedPathLength = pathLength,
			ColliderId = StableColliderId(anchor.StableObjectId),
			DomainId = anchor.ObserverKind == TransportObserverKind.Domain ? StableDomainId(anchor.StableObjectId) : -1,
			PortalCrossingCount = anchor.ObserverKind == TransportObserverKind.Portal ? 1 : 0,
			BoundaryEventCount = anchor.Mode == TransportAnchorMode.DomainBoundarySample ? 1 : 0,
			HitDistance = pathLength,
			Normal = anchor.WorldNormalOrAxis == Vector3.Zero ? Vector3.Up : anchor.WorldNormalOrAxis.Normalized(),
			ProjectedPixel = pixel,
			ProjectedTileId = tileId
		};
	}

	private static bool TryProject(Camera3D camera, Vector3 worldPosition, int filmWidth, int filmHeight, out Vector2I pixel)
	{
		pixel = new Vector2I(-1, -1);
		if (camera == null || !GodotObject.IsInstanceValid(camera) || camera.IsPositionBehind(worldPosition))
			return false;
		Vector2 viewportPixel = camera.UnprojectPosition(worldPosition);
		Vector2 viewportSize = camera.GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
		if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
			return false;
		int x = Mathf.FloorToInt(viewportPixel.X / viewportSize.X * Math.Max(1, filmWidth));
		int y = Mathf.FloorToInt(viewportPixel.Y / viewportSize.Y * Math.Max(1, filmHeight));
		if (x < 0 || y < 0 || x >= filmWidth || y >= filmHeight)
			return false;
		pixel = new Vector2I(x, y);
		return true;
	}

	private static ulong StableColliderId(string stableId)
	{
		unchecked
		{
			ulong hash = 1469598103934665603UL;
			foreach (char c in stableId ?? "")
			{
				hash ^= c;
				hash *= 1099511628211UL;
			}
			return hash;
		}
	}

	private static int StableDomainId(string stableId) => (int)(StableColliderId(stableId) & 0x7fffffff);
}
