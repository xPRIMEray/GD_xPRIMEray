using Godot;
using RendererCore.SceneSnapshot;

namespace RenderBackends;

public sealed class CoreBackend : IRenderBackend
{
    public string Name => "Core";

    public void RenderFrame(SceneSnapshot snapshot)
    {
        GD.Print(snapshot?.DebugSummary() ?? "SceneSnapshot: <null>");
    }
}
