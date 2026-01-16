using Godot;
using System;

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

	private Vector3[] _pts = Array.Empty<Vector3>();
	private int[] _offsets = Array.Empty<int>();
	private int[] _counts = Array.Empty<int>();
	private RayBeamRenderer.HitPayload[] _hits = Array.Empty<RayBeamRenderer.HitPayload>();
	private int _rayCount;
	private int _ptCount;
	private float _normalLenWorld;

	public override void _Ready()
	{
		_cam = GetNodeOrNull<Camera3D>(CameraPath);
		MouseFilter = MouseFilterEnum.Ignore;
		ClipContents = false;
	}

	public void ClearOverlay()
	{
		_rayCount = 0;
		_ptCount = 0;
		QueueRedraw();
	}

	public void SetData(
		Camera3D cam,
		ReadOnlySpan<Vector3> pts,
		ReadOnlySpan<int> offsets,
		ReadOnlySpan<int> counts,
		ReadOnlySpan<RayBeamRenderer.HitPayload> hits,
		float normalLen)
	{
		_cam = cam ?? _cam;
		_normalLenWorld = normalLen > 0f ? normalLen : NormalLenWorld;

		int rayCount = Math.Min(offsets.Length, counts.Length);
		rayCount = Math.Min(rayCount, hits.Length);

		if (rayCount <= 0 || pts.Length <= 0)
		{
			_rayCount = 0;
			_ptCount = 0;
			QueueRedraw();
			return;
		}

		EnsureCapacity(rayCount, pts.Length);

		pts.CopyTo(_pts.AsSpan(0, pts.Length));
		offsets.Slice(0, rayCount).CopyTo(_offsets.AsSpan(0, rayCount));
		counts.Slice(0, rayCount).CopyTo(_counts.AsSpan(0, rayCount));
		hits.Slice(0, rayCount).CopyTo(_hits.AsSpan(0, rayCount));

		_rayCount = rayCount;
		_ptCount = pts.Length;
		QueueRedraw();
	}

	private Vector2 ScreenToLocal(Vector2 screen)
	{
		return GetGlobalTransformWithCanvas().AffineInverse() * screen;
	}
	
	public override void _Draw()
	{
		if (_cam == null || !IsInstanceValid(_cam)) return;
		if (_rayCount <= 0 || _ptCount <= 0) return;

		if (DrawRays)
		{
			for (int r = 0; r < _rayCount; r++)
			{
				int start = _offsets[r];
				int count = _counts[r];
				if (count < 2) continue;
				if (start < 0 || (start + count) > _ptCount) continue;

				bool hadHit = r < _hits.Length && _hits[r].Valid;
				Color c = hadHit ? HitRayColor : GetRayColor(r);

				Vector3 prevW = _pts[start];
				for (int i = 1; i < count; i++)
				{
					Vector3 curW = _pts[start + i];

					bool prevBehind = _cam.IsPositionBehind(prevW);
					bool curBehind  = _cam.IsPositionBehind(curW);

					if (!(prevBehind && curBehind))
					{
						Vector2 prev = ScreenToLocal(_cam.UnprojectPosition(prevW));
						Vector2 cur  = ScreenToLocal(_cam.UnprojectPosition(curW));
						DrawLine(prev, cur, c, RayWidth);
					}

					prevW = curW;
				}
			}
		}

		if (DrawHitNormals)
		{
			int n = Mathf.Min(_rayCount, _hits.Length);
			for (int i = 0; i < n; i++)
			{
				var h = _hits[i];
				if (!h.Valid) continue;

				Vector3 p0w = h.Position;
				Vector3 p1w = h.Position + h.Normal * _normalLenWorld;

				Vector2 p0 = ScreenToLocal(_cam.UnprojectPosition(p0w));
				Vector2 p1 = ScreenToLocal(_cam.UnprojectPosition(p1w));

				DrawLine(p0, p1, NormalColor, NormalWidth);
			}
		}
	}


	private void EnsureCapacity(int rays, int pts)
	{
		if (_offsets.Length < rays) _offsets = new int[rays];
		if (_counts.Length < rays) _counts = new int[rays];
		if (_hits.Length < rays) _hits = new RayBeamRenderer.HitPayload[rays];
		if (_pts.Length < pts) _pts = new Vector3[pts];
	}

	private Color GetRayColor(int rayIndex)
	{
		float h = (rayIndex * 0.6180339f) % 1f;
		Color c = Color.FromHsv(h, 0.65f, 1.0f);
		c.A = RayColor.A;
		return c;
	}
}
