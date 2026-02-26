using Godot;
using System.Reflection;

public static class AutoCalUIBridge
{
    public static void ApplySettings(bool smartEnabled, bool shadowEval, bool autoApply, bool verbose)
    {
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
            return;

        ApplySettingsRecursive(tree.Root, smartEnabled, shadowEval, autoApply, verbose);
    }

    private static void ApplySettingsRecursive(Node node, bool smartEnabled, bool shadowEval, bool autoApply, bool verbose)
    {
        if (node is RenderTestRunner runner)
        {
            runner.EnableSceneAutoCalibration = smartEnabled;
            runner.EnableShadowCalibrationEvaluation = shadowEval;
            runner.ApplyAutoCalibratedPreset = autoApply;
            runner.AutoCalVerboseLogs = verbose;

            FieldInfo shadowVerboseField = typeof(RenderTestRunner).GetField(
                "ShadowEvalVerboseLogs",
                BindingFlags.Instance | BindingFlags.Public);

            if (shadowVerboseField != null && shadowVerboseField.FieldType == typeof(bool))
                shadowVerboseField.SetValue(runner, verbose);
        }

        foreach (Node child in node.GetChildren())
            ApplySettingsRecursive(child, smartEnabled, shadowEval, autoApply, verbose);
    }
}
