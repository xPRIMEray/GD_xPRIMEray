using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using XPrimeRay.Core.Geometry;
using XPrimeRay.Spatial;
using GdVector3 = Godot.Vector3;
using NumericsVector3 = System.Numerics.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace GodotAdapter;

/// <summary>
/// Narrow Godot-to-spatial-kernel adapter for enabled BoxShape3D and
/// uniformly-scaled SphereShape3D primitives used by formal dual validation.
/// </summary>
public static class SpatialSnapshotBuilder
{
    public const string SchemaVersion = FrozenGeometrySnapshot.SchemaVersion;

    public static FrozenGeometrySnapshot BuildFromGodotScene(
        Node root,
        string sourceGroup = "fixture_source",
        string backgroundGroup = "fixture_background")
    {
        Node sceneRoot = root?.GetTree()?.CurrentScene ?? root
            ?? throw new InvalidOperationException("A scene root is required.");
        List<ShapeRecord> records = new();
        bool hasExplicitBackground = !string.IsNullOrWhiteSpace(backgroundGroup) &&
            sceneRoot.GetTree().GetNodesInGroup(backgroundGroup).Count > 0;
        CollectShapes(sceneRoot, records, sourceGroup, backgroundGroup, hasExplicitBackground);

        var ordered = records
            .GroupBy(r => r.OwnerPath, StringComparer.Ordinal)
            .SelectMany(group => group.OrderBy(r => r.ShapePath, StringComparer.Ordinal)
                .Select((record, ordinal) => record with { ShapeOrdinal = ordinal }))
            .OrderBy(r => r.ShapePath, StringComparer.Ordinal)
            .ToArray();

        List<FrozenOrientedBox> boxes = new(ordered.Length);
        List<FrozenSphere> spheres = new();
        foreach (ShapeRecord record in ordered)
        {
            Transform3D godotTransform = record.Shape.GlobalTransform;
            string canonicalId = $"{record.ShapePath}#{record.ShapeOrdinal}";
            FrozenPrimitiveFlags flags = record.Excluded
                ? FrozenPrimitiveFlags.Excluded
                : FrozenPrimitiveFlags.CollideWithBodies;
            switch (record.Shape.Shape)
            {
                case BoxShape3D box:
                {
                    Matrix4x4 worldFromLocal = ToNumerics(godotTransform);
                    if (!Matrix4x4.Invert(worldFromLocal, out Matrix4x4 localFromWorld))
                        throw new InvalidOperationException($"Non-invertible geometry transform={record.Shape.GetPath()}");
                    NumericsVector3 halfExtents = ToNumerics(box.Size) * 0.5f;
                    Aabb3 worldBounds = BuildWorldBounds(worldFromLocal, halfExtents);
                    boxes.Add(new FrozenOrientedBox(
                        canonicalId,
                        record.SurfaceClass,
                        worldFromLocal,
                        localFromWorld,
                        halfExtents,
                        worldBounds,
                        record.Owner.CollisionLayer,
                        flags));
                    break;
                }
                case SphereShape3D sphere:
                {
                    float scale = RequireOrthogonalUniformScale(godotTransform.Basis, record.Shape.GetPath().ToString());
                    spheres.Add(new FrozenSphere(
                        canonicalId,
                        record.SurfaceClass,
                        ToNumerics(godotTransform.Origin),
                        sphere.Radius * scale,
                        record.Owner.CollisionLayer,
                        flags));
                    break;
                }
                default:
                    throw new NotSupportedException(
                        $"Spatial Kernel supports BoxShape3D and SphereShape3D; unsupported={record.Shape.GetPath()} type={record.Shape.Shape.GetType().Name}");
            }
        }

        return new FrozenGeometrySnapshot(boxes, spheres);
    }

