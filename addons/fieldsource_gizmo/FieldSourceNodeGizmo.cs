using Godot;

[Tool]
public partial class FieldSourceNodeGizmo : EditorNode3DGizmoPlugin
{
	private const int DefaultSegments = 48;
	private const float MarkerHalfLength = 0.06f;

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

		gizmo.AddLines(BuildAxisMarker(MarkerHalfLength), centerMaterial, false);

		if (field.ResolveAcademicRadii(out float inner, out float outer, out _))
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

		TryConfigureMaterial("fieldsource_center", true);
		TryConfigureMaterial("fieldsource_inner", false);
		TryConfigureMaterial("fieldsource_outer", false);

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

	private static Vector3[] BuildCircle(float radius, int segments)
	{
		int safeSegments = Mathf.Max(3, segments);
		var lines = new Vector3[safeSegments * 2];

		for (int i = 0; i < safeSegments; i++)
		{
			float a0 = Mathf.Tau * i / safeSegments;
			float a1 = Mathf.Tau * (i + 1) / safeSegments;

			lines[i * 2]     = new Vector3(Mathf.Cos(a0) * radius, 0.0f, Mathf.Sin(a0) * radius);
			lines[i * 2 + 1] = new Vector3(Mathf.Cos(a1) * radius, 0.0f, Mathf.Sin(a1) * radius);
		}

		return lines;
	}
}
