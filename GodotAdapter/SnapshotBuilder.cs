using System;
using System.Collections.Generic;
using System.Numerics;
using Godot;
using RendererCore.Fields;
using RendererCore.SceneSnapshot;
using GdVector3 = Godot.Vector3;
using NumVector3 = System.Numerics.Vector3;

namespace GodotAdapter;

public static class SnapshotBuilder
{
    public static SceneSnapshot BuildFromGodotScene(Node root)
    {
        var sceneRoot = root?.GetTree()?.CurrentScene ?? root;
        if (sceneRoot == null)
        {
            return new SceneSnapshot
            {
                Instances = InstanceSOA.Empty(),
                Fields = new FieldEntitySOA(),
                FieldParams = new PackedParamBuffer(),
                FieldTLAS = new FieldTLAS(Array.Empty<BVHNode>(), Array.Empty<int>(), -1)
            };
        }

        var fieldNodes = new List<FieldSource3D>();
        CollectFieldNodes(sceneRoot, fieldNodes);
        fieldNodes.Sort(static (a, b) => string.CompareOrdinal(a.GetPath().ToString(), b.GetPath().ToString()));

        var enabledFields = new List<FieldSource3D>(fieldNodes.Count);
        foreach (var field in fieldNodes)
        {
            if (field.Enabled)
            {
                enabledFields.Add(field);
            }
        }

        var fieldCount = enabledFields.Count;
        var fieldParams = new PackedParamBuffer();

        var fields = new FieldEntitySOA
        {
            Count = fieldCount,
            MetricModel = new int[fieldCount],
            ShapeType = new int[fieldCount],
            CurveType = new int[fieldCount],
            WorldFromLocal = new Matrix4x4[fieldCount],
            LocalFromWorld = new Matrix4x4[fieldCount],
            WorldBounds = new Aabb3[fieldCount],
            ParamOffset = new int[fieldCount],
            ParamLength = new int[fieldCount],
            Flags = new uint[fieldCount]
        };

        for (var i = 0; i < fieldCount; i++)
        {
            var field = enabledFields[i];
            var worldFromLocal = field.GlobalTransform;
            var localFromWorld = worldFromLocal.AffineInverse();

            field.GetPackedParams8(out var rInner, out var rOuter, out var amp, out var a, out var b, out var c, out var r0, out var r1);
            var metricModel = (int)field.MetricModel;
            var shapeType = (int)field.ShapeType;
            var curveType = (int)field.CurveType;
            var flags = field.ModeFlags;

            var paramOffset = fieldParams.AppendBlock8(rInner, rOuter, amp, a, b, c, r0, r1);

            fields.MetricModel[i] = metricModel;
            fields.ShapeType[i] = shapeType;
            fields.CurveType[i] = curveType;
            fields.WorldFromLocal[i] = ToMatrix4x4(worldFromLocal);
            fields.LocalFromWorld[i] = ToMatrix4x4(localFromWorld);
            fields.WorldBounds[i] = ToAabb3(field.GetWorldInfluenceAabbConservative());
            fields.ParamOffset[i] = paramOffset;
            fields.ParamLength[i] = 8;
            fields.Flags[i] = flags;
        }

        var ftlas = RendererCore.Fields.FieldTLAS.Build(fields);

        return new SceneSnapshot
        {
            Instances = InstanceSOA.Empty(),
            Fields = fields,
            FieldParams = fieldParams,
            FieldTLAS = ftlas
        };
    }

    private static void CollectFieldNodes(Node root, List<FieldSource3D> results)
    {
        if (root is FieldSource3D field)
        {
            results.Add(field);
        }

        foreach (Node child in root.GetChildren())
        {
            CollectFieldNodes(child, results);
        }
    }

    private static Aabb3 ToAabb3(Aabb aabb)
    {
        var p0 = ToNumerics(aabb.Position);
        var p1 = ToNumerics(aabb.Position + aabb.Size);
        var min = NumVector3.Min(p0, p1);
        var max = NumVector3.Max(p0, p1);
        return new Aabb3(min, max);
    }

    private static NumVector3 ToNumerics(GdVector3 v)
    {
        return new NumVector3(v.X, v.Y, v.Z);
    }

    private static Matrix4x4 ToMatrix4x4(Transform3D t)
    {
        var bx = t.Basis.X;
        var by = t.Basis.Y;
        var bz = t.Basis.Z;
        var o = t.Origin;

        return new Matrix4x4(
            bx.X, by.X, bz.X, 0f,
            bx.Y, by.Y, bz.Y, 0f,
            bx.Z, by.Z, bz.Z, 0f,
            o.X, o.Y, o.Z, 1f);
    }
}
