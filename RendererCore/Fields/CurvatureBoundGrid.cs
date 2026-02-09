using System;
using System.Numerics;
using RendererCore.SceneSnapshot;

namespace RendererCore.Fields;

public sealed class CurvatureBoundGrid
{
    public readonly Vector3 OriginWorld;
    public readonly float CellSize;
    public readonly int DimX;
    public readonly int DimY;
    public readonly int DimZ;
    public readonly float[] Kmax;

    public CurvatureBoundGrid(Vector3 originWorld, float cellSize, int dimX, int dimY, int dimZ, float[] kmax)
    {
        OriginWorld = originWorld;
        CellSize = cellSize;
        DimX = dimX;
        DimY = dimY;
        DimZ = dimZ;
        Kmax = kmax ?? Array.Empty<float>();
    }

    public float LookupKmax(Vector3 pWorld)
    {
        if (Kmax.Length == 0 || CellSize <= 0f || DimX <= 0 || DimY <= 0 || DimZ <= 0)
        {
            return 0f;
        }

        var fx = (pWorld.X - OriginWorld.X) / CellSize;
        var fy = (pWorld.Y - OriginWorld.Y) / CellSize;
        var fz = (pWorld.Z - OriginWorld.Z) / CellSize;

        var ix = (int)MathF.Floor(fx);
        var iy = (int)MathF.Floor(fy);
        var iz = (int)MathF.Floor(fz);

        if (ix < 0 || iy < 0 || iz < 0 || ix >= DimX || iy >= DimY || iz >= DimZ)
        {
            return 0f;
        }

        var index = ix + (DimX * (iy + (DimY * iz)));
        if ((uint)index >= (uint)Kmax.Length)
        {
            return 0f;
        }

        return Kmax[index];
    }

    public bool IsInside(Vector3 pWorld)
    {
        return pWorld.X >= OriginWorld.X
            && pWorld.X < OriginWorld.X + (DimX * CellSize)
            && pWorld.Y >= OriginWorld.Y
            && pWorld.Y < OriginWorld.Y + (DimY * CellSize)
            && pWorld.Z >= OriginWorld.Z
            && pWorld.Z < OriginWorld.Z + (DimZ * CellSize);
    }

    public static CurvatureBoundGrid BuildAroundCamera(
        Vector3 camPos,
        float cellSize,
        int dimX, int dimY, int dimZ,
        in SceneSnapshot.SceneSnapshot snapshot)
    {
        var origin = camPos - (new Vector3(dimX, dimY, dimZ) * (cellSize * 0.5f));

        var totalCells = (long)dimX * dimY * dimZ;
        if (cellSize <= 0f || dimX <= 0 || dimY <= 0 || dimZ <= 0 || totalCells <= 0 || totalCells > int.MaxValue)
        {
            return new CurvatureBoundGrid(origin, cellSize, dimX, dimY, dimZ, Array.Empty<float>());
        }

        var kmax = new float[(int)totalCells];

        if (snapshot == null || snapshot.Fields == null || snapshot.Fields.Count <= 0)
        {
            return new CurvatureBoundGrid(origin, cellSize, dimX, dimY, dimZ, kmax);
        }

        var fields = snapshot.Fields;
        var fieldParams = snapshot.FieldParams?.Data;
        var curveType = fields.CurveType;
        var paramOffset = fields.ParamOffset;
        var worldBounds = fields.WorldBounds;
        var fieldCount = fields.Count;

        Span<int> candidates = stackalloc int[256];

        for (var z = 0; z < dimZ; z++)
        {
            for (var y = 0; y < dimY; y++)
            {
                for (var x = 0; x < dimX; x++)
                {
                    var cellMin = origin + new Vector3(x * cellSize, y * cellSize, z * cellSize);
                    var cellMax = cellMin + new Vector3(cellSize, cellSize, cellSize);
                    var region = new Aabb3(cellMin, cellMax);

                    var candidateCount = 0;
                    if (snapshot.FieldTLAS != null)
                    {
                        candidateCount = snapshot.FieldTLAS.QueryAabb(region, candidates);
                    }
                    else if (worldBounds != null)
                    {
                        var boundsCount = Math.Min(fieldCount, worldBounds.Length);
                        for (var i = 0; i < boundsCount; i++)
                        {
                            if (!worldBounds[i].Overlaps(region))
                            {
                                continue;
                            }

                            if (candidateCount >= candidates.Length)
                            {
                                break;
                            }

                            candidates[candidateCount++] = i;
                        }
                    }

                    var sum = 0f;
                    for (var i = 0; i < candidateCount; i++)
                    {
                        var fieldIndex = candidates[i];
                        if (fieldIndex < 0 || fieldIndex >= fieldCount)
                        {
                            continue;
                        }

                        if (paramOffset == null || fieldParams == null || fieldIndex >= paramOffset.Length)
                        {
                            continue;
                        }

                        var offset = paramOffset[fieldIndex];
                        if (offset < 0 || offset + 5 >= fieldParams.Length)
                        {
                            continue;
                        }

                        var amp = fieldParams[offset + 2];
                        var a = fieldParams[offset + 3];
                        var b = fieldParams[offset + 4];
                        var c = fieldParams[offset + 5];
                        var curve = (curveType != null && fieldIndex < curveType.Length)
                            ? (FieldCurveType)curveType[fieldIndex]
                            : FieldCurveType.Linear;

                        var fmax = Fmax(curve, a, b, c);
                        sum += MathF.Abs(amp) * fmax;
                    }

                    var index = x + (dimX * (y + (dimY * z)));
                    kmax[index] = sum;
                }
            }
        }

        return new CurvatureBoundGrid(origin, cellSize, dimX, dimY, dimZ, kmax);
    }

    private static float Fmax(FieldCurveType type, float a, float b, float c)
    {
        if (type != FieldCurveType.Polynomial)
        {
            return 1f;
        }

        var f0 = FieldCurves.Eval(FieldCurveType.Polynomial, 0f, a, b, c, clamp01: false);
        var f05 = FieldCurves.Eval(FieldCurveType.Polynomial, 0.5f, a, b, c, clamp01: false);
        var f1 = FieldCurves.Eval(FieldCurveType.Polynomial, 1f, a, b, c, clamp01: false);
        var max = MathF.Max(MathF.Abs(f0), MathF.Max(MathF.Abs(f05), MathF.Abs(f1)));
        return max * 1.25f;
    }
}
