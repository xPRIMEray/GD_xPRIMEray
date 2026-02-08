using System;
using System.Text;
using RendererCore.Fields;

namespace RendererCore.SceneSnapshot;

public sealed class SceneSnapshot
{
    public InstanceSOA Instances { get; init; } = InstanceSOA.Empty();
    public FieldEntitySOA Fields { get; init; } = new();
    public PackedParamBuffer FieldParams { get; init; } = new();
    public FieldTLAS FieldTLAS { get; init; }

    public string DebugSummary()
    {
        var instanceCount = Instances?.Count ?? 0;
        var fieldCount = Fields?.Count ?? 0;
        var paramCount = FieldParams?.Data?.Length ?? 0;
        var grinCount = 0;
        var gordonCount = 0;
        var otherMetricCount = 0;

        if (Fields?.MetricModel != null)
        {
            var metricModels = Fields.MetricModel;
            for (var i = 0; i < Math.Min(fieldCount, metricModels.Length); i++)
            {
                var metric = metricModels[i];
                if (metric == (int)MetricModel.GRIN)
                {
                    grinCount++;
                }
                else if (metric == (int)MetricModel.GordonMetric)
                {
                    gordonCount++;
                }
                else
                {
                    otherMetricCount++;
                }
            }
        }

        var summary = new StringBuilder();
        summary.Append($"SceneSnapshot: Instances={instanceCount}, Fields={fieldCount}, FieldParams={paramCount}");
        if (FieldTLAS != null)
        {
            summary.Append($", FieldTLASNodes={FieldTLAS.Nodes.Length}");
        }
        summary.Append($", MetricModel(GRIN={grinCount}, GordonMetric={gordonCount}");
        if (otherMetricCount > 0)
        {
            summary.Append($", Other={otherMetricCount}");
        }
        summary.Append(')');

        if (fieldCount > 0 && Fields != null)
        {
            var take = Math.Min(3, fieldCount);
            summary.Append(", FirstFields=[");
            for (var i = 0; i < take; i++)
            {
                if (i > 0)
                {
                    summary.Append(", ");
                }

                var rInner = 0f;
                var rOuter = 0f;
                var amp = 0f;
                var offset = (Fields.ParamOffset != null && i < Fields.ParamOffset.Length) ? Fields.ParamOffset[i] : -1;
                if (offset >= 0 && FieldParams?.Data != null && FieldParams.Data.Length >= offset + 3)
                {
                    rInner = FieldParams.Data[offset + 0];
                    rOuter = FieldParams.Data[offset + 1];
                    amp = FieldParams.Data[offset + 2];
                }

                var curveType = (Fields.CurveType != null && i < Fields.CurveType.Length) ? Fields.CurveType[i] : 0;
                var shapeType = (Fields.ShapeType != null && i < Fields.ShapeType.Length) ? Fields.ShapeType[i] : 0;
                summary.Append($"{i}:(rInner={rInner}, rOuter={rOuter}, amp={amp}, curveType={curveType}, shapeType={shapeType})");
            }
            summary.Append(']');
        }

        return summary.ToString();
    }
}
