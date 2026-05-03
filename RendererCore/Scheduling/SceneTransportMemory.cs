using System.Collections.Generic;

namespace RendererCore.Scheduling;

// Passive research records only. SceneTransportMemory is not consumed by render
// scheduling, hit selection, shading, resolver decisions, or adaptive precision.
public sealed class SceneTransportMemory
{
	public string SceneFingerprint { get; init; } = "";
	public List<TransportCoherenceBasinRecord> StableBasins { get; } = new();
	public List<UnstableSeamRecord> UnstableSeams { get; } = new();
	public List<RequiredPrecisionRegionRecord> RequiredPrecisionRegions { get; } = new();
	public List<LocalTransportFingerprintRecord> LocalTransportFingerprints { get; } = new();
	public string DiagnosticOnlyGuardrail { get; init; } =
		"SceneTransportMemory is diagnostic-only and must not feed rendering until a separate future plan approves it.";
}

public sealed class TransportCoherenceBasinRecord
{
	public string BasinId { get; init; } = "";
	public string ObjectId { get; init; } = "";
	public int CenterX { get; init; }
	public int CenterY { get; init; }
	public double LocalCoherenceScore { get; init; }
	public double TransportEntropy { get; init; }
	public string StabilityClass { get; init; } = "";
	public string RecommendedStepLength { get; init; } = "";
	public string PrecisionFloor { get; init; } = "";
	public double StabilityConfidence { get; init; }
	public string RevisitFrequencyRecommendation { get; init; } = "";
}

public sealed class UnstableSeamRecord
{
	public string SeamId { get; init; } = "";
	public string ObjectId { get; init; } = "";
	public int CenterX { get; init; }
	public int CenterY { get; init; }
	public string SeamClass { get; init; } = "";
	public double TransportDivergenceScore { get; init; }
	public int ManifoldFragmentationCount { get; init; }
	public string PrecisionFloor { get; init; } = "";
}

public sealed class RequiredPrecisionRegionRecord
{
	public string RegionId { get; init; } = "";
	public string ObjectId { get; init; } = "";
	public int CenterX { get; init; }
	public int CenterY { get; init; }
	public string RequiredStepLength { get; init; } = "";
	public string PrecisionFloor { get; init; } = "";
	public double Confidence { get; init; }
}

public sealed class LocalTransportFingerprintRecord
{
	public string FingerprintId { get; init; } = "";
	public string ObjectId { get; init; } = "";
	public int PixelX { get; init; }
	public int PixelY { get; init; }
	public string TransportSignature { get; init; } = "";
	public double DecisionRisk { get; init; }
	public double LocalCoherenceScore { get; init; }
}
