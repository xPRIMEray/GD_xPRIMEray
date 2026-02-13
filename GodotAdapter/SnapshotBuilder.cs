using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Godot;
using RendererCore.Common;
using RendererCore.Fields;
using RendererCore.Geometry;
using RendererCore.SceneSnapshot;
using GdVector3 = Godot.Vector3;
using NumVector3 = System.Numerics.Vector3;

namespace GodotAdapter;

public static class SnapshotBuilder
{
    private static double _nextSnapshotLogTimeSec = 0;
    private const float GeometryInflate = 0.02f; // expands by this in all axes
    private const float FallbackRadius = 0.05f;

    public static SceneSnapshot BuildFromGodotScene(Node root)
    {
        var sceneRoot = root?.GetTree()?.CurrentScene ?? root;
        if (sceneRoot == null)
        {
            var emptyGeometry = new GeometryEntitySOA(0);
            return new SceneSnapshot
            {
                Instances = InstanceSOA.Empty(),
                Fields = new FieldEntitySOA(),
                FieldParams = new PackedParamBuffer(),
                FieldTLAS = new FieldTLAS(Array.Empty<RendererCore.Fields.BVHNode>(), Array.Empty<int>(), -1),
                Geometry = emptyGeometry,
                GeometryTLAS = GeometryTLAS.Build(emptyGeometry)
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

        var collisionNodes = new List<CollisionObject3D>();
        var visualNodes = new List<VisualInstance3D>();
        CollectGeometryNodes(sceneRoot, collisionNodes, visualNodes);
        collisionNodes.Sort(static (a, b) => string.CompareOrdinal(a.GetPath().ToString(), b.GetPath().ToString()));
        visualNodes.Sort(static (a, b) => string.CompareOrdinal(a.GetPath().ToString(), b.GetPath().ToString()));

        var geometryNodes = new List<Node3D>(collisionNodes.Count + visualNodes.Count);
        var seenInstanceIds = new HashSet<ulong>();
        var nodesWithCollisionDescendant = BuildNodesWithCollisionDescendants(collisionNodes);
        var collisionIncluded = 0;
        var collisionSkippedNonRaycast = 0;
        var visualSkippedByCollisionPreference = 0;

        foreach (var collision in collisionNodes)
        {
            if (!IsRaycastTarget(collision))
            {
                collisionSkippedNonRaycast++;
                continue;
            }

            if (seenInstanceIds.Add(collision.GetInstanceId()))
            {
                geometryNodes.Add(collision);
                collisionIncluded++;
            }
        }

        foreach (var visual in visualNodes)
        {
            if (HasCollisionObjectAncestor(visual) || nodesWithCollisionDescendant.Contains(visual.GetInstanceId()))
            {
                visualSkippedByCollisionPreference++;
                continue;
            }

            if (seenInstanceIds.Add(visual.GetInstanceId()))
            {
                geometryNodes.Add(visual);
            }
        }

        var geometryCount = geometryNodes.Count;
        var geometry = new GeometryEntitySOA(geometryCount);
        for (var i = 0; i < geometryCount; i++)
        {
            var node = geometryNodes[i];
            var worldAabb = GetWorldBounds(node);
            worldAabb = InflateAabb(worldAabb, GeometryInflate);
            geometry.WorldBounds[i] = ToAabb3(worldAabb);
            geometry.GodotInstanceIds[i] = (long)node.GetInstanceId();
        }

        var geometryTlas = GeometryTLAS.Build(geometry);

        var visualIncluded = geometryCount - collisionIncluded;
        MaybeLogSnapshot(
            geometryCount,
            collisionIncluded,
            visualIncluded,
            visualSkippedByCollisionPreference,
            collisionSkippedNonRaycast,
            geometry);

        return new SceneSnapshot
        {
            Instances = InstanceSOA.Empty(),
            Fields = fields,
            FieldParams = fieldParams,
            FieldTLAS = ftlas,
            Geometry = geometry,
            GeometryTLAS = geometryTlas
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

    private static void CollectGeometryNodes(Node root, List<CollisionObject3D> collisions, List<VisualInstance3D> visuals)
    {
        if (root is CollisionObject3D collision)
        {
            collisions.Add(collision);
        }
        else if (root is VisualInstance3D visual)
        {
            visuals.Add(visual);
        }

        foreach (Node child in root.GetChildren())
        {
            CollectGeometryNodes(child, collisions, visuals);
        }
    }

    private static bool IsRaycastTarget(CollisionObject3D collision)
    {
        return collision != null && collision.CollisionLayer != 0u;
    }

    private static bool HasCollisionObjectAncestor(Node node)
    {
        for (Node p = node?.GetParent(); p != null; p = p.GetParent())
        {
            if (p is CollisionObject3D)
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<ulong> BuildNodesWithCollisionDescendants(List<CollisionObject3D> collisions)
    {
        var withCollisionDescendant = new HashSet<ulong>();
        foreach (var collision in collisions)
        {
            for (Node p = collision?.GetParent(); p != null; p = p.GetParent())
            {
                withCollisionDescendant.Add(p.GetInstanceId());
            }
        }
        return withCollisionDescendant;
    }

    private static Aabb3 ToAabb3(Aabb aabb)
    {
        var p0 = ToNumerics(aabb.Position);
        var p1 = ToNumerics(aabb.Position + aabb.Size);
        var min = NumVector3.Min(p0, p1);
        var max = NumVector3.Max(p0, p1);
        return new Aabb3(min, max);
    }

    private static Aabb GetWorldBounds(Node3D node)
    {
        if (node is VisualInstance3D visual)
        {
            var local = visual.GetAabb();
            return TransformAabb(local, visual.GlobalTransform);
        }

        if (node is CollisionObject3D collision)
        {
            if (TryBuildCollisionObjectAabb(collision, out var collisionAabb))
            {
                return collisionAabb;
            }
            return BuildFallbackAabb(node);
        }

        return BuildFallbackAabb(node);
    }

    private static bool TryBuildCollisionObjectAabb(CollisionObject3D collision, out Aabb aabb)
    {
        var found = false;
        aabb = default;
        AccumulateCollisionShapeAabb(collision, collision, ref found, ref aabb);
        return found;
    }

    private static void AccumulateCollisionShapeAabb(
        CollisionObject3D rootCollision,
        Node root,
        ref bool found,
        ref Aabb aabb)
    {
        if (root is CollisionObject3D collision && collision != rootCollision)
        {
            return;
        }

        if (root is CollisionShape3D shapeNode)
        {
            if (!shapeNode.Disabled)
            {
                var shape = shapeNode.Shape;
                if (shape != null && TryGetShapeLocalAabb(shape, out var local))
                {
                    var world = TransformAabb(local, shapeNode.GlobalTransform);
                    aabb = found ? UnionAabb(aabb, world) : world;
                    found = true;
                }
            }
        }

        foreach (Node child in root.GetChildren())
        {
            AccumulateCollisionShapeAabb(rootCollision, child, ref found, ref aabb);
        }
    }

    private static bool TryGetShapeLocalAabb(Shape3D shape, out Aabb local)
    {
        switch (shape)
        {
            case BoxShape3D box:
            {
                var size = box.Size;
                var ext = size * 0.5f;
                local = new Aabb(-ext, size);
                return true;
            }
            case SphereShape3D sphere:
            {
                float r = sphere.Radius;
                var size = new GdVector3(r * 2f, r * 2f, r * 2f);
                local = new Aabb(new GdVector3(-r, -r, -r), size);
                return true;
            }
            case CapsuleShape3D capsule:
            {
                float r = capsule.Radius;
                float halfHeight = capsule.Height * 0.5f;
                float y = halfHeight + r;
                local = new Aabb(new GdVector3(-r, -y, -r), new GdVector3(r * 2f, y * 2f, r * 2f));
                return true;
            }
            case CylinderShape3D cylinder:
            {
                float r = cylinder.Radius;
                float halfHeight = cylinder.Height * 0.5f;
                local = new Aabb(new GdVector3(-r, -halfHeight, -r), new GdVector3(r * 2f, halfHeight * 2f, r * 2f));
                return true;
            }
            default:
                local = default;
                return false;
        }
    }

    private static Aabb BuildFallbackAabb(Node3D node)
    {
        var p = node.GlobalPosition;
        var min = new GdVector3(p.X - FallbackRadius, p.Y - FallbackRadius, p.Z - FallbackRadius);
        var max = new GdVector3(p.X + FallbackRadius, p.Y + FallbackRadius, p.Z + FallbackRadius);
        return new Aabb(min, max - min);
    }

    private static Aabb UnionAabb(in Aabb a, in Aabb b)
    {
        var minA = a.Position;
        var maxA = a.Position + a.Size;
        var minB = b.Position;
        var maxB = b.Position + b.Size;
        var min = new GdVector3(
            MathF.Min(minA.X, minB.X),
            MathF.Min(minA.Y, minB.Y),
            MathF.Min(minA.Z, minB.Z));
        var max = new GdVector3(
            MathF.Max(maxA.X, maxB.X),
            MathF.Max(maxA.Y, maxB.Y),
            MathF.Max(maxA.Z, maxB.Z));
        return new Aabb(min, max - min);
    }

    private static Aabb TransformAabb(in Aabb local, in Transform3D transform)
    {
        var min = new GdVector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new GdVector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var p = local.Position;
        var s = local.Size;

        for (var i = 0; i < 8; i++)
        {
            var corner = new GdVector3(
                p.X + ((i & 1) != 0 ? s.X : 0f),
                p.Y + ((i & 2) != 0 ? s.Y : 0f),
                p.Z + ((i & 4) != 0 ? s.Z : 0f));
            var world = transform * corner;
            min = new GdVector3(
                MathF.Min(min.X, world.X),
                MathF.Min(min.Y, world.Y),
                MathF.Min(min.Z, world.Z));
            max = new GdVector3(
                MathF.Max(max.X, world.X),
                MathF.Max(max.Y, world.Y),
                MathF.Max(max.Z, world.Z));
        }

        return new Aabb(min, max - min);
    }

    private static Aabb InflateAabb(in Aabb aabb, float inflate)
    {
        if (inflate <= 0.0f)
        {
            return aabb;
        }

        var min = aabb.Position - new GdVector3(inflate, inflate, inflate);
        var max = aabb.Position + aabb.Size + new GdVector3(inflate, inflate, inflate);
        return new Aabb(min, max - min);
    }

    private static void MaybeLogSnapshot(
        int geometryCount,
        int collisionIncluded,
        int visualIncluded,
        int visualSkippedByCollisionPreference,
        int collisionSkippedNonRaycast,
        GeometryEntitySOA geometry)
    {
        if (!DebugLogConfig.EnableSnapshotLog)
        {
            return;
        }

        var now = Time.GetTicksMsec() * 0.001;
        if (now < _nextSnapshotLogTimeSec)
        {
            return;
        }

        _nextSnapshotLogTimeSec = now + Math.Max(0.05, DebugLogConfig.SnapshotLogIntervalSec);
        GD.Print(
            $"[SNAPSHOT] geomCount={geometryCount} collisionIncluded={collisionIncluded} visualIncluded={visualIncluded} " +
            $"visualSkippedPref={visualSkippedByCollisionPreference} collisionSkippedNonRaycast={collisionSkippedNonRaycast}");
        MaybeLogGeometryBoundsSamples(geometry);
    }

    private static void MaybeLogGeometryBoundsSamples(GeometryEntitySOA geometry)
    {
        if (geometry == null || geometry.Count <= 0 || geometry.WorldBounds == null || geometry.GodotInstanceIds == null)
        {
            GD.Print("[SNAPSHOT][Bounds] samples=0 (geometry list empty)");
            return;
        }

        int count = Math.Min(3, Math.Min(geometry.Count, Math.Min(geometry.WorldBounds.Length, geometry.GodotInstanceIds.Length)));
        var sb = new StringBuilder(256);
        sb.Append("[SNAPSHOT][Bounds] samples=").Append(count);

        for (int i = 0; i < count; i++)
        {
            ref readonly var bounds = ref geometry.WorldBounds[i];
            long id = geometry.GodotInstanceIds[i];
            sb.Append(" {id=").Append(id)
              .Append(" min=(").Append(bounds.Min.X.ToString("0.###")).Append(',').Append(bounds.Min.Y.ToString("0.###")).Append(',').Append(bounds.Min.Z.ToString("0.###")).Append(')')
              .Append(" max=(").Append(bounds.Max.X.ToString("0.###")).Append(',').Append(bounds.Max.Y.ToString("0.###")).Append(',').Append(bounds.Max.Z.ToString("0.###")).Append(")}");
        }

        GD.Print(sb.ToString());
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
