using Godot;
using System;

public partial class GrinFilmCamera : Node
{
    [Export] public NodePath RayBeamRendererPath;
    [Export] public NodePath FilmViewPath; // optional: a TextureRect in your UI

    [Export] public int Width = 160;
    [Export] public int Height = 90;

    [Export] public int RowsPerFrame = 8;
    [Export] public bool UpdateEveryFrame = true;
    [Export] public float MaxDistance = 50f;
    [Export] public Color SkyColor = new Color(0, 0, 0, 1);

    [Export] public bool UseCameraPropsBetaGamma = true;

    private Image _img;
    private ImageTexture _tex;

    private TextureRect _filmView;   // if user supplies FilmViewPath
    private TextureRect _overlayRect; // auto-created fallback

    private int _rowCursor = 0;

    private Camera3D _cam;
    private RayBeamRenderer _rbr;

    public override void _Ready()
    {
        GD.Print("✅ GrinFilmCamera READY: ", GetPath());

        _cam = GetViewport().GetCamera3D();
        if (_cam == null)
        {
            GD.PushError("GrinFilmCamera: No active Camera3D found in viewport.");
            return;
        }

        _rbr = GetNodeOrNull<RayBeamRenderer>(RayBeamRendererPath);
        GD.Print("RayBeamRenderer found? ", _rbr != null);
        if (_rbr == null)
        {
            GD.PushError("GrinFilmCamera: RayBeamRendererPath missing or invalid.");
            return;
        }

        _filmView = GetNodeOrNull<TextureRect>(FilmViewPath);
        GD.Print("FilmView found? ", _filmView != null);

        // Create image + texture
        _img = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
        _img.Fill(SkyColor);
        _tex = ImageTexture.CreateFromImage(_img);

        // If FilmViewPath is set, use it.
        if (_filmView != null)
        {
            _filmView.Texture = _tex;
        }
        else
        {
            // Otherwise auto-create an overlay.
            var layer = new CanvasLayer();
            AddChild(layer);

            _overlayRect = new TextureRect();
            _overlayRect.Texture = _tex;

            // Godot 4 settings
            _overlayRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _overlayRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;

            _overlayRect.AnchorLeft = 0;
            _overlayRect.AnchorTop = 0;
            _overlayRect.AnchorRight = 1;
            _overlayRect.AnchorBottom = 1;
            _overlayRect.OffsetLeft = 0;
            _overlayRect.OffsetTop = 0;
            _overlayRect.OffsetRight = 0;
            _overlayRect.OffsetBottom = 0;

            _overlayRect.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            layer.AddChild(_overlayRect);

            GD.Print("GrinFilmCamera: No FilmViewPath set, created overlay TextureRect.");
        }

        GD.Print("✅ GrinFilmCamera ready. Rendering film.");
    }

    public override void _Process(double delta)
    {
        if (!UpdateEveryFrame) return;
        RenderStep();
    }

    public void RenderStep()
    {
        if (_rbr == null || _cam == null) return;

        var space = _cam.GetWorld3D().DirectSpaceState;

        var fieldSources = GetTree().GetNodesInGroup("field_sources");
        bool hasSources = fieldSources.Count > 0;

        float beta = 0f;
        float gamma = 2f;
        if (UseCameraPropsBetaGamma)
        {
            beta = ReadFloat(_cam, "Beta", 0f);
            gamma = ReadFloat(_cam, "Gamma", 2f);
        }

        Vector3 center = _rbr.FieldCenterIsCamera ? _cam.GlobalPosition : _rbr.FieldCenter;
        var basis = _cam.GlobalTransform.Basis;

        float fovRad = Mathf.DegToRad(_cam.Fov);
        float tanHalf = Mathf.Tan(fovRad * 0.5f);
        float aspect = (float)Width / Mathf.Max(1f, Height);

        int yStart = _rowCursor;
        int yEnd = Mathf.Min(Height, _rowCursor + Mathf.Max(1, RowsPerFrame));

        for (int y = yStart; y < yEnd; y++)
        {
            float v = ((y + 0.5f) / Height) * 2f - 1f;
            v = -v;

            for (int x = 0; x < Width; x++)
            {
                float u = ((x + 0.5f) / Width) * 2f - 1f;

                Vector3 dirCam = new Vector3(
                    u * tanHalf * aspect,
                    v * tanHalf,
                    -1f
                ).Normalized();

                Vector3 dirWorld = (basis * dirCam).Normalized();
                Vector3 bendDir = basis.X;

                RayBeamRenderer.HitPayload hit;
                _rbr.SimulateRayCamera(
                    space,
                    _cam.GlobalPosition,
                    dirWorld,
                    bendDir,
                    center,
                    beta,
                    gamma,
                    fieldSources,
                    hasSources,
                    _rbr.CollisionMask,
                    MaxDistance,
                    out hit
                );

                Color col = SkyColor;

                if (hit.Valid)
                {
                    float t = Mathf.Clamp(hit.Distance / Mathf.Max(0.001f, MaxDistance), 0f, 1f);
                    float shade = 1f - t;
                    col = new Color(shade, shade, shade, 1f);
                }

                _img.SetPixel(x, y, col);
            }
        }

        _tex.Update(_img);

        _rowCursor = yEnd;
        if (_rowCursor >= Height) _rowCursor = 0;
    }

    private static float ReadFloat(Node obj, StringName prop, float fallback)
    {
        if (obj == null) return fallback;
        Variant v = obj.Get(prop);
        return v.VariantType switch
        {
            Variant.Type.Float => (float)v,
            Variant.Type.Int => (int)v,
            _ => fallback
        };
    }
}
