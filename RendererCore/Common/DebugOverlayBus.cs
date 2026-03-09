using Godot;
using System.Collections.Generic;

namespace RendererCore.Common;

public static class DebugOverlayBus
{
	public enum DebugOverlayItemType
	{
		Line,
		Text
	}

	public readonly struct DebugOverlayItem
	{
		public readonly DebugOverlayItemType Type;
		public readonly Vector2 A;
		public readonly Vector2 B;
		public readonly Vector2 Pos;
		public readonly string Text;
		public readonly Color Color;
		public readonly float Thickness;

		public DebugOverlayItem(
			DebugOverlayItemType type,
			Vector2 a,
			Vector2 b,
			Vector2 pos,
			string text,
			Color color,
			float thickness)
		{
			Type = type;
			A = a;
			B = b;
			Pos = pos;
			Text = text;
			Color = color;
			Thickness = thickness;
		}
	}

	private static readonly List<DebugOverlayItem> _items = new List<DebugOverlayItem>(256);

	public static IReadOnlyList<DebugOverlayItem> Items => _items;
	public static int Count => _items.Count;

	public static void ClearFrame()
	{
		_items.Clear();
	}

	public static void AddLine(Vector2 a, Vector2 b, Color color, float thickness = 1f)
	{
		_items.Add(new DebugOverlayItem(DebugOverlayItemType.Line, a, b, default, string.Empty, color, thickness));
	}

	public static void AddText(Vector2 pos, string text, Color color)
	{
		var item = new DebugOverlayItem(DebugOverlayItemType.Text, default, default, pos, text ?? string.Empty, color, 0f);
		for (int i = 0; i < _items.Count; i++)
		{
			if (_items[i].Type == DebugOverlayItemType.Text && _items[i].Pos == pos)
			{
				_items[i] = item;
				return;
			}
		}

		_items.Add(item);
	}
}
