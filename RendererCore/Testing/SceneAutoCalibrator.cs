using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using RendererCore.Fields;

public enum ProbeEarlyExitReason
{
	None = 0,
	TimeBudget = 1,
	NodeCap = 2,
	MeshCap = 3,
	NullRoot = 4
}

public sealed class ProbeBudget
{
	public double max_msec = 10.0;
	public int max_nodes = 2000;
	public int max_meshes = 64;
	public int max_children_per_node = 256;
}

public sealed class ProbeTelemetry
{
	public double elapsed_msec;
	public int visited_nodes;
	public int inspected_meshes;
	public ProbeEarlyExitReason early_exit_reason = ProbeEarlyExitReason.None;
}

public static class SceneAutoCalibrator
{
	private static readonly string[] FieldSourceGroupNames =
	{
		"field_sources",
		"field_source",
		"curvature_fields",
		"curvature_field"
	};

	private static readonly string[] CurvatureFieldGroupNames =
	{
		"curvature_fields",
		"curvature_field"
	};

	private static readonly string[] GrinVolumeGroupNames =
	{
		"grin_volumes",
		"grin_volume"
	};

	public static SceneProbeReport CollectSignature(RenderTestRunner runner)
	{
		Node root = null;
		if (runner != null && GodotObject.IsInstanceValid(runner))
		{
			root = runner.GetTree()?.CurrentScene;
			if (root == null || !GodotObject.IsInstanceValid(root))
			{
				root = runner;
			}
		}

		(SceneProbeReport signature, ProbeTelemetry telemetry) = ProbeScene(root, new ProbeBudget());
		_ = telemetry;
		return signature;
	}

