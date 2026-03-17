namespace RendererCore.Transport;

public struct StepResult
{
    public MetricRayState NewState;
    public float ErrorEstimate;
    public float ConstraintDrift;
    public float RecommendedDt;
}
