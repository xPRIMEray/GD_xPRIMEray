using Godot;
using System;

[Tool]
public partial class FieldSourceNodeGizmo : EditorNode3DGizmoPlugin
{
	private const int DefaultSegments = 48;
	private const float MarkerHalfLength = 0.06f;
	private const float ZoneYOffset = 0.01f;

	private bool _materialsReady;

	public override string _GetGizmoName() => "FieldSource3D";

	public override bool _HasGizmo(Node3D forNode) => forNode is FieldSource3D;

	public override void _Redraw(EditorNode3DGizmo gizmo)
	{
		gizmo.Clear();

		if (gizmo.GetNode3D() is not FieldSource3D field)
		{
			return;
		}

		EnsureMaterials();

		Material centerMaterial = GetMaterial("fieldsource_center", gizmo);
		Material innerMaterial = GetMaterial("fieldsource_inner", gizmo);
		Material outerMaterial = GetMaterial("fieldsource_outer", gizmo);
		Material densityMaterial = GetMaterial("fieldsource_density", gizmo);
		Material zoneLowMaterial = GetMaterial("fieldsource_zone_low", gizmo);
		Material zoneMidMaterial = GetMaterial("fieldsource_zone_mid", gizmo);
		Material zoneHighMaterial = GetMaterial("fieldsource_zone_high", gizmo);

		gizmo.AddLines(BuildAxisMarker(MarkerHalfLength), centerMaterial, false);

		bool hasRadii = field.ResolveAcademicRadii(out float inner, out float outer, out _);
		if (hasRadii)
		{
			if (inner > 0.0f)
			{
				gizmo.AddLines(BuildCircle(inner, DefaultSegments), innerMaterial, false);
			}

			if (outer > 0.0f)
			{
				gizmo.AddLines(BuildCircle(outer, DefaultSegments), outerMaterial, false);
			}
		}

		if (field.DebugVizShowDensityVectors && hasRadii)
		{
			FieldSource3D.ResolvedFieldParams resolved = field.ResolveEffectiveParams(out _);
			Vector3[] vectors = BuildDensityVectors(field, resolved, inner, outer);
			if (vectors.Length > 0)
			{
				gizmo.AddLines(vectors, densityMaterial, false);
			}
		}

		if (field.DebugVizShowDensityZones && hasRadii)
		{
			FieldSource3D.ResolvedFieldParams resolved = field.ResolveEffectiveParams(out _);
			int zoneCount = Mathf.Clamp(field.DebugVizDensityZoneCount, 2, 6);
			for (int i = 0; i < zoneCount; i++)
			{
				float t = (i + 1f) / (zoneCount + 1f);
				float radius = Mathf.Lerp(inner, outer, t);
				float strength = ComputeNormalizedStrength(resolved, radius, inner, outer);
				Material zoneMaterial = SelectZoneMaterial(strength, zoneLowMaterial, zoneMidMaterial, zoneHighMaterial);
				float yOffset = ((i & 1) == 0) ? ZoneYOffset : -ZoneYOffset;
				gizmo.AddLines(BuildCircle(radius, DefaultSegments, yOffset), zoneMaterial, false);
			}
		}
	}

	private void EnsureMaterials()
	{
		if (_materialsReady)
		{
			return;
		}

		CreateMaterial("fieldsource_center", new Color(0.7f, 1.0f, 1.0f, 1.0f));
		CreateMaterial("fieldsource_inner", new Color(1.0f, 0.95f, 0.25f, 0.9f));
		CreateMaterial("fieldsource_outer", new Color(1.0f, 0.55f, 0.1f, 0.8f));
		CreateMaterial("fieldsource_density", new Color(1.0f, 0.45f, 0.25f, 0.95f));
		CreateMaterial("fieldsource_zone_low", new Color(1.0f, 0.95f, 0.25f, 0.9f));
		CreateMaterial("fieldsource_zone_mid", new Color(1.0f, 0.68f, 0.2f, 0.92f));
		CreateMaterial("fieldsource_zone_high", new Color(1.0f, 0.3f, 0.16f, 0.96f));

		TryConfigureMaterial("fieldsource_center", true);
		TryConfigureMaterial("fieldsource_inner", false);
		TryConfigureMaterial("fieldsource_outer", false);
		TryConfigureMaterial("fieldsource_density", true);
		TryConfigureMaterial("fieldsource_zone_low", true);
		TryConfigureMaterial("fieldsource_zone_mid", true);
		TryConfigureMaterial("fieldsource_zone_high", true);

		_materialsReady = true;
	}

	private void TryConfigureMaterial(string name, bool forceOnTop)
	{
		Material material = GetMaterial(name, null);
		if (material is StandardMaterial3D standardMaterial)
		{
			standardMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
			standardMaterial.NoDepthTest = forceOnTop;
			standardMaterial.RenderPriority = forceOnTop ? 127 : 0;
		}
	}

