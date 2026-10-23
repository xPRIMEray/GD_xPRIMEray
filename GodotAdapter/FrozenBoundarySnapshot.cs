using Godot;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GodotAdapter;

/// <summary>
/// Immutable Formal authority over the effective BoundaryLayerVolume state consumed by
/// RayBeamRenderer. Canonical content identity is independent of scene-group enumeration;
/// ExecutionAuthoritySha256 intentionally retains the legacy execution order and 32-bit
/// crossing-mask ordinals.
/// </summary>
public sealed class FrozenBoundarySnapshot
{
	public const string SchemaId = "FrozenBoundarySnapshot-v1-le";
	public const string ExecutionPolicyId = "boundary-layer-execution-v1";

	public sealed class BoundaryRecord
	{
		public string CanonicalId { get; }
		public int LegacyExecutionOrdinal { get; }
		public string LinkedCanonicalId { get; }
		public bool CrossingTrackedByLegacyMask { get; }
		public RayBeamRenderer.BoundaryLayerSnap Effective { get; }

		internal BoundaryRecord(
			string canonicalId,
			int legacyExecutionOrdinal,
			string linkedCanonicalId,
			bool crossingTrackedByLegacyMask,
			in RayBeamRenderer.BoundaryLayerSnap effective)
		{
			CanonicalId = canonicalId;
			LegacyExecutionOrdinal = legacyExecutionOrdinal;
			LinkedCanonicalId = linkedCanonicalId ?? string.Empty;
			CrossingTrackedByLegacyMask = crossingTrackedByLegacyMask;
			Effective = effective;
		}
	}

	private readonly BoundaryRecord[] _recordsInExecutionOrder;
	private readonly ReadOnlyCollection<BoundaryRecord> _readOnlyRecords;
	private readonly RayBeamRenderer.BoundaryLayerSnap[] _executionLayers;
	private readonly ReadOnlyCollection<RayBeamRenderer.BoundaryLayerSnap> _readOnlyExecutionLayers;
	private readonly byte[] _canonicalBytes;
	private readonly byte[] _executionBytes;

	public IReadOnlyList<BoundaryRecord> RecordsInExecutionOrder => _readOnlyRecords;
	public string ContentSha256 { get; }
	public string ExecutionAuthoritySha256 { get; }
	public int Count => _recordsInExecutionOrder.Length;

	// BoundaryLayerSnap is a mutable value type. The backing array therefore remains private,
	// and consumers receive only a ReadOnlyCollection whose indexer returns a value copy.
	// Casting the view to IList<T> still cannot mutate it (ReadOnlyCollection rejects writes).
	internal IReadOnlyList<RayBeamRenderer.BoundaryLayerSnap> ExecutionView => _readOnlyExecutionLayers;

	private FrozenBoundarySnapshot(BoundaryRecord[] records)
	{
		_recordsInExecutionOrder = records ?? Array.Empty<BoundaryRecord>();
		_readOnlyRecords = Array.AsReadOnly(_recordsInExecutionOrder);
		_executionLayers = new RayBeamRenderer.BoundaryLayerSnap[_recordsInExecutionOrder.Length];
		for (int i = 0; i < _recordsInExecutionOrder.Length; i++)
			_executionLayers[i] = _recordsInExecutionOrder[i].Effective;
		_readOnlyExecutionLayers = Array.AsReadOnly(_executionLayers);

		_canonicalBytes = SerializeCanonical(_recordsInExecutionOrder);
		_executionBytes = SerializeExecution(_recordsInExecutionOrder);
		ContentSha256 = Sha256Hex(_canonicalBytes);
		ExecutionAuthoritySha256 = Sha256Hex(_executionBytes);
	}

	public RayBeamRenderer.BoundaryLayerSnap[] CopyExecutionLayers()
	{
		return (RayBeamRenderer.BoundaryLayerSnap[])_executionLayers.Clone();
	}

	public byte[] CopyCanonicalSerialization() => (byte[])_canonicalBytes.Clone();
	public byte[] CopyExecutionSerialization() => (byte[])_executionBytes.Clone();

