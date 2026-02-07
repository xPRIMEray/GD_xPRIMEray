using RendererCore.SceneSnapshot;

namespace RenderBackends;

public interface IRenderBackend
{
    string Name { get; }
    void RenderFrame(SceneSnapshot snapshot);
}
