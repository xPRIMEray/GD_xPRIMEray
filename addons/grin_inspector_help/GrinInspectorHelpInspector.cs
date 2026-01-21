using Godot;

/// <summary>
/// Adds an always-on Inspector "Help" section for GRIN/xPRIMEray nodes.
/// We intentionally avoid overriding _ParseProperty (signature differs across Godot 4.x),
/// and instead inject a guide panel in _ParseBegin.
/// </summary>
[Tool]
public partial class GrinInspectorHelpInspector : EditorInspectorPlugin
{
    public override bool _CanHandle(GodotObject @object)
    {
        // Keep this very permissive: any Node in this project that exposes GRIN settings can benefit.
        // We'll filter inside _ParseBegin by type name to avoid clutter on unrelated nodes.
        return @object is Node;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is not Node node)
            return;

        // Only attach to our known GRIN/xPRIMEray nodes (by type name so refactors don't break hard).
        var typeName = node.GetType().Name;
        var isGrinFilm = typeName.Contains("GrinFilmCamera") || typeName.Contains("GrinFilm");
        var isFieldSource = typeName.Contains("FieldSource") || typeName.Contains("Field");
        if (!isGrinFilm && !isFieldSource)
            return;

        // Container
        var root = new VBoxContainer
        {
            Name = "GRIN_Inspector_Help",
            CustomMinimumSize = new Vector2(0, 0)
        };

        var header = new HBoxContainer();
        var title = new Label
        {
            Text = "🧭 GRIN Inspector Guide",
            ThemeTypeVariation = "HeaderSmall"
        };
        header.AddChild(title);

        // A compact note so it doesn't overwhelm the Inspector
        var subtitle = new Label
        {
            Text = "What each knob is *trying* to do + what to watch in the render/perf logs.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };

        var help = new RichTextLabel
        {
            FitContent = true,
            ScrollActive = false,
            BbcodeEnabled = true
        };

        help.Text =
            "[b]Goal:[/b] maximize FPS while keeping the curved-ray result stable (no missing strips / no sudden black wedges).\n\n" +

            "[b]How to read your perf line:[/b]\n" +
            "• [code]p1[/code] = pass 1 time (emit/step)  • [code]p2p[/code] = pass 2 per-pixel resolve  • [code]p2s[/code] = pass 2 subdivision/hit refinement\n" +
            "• [code]tested[/code] grows when we do more candidate segment checks.  [code]avgP2Stride[/code] > 1 means we’re skipping candidates.\n" +
            "• [code]skipSegs[/code] indicates segments skipped by stride.  [code]p2ForcePx[/code] counts pixels forced to stride-1 fallback (miss protection).\n\n" +

            "[b]If you see BLACK wedges / streaks (N·V shading):[/b]\n" +
            "This usually means the shading normal is facing away from the view direction [i]for the hit you actually recorded[/i]. With extreme curvature, the ray can legitimately hit a back-face, or the normal might not be flipped/handled as you expect.\n" +
            "Try in this order:\n" +
            "1) Enable [code]FlipNormalToCamera[/code] → if black turns to lit, it's a normal-orientation issue (not a miss).\n" +
            "2) If you want physical backfaces, enable [code]Pass2HitBackFaces[/code] (but expect different look).\n" +
            "3) If you think you’re ‘teleporting through’ thin geometry, reduce curvature (beta/gamma) or increase segment density (smaller step / more segments).\n\n" +

            "[b]UsePass2CollisionStride (big performance knob):[/b]\n" +
            "• [i]What it does:[/i] skips candidate segments in pass 2 by a stride that increases with distance.\n" +
            "• [i]Turn it ON when:[/i] p2p is dominating and the image is stable. Watch [code]avgP2Stride[/code] rise and [code]tested[/code] fall.\n" +
            "• [i]Turn it OFF when:[/i] you see missing strips/holes or 'only renders bands' artifacts.\n" +
            "• [i]Regression tell:[/i] [code]skipSegs[/code] high but [code]p2ForcePx[/code] stays ~0 and image has holes → stride is skipping real candidates (tune near/far/thresholds).\n\n" +

            "[b]MinSegLenForStrideSkip:[/b]\n" +
            "• [i]What it does:[/i] only allow stride-skipping on segments longer than this.\n" +
            "• [i]If too LOW:[/i] you may skip lots of tiny segments near curved fields → 'striped rendering' + hitPct collapse.\n" +
            "• [i]If too HIGH:[/i] stride becomes ineffective → performance gain disappears.\n" +
            "• [i]Rule of thumb:[/i] set it near your typical segment length in the critical field zone.\n\n" +

            "[b]Stride Near / Far / FarT (distance curve):[/b]\n" +
            "• Near = stride at close distances (usually 1 or 2).\n" +
            "• Far = stride cap at long distances.\n" +
            "• FarT = how quickly you approach Far (smaller = ramps sooner).\n" +
            "If you see banding at distance → lower Far or increase FarT.\n\n" +

            "[b]Pass2HitFromInside:[/b]\n" +
            "Enable when your camera/rays are inside a closed mesh/volume and you still want surface hits.\n\n" +

            "[b]NearestHitOnly:[/b]\n" +
            "Use for stable surfaces (first intersection). Disable only if you intentionally want 'through-hits' effects.\n\n" +

            "[b]What to tweak first (fast loop):[/b]\n" +
            "1) If artifacts: disable stride OR raise MinSegLenForStrideSkip.\n" +
            "2) If stable but slow: enable stride, then raise Far gradually.\n" +
            "3) If black wedges: FlipNormalToCamera, then decide whether backfaces are allowed.\n";

        root.AddChild(header);
        root.AddChild(subtitle);
        root.AddChild(help);

        AddCustomControl(root);
    }
}
