using Godot;

namespace RendererCore.Common;

public enum CurvatureDomainKind
{
	Unknown = 0,
	MouthNear = 1,
	ThroatBridge = 2,
	FarWall = 3,
	TangentialFar = 4,
	Background = 5,
	BoundaryMixed = 6
}

public struct DomainSignature
{
	public CurvatureDomainKind Kind;
	public float Confidence;
	public float PhaseCoherence;
	public float CurvatureMagnitude;
	public float NormalDiscontinuity;
	public Vector2 BoundaryGradient;
	public float RadialScore;
	public float TangentialScore;
}

public struct PixelDomainState
{
	public DomainSignature Primary;
	public DomainSignature Secondary;
	public bool IsBoundaryPixel;
	public float BoundaryConfidence;
	public int StableFrameCount;
}
