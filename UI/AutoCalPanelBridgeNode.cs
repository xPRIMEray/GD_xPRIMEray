using Godot;

public partial class AutoCalPanelBridgeNode : Node
{
    public void ApplySettings(bool smartEnabled, bool shadowEval, bool autoApply, bool verbose)
    {
        AutoCalUIBridge.ApplySettings(smartEnabled, shadowEval, autoApply, verbose);
    }

    public string GetStatusLine()
    {
        RenderTestRunner runner = FindActiveRenderTestRunner();
        return runner != null
            ? runner.GetAutoCalStatusLine()
            : "AutoCal: no RenderTestRunner found";
    }

    private RenderTestRunner FindActiveRenderTestRunner()
    {
        SceneTree tree = GetTree();
        if (tree == null || tree.Root == null || !GodotObject.IsInstanceValid(tree.Root))
        {
            return null;
        }

        return FindRenderTestRunnerRecursive(tree.Root);
    }

    private static RenderTestRunner FindRenderTestRunnerRecursive(Node node)
    {
        if (node is RenderTestRunner runner && runner.IsInsideTree())
        {
            return runner;
        }

        foreach (Node child in node.GetChildren())
        {
            RenderTestRunner found = FindRenderTestRunnerRecursive(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