	public static FrozenBoundarySnapshot Capture(
		Node sceneRoot,
		Godot.Collections.Array<Node> boundaryNodes)
	{
		if (sceneRoot == null)
			throw new InvalidOperationException("Formal boundary capture requires a scene root.");

		boundaryNodes ??= new Godot.Collections.Array<Node>();
		var active = new List<(BoundaryLayerVolume Node, string Id)>(boundaryNodes.Count);
		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (Node node in boundaryNodes)
		{
			if (node is not BoundaryLayerVolume boundary || !boundary.Enabled)
				continue;
			string id = CanonicalIdFor(sceneRoot, boundary);
			if (!ids.Add(id))
				throw new InvalidOperationException($"Duplicate canonical boundary id '{id}'.");
			active.Add((boundary, id));
		}

		var indexByInstanceId = new Dictionary<ulong, int>(active.Count);
		for (int i = 0; i < active.Count; i++)
			indexByInstanceId.Add(active[i].Node.GetInstanceId(), i);

		var records = new BoundaryRecord[active.Count];
		for (int i = 0; i < active.Count; i++)
		{
			BoundaryLayerVolume boundary = active[i].Node;
			RayBeamRenderer.BoundaryLayerSnap snap = BuildEffectiveSnap(boundary);
			string linkedId = string.Empty;
			Node3D linkedNode = null;
			if (!boundary.LinkedBoundaryPath.IsEmpty)
			{
				linkedNode = boundary.GetNodeOrNull<Node3D>(boundary.LinkedBoundaryPath);
				if (linkedNode == null)
					throw new InvalidOperationException($"Boundary '{active[i].Id}' has a missing linked node '{boundary.LinkedBoundaryPath}'.");
				if (linkedNode is BoundaryLayerVolume linkedBoundary && linkedBoundary.Radius < 0f)
					throw new InvalidOperationException($"Boundary '{active[i].Id}' has a negative linked-boundary radius.");
				linkedId = CanonicalIdFor(sceneRoot, linkedNode);
			}

			if (boundary.Behavior == BoundaryLayerVolume.BoundaryBehavior.SceneTransform && linkedNode == null)
				throw new InvalidOperationException($"SceneTransform boundary '{active[i].Id}' requires a valid linked node.");

			snap.LinkedLayerIndex = -1;
			if (snap.LinkedNodeInstanceId != 0 &&
				indexByInstanceId.TryGetValue(snap.LinkedNodeInstanceId, out int linkedIndex))
			{
				snap.LinkedLayerIndex = linkedIndex;
			}

			ValidateEffective(active[i].Id, in snap);
			bool crossingTracked = snap.ExecutionMode != BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent || i < 32;
			records[i] = new BoundaryRecord(active[i].Id, i, linkedId, crossingTracked, in snap);
		}

		return new FrozenBoundarySnapshot(records);
	}

	public bool IsExactlyEquivalentToRendererCache(
		RayBeamRenderer.BoundaryLayerSnap[] rendererLayers,
		out string reason)
	{
		rendererLayers ??= Array.Empty<RayBeamRenderer.BoundaryLayerSnap>();
		if (rendererLayers.Length != _executionLayers.Length)
		{
			reason = $"boundary_count scene={_executionLayers.Length} renderer={rendererLayers.Length}";
			return false;
		}
		for (int i = 0; i < _executionLayers.Length; i++)
		{
			if (!EffectiveEquals(in _executionLayers[i], in rendererLayers[i]))
			{
				reason = $"boundary_effective_state ordinal={i} canonical_id={_recordsInExecutionOrder[i].CanonicalId}";
				return false;
			}
		}
		reason = string.Empty;
		return true;
	}

