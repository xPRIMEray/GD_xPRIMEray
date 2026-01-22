using Godot;
using System;

public sealed class FieldGrid3D
{
	public Vector3 GridOrigin { get; private set; }
	public float CellSize { get; private set; }
	public int DimX { get; private set; }
	public int DimY { get; private set; }
	public int DimZ { get; private set; }

	private Vector3[] _field = Array.Empty<Vector3>();

	public void BuildFromSources(
		RayBeamRenderer.FieldSourceSnap[] sources,
		float globalBeta,
		float globalGamma,
		float bendScale,
		float fieldStrength,
		Aabb bounds,
		float cellSize)
	{
		CellSize = Mathf.Max(0.001f, cellSize);
		GridOrigin = bounds.Position;

		Vector3 size = bounds.Size;
		DimX = Math.Max(1, Mathf.FloorToInt(size.X / CellSize) + 1);
		DimY = Math.Max(1, Mathf.FloorToInt(size.Y / CellSize) + 1);
		DimZ = Math.Max(1, Mathf.FloorToInt(size.Z / CellSize) + 1);

		int total = DimX * DimY * DimZ;
		if (_field.Length != total)
			_field = new Vector3[total];

		int index = 0;
		for (int z = 0; z < DimZ; z++)
		{
			float pz = GridOrigin.Z + z * CellSize;
			for (int y = 0; y < DimY; y++)
			{
				float py = GridOrigin.Y + y * CellSize;
				for (int x = 0; x < DimX; x++)
				{
					float px = GridOrigin.X + x * CellSize;
					Vector3 p = new Vector3(px, py, pz);
					_field[index++] = RayBeamRenderer.ComputeAccelerationAtPointSnap(
						p,
						sources,
						globalBeta,
						globalGamma,
						bendScale,
						fieldStrength);
				}
			}
		}
	}

	public bool TrySample(Vector3 p, out Vector3 a)
	{
		a = Vector3.Zero;
		if (_field.Length == 0 || DimX <= 0 || DimY <= 0 || DimZ <= 0)
			return false;

		float maxX = GridOrigin.X + (DimX - 1) * CellSize;
		float maxY = GridOrigin.Y + (DimY - 1) * CellSize;
		float maxZ = GridOrigin.Z + (DimZ - 1) * CellSize;

		if (p.X < GridOrigin.X || p.Y < GridOrigin.Y || p.Z < GridOrigin.Z)
			return false;
		if (p.X > maxX || p.Y > maxY || p.Z > maxZ)
			return false;

		float localX = (p.X - GridOrigin.X) / CellSize;
		float localY = (p.Y - GridOrigin.Y) / CellSize;
		float localZ = (p.Z - GridOrigin.Z) / CellSize;

		int x0 = Mathf.Clamp(Mathf.FloorToInt(localX), 0, DimX - 1);
		int y0 = Mathf.Clamp(Mathf.FloorToInt(localY), 0, DimY - 1);
		int z0 = Mathf.Clamp(Mathf.FloorToInt(localZ), 0, DimZ - 1);

		int x1 = Mathf.Min(x0 + 1, DimX - 1);
		int y1 = Mathf.Min(y0 + 1, DimY - 1);
		int z1 = Mathf.Min(z0 + 1, DimZ - 1);

		float fx = (x1 == x0) ? 0f : (localX - x0);
		float fy = (y1 == y0) ? 0f : (localY - y0);
		float fz = (z1 == z0) ? 0f : (localZ - z0);

		Vector3 v000 = _field[Index(x0, y0, z0)];
		Vector3 v100 = _field[Index(x1, y0, z0)];
		Vector3 v010 = _field[Index(x0, y1, z0)];
		Vector3 v110 = _field[Index(x1, y1, z0)];
		Vector3 v001 = _field[Index(x0, y0, z1)];
		Vector3 v101 = _field[Index(x1, y0, z1)];
		Vector3 v011 = _field[Index(x0, y1, z1)];
		Vector3 v111 = _field[Index(x1, y1, z1)];

		Vector3 c00 = v000 * (1f - fx) + v100 * fx;
		Vector3 c10 = v010 * (1f - fx) + v110 * fx;
		Vector3 c01 = v001 * (1f - fx) + v101 * fx;
		Vector3 c11 = v011 * (1f - fx) + v111 * fx;

		Vector3 c0 = c00 * (1f - fy) + c10 * fy;
		Vector3 c1 = c01 * (1f - fy) + c11 * fy;

		a = c0 * (1f - fz) + c1 * fz;
		return true;
	}

	private int Index(int x, int y, int z)
	{
		return (z * DimY + y) * DimX + x;
	}
}
