using Godot;
using System.Collections.Generic;

namespace RendererCore.Validation;

// Passive validation records only. ReferenceTransportOracle outputs are never
// consumed by rendering, scheduling, hit selection, shading, resolver decisions,
// traversal order, or adaptive precision.
public sealed class ReferenceTransportOracle
{
	public const string DiagnosticOnlyGuardrail =
		"ReferenceTransportOracle computes best-known renderer-reference transport paths for validation only.";
}

public sealed record OracleIntegrationSettings
{
	public float OracleStepLength { get; init; } = 0.0015625f;
	public float Tolerance { get; init; } = 0.0001f;
	public int MaxSteps { get; init; } = 65536;
	public int ReplayCount { get; init; } = 2;
	public bool AdaptiveLocalRefinement { get; init; } = true;
	public bool TrajectoryFamilySamples { get; init; } = true;
}

public sealed record OracleSampleRequest
{
	public string SampleId { get; init; } = "";
	public string RoiId { get; init; } = "";
	public int PixelX { get; init; }
	public int PixelY { get; init; }
	public string Source { get; init; } = "manual_roi";
}

public sealed record ParentTrajectoryRecord
{
	public string SampleId { get; init; } = "";
	public int PixelX { get; init; }
	public int PixelY { get; init; }
	public float OracleStepLength { get; init; }
	public bool Hit { get; init; }
	public ulong ColliderId { get; init; }
	public int DomainId { get; init; } = -1;
	public Vector3 HitNormal { get; init; } = Vector3.Up;
	public float HitDistance { get; init; } = -1f;
	public float PathLength { get; init; }
	public int StepCount { get; init; }
	public int BoundaryEventCount { get; init; }
	public int PortalEventCount { get; init; }
	public string TerminationReason { get; init; } = "";
	public List<Vector3> Polyline { get; init; } = new();
}

public sealed record OracleSegmentRecord
{
	public string SampleId { get; init; } = "";
	public int SegmentIndex { get; init; }
	public Vector3 A { get; init; }
	public Vector3 B { get; init; }
	public float TraveledB { get; init; }
	public float RadiusBound { get; init; }
	public int BoundaryEventCount { get; init; }
	public int DomainEventCount { get; init; }
	public int PortalEventCount { get; init; }
	public float CurvatureKmax { get; init; }
}

public sealed record ProductionOracleComparisonRecord
{
	public string SampleId { get; init; } = "";
	public int PixelX { get; init; }
	public int PixelY { get; init; }
	public float ProductionStepLength { get; init; }
	public float OracleStepLength { get; init; }
	public bool ColliderMatch { get; init; }
	public bool DomainMatch { get; init; }
	public float NormalAngleDelta { get; init; }
	public float HitDistanceDelta { get; init; }
	public float PathLengthDelta { get; init; }
	public int BoundaryEventDelta { get; init; }
	public int StepCountDelta { get; init; }
	public float OwnershipGraphAgreement { get; init; }
	public EpsilonStabilityClass EpsilonStabilityClass { get; init; }
	public string SecondaryTags { get; init; } = "";
	public float DecisionRisk { get; init; }
}

public sealed record TrajectoryFamilyRecord
{
	public string FamilyId { get; init; } = "";
	public string SampleId { get; init; } = "";
	public int PixelX { get; init; }
	public int PixelY { get; init; }
	public int OffsetX { get; init; }
	public int OffsetY { get; init; }
	public string FamilyClass { get; init; } = "";
	public ulong ColliderId { get; init; }
	public int BoundaryEventCount { get; init; }
	public float PathLength { get; init; }
}

public sealed record PrecisionCostCurveRecord
{
	public string SampleId { get; init; } = "";
	public float StepLength { get; init; }
	public long RuntimeMs { get; init; }
	public int StepCount { get; init; }
	public int EventCount { get; init; }
	public int RefinementCount { get; init; }
	public float GraphStability { get; init; }
	public float DecisionRisk { get; init; }
}

public enum EpsilonStabilityClass
{
	Stable = 0,
	ThresholdSnap = 1,
	Unresolved = 2,
	MultiSolution = 3
}
