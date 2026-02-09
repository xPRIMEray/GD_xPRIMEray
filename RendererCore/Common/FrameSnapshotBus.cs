namespace RendererCore.Common;

public static class FrameSnapshotBus
{
    public static bool HasSnapshot { get; private set; }

    public static RendererCore.SceneSnapshot.SceneSnapshot? CurrentSnapshot { get; private set; }

    public static ulong FrameId { get; private set; }

    public static void Set(RendererCore.SceneSnapshot.SceneSnapshot snapshot, ulong frameId)
    {
        CurrentSnapshot = snapshot;
        FrameId = frameId;
        HasSnapshot = true;
    }

    public static void Clear()
    {
        CurrentSnapshot = null;
        FrameId = 0;
        HasSnapshot = false;
    }
}
