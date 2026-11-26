/// <summary>
/// Terminal or transitional reason for a Cathedral Probe snapshot request.
/// </summary>
public enum ProbeSnapshotLifecycleReason : byte
{
	None = 0,
	PhysicsSpaceUnavailable = 1,
	ContextConstructionFailed = 2,
	ContextChanged = 3,
	DimensionsChanged = 4,
	RequestSuperseded = 5,
	TransportRejected = 6,
	CapacityExceeded = 7,
	UnprocessedPixelsRemaining = 8,
	TimeoutOrFrameBudgetExhausted = 9,
	InternalValidationFailed = 10,
	GeometryEpochChanged = 11,
	FieldSourceEpochChanged = 12,
	BoundaryEpochChanged = 13,
	PolicyChanged = 14,
	SpatialAuthorityPromotionFailed = 15,
	ResourceLimitExceeded = 16,
	ComputePolicyInvalid = 17,
	// FIX-P1a: transport-consumed field-source content changed while the field-source epoch did not.
	FieldSourceContentChanged = 18,
}