	private static RayBeamRenderer.BoundaryLayerSnap BuildEffectiveSnap(BoundaryLayerVolume boundary)
	{
		bool isBox = boundary.ShapeType == BoundaryLayerVolume.BoundaryShapeType.Box;
		Transform3D worldFromLocal = boundary.GlobalTransform;
		Node3D linkedNode = boundary.LinkedBoundaryPath.IsEmpty
			? null
			: boundary.GetNodeOrNull<Node3D>(boundary.LinkedBoundaryPath);
		float linkedRadius = boundary.Radius;
		if (linkedNode is BoundaryLayerVolume linkedBoundary)
			linkedRadius = Mathf.Max(0f, linkedBoundary.Radius);
		Vector3 biasWorld = worldFromLocal.Basis * boundary.BiasDirection;
		float biasLength = biasWorld.Length();
		if (biasLength > FieldMath.Epsilon)
			biasWorld /= biasLength;

		return new RayBeamRenderer.BoundaryLayerSnap
		{
			Enabled = true,
			NodeInstanceId = boundary.GetInstanceId(),
			Center = boundary.GlobalPosition,
			WorldFromLocal = worldFromLocal,
			LocalFromWorld = worldFromLocal.AffineInverse(),
			ShapeType = boundary.ShapeType,
			Radius = Mathf.Max(0f, boundary.Radius),
			HalfExtents = isBox ? boundary.BoxExtents : Vector3.Zero,
			InvBasis = isBox ? worldFromLocal.Basis.Inverse() : default,
			ExecutionMode = boundary.ExecutionMode,
			CrossingPolicy = boundary.CrossingPolicy,
			Behavior = boundary.Behavior,
			BiasDirection = biasWorld,
			BiasStrength = Mathf.Clamp(boundary.BiasStrength, 0f, 1f),
			HasLinkedTransform = linkedNode != null,
			LinkedNodeInstanceId = linkedNode?.GetInstanceId() ?? 0,
			LinkedLayerIndex = -1,
			LinkedWorldFromLocal = linkedNode?.GlobalTransform ?? default,
			LinkedCenter = linkedNode?.GlobalPosition ?? Vector3.Zero,
			LinkedRadius = linkedRadius,
			SceneTransformExitOffset = Mathf.Max(0f, boundary.SceneTransformExitOffset),
			IOR = Mathf.Max(0.1f, boundary.IOR),
			Reflectivity = Mathf.Clamp(boundary.Reflectivity, 0f, 1f),
			MaterialTint = boundary.MaterialTint,
			NodeName = boundary.Name,
			DebugLogCrossings = boundary.DebugLogCrossings,
		};
	}

	private static string CanonicalIdFor(Node sceneRoot, Node node)
	{
		if (node == null || !GodotObject.IsInstanceValid(node))
			throw new InvalidOperationException("Boundary canonical identity cannot reference an invalid node.");
		NodePath relativePath = sceneRoot.GetPathTo(node);
		string relative = relativePath.ToString();
		if (relativePath.IsAbsolute() || string.IsNullOrEmpty(relative) || relative == "." || relative == ".." ||
			relative.StartsWith("../", StringComparison.Ordinal))
			throw new InvalidOperationException($"Node '{node.GetPath()}' is not a stable descendant of scene root '{sceneRoot.GetPath()}'.");
		return "boundary-v1:" + relative;
	}

