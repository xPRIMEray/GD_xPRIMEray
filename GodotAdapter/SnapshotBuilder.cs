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
        var fieldNodes = new List<FieldSource3D>();
        CollectFieldNodes(root, fieldNodes);
        fieldNodes.Sort(static (a, b) => string.CompareOrdinal(a.GetPath().ToString(), b.GetPath().ToString()));

        var fieldCount = fieldNodes.Count;
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
            var field = fieldNodes[i];
            var worldFromLocal = field.GlobalTransform;
            var localFromWorld = worldFromLocal.AffineInverse();

            var rInner = field.InnerRadius;
            var rOuter = field.OuterRadius;
            var amp = field.Strength;

            // TODO: curveType/metricModel/shapeType/flags not exposed on FieldSource3D yet.
            var curveType = FieldCurveType.Power;
            var curveA = 1f;
            var curveB = 0f;
            var curveC = 0f;
            var metricModel = MetricModel.GRIN;
            var shapeType = FieldShapeType.SphereRadial;
            var flags = 0u;

            var paramOffset = fieldParams.AppendBlock8(rInner, rOuter, amp, curveA, curveB, curveC, 0f, 0f);

            fields.MetricModel[i] = (int)metricModel;
            fields.ShapeType[i] = (int)shapeType;
            fields.CurveType[i] = (int)curveType;
            fields.WorldFromLocal[i] = ToMatrix4x4(worldFromLocal);
            fields.LocalFromWorld[i] = ToMatrix4x4(localFromWorld);
            fields.WorldBounds[i] = BuildSphereWorldBounds(worldFromLocal, rOuter);
            fields.ParamOffset[i] = paramOffset;
            fields.ParamLength[i] = 8;
            fields.Flags[i] = flags;
        }

        return new SceneSnapshot
        {
            Instances = InstanceSOA.Empty(),
            Fields = fields,
            FieldParams = fieldParams
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

    private static Aabb3 BuildSphereWorldBounds(Transform3D worldFromLocal, float rOuter)
    {
        var r = MathF.Max(0f, rOuter);
        var localCorners = new GdVector3[8]
        {
            new(-r, -r, -r),
            new(-r, -r,  r),
            new(-r,  r, -r),
            new(-r,  r,  r),
            new( r, -r, -r),
            new( r, -r,  r),
            new( r,  r, -r),
            new( r,  r,  r)
        };

        var min = new NumVector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new NumVector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        foreach (var local in localCorners)
        {
            var world = worldFromLocal * local;
            var p = ToNumerics(world);
            min = new NumVector3(MathF.Min(min.X, p.X), MathF.Min(min.Y, p.Y), MathF.Min(min.Z, p.Z));
            max = new NumVector3(MathF.Max(max.X, p.X), MathF.Max(max.Y, p.Y), MathF.Max(max.Z, p.Z));
        }

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
            bx.X, by.X, bz.X, o.X,
            bx.Y, by.Y, bz.Y, o.Y,
            bx.Z, by.Z, bz.Z, o.Z,
            0f, 0f, 0f, 1f);
    }
}
