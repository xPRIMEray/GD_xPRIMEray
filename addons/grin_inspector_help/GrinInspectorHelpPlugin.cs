#if TOOLS
using Godot;

[Tool]
public partial class GrinInspectorHelpPlugin : EditorPlugin
{
    private GrinInspectorHelpInspector _inspector;

    public override void _EnterTree()
    {
        _inspector = new GrinInspectorHelpInspector();
        AddInspectorPlugin(_inspector);
    }

    public override void _ExitTree()
    {
        if (_inspector != null)
        {
            RemoveInspectorPlugin(_inspector);
            _inspector = null;
        }
    }
}
#endif
