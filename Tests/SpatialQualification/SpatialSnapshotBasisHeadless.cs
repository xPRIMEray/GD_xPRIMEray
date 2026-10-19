using Godot;
using System;
using System.Linq;
using System.Numerics;
using XPrimeRay.Spatial;
using GdVector3 = Godot.Vector3;
using NumVector3 = System.Numerics.Vector3;

/// <summary>
/// Regression for the Godot column-basis to System.Numerics row-vector
/// conversion used by the frozen Formal spatial snapshot.
/// </summary>
public partial class SpatialSnapshotBasisHeadless : Node
{
	public override void _Ready() => CallDeferred(nameof(Run));

	private void Run()
	{
		try
		{
			Basis rotation = new(new Godot.Quaternion(new GdVector3(0f, 0f, 1f), Mathf.DegToRad(35f)));
			StaticBody3D body = new()
			{
				Name = "RotatedBody",
				CollisionLayer = 1,
				Transform = new Transform3D(rotation, new GdVector3(3.25f, -1.5f, 2.75f))
			};
			body.AddToGroup("fixture_geometry");
			CollisionShape3D shape = new()
			{
				Name = "RotatedBox",
				Shape = new BoxShape3D { Size = new GdVector3(2f, 4f, 1f) }
			};
			AddChild(body);
			body.AddChild(shape);

			FrozenGeometrySnapshot snapshot = GodotAdapter.SpatialSnapshotBuilder.BuildFromGodotScene(this);
			FrozenOrientedBox primitive = snapshot.Primitives.Single();

			GdVector3 localPoint = new(0.45f, -0.65f, 0.2f);
			GdVector3 godotWorldPoint = shape.GlobalTransform * localPoint;
			NumVector3 numericsWorldPoint = NumVector3.Transform(ToNumerics(localPoint), primitive.WorldFromLocal);
			AssertClose(numericsWorldPoint, ToNumerics(godotWorldPoint), "local_to_world_off_axis");

			NumVector3 numericsLocalPoint = NumVector3.Transform(ToNumerics(godotWorldPoint), primitive.LocalFromWorld);
			AssertClose(numericsLocalPoint, ToNumerics(localPoint), "world_to_local_off_axis");

			GdVector3 hitFromLocal = new(-2f, 1.8f, 0.1f);
			GdVector3 hitToLocal = new(2f, 1.8f, 0.1f);
			NumVector3 hitFrom = ToNumerics(shape.GlobalTransform * hitFromLocal);
			NumVector3 hitTo = ToNumerics(shape.GlobalTransform * hitToLocal);
			LinearScanSpatialQuery corrected = new(snapshot);
			Assert(corrected.IntersectsSegment(hitFrom, hitTo, 1, out SurfaceHit hit), "corrected rotated segment hit");
			Assert(hit.SegmentT > 0f && hit.SegmentT < 1f, "corrected rotated segment finite t");

			GdVector3 missFromLocal = new(-2f, 2.2f, 0.1f);
			GdVector3 missToLocal = new(2f, 2.2f, 0.1f);
			Assert(!corrected.IntersectsSegment(
				ToNumerics(shape.GlobalTransform * missFromLocal),
				ToNumerics(shape.GlobalTransform * missToLocal),
				1,
				out _), "corrected rotated segment miss");

			Matrix4x4 legacyWorld = LegacyTransposedMatrix(shape.GlobalTransform);
			Matrix4x4.Invert(legacyWorld, out Matrix4x4 legacyLocal);
			FrozenOrientedBox legacyPrimitive = new(
				primitive.CanonicalPrimitiveId,
				primitive.SurfaceClass,
				legacyWorld,
				legacyLocal,
				primitive.HalfExtents,
				primitive.WorldBounds,
				primitive.CollisionLayer,
				primitive.Flags);
			LinearScanSpatialQuery legacy = new(new FrozenGeometrySnapshot(new[] { legacyPrimitive }));
			Assert(!legacy.IntersectsSegment(hitFrom, hitTo, 1, out _), "old transposed convention must fail regression segment");

			GD.Print($"SPATIAL_SNAPSHOT_BASIS PASS geometry={snapshot.GeometrySnapshotSha256} hitT={hit.SegmentT:R}");
			GetTree().Quit(0);
		}
		catch (Exception exception)
		{
			GD.PushError("SPATIAL_SNAPSHOT_BASIS FAIL: " + exception);
			GetTree().Quit(1);
		}
	}

	private static Matrix4x4 LegacyTransposedMatrix(Transform3D value)
	{
		GdVector3 bx = value.Basis.X;
		GdVector3 by = value.Basis.Y;
		GdVector3 bz = value.Basis.Z;
		GdVector3 o = value.Origin;
		return new Matrix4x4(
			bx.X, by.X, bz.X, 0f,
			bx.Y, by.Y, bz.Y, 0f,
			bx.Z, by.Z, bz.Z, 0f,
			o.X, o.Y, o.Z, 1f);
	}

	private static NumVector3 ToNumerics(GdVector3 value) => new(value.X, value.Y, value.Z);

	private static void AssertClose(NumVector3 actual, NumVector3 expected, string label)
	{
		if (NumVector3.Distance(actual, expected) > 1e-5f)
			throw new InvalidOperationException($"{label}: actual={actual} expected={expected}");
	}

	private static void Assert(bool condition, string label)
	{
		if (!condition) throw new InvalidOperationException(label);
	}
}
