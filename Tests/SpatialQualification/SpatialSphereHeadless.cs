using Godot;
using System;
using System.Linq;
using XPrimeRay.Spatial;
using GdVector3 = Godot.Vector3;
using NumVector3 = System.Numerics.Vector3;

/// <summary>Godot adapter, physics witness, and actual Gallery sphere ownership qualification.</summary>
public partial class SpatialSphereHeadless : Node
{
	public override void _Ready() => CallDeferred(nameof(Run));

	private async void Run()
	{
		try
		{
			await QualifyTransformedSphereAdapter();
			QualifyRejectedTransforms();
			await QualifyActualGallerySphere();
			GD.Print("SPATIAL_SPHERE_OWNERSHIP PASS");
			GetTree().Quit(0);
		}
		catch (Exception exception)
		{
			GD.PushError("SPATIAL_SPHERE_OWNERSHIP FAIL: " + exception);
			GetTree().Quit(1);
		}
	}

	private async System.Threading.Tasks.Task QualifyTransformedSphereAdapter()
	{
		Basis rotation = new(new Quaternion(new GdVector3(0.3f, 0.8f, 0.2f).Normalized(), 0.73f));
		Node3D rotatedParent = new()
		{
			Name = "RotatedSphereParent",
			Transform = new Transform3D(rotation.Scaled(GdVector3.One * 1.5f), new GdVector3(3f, 2f, -4f))
		};
		StaticBody3D body = CreateSphereBody("TransformedSphere", Transform3D.Identity, 2f);
		rotatedParent.AddChild(body);
		AddChild(rotatedParent);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

		FrozenGeometrySnapshot snapshot = GodotAdapter.SpatialSnapshotBuilder.BuildFromGodotScene(this, "fixture_geometry", "fixture_background");
		FrozenSphere sphere = snapshot.Spheres.Single();
		AssertClose(sphere.Center, new NumVector3(3f, 2f, -4f), 1e-5f, "adapter world center");
		Assert(MathF.Abs(sphere.Radius - 3f) <= 1e-5f, "adapter uniform world radius");
		Assert(sphere.SurfaceClass == SpatialSurfaceClass.Geometry, "adapter sphere surface class");

		GdVector3 from = new(7f, 2f, -4f);
		GdVector3 to = new(-1f, 2f, -4f);
		Godot.Collections.Dictionary godot = IntersectGodot(from, to);
		Assert(godot.Count > 0 && ReferenceEquals(godot["collider"].AsGodotObject(), body), "Godot transformed sphere hit");
		LinearScanSpatialQuery linear = new(snapshot);
		SpatialBvhQuery bvh = new(snapshot);
		Assert(linear.IntersectsSegment(ToNumerics(from), ToNumerics(to), 1, out SurfaceHit linearHit), "linear transformed sphere hit");
		Assert(bvh.IntersectsSegment(ToNumerics(from), ToNumerics(to), 1, out SurfaceHit bvhHit), "BVH transformed sphere hit");
		CompareWitnesses(from, to, godot, linearHit, bvhHit, sphere.CanonicalPrimitiveId, 2e-5f, "transformed sphere");

		RemoveChild(rotatedParent);
		rotatedParent.QueueFree();
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
	}

	private void QualifyRejectedTransforms()
	{
		StaticBody3D nonuniform = CreateSphereBody("NonuniformSphere", new Transform3D(
			Basis.Identity.Scaled(new GdVector3(1f, 2f, 1f)), GdVector3.Zero), 1f);
		AddChild(nonuniform);
		AssertSnapshotRejected("nonuniform sphere scale");
		RemoveChild(nonuniform);
		nonuniform.QueueFree();

		Basis shear = new(new GdVector3(1f, 0f, 0f), new GdVector3(0.2f, 1f, 0f), new GdVector3(0f, 0f, 1f));
		StaticBody3D sheared = CreateSphereBody("ShearedSphere", new Transform3D(shear, GdVector3.Zero), 1f);
		AddChild(sheared);
		AssertSnapshotRejected("sheared sphere transform");
		RemoveChild(sheared);
		sheared.QueueFree();
	}

