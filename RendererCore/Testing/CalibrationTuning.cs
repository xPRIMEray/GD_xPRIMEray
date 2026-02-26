public static class CalibrationTuning
{
	// Confidence guards: if probe telemetry suggests heavy truncation, keep classification conservative.
	public const int MaxChildrenSkippedForConfidentClassification = 32;

	// Tiny scenes: very low node + mesh counts.
	public const int TinyMaxNodes = 24;
	public const int TinyMaxMeshes = 2;
	public const int TinyMaxSurfacesEstimate = 8;
	public const long TinyMaxTrianglesEstimate = 20_000;

	// Dense geometry scenes: high mesh / surface / triangle estimates.
	public const int DenseMinMeshes = 24;
	public const int DenseMinSurfacesEstimate = 96;
	public const long DenseMinTrianglesEstimate = 300_000;

	// Field-heavy scenes: many field nodes, GRIN box volumes, or curvature-related field nodes.
	public const int FieldHeavyMinFieldSources = 6;
	public const int FieldHeavyMinGrinVolumes = 3;
	public const int FieldHeavyMinCurvatureFields = 2;
	public const int FieldHeavyMinCombinedFieldScore = 8;

	// v0 recommendation cadence hints (preview-only until mapped to runtime config).
	public const int DenseGeometrySamplingMs = 4;
	public const int FieldHeavySamplingMs = 1;
}