	public static (SceneProbeReport signature, ProbeTelemetry telemetry) ProbeScene(Node root, ProbeBudget budget)
	{
		SceneProbeReport sig = new SceneProbeReport();
		ProbeTelemetry telemetry = new ProbeTelemetry();

		if (root == null || !GodotObject.IsInstanceValid(root))
		{
			telemetry.early_exit_reason = ProbeEarlyExitReason.NullRoot;
			return (sig, telemetry);
		}

		double maxMsec = budget != null && budget.max_msec > 0.0 ? budget.max_msec : 10.0;
		int maxNodes = budget != null && budget.max_nodes > 0 ? budget.max_nodes : 2000;
		int maxMeshes = budget != null && budget.max_meshes > 0 ? budget.max_meshes : 64;
		int maxChildrenPerNode = budget != null && budget.max_children_per_node > 0 ? budget.max_children_per_node : 256;

		Stopwatch sw = Stopwatch.StartNew();
		Stack<Node> stack = new Stack<Node>(Math.Min(maxNodes, 256));
		stack.Push(root);

		int surfaceEstimate = 0;
		int childLinksSkipped = 0;
		int fieldSourceCount = 0;
		int grinVolumeCount = 0;
		int curvatureFieldCount = 0;

		while (stack.Count > 0)
		{
			if (sw.Elapsed.TotalMilliseconds >= maxMsec)
			{
				telemetry.early_exit_reason = ProbeEarlyExitReason.TimeBudget;
				break;
			}

			if (telemetry.visited_nodes >= maxNodes)
			{
				telemetry.early_exit_reason = ProbeEarlyExitReason.NodeCap;
				break;
			}

			Node node = stack.Pop();
			if (node == null || !GodotObject.IsInstanceValid(node))
			{
				continue;
			}

			telemetry.visited_nodes++;

			string typeName = node.GetType().Name ?? string.Empty;
			bool isFieldSource = IsLikelyFieldSourceNode(node, typeName);
			bool isCurvatureField = IsLikelyCurvatureFieldNode(node, typeName);
			bool isGrinVolume = false;

			if (isFieldSource)
			{
				fieldSourceCount++;

				if (node is FieldSource3D fieldNode)
				{
					if (fieldNode.ShapeType == FieldShapeType.BoxVolume && fieldNode.MetricModel == MetricModel.GRIN)
					{
						isGrinVolume = true;
					}
				}
				else
				{
					isGrinVolume = HasAnyGroup(node, GrinVolumeGroupNames);
				}
			}
			else if (HasAnyGroup(node, GrinVolumeGroupNames))
			{
				// Preserve grin signal even when field source script typing is unavailable.
				isGrinVolume = true;
			}

			if (isCurvatureField)
			{
				curvatureFieldCount++;
			}

			if (isGrinVolume)
			{
				grinVolumeCount++;
			}

			if (node is MeshInstance3D meshNode)
			{
				if (telemetry.inspected_meshes >= maxMeshes)
				{
					telemetry.early_exit_reason = ProbeEarlyExitReason.MeshCap;
					break;
				}

				telemetry.inspected_meshes++;
				Mesh mesh = meshNode.Mesh;
				if (mesh != null && GodotObject.IsInstanceValid(mesh))
				{
					surfaceEstimate += Math.Max(0, mesh.GetSurfaceCount());
				}
			}
			else if (node is MultiMeshInstance3D multiMeshNode)
			{
				if (telemetry.inspected_meshes >= maxMeshes)
				{
					telemetry.early_exit_reason = ProbeEarlyExitReason.MeshCap;
					break;
				}

				telemetry.inspected_meshes++;
				MultiMesh mm = multiMeshNode.Multimesh;
				if (mm != null && GodotObject.IsInstanceValid(mm))
				{
					Mesh mesh = mm.Mesh;
					if (mesh != null && GodotObject.IsInstanceValid(mesh))
					{
						surfaceEstimate += Math.Max(0, mesh.GetSurfaceCount());
					}
				}
			}

			int childCount = node.GetChildCount();
			int childLimit = Math.Min(Math.Max(0, maxChildrenPerNode), Math.Max(0, childCount));
			if (childCount > childLimit)
			{
				childLinksSkipped += (childCount - childLimit);
			}

			for (int i = childLimit - 1; i >= 0; i--)
			{
				Node child = node.GetChild(i);
				if (child != null)
				{
					stack.Push(child);
				}
			}
		}

		sw.Stop();
		telemetry.elapsed_msec = sw.Elapsed.TotalMilliseconds;

		sig.scene_node_count = telemetry.visited_nodes;
		sig.scene_mesh_count = telemetry.inspected_meshes;
		sig.scene_surface_count_estimate = surfaceEstimate;
		sig.scene_triangle_count_known = false;
		sig.scene_triangle_count_estimate = -1;
		sig.scene_children_skipped = childLinksSkipped;
		sig.scene_field_source_count = fieldSourceCount;
		sig.scene_grin_volume_count = grinVolumeCount;
		sig.scene_curvature_field_count = curvatureFieldCount;
		sig.probe_early_exit_reason = telemetry.early_exit_reason;

		return (sig, telemetry);
	}

	public static SceneProbeArchetype Classify(SceneProbeReport sig)
	{
		return ClassifyArchetype(sig);
	}

