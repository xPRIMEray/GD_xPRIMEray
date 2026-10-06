#nullable enable

/// <summary>
/// Exact probe-context identity after caller-side quantization.
/// </summary>
public readonly struct ProbeContextKey : System.IEquatable<ProbeContextKey>
{
	public readonly ObserverId ObserverId;
	public readonly uint CameraOriginHash;
	public readonly uint CameraBasisHash;
	public readonly float FovDeg;
	public readonly ushort FilmWidth;
	public readonly ushort FilmHeight;
	public readonly ushort BaseStepsPerRay;
	public readonly float BendScale;
	public readonly float FieldStrength;
	public readonly uint FieldSourceEpoch;
	public readonly uint GeometryEpoch;
	public readonly uint BoundaryLayerEpoch;
	public readonly byte RefinementPolicyVersion;
	public readonly string FormalPolicySha256;

	public ProbeContextKey(
		ObserverId observerId,
		uint cameraOriginHash,
		uint cameraBasisHash,
		float fovDeg,
		ushort filmWidth,
		ushort filmHeight,
		ushort baseStepsPerRay,
		float bendScale,
		float fieldStrength,
		uint fieldSourceEpoch,
		uint geometryEpoch,
		uint boundaryLayerEpoch,
		byte refinementPolicyVersion,
		string formalPolicySha256 = "")
	{
		ObserverId = observerId;
		CameraOriginHash = cameraOriginHash;
		CameraBasisHash = cameraBasisHash;
		FovDeg = fovDeg;
		FilmWidth = filmWidth;
		FilmHeight = filmHeight;
		BaseStepsPerRay = baseStepsPerRay;
		BendScale = bendScale;
		FieldStrength = fieldStrength;
		FieldSourceEpoch = fieldSourceEpoch;
		GeometryEpoch = geometryEpoch;
		BoundaryLayerEpoch = boundaryLayerEpoch;
		RefinementPolicyVersion = refinementPolicyVersion;
		FormalPolicySha256 = formalPolicySha256 ?? string.Empty;
	}

	public bool Equals(ProbeContextKey other)
	{
		return CameraOriginHash == other.CameraOriginHash
			&& ObserverId == other.ObserverId
			&& CameraBasisHash == other.CameraBasisHash
			&& FovDeg.Equals(other.FovDeg)
			&& FilmWidth == other.FilmWidth
			&& FilmHeight == other.FilmHeight
			&& BaseStepsPerRay == other.BaseStepsPerRay
			&& BendScale.Equals(other.BendScale)
			&& FieldStrength.Equals(other.FieldStrength)
			&& FieldSourceEpoch == other.FieldSourceEpoch
			&& GeometryEpoch == other.GeometryEpoch
			&& BoundaryLayerEpoch == other.BoundaryLayerEpoch
			&& RefinementPolicyVersion == other.RefinementPolicyVersion
			&& string.Equals(FormalPolicySha256, other.FormalPolicySha256, System.StringComparison.Ordinal);
	}

	public override bool Equals(object? obj)
	{
		return obj is ProbeContextKey other && Equals(other);
	}

	public override int GetHashCode()
	{
		var hash = new System.HashCode();
		hash.Add(ObserverId);
		hash.Add(CameraOriginHash);
		hash.Add(CameraBasisHash);
		hash.Add(FovDeg);
		hash.Add(FilmWidth);
		hash.Add(FilmHeight);
		hash.Add(BaseStepsPerRay);
		hash.Add(BendScale);
		hash.Add(FieldStrength);
		hash.Add(FieldSourceEpoch);
		hash.Add(GeometryEpoch);
		hash.Add(BoundaryLayerEpoch);
		hash.Add(RefinementPolicyVersion);
		hash.Add(FormalPolicySha256, System.StringComparer.Ordinal);
		return hash.ToHashCode();
	}

	public static bool operator ==(ProbeContextKey left, ProbeContextKey right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(ProbeContextKey left, ProbeContextKey right)
	{
		return !left.Equals(right);
	}
}

#nullable restore
