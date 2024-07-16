using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;

// Code taken from https://github.com/DelvUI/DelvUI/blob/develop/DelvUI/Helpers/ClipRectsHelper.cs

namespace CatScan.Ui;

public class ClipRectsHelper
{
	private static List<ClipRect> _clipRects = new List<ClipRect>();

	private static List<string> _ignoredAddonNames = new List<string>()
	{
		"_FocusTargetInfo",
	};

	public unsafe static void Update()
	{
		_clipRects.Clear();

		AtkStage* stage = AtkStage.Instance();
		if (stage == null) { return; }

		RaptureAtkUnitManager* manager = stage->RaptureAtkUnitManager;
		if (manager == null) { return; }

		AtkUnitList* loadedUnitsList = &manager->AtkUnitManager.AllLoadedUnitsList;
		if (loadedUnitsList == null) { return; }

		for (int i = 0; i < loadedUnitsList->Count; i++)
		{
			try
			{
				AtkUnitBase* addon = *(AtkUnitBase**)Unsafe.AsPointer(ref loadedUnitsList->Entries[i]);
				if (addon == null || !addon->IsVisible || addon->WindowNode == null || addon->Scale == 0)
				{
					continue;
				}

				string name = addon->NameString;
				if (_ignoredAddonNames.Contains(name))
				{
					continue;
				}

				float margin = 5 * addon->Scale;
				float bottomMargin = 13 * addon->Scale;

				Vector2 pos = new Vector2(addon->X + margin, addon->Y + margin);
				Vector2 size = new Vector2(
					addon->WindowNode->AtkResNode.Width * addon->Scale - margin,
					addon->WindowNode->AtkResNode.Height * addon->Scale - bottomMargin
				);

				// special case for duty finder
				if (name == "ContentsFinder")
				{
					size.X += size.X + (16 * addon->Scale);
					size.Y += (30 * addon->Scale);
				}

				if (name == "Journal")
				{
					size.X += size.X + (16 * addon->Scale);
				}

				// just in case this causes weird issues / crashes (doubt it though...)
				ClipRect clipRect = new ClipRect(name, pos, pos + size);
				if (clipRect.Max.X < clipRect.Min.X || clipRect.Max.Y < clipRect.Min.Y)
				{
					continue;
				}

				_clipRects.Add(clipRect);
			}
			catch { }
		}
	}

	public static IEnumerable<ClipRect> ActiveClipRects()
	{
		return _clipRects;
	}

	public static ClipRect? GetClipRectForArea(Vector2 pos, Vector2 size)
	{
		var rects = ActiveClipRects();

		foreach (ClipRect clipRect in rects)
		{
			ClipRect area = new ClipRect(pos, pos + size);
			if (clipRect.IntersectsWith(area))
			{
				return clipRect;
			}
		}

		return null;
	}
}

public struct ClipRect
{
	public readonly string Name = "Custom";
	public readonly Vector2 Min;
	public readonly Vector2 Max;

	private readonly Rectangle Rectangle;

	public ClipRect(Vector2 min, Vector2 max)
	{
		Vector2 screenSize = ImGui.GetMainViewport().Size;

		Min = Clamp(min, Vector2.Zero, screenSize);
		Max = Clamp(max, Vector2.Zero, screenSize);

		Vector2 size = Max - Min;

		Rectangle = new Rectangle((int)Min.X, (int)Min.Y, (int)size.X, (int)size.Y);
	}

	public ClipRect(string name, Vector2 min, Vector2 max) : this(min, max)
	{
		Name = name;
	}

	public bool Contains(Vector2 point)
	{
		return Rectangle.Contains((int)point.X, (int)point.Y);
	}

	public bool IntersectsWith(ClipRect other)
	{
		return Rectangle.IntersectsWith(other.Rectangle);
	}

	private static Vector2 Clamp(Vector2 vector, Vector2 min, Vector2 max)
	{
		return new Vector2(Math.Max(min.X, Math.Min(max.X, vector.X)), Math.Max(min.Y, Math.Min(max.Y, vector.Y)));
	}
}