	private async System.Threading.Tasks.Task QualifyActualGallerySphere()
	{
		PackedScene packed = GD.Load<PackedScene>("res://Experiences/SuspendedPortalGallery.tscn");
		Node3D gallery = packed.Instantiate<Node3D>();
		AddChild(gallery);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

		Node3D world = gallery.GetNode<Node3D>("SuspendedPortalGalleryWorld");
		StaticBody3D body = world.GetNode<StaticBody3D>("DiagnosticGeometryZoo/ReferenceSphere");
		CollisionShape3D collision = body.GetChildren().OfType<CollisionShape3D>().Single();
		Assert(collision.Shape is SphereShape3D shape && MathF.Abs(shape.Radius - 1.35f) <= 1e-6f, "Gallery sphere radius");
		Assert(body.IsInGroup("fixture_geometry") && body.IsInGroup("raytrace_geometry") && !body.IsInGroup("spatial_kernel_excluded"),
			"Gallery sphere authoritative groups");

		FrozenGeometrySnapshot snapshot = GodotAdapter.SpatialSnapshotBuilder.BuildFromGodotScene(this, "fixture_geometry", "fixture_background");
		Assert(snapshot.PrimitiveCount == 25 && snapshot.Spheres.Count == 1, "Gallery mixed primitive count");
		FrozenSphere sphere = snapshot.Spheres.Single();
		Assert(sphere.SurfaceClass == SpatialSurfaceClass.Geometry, "Gallery sphere Geometry class");
		Assert(MathF.Abs(sphere.Radius - 1.35f) <= 1e-6f, "Gallery frozen radius");

		GdVector3 center = body.GlobalPosition;
		GdVector3 from = center + new GdVector3(0f, 0f, 2f);
		GdVector3 to = center - new GdVector3(0f, 0f, 2f);
		Godot.Collections.Dictionary godot = IntersectGodot(from, to);
		Assert(godot.Count > 0 && ReferenceEquals(godot["collider"].AsGodotObject(), body), "Gallery Godot identifies ReferenceSphere");
		LinearScanSpatialQuery linear = new(snapshot);
		SpatialBvhQuery bvh = new(snapshot);
		Assert(linear.IntersectsSegment(ToNumerics(from), ToNumerics(to), 1, out SurfaceHit linearHit), "Gallery LinearScan sphere hit");
		Assert(bvh.IntersectsSegment(ToNumerics(from), ToNumerics(to), 1, out SurfaceHit bvhHit), "Gallery BVH sphere hit");
		CompareWitnesses(from, to, godot, linearHit, bvhHit, sphere.CanonicalPrimitiveId, 2e-5f, "Gallery ReferenceSphere");
		Assert(linearHit.SurfaceClass == SpatialSurfaceClass.Geometry && bvhHit.SurfaceClass == SpatialSurfaceClass.Geometry,
			"Gallery sphere witness semantics");

		GdVector3 missFrom = center + new GdVector3(0f, 2f, 2f);
		GdVector3 missTo = center + new GdVector3(0f, 2f, -2f);
		Godot.Collections.Dictionary godotMiss = IntersectGodot(missFrom, missTo);
		bool godotSphereMiss = godotMiss.Count == 0 || !ReferenceEquals(godotMiss["collider"].AsGodotObject(), body);
		bool linearMiss = !linear.IntersectsSegment(ToNumerics(missFrom), ToNumerics(missTo), 1, out SurfaceHit linearMissHit) ||
			linearMissHit.CanonicalPrimitiveId != sphere.CanonicalPrimitiveId;
		bool bvhMiss = !bvh.IntersectsSegment(ToNumerics(missFrom), ToNumerics(missTo), 1, out SurfaceHit bvhMissHit) ||
			bvhMissHit.CanonicalPrimitiveId != sphere.CanonicalPrimitiveId;
		Assert(godotSphereMiss && linearMiss && bvhMiss, "Gallery ReferenceSphere known miss");

		GD.Print($"SPATIAL_SPHERE_GALLERY primitives={snapshot.PrimitiveCount} schema={snapshot.EffectiveSchemaVersion} policy={snapshot.IntersectionPolicyVersion} " +
			$"geometry={snapshot.GeometrySnapshotSha256} primitive={sphere.CanonicalPrimitiveId} hitT={linearHit.SegmentT:R}");
	}

	private Godot.Collections.Dictionary IntersectGodot(GdVector3 from, GdVector3 to)
	{
		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, to, 1);
		query.CollideWithBodies = true;
		query.CollideWithAreas = false;
		query.HitFromInside = true;
		query.HitBackFaces = true;
		return GetViewport().World3D.DirectSpaceState.IntersectRay(query);
	}

	private static StaticBody3D CreateSphereBody(string name, Transform3D transform, float radius)
	{
		StaticBody3D body = new() { Name = name, Transform = transform, CollisionLayer = 1 };
		body.AddToGroup("fixture_geometry");
		body.AddToGroup("raytrace_geometry");
		body.AddChild(new CollisionShape3D { Name = "CollisionShape3D", Shape = new SphereShape3D { Radius = radius } });
		return body;
	}

	private void AssertSnapshotRejected(string label)
	{
		bool rejected = false;
		try { _ = GodotAdapter.SpatialSnapshotBuilder.BuildFromGodotScene(this); }
		catch (NotSupportedException) { rejected = true; }
		Assert(rejected, label + " rejected");
	}

	private static void CompareWitnesses(
		GdVector3 from, GdVector3 to, Godot.Collections.Dictionary godot,
		SurfaceHit linear, SurfaceHit bvh, string expectedId, float tolerance, string label)
	{
		GdVector3 godotPosition = (GdVector3)godot["position"];
		GdVector3 godotNormal = (GdVector3)godot["normal"];
		float godotT = (godotPosition - from).Dot(to - from) / (to - from).LengthSquared();
		Assert(linear.CanonicalPrimitiveId == expectedId && bvh.CanonicalPrimitiveId == expectedId, label + " primitive ID");
		Assert(MathF.Abs(linear.SegmentT - godotT) <= tolerance && MathF.Abs(bvh.SegmentT - godotT) <= tolerance, label + " segment t");
		AssertClose(linear.Position, ToNumerics(godotPosition), tolerance, label + " position");
		AssertClose(bvh.Position, ToNumerics(godotPosition), tolerance, label + " BVH position");
		AssertClose(linear.Normal, ToNumerics(godotNormal), tolerance, label + " normal");
		AssertClose(bvh.Normal, ToNumerics(godotNormal), tolerance, label + " BVH normal");
	}

	private static NumVector3 ToNumerics(GdVector3 value) => new(value.X, value.Y, value.Z);

	private static void AssertClose(NumVector3 actual, NumVector3 expected, float tolerance, string label)
	{
		if (NumVector3.Distance(actual, expected) > tolerance)
			throw new InvalidOperationException($"{label}: actual={actual} expected={expected} tolerance={tolerance:R}");
	}

	private static void Assert(bool condition, string label)
	{
		if (!condition) throw new InvalidOperationException(label);
	}
}
