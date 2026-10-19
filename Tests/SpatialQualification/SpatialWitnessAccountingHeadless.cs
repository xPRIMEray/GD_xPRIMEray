using Godot;
using GodotAdapter;
using System;
using System.Numerics;
using System.Text.Json;
using XPrimeRay.Core.Geometry;
using XPrimeRay.Spatial;
using NumVector3 = System.Numerics.Vector3;

public partial class SpatialWitnessAccountingHeadless : Node
{
	public override void _Ready() => CallDeferred(nameof(Run));

	private void Run()
	{
		try
		{
			FrozenGeometrySnapshot snapshot = new(new[]
			{
				new FrozenOrientedBox(
					"/Witness/Box:0", SpatialSurfaceClass.Geometry,
					Matrix4x4.Identity, Matrix4x4.Identity, NumVector3.One,
					new Aabb3(-NumVector3.One, NumVector3.One), 1, FrozenPrimitiveFlags.None)
			});
			RayBeamRenderer.RaySeg segment = new()
			{
				A = new Godot.Vector3(-2f, 0f, 0f),
				B = new Godot.Vector3(2f, 0f, 0f),
				TraveledB = 4f
			};
			using PhysicsRayQueryParameters3D parameters = PhysicsRayQueryParameters3D.Create(segment.A, segment.B, 1);
			RayBeamRenderer.FormalProbeQuery[] queries =
			{
				new RayBeamRenderer.FormalProbeQuery(0, 0, in segment, parameters)
			};

			SpatialKernelDualValidationResult matching = SpatialKernelDualValidator.Evaluate(
				snapshot, queries, new[] { 1 }, 1, new[] { true }, null);
			Assert(matching.GodotQueryComparisonCount == 1, "matching comparison executed");
			Assert(matching.MismatchQueryCount == 0, "matching query count");
			Assert(DivergenceCount(matching.QueryDivergenceJson) == 0, "matching detailed records empty");

			SpatialKernelDualValidationResult divergentWithoutDetails = SpatialKernelDualValidator.Evaluate(
				snapshot, queries, new[] { 1 }, 1, new[] { false }, null);
			Assert(divergentWithoutDetails.GodotQueryComparisonCount == 1, "divergent comparison executed");
			Assert(divergentWithoutDetails.MismatchQueryCount == 1, "divergence counted without observations");
			Assert(DivergenceCount(divergentWithoutDetails.QueryDivergenceJson) == 0, "detailed records remain optional");

			GodotReplayQueryObservation[] observations =
			{
				new(false, 0, string.Empty, string.Empty, -1, Godot.Vector3.Zero, Godot.Vector3.Zero)
			};
			SpatialKernelDualValidationResult divergentWithDetails = SpatialKernelDualValidator.Evaluate(
				snapshot, queries, new[] { 1 }, 1, new[] { false }, observations);
			Assert(divergentWithDetails.MismatchQueryCount == 1, "diagnostics do not change count");
			Assert(DivergenceCount(divergentWithDetails.QueryDivergenceJson) == 1, "diagnostics capture record");

			GD.Print("SPATIAL_WITNESS_ACCOUNTING PASS comparisons=1 matching=0 divergent=1 details_off=0 details_on=1");
			GetTree().Quit(0);
		}
		catch (Exception exception)
		{
			GD.PushError("SPATIAL_WITNESS_ACCOUNTING FAIL: " + exception);
			GetTree().Quit(1);
		}
	}

	private static int DivergenceCount(string json)
	{
		using JsonDocument document = JsonDocument.Parse(json);
		return document.RootElement.GetProperty("divergence_count").GetInt32();
	}

	private static void Assert(bool condition, string label)
	{
		if (!condition) throw new InvalidOperationException(label);
	}
}
