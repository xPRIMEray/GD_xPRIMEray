using System;
using RendererCore.SceneSnapshot;

namespace RenderBackends;

public sealed class LegacyBackend : IRenderBackend
{
    private readonly GrinFilmCamera _camera;

    public LegacyBackend(GrinFilmCamera camera)
    {
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
    }

    public string Name => "Legacy";

    public void RenderFrame(SceneSnapshot snapshot)
    {
        // Legacy path ignores snapshot for now.
        _camera.RenderStep();
    }
}
