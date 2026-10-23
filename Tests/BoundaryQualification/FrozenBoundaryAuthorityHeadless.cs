using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public partial class FrozenBoundaryAuthorityHeadless : Node
{
	public override void _Ready() => CallDeferred(nameof(Run));

	private void Run()
	{
		try
		{
			QualifyIdentityAndOrdering();
			QualifyImmutableExecutionViewAndOwnership();
			QualifyEffectiveParametersAndValidation();
			QualifyCanonicalPathIdentity();
			QualifyLegacyInteractionOrderAndRetryReuse();
			QualifyLegacyCrossingMaskLimit();
			GD.Print("FROZEN_BOUNDARY_AUTHORITY PASS");
			GetTree().Quit(0);
		}
		catch (Exception exception)
		{
			GD.PushError("FROZEN_BOUNDARY_AUTHORITY FAIL: " + exception);
			GetTree().Quit(1);
		}
	}

	private void QualifyIdentityAndOrdering()
	{
		Node3D firstRoot = CreateLinkedPair("FirstRoot", out BoundaryLayerVolume firstGallery, out BoundaryLayerVolume firstEarth);
		AddChild(firstRoot);
		GodotAdapter.FrozenBoundarySnapshot first = Capture(firstRoot, firstGallery, firstEarth);
		GodotAdapter.FrozenBoundarySnapshot reversed = Capture(firstRoot, firstEarth, firstGallery);
		Assert(first.ContentSha256 == reversed.ContentSha256, "canonical content hash must ignore enumeration order");
		Assert(first.ExecutionAuthoritySha256 != reversed.ExecutionAuthoritySha256, "execution hash must retain legacy order");
		Assert(first.RecordsInExecutionOrder[0].CanonicalId.EndsWith("GalleryBoundary", StringComparison.Ordinal), "Gallery first legacy ordinal");
		Assert(first.RecordsInExecutionOrder[1].CanonicalId.EndsWith("EarthBoundary", StringComparison.Ordinal), "Earth second legacy ordinal");
		Assert(first.RecordsInExecutionOrder[0].Effective.LinkedLayerIndex == 1, "Gallery linked ordinal");
		Assert(first.RecordsInExecutionOrder[1].Effective.LinkedLayerIndex == 0, "Earth linked ordinal");
		Assert(first.RecordsInExecutionOrder[0].LinkedCanonicalId.EndsWith("EarthBoundary", StringComparison.Ordinal), "linked canonical identity");
		Assert(Mathf.IsEqualApprox(first.RecordsInExecutionOrder[0].Effective.LinkedRadius, 1.1f), "linked effective radius");
		Assert(first.RecordsInExecutionOrder[0].Effective.LinkedCenter.IsEqualApprox(firstEarth.GlobalPosition), "linked effective transform/center");

		ulong firstRuntimeId = firstGallery.GetInstanceId();
		RemoveChild(firstRoot);
		firstRoot.Free();
		Node3D secondRoot = CreateLinkedPair("SecondRoot", out BoundaryLayerVolume secondGallery, out BoundaryLayerVolume secondEarth);
		AddChild(secondRoot);
		GodotAdapter.FrozenBoundarySnapshot second = Capture(secondRoot, secondGallery, secondEarth);
		Assert(firstRuntimeId != secondGallery.GetInstanceId(), "fixture must replace runtime instances");
		Assert(first.ContentSha256 == second.ContentSha256, "canonical identity must ignore runtime instance ids");
		Assert(first.ExecutionAuthoritySha256 == second.ExecutionAuthoritySha256, "equivalent execution authority must survive instance replacement");
		Assert(first.CopyCanonicalSerialization().SequenceEqual(second.CopyCanonicalSerialization()), "canonical serialization exact bytes");
		Assert(first.CopyExecutionSerialization().SequenceEqual(second.CopyExecutionSerialization()), "execution serialization exact bytes");

		RayBeamRenderer.BoundaryLayerSnap[] equivalentCache = second.CopyExecutionLayers();
		Assert(second.IsExactlyEquivalentToRendererCache(equivalentCache, out _), "scene/cache effective equivalence");
		equivalentCache[0].IOR = MathF.BitIncrement(equivalentCache[0].IOR);
		Assert(!second.IsExactlyEquivalentToRendererCache(equivalentCache, out _), "scene/cache effective mismatch detection");

		GD.Print($"FROZEN_BOUNDARY identity content={second.ContentSha256} execution={second.ExecutionAuthoritySha256} order=GalleryBoundary,EarthBoundary");
		RemoveChild(secondRoot);
		secondRoot.Free();
	}

	private void QualifyEffectiveParametersAndValidation()
	{
		Node3D root = CreateLinkedPair("MutationRoot", out BoundaryLayerVolume gallery, out BoundaryLayerVolume earth);
		AddChild(root);
		string baseline = Capture(root, gallery, earth).ContentSha256;
		var mutations = new List<(string Label, Action Mutate, Action Restore)>
		{
			("transform", () => gallery.Position += new Vector3(0.125f, 0f, 0f), () => gallery.Position -= new Vector3(0.125f, 0f, 0f)),
			("shape", () => gallery.ShapeType = BoundaryLayerVolume.BoundaryShapeType.Box, () => gallery.ShapeType = BoundaryLayerVolume.BoundaryShapeType.Sphere),
			("radius", () => gallery.Radius = MathF.BitIncrement(gallery.Radius), () => gallery.Radius = 1.1f),
			("execution_mode", () => { gallery.Behavior = BoundaryLayerVolume.BoundaryBehavior.DirectionBias; gallery.ExecutionMode = BoundaryLayerVolume.BoundaryExecutionMode.Continuous; }, () => { gallery.ExecutionMode = BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent; gallery.Behavior = BoundaryLayerVolume.BoundaryBehavior.SceneTransform; }),
			("crossing_policy", () => gallery.CrossingPolicy = BoundaryLayerVolume.BoundaryCrossingPolicy.EntryAndExit, () => gallery.CrossingPolicy = BoundaryLayerVolume.BoundaryCrossingPolicy.EntryOnly),
			("behavior", () => gallery.Behavior = BoundaryLayerVolume.BoundaryBehavior.DielectricRefraction, () => gallery.Behavior = BoundaryLayerVolume.BoundaryBehavior.SceneTransform),
			("bias_direction", () => gallery.BiasDirection = Vector3.Right, () => gallery.BiasDirection = Vector3.Up),
			("bias_strength", () => gallery.BiasStrength = 0.25f, () => gallery.BiasStrength = 0.02f),
			("exit_offset", () => gallery.SceneTransformExitOffset = 0.41f, () => gallery.SceneTransformExitOffset = 0.4f),
			("ior", () => gallery.IOR = MathF.BitIncrement(gallery.IOR), () => gallery.IOR = 1.5f),
		};

		foreach ((string label, Action mutate, Action restore) in mutations)
		{
			mutate();
			string changed = Capture(root, gallery, earth).ContentSha256;
			Assert(changed != baseline, $"effective mutation must change content hash: {label}");
			restore();
			Assert(Capture(root, gallery, earth).ContentSha256 == baseline, $"mutation restore: {label}");
		}

		gallery.ShapeType = BoundaryLayerVolume.BoundaryShapeType.Box;
		string boxBaseline = Capture(root, gallery, earth).ContentSha256;
		gallery.BoxExtents += new Vector3(0f, 0.125f, 0f);
		Assert(Capture(root, gallery, earth).ContentSha256 != boxBaseline, "effective box extents mutation");
		gallery.BoxExtents -= new Vector3(0f, 0.125f, 0f);
		gallery.ShapeType = BoundaryLayerVolume.BoundaryShapeType.Sphere;
		Assert(Capture(root, gallery, earth).ContentSha256 == baseline, "box mutation restore");

		Color tint = gallery.MaterialTint;
		bool debug = gallery.DebugLogCrossings;
		float reflectivity = gallery.Reflectivity;
		gallery.MaterialTint = Colors.Magenta;
		gallery.DebugLogCrossings = !debug;
		gallery.Reflectivity = 0.123f;
		Assert(Capture(root, gallery, earth).ContentSha256 == baseline, "presentation/debug/unused reflectivity excluded from scientific hash");
		gallery.MaterialTint = tint;
		gallery.DebugLogCrossings = debug;
		gallery.Reflectivity = reflectivity;

		NodePath validLink = gallery.LinkedBoundaryPath;
		gallery.LinkedBoundaryPath = new NodePath("../MissingBoundary");
		AssertThrows(() => Capture(root, gallery, earth), "missing linked-node rejection");
		gallery.LinkedBoundaryPath = validLink;
		AssertThrows(() => Capture(root, gallery, gallery), "duplicate canonical-id rejection");
		gallery.Position = new Vector3(float.NaN, 0f, 0f);
		AssertThrows(() => Capture(root, gallery, earth), "non-finite transform rejection");
		gallery.Position = new Vector3(0f, 1.1f, -2f);
		gallery.Scale = new Vector3(0f, 1f, 1f);
		AssertThrows(() => Capture(root, gallery, earth), "singular transform rejection");
		gallery.Scale = Vector3.One;

		gallery.ShapeType = BoundaryLayerVolume.BoundaryShapeType.Box;
		gallery.BoxExtents = new Vector3(1f, -0.25f, 1f);
		AssertThrows(() => Capture(root, gallery, earth), "negative box half-extents rejection");
		gallery.ShapeType = BoundaryLayerVolume.BoundaryShapeType.Sphere;
		gallery.BoxExtents = Vector3.One;

		gallery.ExecutionMode = BoundaryLayerVolume.BoundaryExecutionMode.Continuous;
		gallery.Behavior = BoundaryLayerVolume.BoundaryBehavior.PerfectMirrorReflection;
		AssertThrows(() => Capture(root, gallery, earth), "unsupported Continuous behavior rejection");
		gallery.ExecutionMode = BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent;
		gallery.Behavior = BoundaryLayerVolume.BoundaryBehavior.SceneTransform;
		earth.Radius = -0.5f;
		AssertThrows(() => Capture(root, gallery, earth), "negative linked BoundaryLayerVolume radius rejection");
		earth.Radius = 1.1f;

		Node3D genericLink = new() { Name = "GenericLink", Scale = new Vector3(0f, 1f, 1f) };
		root.AddChild(genericLink);
		gallery.LinkedBoundaryPath = new NodePath("../GenericLink");
		AssertThrows(() => Capture(root, gallery, earth), "singular active linked transform rejection");
		genericLink.Scale = Vector3.One;
		gallery.Radius = -0.5f;
		AssertThrows(() => Capture(root, gallery, earth), "negative effective linked radius rejection");
		gallery.Radius = 1.1f;
		gallery.LinkedBoundaryPath = validLink;

		RemoveChild(root);
		root.Free();
	}

	private void QualifyImmutableExecutionViewAndOwnership()
	{
		Node3D root = CreateLinkedPair("ImmutableRoot", out BoundaryLayerVolume gallery, out BoundaryLayerVolume earth);
		AddChild(root);
		GodotAdapter.FrozenBoundarySnapshot snapshot = Capture(root, gallery, earth);
		string executionHash = snapshot.ExecutionAuthoritySha256;
		Vector3 consumedBefore = InvokeBoundaryEntry(snapshot.ExecutionView);

		RayBeamRenderer.BoundaryLayerSnap[] copy = snapshot.CopyExecutionLayers();
		copy[0].BiasStrength = 1f;
		copy[0].IOR = 4f;
		Assert(snapshot.ExecutionAuthoritySha256 == executionHash, "copy mutation cannot change captured execution identity");
		Assert(Mathf.IsEqualApprox(snapshot.RecordsInExecutionOrder[0].Effective.BiasStrength, 0.02f), "copy mutation cannot change captured record");
		Assert(InvokeBoundaryEntry(snapshot.ExecutionView).IsEqualApprox(consumedBefore), "copy mutation cannot change consumed execution values");

		if (snapshot.ExecutionView is IList<RayBeamRenderer.BoundaryLayerSnap> listView)
		{
			AssertThrows<NotSupportedException>(() => listView[0] = copy[0], "read-only execution view rejects indexed writes");
		}

		var renderer = new RayBeamRenderer();
		renderer.BeginBoundaryExecutionOwnershipDiagnosticsForTesting();
		Assert(
			renderer.ValidateBoundaryExecutionOwnershipForTesting(snapshot, RayBeamRenderer.BoundaryExecutionOwnership.FrozenFormal) == snapshot.Count,
			"Formal ownership resolves captured execution view");
		AssertThrows<InvalidOperationException>(
			() => renderer.ValidateBoundaryExecutionOwnershipForTesting(null, RayBeamRenderer.BoundaryExecutionOwnership.FrozenFormal),
			"missing Formal snapshot must fail closed");
		Assert(
			renderer.ValidateBoundaryExecutionOwnershipForTesting(null, RayBeamRenderer.BoundaryExecutionOwnership.LiveCache) == 0,
			"explicit LIVE ownership retains renderer-cache fallback");
		RayBeamRenderer.BoundaryExecutionOwnershipDiagnostics ownership =
			renderer.GetBoundaryExecutionOwnershipDiagnosticsForTesting();
		Assert(ownership.FrozenFormalResolveCount == 1, "actual resolver records frozen Formal execution");
		Assert(ownership.MissingFrozenFormalRejectCount == 1, "actual resolver records missing Formal rejection");
		Assert(ownership.LiveCacheResolveCount == 1, "actual resolver records explicit LIVE execution");
		Assert(ownership.FrozenFormalIdentityMismatchCount == 0, "one frozen identity remains consistent");
		Assert(ownership.FrozenFormalExecutionSha256 == snapshot.ExecutionAuthoritySha256, "consumed frozen identity matches captured hash");
		renderer.EndBoundaryExecutionOwnershipDiagnosticsForTesting();
		renderer.Dispose();

		RemoveChild(root);
		root.Free();
	}

	private void QualifyCanonicalPathIdentity()
	{
		Node3D root = new() { Name = "IdentityRoot" };
		AddChild(root);
		BoundaryLayerVolume leading = new() { Name = " Boundary" };
		BoundaryLayerVolume trailing = new() { Name = "Boundary " };
		BoundaryLayerVolume backslash = new() { Name = "Boundary\\Marker" };
		root.AddChild(leading);
		root.AddChild(trailing);
		root.AddChild(backslash);
		GodotAdapter.FrozenBoundarySnapshot snapshot = Capture(root, leading, trailing, backslash);
		string leadingId = snapshot.RecordsInExecutionOrder[0].CanonicalId;
		string trailingId = snapshot.RecordsInExecutionOrder[1].CanonicalId;
		string backslashId = snapshot.RecordsInExecutionOrder[2].CanonicalId;
		Assert(leadingId != trailingId, "leading and trailing whitespace retain distinct canonical identities");
		Assert(leadingId.EndsWith(" Boundary", StringComparison.Ordinal), "leading whitespace is preserved exactly");
		Assert(trailingId.EndsWith("Boundary ", StringComparison.Ordinal), "trailing whitespace is preserved exactly");
		Assert(backslashId.EndsWith("Boundary\\Marker", StringComparison.Ordinal), "backslash is preserved without slash normalization");

		RemoveChild(root);
		root.Free();
	}

	private void QualifyLegacyCrossingMaskLimit()
	{
		Node3D root = new() { Name = "MaskRoot" };
		AddChild(root);
		var nodes = new Godot.Collections.Array<Node>();
		for (int i = 0; i < 33; i++)
		{
			BoundaryLayerVolume layer = new()
			{
				Name = $"Layer{i:D2}",
				ExecutionMode = BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent,
				Behavior = BoundaryLayerVolume.BoundaryBehavior.DirectionBias,
			};
			root.AddChild(layer);
			nodes.Add(layer);
		}
		GodotAdapter.FrozenBoundarySnapshot snapshot = GodotAdapter.FrozenBoundarySnapshot.Capture(root, nodes);
		Assert(snapshot.Count == 33, "33-layer snapshot count");
		for (int i = 0; i < 32; i++)
			Assert(snapshot.RecordsInExecutionOrder[i].CrossingTrackedByLegacyMask, $"crossing bit {i}");
		Assert(!snapshot.RecordsInExecutionOrder[32].CrossingTrackedByLegacyMask, "33rd layer preserves legacy untracked limit");
		RemoveChild(root);
		root.Free();
	}

	private void QualifyLegacyInteractionOrderAndRetryReuse()
	{
		Node3D root = new() { Name = "OverlapRoot" };
		AddChild(root);
		BoundaryLayerVolume right = new()
		{
			Name = "RightBias",
			ExecutionMode = BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent,
			CrossingPolicy = BoundaryLayerVolume.BoundaryCrossingPolicy.EntryOnly,
			Behavior = BoundaryLayerVolume.BoundaryBehavior.DirectionBias,
			Radius = 1f,
			BiasDirection = Vector3.Right,
			BiasStrength = 0.6f,
		};
		BoundaryLayerVolume up = new()
		{
			Name = "UpBias",
			ExecutionMode = BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent,
			CrossingPolicy = BoundaryLayerVolume.BoundaryCrossingPolicy.EntryOnly,
			Behavior = BoundaryLayerVolume.BoundaryBehavior.DirectionBias,
			Radius = 1f,
			BiasDirection = Vector3.Up,
			BiasStrength = 0.6f,
		};
		root.AddChild(right);
		root.AddChild(up);

		GodotAdapter.FrozenBoundarySnapshot rightThenUp = Capture(root, right, up);
		GodotAdapter.FrozenBoundarySnapshot upThenRight = Capture(root, up, right);
		Vector3 first = InvokeBoundaryEntry(rightThenUp.ExecutionView);
		Vector3 retry = InvokeBoundaryEntry(rightThenUp.ExecutionView);
		Vector3 reversed = InvokeBoundaryEntry(upThenRight.ExecutionView);
		Assert(first.IsEqualApprox(retry), "pixel retry reuses identical frozen execution state");
		Assert(!first.IsEqualApprox(reversed), "overlapping simultaneous crossings retain array execution order");

		// A scene/cache rebuild after capture cannot mutate the acquisition-owned array.
		right.BiasStrength = 0.1f;
		GodotAdapter.FrozenBoundarySnapshot rebuilt = Capture(root, right, up);
		Assert(rebuilt.ExecutionAuthoritySha256 != rightThenUp.ExecutionAuthoritySha256, "rebuild observes changed scene state");
		Assert(InvokeBoundaryEntry(rightThenUp.ExecutionView).IsEqualApprox(first), "captured view survives rebuild unchanged");

		RemoveChild(root);
		root.Free();
	}

	private static Vector3 InvokeBoundaryEntry(IReadOnlyList<RayBeamRenderer.BoundaryLayerSnap> layers)
	{
		MethodInfo method = typeof(RayBeamRenderer).GetMethod(
			"ApplyBoundaryLayerInteractions",
			BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("production boundary interaction method unavailable");
		var renderer = new RayBeamRenderer();
		object[] arguments = { Vector3.Zero, Vector3.Back, layers, 0u };
		method.Invoke(renderer, arguments);
		renderer.Dispose();
		return (Vector3)arguments[1];
	}

	private static Node3D CreateLinkedPair(
		string rootName,
		out BoundaryLayerVolume gallery,
		out BoundaryLayerVolume earth)
	{
		Node3D root = new() { Name = rootName };
		gallery = CreatePortalBoundary("GalleryBoundary", new Vector3(0f, 1.1f, -2f), "../EarthBoundary");
		earth = CreatePortalBoundary("EarthBoundary", new Vector3(0f, 1.1f, 2f), "../GalleryBoundary");
		root.AddChild(gallery);
		root.AddChild(earth);
		return root;
	}

	private static BoundaryLayerVolume CreatePortalBoundary(string name, Vector3 position, string link)
	{
		return new BoundaryLayerVolume
		{
			Name = name,
			Position = position,
			Enabled = true,
			ShapeType = BoundaryLayerVolume.BoundaryShapeType.Sphere,
			Radius = 1.1f,
			ExecutionMode = BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent,
			CrossingPolicy = BoundaryLayerVolume.BoundaryCrossingPolicy.EntryOnly,
			Behavior = BoundaryLayerVolume.BoundaryBehavior.SceneTransform,
			BiasDirection = Vector3.Up,
			BiasStrength = 0.02f,
			IOR = 1.5f,
			SceneTransformExitOffset = 0.4f,
			LinkedBoundaryPath = new NodePath(link),
		};
	}

	private static GodotAdapter.FrozenBoundarySnapshot Capture(Node root, params BoundaryLayerVolume[] boundaries)
	{
		var nodes = new Godot.Collections.Array<Node>();
		foreach (BoundaryLayerVolume boundary in boundaries)
			nodes.Add(boundary);
		return GodotAdapter.FrozenBoundarySnapshot.Capture(root, nodes);
	}

	private static void AssertThrows(Action action, string label)
	{
		try { action(); }
		catch (InvalidOperationException) { return; }
		throw new InvalidOperationException(label);
	}

	private static void AssertThrows<TException>(Action action, string label) where TException : Exception
	{
		try { action(); }
		catch (TException) { return; }
		throw new InvalidOperationException(label);
	}

	private static void Assert(bool condition, string label)
	{
		if (!condition) throw new InvalidOperationException(label);
	}
}
