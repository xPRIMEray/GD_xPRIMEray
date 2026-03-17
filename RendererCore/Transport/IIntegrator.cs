using RendererCore.SceneSnapshot;

namespace RendererCore.Transport;

public interface IIntegrator
{
    StepResult Step(
        in MetricRayState state,
        float dt,
        IMetricField field,
        in RendererCore.SceneSnapshot.SceneSnapshot snapshot);
}
