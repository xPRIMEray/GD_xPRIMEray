using System;
using System.Collections.Generic;
using Godot;

public enum SourcePatternMode
{
	SinglePoint = 0,
	LineX = 1,
	LineY = 2,
	CrossXY = 3,
	GridXY = 4
}

public readonly struct SourcePatternConfig
{
	public readonly SourcePatternMode Mode;
	public readonly int CountX;
	public readonly int CountY;
	public readonly float SpacingX;
	public readonly float SpacingY;
	public readonly bool IncludeCenterPoint;

	public SourcePatternConfig(
		SourcePatternMode mode,
		int countX,
		int countY,
		float spacingX,
		float spacingY,
		bool includeCenterPoint)
	{
		Mode = mode;
		CountX = Math.Max(1, countX);
		CountY = Math.Max(1, countY);
		SpacingX = Math.Max(0f, spacingX);
		SpacingY = Math.Max(0f, spacingY);
		IncludeCenterPoint = includeCenterPoint;
	}
}

public static class SourcePatternHelper
{
	public static Vector3[] BuildLocalOffsets(in SourcePatternConfig cfg)
	{
		return cfg.Mode switch
		{
			SourcePatternMode.SinglePoint => new[] { Vector3.Zero },
			SourcePatternMode.LineX => BuildLineX(cfg.CountX, cfg.SpacingX, cfg.IncludeCenterPoint),
			SourcePatternMode.LineY => BuildLineY(cfg.CountY, cfg.SpacingY, cfg.IncludeCenterPoint),
			SourcePatternMode.GridXY => BuildGrid(cfg.CountX, cfg.CountY, cfg.SpacingX, cfg.SpacingY, cfg.IncludeCenterPoint),
			_ => BuildCross(cfg.CountX, cfg.CountY, cfg.SpacingX, cfg.SpacingY, cfg.IncludeCenterPoint)
		};
	}

	private static Vector3[] BuildLineX(int count, float spacing, bool includeCenterPoint)
	{
		List<Vector3> offsets = new List<Vector3>(Math.Max(1, count + 1));
		AppendLineX(offsets, count, spacing);
		AddCenterIfRequested(offsets, includeCenterPoint);
		return offsets.ToArray();
	}

	private static Vector3[] BuildLineY(int count, float spacing, bool includeCenterPoint)
	{
		List<Vector3> offsets = new List<Vector3>(Math.Max(1, count + 1));
		AppendLineY(offsets, count, spacing);
		AddCenterIfRequested(offsets, includeCenterPoint);
		return offsets.ToArray();
	}

	private static Vector3[] BuildCross(int countX, int countY, float spacingX, float spacingY, bool includeCenterPoint)
	{
		List<Vector3> offsets = new List<Vector3>(Math.Max(3, countX + countY + 1));
		AppendLineX(offsets, countX, spacingX);
		AppendLineY(offsets, countY, spacingY);
		AddCenterIfRequested(offsets, includeCenterPoint);
		return offsets.ToArray();
	}

	private static Vector3[] BuildGrid(int countX, int countY, float spacingX, float spacingY, bool includeCenterPoint)
	{
		List<Vector3> offsets = new List<Vector3>(Math.Max(1, countX * countY + 1));
		float startX = -((countX - 1) * 0.5f) * spacingX;
		float startY = -((countY - 1) * 0.5f) * spacingY;
		for (int y = 0; y < countY; y++)
		{
			float py = startY + (y * spacingY);
			for (int x = 0; x < countX; x++)
			{
				float px = startX + (x * spacingX);
				TryAddUnique(offsets, new Vector3(px, py, 0f));
			}
		}
		AddCenterIfRequested(offsets, includeCenterPoint);
		return offsets.ToArray();
	}

	private static void AppendLineX(List<Vector3> offsets, int count, float spacing)
	{
		float start = -((count - 1) * 0.5f) * spacing;
		for (int i = 0; i < count; i++)
		{
			TryAddUnique(offsets, new Vector3(start + (i * spacing), 0f, 0f));
		}
	}

	private static void AppendLineY(List<Vector3> offsets, int count, float spacing)
	{
		float start = -((count - 1) * 0.5f) * spacing;
		for (int i = 0; i < count; i++)
		{
			TryAddUnique(offsets, new Vector3(0f, start + (i * spacing), 0f));
		}
	}

	private static void AddCenterIfRequested(List<Vector3> offsets, bool includeCenterPoint)
	{
		if (includeCenterPoint)
		{
			TryAddUnique(offsets, Vector3.Zero);
		}
	}

	private static void TryAddUnique(List<Vector3> offsets, Vector3 candidate)
	{
		for (int i = 0; i < offsets.Count; i++)
		{
			if (offsets[i].IsEqualApprox(candidate))
			{
				return;
			}
		}
		offsets.Add(candidate);
	}
}
