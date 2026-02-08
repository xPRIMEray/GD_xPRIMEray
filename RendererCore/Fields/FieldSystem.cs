using System;
using System.Numerics;
using RendererCore.SceneSnapshot;

namespace RendererCore.Fields;

public static class FieldSystem
{
    public static Vector3 AccelAt(Vector3 pWorld, in SceneSnapshot.SceneSnapshot snapshot)
    {
        const float Eps = 1e-6f;

        var fields = snapshot.Fields;
        if (fields == null || fields.Count <= 0)
        {
            return Vector3.Zero;
        }

        // TODO: FTLAS candidate pruning.
        var count = fields.Count;
        var worldBounds = fields.WorldBounds;
        var localFromWorld = fields.LocalFromWorld;
        var worldFromLocal = fields.WorldFromLocal;
        var paramOffset = fields.ParamOffset;
        var curveType = fields.CurveType;
        var shapeType = fields.ShapeType;
        var metricModel = fields.MetricModel;
        var fieldParams = snapshot.FieldParams?.Data;

        if (worldBounds == null || localFromWorld == null || worldFromLocal == null || paramOffset == null || fieldParams == null
            || curveType == null || shapeType == null || metricModel == null)
        {
            return Vector3.Zero;
        }

        var total = Vector3.Zero;
        void AccumulateField(int i)
        {
            if (i >= count || i >= worldBounds.Length || i >= localFromWorld.Length || i >= worldFromLocal.Length || i >= paramOffset.Length)
            {
                return;
            }

            if (!worldBounds[i].Contains(pWorld))
            {
                return;
            }

            var pLocal = Vector3.Transform(pWorld, localFromWorld[i]);

            var shape = (i < shapeType.Length) ? (FieldShapeType)shapeType[i] : FieldShapeType.SphereRadial;
            float r;
            switch (shape)
            {
                case FieldShapeType.SphereRadial:
                    r = pLocal.Length();
                    break;
                case FieldShapeType.BoxVolume:
                    // TODO: Real Box distance model.
                    r = pLocal.Length();
                    break;
                default:
                    r = pLocal.Length();
                    break;
            }

            var offset = paramOffset[i];
            if (offset < 0 || offset + 5 >= fieldParams.Length)
            {
                return;
            }

            var rInner = fieldParams[offset + 0];
            var rOuter = fieldParams[offset + 1];
            var amp = fieldParams[offset + 2];
            var a = fieldParams[offset + 3];
            var b = fieldParams[offset + 4];
            var c = fieldParams[offset + 5];

            if (rOuter <= 0f)
            {
                return;
            }

            if (r > rOuter)
            {
                return;
            }

            var denom = MathF.Max(Eps, rOuter - rInner);
            var u = Saturate((r - rInner) / denom);
            var curve = (i < curveType.Length) ? (FieldCurveType)curveType[i] : FieldCurveType.Linear;
            var f = FieldCurves.Eval(curve, u, a, b, c, clamp01: true);

            // TODO: 1/r^2 flags behavior.
            var magnitude = amp * f;
            if (r < Eps)
            {
                return;
            }

            var dirLocal = pLocal * (1f / r);
            var metric = (i < metricModel.Length) ? (MetricModel)metricModel[i] : MetricModel.GRIN;
            if (metric == MetricModel.GordonMetric)
            {
                dirLocal = -dirLocal;
            }

            var contributionLocal = dirLocal * magnitude;
            var contributionWorld = Vector3.TransformNormal(contributionLocal, worldFromLocal[i]);
            total += contributionWorld;
        }

        if (snapshot.FieldTLAS != null)
        {
            Span<int> candidates = stackalloc int[256];
            var candidateCount = snapshot.FieldTLAS.QueryPoint(pWorld, candidates);
            for (var i = 0; i < candidateCount; i++)
            {
                AccumulateField(candidates[i]);
            }
        }
        else
        {
            for (var i = 0; i < count; i++)
            {
                AccumulateField(i);
            }
        }

        return total;
    }

    private static float Saturate(float v)
    {
        if (v < 0f) return 0f;
        if (v > 1f) return 1f;
        return v;
    }
}
