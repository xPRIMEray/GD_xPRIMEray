using Godot;

[Tool]
public partial class FieldSourceGizmoPlugin : EditorPlugin
{
	private FieldSourceNodeGizmo _gizmo;

	public override void _EnterTree()
	{
		_gizmo = new FieldSourceNodeGizmo();
		AddNode3DGizmoPlugin(_gizmo);
	}

	public override void _ExitTree()
	{
		if (_gizmo != null)
		{
			RemoveNode3DGizmoPlugin(_gizmo);
			_gizmo = null;
		}
	}
}
