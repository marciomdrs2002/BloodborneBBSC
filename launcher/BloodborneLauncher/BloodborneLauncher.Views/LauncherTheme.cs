using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using BloodborneLauncher.Core;
using Wpf.Ui.Appearance;

namespace BloodborneLauncher.Views;

public static class LauncherTheme
{
	public static readonly Choice[] Themes = new Choice[7]
	{
		new Choice("Default", "default"),
		new Choice("Crimson", "crimson", "The blood moon: deep red"),
		new Choice("Moonlight", "moon", "Pale blue, like the Moonlight Greatsword"),
		new Choice("Amber", "amber", "Lamplight gold"),
		new Choice("Nightmare", "nightmare", "Arcane violet"),
		new Choice("Verdant", "verdant", "Forbidden Woods green"),
		new Choice("Custom", "custom", "Your own colour, below")
	};

	private static readonly Dictionary<string, Color> Accents = new Dictionary<string, Color>
	{
		["crimson"] = Color.FromRgb(196, 30, 58),
		["moon"] = Color.FromRgb(143, 184, 232),
		["amber"] = Color.FromRgb(217, 154, 43),
		["nightmare"] = Color.FromRgb(142, 99, 214),
		["verdant"] = Color.FromRgb(63, 168, 107)
	};

	public const string DefaultCustomColour = "#C41E3A";

	private static ResourceDictionary? _overlay;

	private static Color? _originalAccent;

	public static Color? Accent(LauncherSettings s)
	{
		string text = s.Str("ui_theme");
		if (text == "custom")
		{
			return ParseColour(s.Str("ui_theme_colour"));
		}
		if (!Accents.TryGetValue(text, out var value))
		{
			return null;
		}
		return value;
	}

	public static Color? ParseColour(string text)
	{
		text = text.Trim();
		int length = text.Length;
		bool flag = ((length == 6 || length == 8) ? true : false);
		if (flag && !text.StartsWith('#'))
		{
			text = "#" + text;
		}
		try
		{
			return (ColorConverter.ConvertFromString(text) is Color color) ? new Color?(Color.FromRgb(color.R, color.G, color.B)) : ((Color?)null);
		}
		catch (FormatException)
		{
			return null;
		}
	}

	public static void Apply(LauncherSettings s, bool glass)
	{
		ResourceDictionary resources = Application.Current.Resources;
		if (_overlay != null)
		{
			resources.MergedDictionaries.Remove(_overlay);
			_overlay = null;
		}
		Color? originalAccent = _originalAccent;
		if (!originalAccent.HasValue)
		{
			_originalAccent = ((resources["SystemAccentColor"] is Color value) ? new Color?(value) : ((Color?)null));
		}
		Color? color = Accent(s);
		ResourceDictionary d;
		if (color.HasValue)
		{
			Color valueOrDefault = color.GetValueOrDefault();
			ApplicationAccentColorManager.Apply(valueOrDefault, ApplicationTheme.Dark);
			d = new ResourceDictionary();
			Solid("AccentTextFillColorPrimaryBrush", Mix(valueOrDefault, Colors.White, 0.25));
			if (glass)
			{
				d["SegmentSelectedBrush"] = Freeze(new LinearGradientBrush(WithAlpha(Mix(valueOrDefault, Colors.White, 0.2), 192), WithAlpha(valueOrDefault, 128), 90.0));
				Solid("WindowBaseBrush", Mix(Color.FromRgb(12, 13, 16), valueOrDefault, 0.1));
				Solid("PillTrackBrush", WithAlpha(Mix(Colors.Black, valueOrDefault, 0.15), 64));
			}
			else
			{
				Solid("SegmentSelectedBrush", Mix(valueOrDefault, Colors.Black, 0.1));
				Solid("WindowBaseBrush", Mix(Color.FromRgb(36, 36, 38), valueOrDefault, 0.06));
				Solid("ContentAreaBrush", Mix(Color.FromRgb(41, 41, 43), valueOrDefault, 0.06));
				Solid("CardBackgroundFillColorDefaultBrush", Mix(Color.FromRgb(46, 46, 49), valueOrDefault, 0.07));
				Solid("CardStrokeColorDefaultBrush", Mix(Color.FromRgb(58, 58, 62), valueOrDefault, 0.12));
				Solid("SidebarItemSelectedBrush", Mix(Color.FromRgb(55, 55, 59), valueOrDefault, 0.2));
				Solid("SidebarItemHoverBrush", Mix(Color.FromRgb(46, 46, 49), valueOrDefault, 0.08));
				Solid("PillTrackBrush", Mix(Color.FromRgb(30, 30, 33), valueOrDefault, 0.05));
				Solid("DialogBackgroundBrush", Mix(Color.FromRgb(44, 44, 47), valueOrDefault, 0.06));
				Solid("PanelDeepBrush", Mix(Color.FromRgb(30, 30, 32), valueOrDefault, 0.04));
			}
			resources.MergedDictionaries.Add(d);
			_overlay = d;
		}
		else
		{
			originalAccent = _originalAccent;
			if (originalAccent.HasValue)
			{
				Color valueOrDefault2 = originalAccent.GetValueOrDefault();
				ApplicationAccentColorManager.Apply(valueOrDefault2, ApplicationTheme.Dark);
			}
		}
		void Solid(string key, Color c)
		{
			d[key] = Freeze(new SolidColorBrush(c));
		}
	}

	public static Color ShadeColour(LauncherSettings s)
	{
		Color? color = Accent(s);
		if (color.HasValue)
		{
			Color valueOrDefault = color.GetValueOrDefault();
			return Mix(Color.FromRgb(10, 11, 14), valueOrDefault, 0.12);
		}
		return Color.FromRgb(10, 11, 14);
	}

	public static Color Mix(Color from, Color to, double amount)
	{
		return Color.FromRgb((byte)Math.Round((double)(int)from.R + (double)(to.R - from.R) * amount), (byte)Math.Round((double)(int)from.G + (double)(to.G - from.G) * amount), (byte)Math.Round((double)(int)from.B + (double)(to.B - from.B) * amount));
	}

	public static Color WithAlpha(Color c, byte alpha)
	{
		return Color.FromArgb(alpha, c.R, c.G, c.B);
	}

	private static T Freeze<T>(T brush) where T : Freezable
	{
		brush.Freeze();
		return brush;
	}
}
