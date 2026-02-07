namespace RenderBackends;

public static class BackendSelector
{
    public static IRenderBackend Create(BackendMode mode, IRenderBackend legacy, IRenderBackend core)
    {
        return mode switch
        {
            BackendMode.Legacy => legacy,
            BackendMode.Core => core,
            BackendMode.Compare => legacy, // TODO: implement compare mode
            _ => legacy
        };
    }
}