	public static SceneProbeArchetype ClassifyArchetype(SceneProbeReport sig)
	{
		if (sig == null)
		{
			return SceneProbeArchetype.Unknown;
		}

		if (sig.probe_early_exit_reason == ProbeEarlyExitReason.NodeCap ||
			sig.probe_early_exit_reason == ProbeEarlyExitReason.MeshCap ||
			sig.probe_early_exit_reason == ProbeEarlyExitReason.TimeBudget ||
			sig.probe_early_exit_reason == ProbeEarlyExitReason.NullRoot ||
			sig.scene_children_skipped > CalibrationTuning.MaxChildrenSkippedForConfidentClassification)
		{
			return SceneProbeArchetype.Unknown;
		}

		if (sig.scene_field_source_count > 0 ||
			sig.scene_curvature_field_count > 0 ||
			sig.scene_grin_volume_count > 0)
		{
			return SceneProbeArchetype.FieldHeavy;
		}

		int fieldScore = Math.Max(0, sig.scene_field_source_count) +
			(2 * Math.Max(0, sig.scene_grin_volume_count)) +
			(2 * Math.Max(0, sig.scene_curvature_field_count));

		if (sig.scene_field_source_count >= CalibrationTuning.FieldHeavyMinFieldSources ||
			sig.scene_grin_volume_count >= CalibrationTuning.FieldHeavyMinGrinVolumes ||
			sig.scene_curvature_field_count >= CalibrationTuning.FieldHeavyMinCurvatureFields ||
			fieldScore >= CalibrationTuning.FieldHeavyMinCombinedFieldScore)
		{
			return SceneProbeArchetype.FieldHeavy;
		}

		bool denseByTriangles = sig.scene_triangle_count_known &&
			sig.scene_triangle_count_estimate >= CalibrationTuning.DenseMinTrianglesEstimate;
		if (sig.scene_mesh_count >= CalibrationTuning.DenseMinMeshes ||
			sig.scene_surface_count_estimate >= CalibrationTuning.DenseMinSurfacesEstimate ||
			denseByTriangles)
		{
			return SceneProbeArchetype.DenseGeometry;
		}

		bool tinyByTriangles = sig.scene_triangle_count_known &&
			sig.scene_triangle_count_estimate >= 0 &&
			sig.scene_triangle_count_estimate <= CalibrationTuning.TinyMaxTrianglesEstimate;
		if (sig.scene_node_count <= CalibrationTuning.TinyMaxNodes &&
			sig.scene_mesh_count <= CalibrationTuning.TinyMaxMeshes &&
			sig.scene_surface_count_estimate <= CalibrationTuning.TinyMaxSurfacesEstimate &&
			(!sig.scene_triangle_count_known || tinyByTriangles))
		{
			return SceneProbeArchetype.Tiny;
		}

		return SceneProbeArchetype.Default;
	}

	private static bool IsLikelyFieldSourceNode(Node node, string typeName)
	{
		if (node is FieldSource3D)
		{
			return true;
		}

		if (HasAnyGroup(node, FieldSourceGroupNames))
		{
			return true;
		}

		if (!string.IsNullOrEmpty(typeName) &&
			typeName.IndexOf("FieldSource", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}

		string nodeName = node.Name.ToString();
		if (!string.IsNullOrEmpty(nodeName) &&
			nodeName.IndexOf("FieldSource", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}

		return false;
	}

	private static bool IsLikelyCurvatureFieldNode(Node node, string typeName)
	{
		if (HasAnyGroup(node, CurvatureFieldGroupNames))
		{
			return true;
		}

		if (!string.IsNullOrEmpty(typeName) &&
			typeName.IndexOf("Curvature", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}

		string nodeName = node.Name.ToString();
		return !string.IsNullOrEmpty(nodeName) &&
			nodeName.IndexOf("Curvature", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static bool HasAnyGroup(Node node, string[] groups)
	{
		if (node == null || groups == null)
		{
			return false;
		}

		for (int i = 0; i < groups.Length; i++)
		{
			string group = groups[i];
			if (!string.IsNullOrEmpty(group) && node.IsInGroup(group))
			{
				return true;
			}
		}

		return false;
	}

	public static CalibratedPreset GeneratePreset(SceneProbeReport sig)
	{
		ulong sceneSignatureHash64 = sig?.ComputeHash64() ?? 0UL;
		return CalibratedPreset.NoOp(sceneSignatureHash64);
	}

	public static CalibratedPreset BuildPreset(SceneProbeReport sig, SceneProbeArchetype archetype, bool? baselinePruneEnabled = null)
	{
		if (sig != null)
		{
			sig.archetype = archetype;
		}

		ulong canonicalSignatureHash64 = sig?.ComputeHash64() ?? 0UL;
		return RecommendationEngine.BuildPreset(sig, archetype, canonicalSignatureHash64, baselinePruneEnabled);
	}
}