	private static void ValidateEffective(string id, in RayBeamRenderer.BoundaryLayerSnap snap)
	{
		if (!IsFinite(snap.Center) || !IsFinite(snap.WorldFromLocal) || !IsFinite(snap.LocalFromWorld) ||
			!IsFinite(snap.Radius) || !IsFinite(snap.HalfExtents) || !IsFinite(snap.BiasDirection) ||
			!IsFinite(snap.BiasStrength) || !IsFinite(snap.IOR) || !IsFinite(snap.SceneTransformExitOffset))
			throw new InvalidOperationException($"Boundary '{id}' contains non-finite effective state.");
		if (Mathf.Abs(snap.WorldFromLocal.Basis.Determinant()) <= 1e-12f)
			throw new InvalidOperationException($"Boundary '{id}' has a singular world transform.");
		if (snap.HasLinkedTransform && (!IsFinite(snap.LinkedWorldFromLocal) || !IsFinite(snap.LinkedCenter) || !IsFinite(snap.LinkedRadius)))
			throw new InvalidOperationException($"Boundary '{id}' contains invalid linked transform state.");
		if (snap.ShapeType == BoundaryLayerVolume.BoundaryShapeType.Box &&
			(snap.HalfExtents.X < 0f || snap.HalfExtents.Y < 0f || snap.HalfExtents.Z < 0f))
			throw new InvalidOperationException($"Boundary '{id}' has negative box half-extents.");
		if (snap.HasLinkedTransform && snap.LinkedRadius < 0f)
			throw new InvalidOperationException($"Boundary '{id}' has a negative effective linked radius.");
		if (snap.Behavior == BoundaryLayerVolume.BoundaryBehavior.SceneTransform && snap.HasLinkedTransform &&
			Mathf.Abs(snap.LinkedWorldFromLocal.Basis.Determinant()) <= 1e-12f)
			throw new InvalidOperationException($"Boundary '{id}' has a singular scientifically active linked transform.");
		if (snap.ExecutionMode == BoundaryLayerVolume.BoundaryExecutionMode.Continuous &&
			snap.Behavior != BoundaryLayerVolume.BoundaryBehavior.DirectionBias)
			throw new InvalidOperationException($"Boundary '{id}' uses unsupported Continuous behavior '{snap.Behavior}'.");
		if (!Enum.IsDefined(snap.ShapeType) || !Enum.IsDefined(snap.ExecutionMode) ||
			!Enum.IsDefined(snap.CrossingPolicy) || !Enum.IsDefined(snap.Behavior))
			throw new InvalidOperationException($"Boundary '{id}' contains an unsupported enum value.");
	}

	private static bool EffectiveEquals(
		in RayBeamRenderer.BoundaryLayerSnap a,
		in RayBeamRenderer.BoundaryLayerSnap b)
	{
		return a.Enabled == b.Enabled &&
			a.NodeInstanceId == b.NodeInstanceId &&
			VectorBitsEqual(a.Center, b.Center) &&
			TransformBitsEqual(a.WorldFromLocal, b.WorldFromLocal) &&
			TransformBitsEqual(a.LocalFromWorld, b.LocalFromWorld) &&
			a.ShapeType == b.ShapeType && FloatBitsEqual(a.Radius, b.Radius) &&
			VectorBitsEqual(a.HalfExtents, b.HalfExtents) && BasisBitsEqual(a.InvBasis, b.InvBasis) &&
			a.ExecutionMode == b.ExecutionMode && a.CrossingPolicy == b.CrossingPolicy && a.Behavior == b.Behavior &&
			VectorBitsEqual(a.BiasDirection, b.BiasDirection) && FloatBitsEqual(a.BiasStrength, b.BiasStrength) &&
			a.HasLinkedTransform == b.HasLinkedTransform && a.LinkedNodeInstanceId == b.LinkedNodeInstanceId &&
			a.LinkedLayerIndex == b.LinkedLayerIndex && TransformBitsEqual(a.LinkedWorldFromLocal, b.LinkedWorldFromLocal) &&
			VectorBitsEqual(a.LinkedCenter, b.LinkedCenter) && FloatBitsEqual(a.LinkedRadius, b.LinkedRadius) &&
			FloatBitsEqual(a.SceneTransformExitOffset, b.SceneTransformExitOffset) && FloatBitsEqual(a.IOR, b.IOR);
	}

	private static byte[] SerializeCanonical(IEnumerable<BoundaryRecord> records)
	{
		BoundaryRecord[] ordered = records.OrderBy(r => r.CanonicalId, StringComparer.Ordinal).ToArray();
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
		WriteString(writer, SchemaId);
		WriteString(writer, ExecutionPolicyId);
		WriteInt32(writer, ordered.Length);
		foreach (BoundaryRecord record in ordered)
			WriteRecord(writer, record, includeExecutionOrdinal: false);
		writer.Flush();
		return stream.ToArray();
	}

	private static byte[] SerializeExecution(IEnumerable<BoundaryRecord> records)
	{
		BoundaryRecord[] ordered = records.OrderBy(r => r.LegacyExecutionOrdinal).ToArray();
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
		WriteString(writer, SchemaId + "/execution-authority");
		WriteString(writer, ExecutionPolicyId);
		WriteInt32(writer, ordered.Length);
		foreach (BoundaryRecord record in ordered)
			WriteRecord(writer, record, includeExecutionOrdinal: true);
		writer.Flush();
		return stream.ToArray();
	}