    private static void CollectShapes(
        Node node,
        List<ShapeRecord> records,
        string sourceGroup,
        string backgroundGroup,
        bool hasExplicitBackground)
    {
        if (node is CollisionShape3D shape && shape.Shape != null)
        {
            CollisionObject3D? owner = FindCollisionOwner(shape);
            if (owner != null && owner.CollisionLayer != 0)
            {
                string ownerPath = owner.GetPath().ToString();
				if (owner.IsInGroup("spatial_kernel_excluded"))
					return;
				SpatialSurfaceClass surfaceClass;
				if (!string.IsNullOrWhiteSpace(sourceGroup) && owner.IsInGroup(sourceGroup))
					surfaceClass = SpatialSurfaceClass.Geometry;
				else if (!string.IsNullOrWhiteSpace(backgroundGroup) && owner.IsInGroup(backgroundGroup))
					surfaceClass = SpatialSurfaceClass.Background;
				else
					surfaceClass = hasExplicitBackground ? SpatialSurfaceClass.Unknown : SpatialSurfaceClass.Background;
                records.Add(new ShapeRecord(
                    shape,
                    owner,
                    ownerPath,
                    shape.GetPath().ToString(),
					surfaceClass,
					false,
                    0));
            }
        }

        foreach (Node child in node.GetChildren())
            CollectShapes(child, records, sourceGroup, backgroundGroup, hasExplicitBackground);
    }

    private static CollisionObject3D? FindCollisionOwner(Node node)
    {
        for (Node? current = node.GetParent(); current != null; current = current.GetParent())
            if (current is CollisionObject3D owner) return owner;
        return null;
    }

    private static Matrix4x4 ToNumerics(Transform3D t)
    {
        GdVector3 bx = t.Basis.X;
        GdVector3 by = t.Basis.Y;
        GdVector3 bz = t.Basis.Z;
        GdVector3 o = t.Origin;
        return new Matrix4x4(
            bx.X, bx.Y, bx.Z, 0f,
            by.X, by.Y, by.Z, 0f,
            bz.X, bz.Y, bz.Z, 0f,
            o.X, o.Y, o.Z, 1f);
    }

    private static NumericsVector3 ToNumerics(GdVector3 v) => new(v.X, v.Y, v.Z);

    private static float RequireOrthogonalUniformScale(Basis basis, string path)
    {
        float sx = basis.X.Length();
        float sy = basis.Y.Length();
        float sz = basis.Z.Length();
        float largest = Math.Max(sx, Math.Max(sy, sz));
        float tolerance = Math.Max(1e-6f, largest * 1e-5f);
        if (!float.IsFinite(largest) || largest <= 0f ||
            Math.Abs(sx - sy) > tolerance || Math.Abs(sx - sz) > tolerance)
            throw new NotSupportedException($"SphereShape3D requires finite uniform scale; shape={path} scale=({sx:R},{sy:R},{sz:R})");

        GdVector3 x = basis.X / sx;
        GdVector3 y = basis.Y / sy;
        GdVector3 z = basis.Z / sz;
        if (Math.Abs(x.Dot(y)) > 1e-5f || Math.Abs(x.Dot(z)) > 1e-5f || Math.Abs(y.Dot(z)) > 1e-5f)
            throw new NotSupportedException($"SphereShape3D does not support sheared transforms; shape={path}");
        return (sx + sy + sz) / 3f;
    }

    private static Aabb3 BuildWorldBounds(Matrix4x4 worldFromLocal, NumericsVector3 halfExtents)
    {
        NumericsVector3 min = new(float.PositiveInfinity);
        NumericsVector3 max = new(float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            NumericsVector3 local = new(
                (i & 1) != 0 ? halfExtents.X : -halfExtents.X,
                (i & 2) != 0 ? halfExtents.Y : -halfExtents.Y,
                (i & 4) != 0 ? halfExtents.Z : -halfExtents.Z);
            NumericsVector3 world = NumericsVector3.Transform(local, worldFromLocal);
            min = NumericsVector3.Min(min, world);
            max = NumericsVector3.Max(max, world);
        }
        return new Aabb3(min, max);
    }

    private readonly record struct ShapeRecord(
        CollisionShape3D Shape,
        CollisionObject3D Owner,
        string OwnerPath,
        string ShapePath,
        SpatialSurfaceClass SurfaceClass,
        bool Excluded,
        int ShapeOrdinal);
}
