using Godot;
using System;
using System.Collections.Generic;

public partial class FilmOverlay2D : TextureRect
{
    [Export] public NodePath CameraPath;
    [Export] public bool DrawRays = true;
    [Export] public bool DrawHitNormals = true;

    [Export] public float RayWidth = 1.0f;
    [Export] public float NormalWidth = 2.0f;
    [Export] public float NormalLenWorld = 0.25f;

    [Export] public Color RayColor = new Color(0.6f, 1.0f, 0.6f, 0.9f);
    [Export] public Color HitRayColor = new Color(1.0f, 0.9f, 0.2f, 1.0f);
    [Export] public Color NormalColor = new Color(1.0f, 0.2f, 0.2f, 1.0f);

    private Camera3D _cam;

    // Polyline list: each ray is a list of WORLD points
    private readonly List<Vector3[]> _rayWorldPts = new();
    private readonly List<bool> _rayHadHit = new();

    // Hit payloads in WORLD space
    public struct Hit
    {
        public bool Valid;
        public Vector3 Position;
        public Vector3 Normal;
        public float Distance;
        public string ColliderName;
    }

    private readonly List<Hit> _hits = new();

    public override void _Ready()
    {
        _cam = GetNodeOrNull<Camera3D>(CameraPath);
        // Ensure this node draws over the film and is not clipping unexpectedly
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = false;
    }

    public void ClearOverlay()
    {
        _rayWorldPts.Clear();
        _rayHadHit.Clear();
        _hits.Clear();
        QueueRedraw();
    }

    /// <summary>
    /// Fast path: pass spans/arrays from Film each band.
    /// pts = concatenated point buffer, offs/cnts define each polyline in pts.
    /// hits aligned to ray index.
    /// </summary>
    public void SetOverlayData(
        Camera3D cam,
        ReadOnlySpan<Vector3> pts,
        ReadOnlySpan<int> offs,
        ReadOnlySpan<int> cnts,
        ReadOnlySpan<Hit> hits)
    {
        _cam = cam ?? _cam;

        _rayWorldPts.Clear();
        _rayHadHit.Clear();
        _hits.Clear();

        int rayCount = Math.Min(offs.Length, cnts.Length);
        rayCount = Math.Min(rayCount, hits.Length);

        for (int r = 0; r < rayCount; r++)
        {
            int o = offs[r];
            int c = cnts[r];
            if (c <= 0) continue;

            // Copy this ray's points into an array
            var arr = new Vector3[c];
            for (int i = 0; i < c; i++)
            {
                int idx = o + i;
                if ((uint)idx >= (uint)pts.Length) break;
                arr[i] = pts[idx];
            }

            _rayWorldPts.Add(arr);
            _rayHadHit.Add(hits[r].Valid);
            _hits.Add(hits[r]);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_cam == null || !IsInstanceValid(_cam))
            return;

        // We'll draw in the overlay's local rect coordinates.
        // If FilmView and FilmOverlay2D share the same anchors/size, this matches 1:1.

        if (DrawRays)
        {
            for (int r = 0; r < _rayWorldPts.Count; r++)
            {
                var wpts = _rayWorldPts[r];
                if (wpts == null || wpts.Length < 2)
                    continue;

                Color c = _rayHadHit[r] ? HitRayColor : RayColor;

                // Convert world polyline to screen polyline (Vector2)
                Vector2 prev = _cam.UnprojectPosition(wpts[0]);

                for (int i = 1; i < wpts.Length; i++)
                {
                    Vector2 cur = _cam.UnprojectPosition(wpts[i]);

                    // Optional: skip giant jumps (usually behind camera / projection flips)
                    if (prev.DistanceTo(cur) < 5000f)
                        DrawLine(prev, cur, c, RayWidth);

                    prev = cur;
                }
            }
        }

        if (DrawHitNormals)
        {
            for (int i = 0; i < _hits.Count; i++)
            {
                var h = _hits[i];
                if (!h.Valid)
                    continue;

                Vector3 p0w = h.Position;
                Vector3 p1w = h.Position + h.Normal * NormalLenWorld;

                Vector2 p0 = _cam.UnprojectPosition(p0w);
                Vector2 p1 = _cam.UnprojectPosition(p1w);

                DrawLine(p0, p1, NormalColor, NormalWidth);
            }
        }
    }
}