	private static void WriteRecord(BinaryWriter writer, BoundaryRecord record, bool includeExecutionOrdinal)
	{
		RayBeamRenderer.BoundaryLayerSnap s = record.Effective;
		WriteString(writer, record.CanonicalId);
		if (includeExecutionOrdinal)
		{
			WriteInt32(writer, record.LegacyExecutionOrdinal);
			WriteInt32(writer, s.LinkedLayerIndex);
			writer.Write(record.CrossingTrackedByLegacyMask);
		}
		writer.Write(s.Enabled);
		WriteInt32(writer, (int)s.ShapeType);
		WriteVector3(writer, s.Center);
		WriteTransform3D(writer, s.WorldFromLocal);
		WriteTransform3D(writer, s.LocalFromWorld);
		WriteSingle(writer, s.Radius);
		WriteVector3(writer, s.HalfExtents);
		WriteBasis(writer, s.InvBasis);
		WriteInt32(writer, (int)s.ExecutionMode);
		WriteInt32(writer, (int)s.CrossingPolicy);
		WriteInt32(writer, (int)s.Behavior);
		WriteVector3(writer, s.BiasDirection);
		WriteSingle(writer, s.BiasStrength);
		writer.Write(s.HasLinkedTransform);
		WriteString(writer, record.LinkedCanonicalId);
		WriteTransform3D(writer, s.LinkedWorldFromLocal);
		WriteVector3(writer, s.LinkedCenter);
		WriteSingle(writer, s.LinkedRadius);
		WriteSingle(writer, s.SceneTransformExitOffset);
		WriteSingle(writer, s.IOR);
	}

	private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

	private static void WriteString(BinaryWriter writer, string value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
		WriteInt32(writer, bytes.Length);
		writer.Write(bytes);
	}

	private static void WriteInt32(BinaryWriter writer, int value)
	{
		Span<byte> bytes = stackalloc byte[4];
		BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
		writer.Write(bytes);
	}

	private static void WriteSingle(BinaryWriter writer, float value)
	{
		Span<byte> bytes = stackalloc byte[4];
		BinaryPrimitives.WriteUInt32LittleEndian(bytes, BitConverter.SingleToUInt32Bits(value));
		writer.Write(bytes);
	}

	private static void WriteVector3(BinaryWriter writer, Vector3 value)
	{
		WriteSingle(writer, value.X); WriteSingle(writer, value.Y); WriteSingle(writer, value.Z);
	}

	private static void WriteBasis(BinaryWriter writer, Basis value)
	{
		WriteVector3(writer, value.X); WriteVector3(writer, value.Y); WriteVector3(writer, value.Z);
	}

	private static void WriteTransform3D(BinaryWriter writer, Transform3D value)
	{
		WriteBasis(writer, value.Basis); WriteVector3(writer, value.Origin);
	}

	private static bool FloatBitsEqual(float a, float b) => BitConverter.SingleToUInt32Bits(a) == BitConverter.SingleToUInt32Bits(b);
	private static bool VectorBitsEqual(Vector3 a, Vector3 b) => FloatBitsEqual(a.X, b.X) && FloatBitsEqual(a.Y, b.Y) && FloatBitsEqual(a.Z, b.Z);
	private static bool BasisBitsEqual(Basis a, Basis b) => VectorBitsEqual(a.X, b.X) && VectorBitsEqual(a.Y, b.Y) && VectorBitsEqual(a.Z, b.Z);
	private static bool TransformBitsEqual(Transform3D a, Transform3D b) => BasisBitsEqual(a.Basis, b.Basis) && VectorBitsEqual(a.Origin, b.Origin);
	private static bool IsFinite(float value) => float.IsFinite(value);
	private static bool IsFinite(Vector3 value) => IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
	private static bool IsFinite(Basis value) => IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
	private static bool IsFinite(Transform3D value) => IsFinite(value.Basis) && IsFinite(value.Origin);
}