	private static Vector3[] BuildAxisMarker(float size)
	{
		return new Vector3[]
		{
			new Vector3(-size, 0.0f, 0.0f),
			new Vector3( size, 0.0f, 0.0f),
			new Vector3(0.0f, 0.0f, -size),
			new Vector3(0.0f, 0.0f,  size)
		};
	}

	private static Vector3[] BuildCircle(float radius, int segments, float yOffset = 0.0f)
	{
		int safeSegments = Mathf.Max(3, segments);
		var lines = new Vector3[safeSegments * 2];

		for (int i = 0; i < safeSegments; i++)
		{
			float a0 = Mathf.Tau * i / safeSegments;
			float a1 = Mathf.Tau * (i + 1) / safeSegments;

			lines[i * 2]     = new Vector3(Mathf.Cos(a0) * radius, yOffset, Mathf.Sin(a0) * radius);
			lines[i * 2 + 1] = new Vector3(Mathf.Cos(a1) * radius, yOffset, Mathf.Sin(a1) * radius);
		}

		return lines;
	}

	private static Vector3[] BuildDensityVectors(FieldSource3D field, FieldSource3D.ResolvedFieldParams resolved, float inner, float outer)
	{
		int count = Mathf.Clamp(field.DebugVizDensityVectorCount, 4, 96);
		float sampleRadius = Mathf.Lerp(inner, outer, 0.72f);
		if (sampleRadius <= 0.0f)
		{
			sampleRadius = outer;
		}

		if (sampleRadius <= 0.0f)
		{
			return Array.Empty<Vector3>();
		}

		float baseMagnitude = Mathf.Max(0.15f, ComputeNormalizedStrength(resolved, sampleRadius, inner, outer));

		bool invertSign = (resolved.modeFlags & 1u) != 0u;
		float lengthScale = Mathf.Max(0.05f, field.DebugVizDensityVectorScale);
		float arrowHeadScale = Mathf.Clamp(lengthScale * 0.22f, 0.02f, 0.2f);
		var lines = new Vector3[count * 4];

		for (int i = 0; i < count; i++)
		{
			float angle = Mathf.Tau * i / count;
			Vector3 radial = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
			Vector3 tangent = new Vector3(-radial.Z, 0.0f, radial.X);
			Vector3 direction = invertSign ? radial : -radial;

			float length = Mathf.Max(outer * 0.08f, lengthScale * Mathf.Lerp(0.3f, 1.0f, baseMagnitude));
			Vector3 start = radial * sampleRadius + Vector3.Up * ZoneYOffset;
			Vector3 tip = start + direction * length;
			Vector3 wing = tip - direction * arrowHeadScale + tangent * (arrowHeadScale * 0.5f);

			lines[i * 4] = start;
			lines[i * 4 + 1] = tip;
			lines[i * 4 + 2] = tip;
			lines[i * 4 + 3] = wing;
		}

		return lines;
	}

	private static float ComputeNormalizedStrength(FieldSource3D.ResolvedFieldParams resolved, float radius, float inner, float outer)
	{
		float safeSpan = Mathf.Max(0.0001f, outer - inner);
		float t = Mathf.Clamp((radius - inner) / safeSpan, 0.0f, 1.0f);
		float strength = resolved.curveType switch
		{
			RendererCore.Fields.FieldCurveType.Linear => 1.0f - t,
			RendererCore.Fields.FieldCurveType.Power => Mathf.Pow(Mathf.Max(0.0f, 1.0f - t), Mathf.Max(0.0f, resolved.a)),
			RendererCore.Fields.FieldCurveType.Polynomial => Mathf.Clamp(resolved.a + resolved.b * t + resolved.c * t * t, 0.0f, 1.0f),
			RendererCore.Fields.FieldCurveType.Exponential => ComputeGaussianLikeStrength(resolved, radius),
			_ => 1.0f - t
		};

		return Mathf.Clamp(strength, 0.0f, 1.0f);
	}

	private static float ComputeGaussianLikeStrength(FieldSource3D.ResolvedFieldParams resolved, float radius)
	{
		float sigma = resolved.sigma;
		if (sigma <= 0.0001f)
		{
			sigma = resolved.a > 0.0001f ? (1.0f / resolved.a) : 1.0f;
		}

		float x = radius / Mathf.Max(0.0001f, sigma);
		return Mathf.Exp(-(x * x));
	}

	private static Material SelectZoneMaterial(float strength, Material low, Material mid, Material high)
	{
		if (strength >= 0.66f)
		{
			return high;
		}

		if (strength >= 0.33f)
		{
			return mid;
		}

		return low;
	}
}
