using System;

namespace RendererCore.SceneSnapshot;

public sealed class SceneSnapshot
{
    public InstanceSOA Instances { get; init; } = InstanceSOA.Empty();
    public FieldEntitySOA Fields { get; init; } = new();
    public PackedParamBuffer FieldParams { get; init; } = new();

    public string DebugSummary()
    {
        var instanceCount = Instances?.Count ?? 0;
        var fieldCount = Fields?.Count ?? 0;
        var paramCount = FieldParams?.Data?.Length ?? 0;
        return $"SceneSnapshot: Instances={instanceCount}, Fields={fieldCount}, FieldParams={paramCount}";
    }
}
