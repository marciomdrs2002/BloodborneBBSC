using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace BloodborneLauncher.Core;

public static class InputMap
{
	public static readonly ControlDef[] Controls = new ControlDef[29]
	{
		new ControlDef("move_forward", "Move forward", "Movement", "", "W", "Left stick"),
		new ControlDef("move_back", "Move back", "Movement", "", "S", "Left stick"),
		new ControlDef("move_left", "Move left", "Movement", "", "A", "Left stick"),
		new ControlDef("move_right", "Move right", "Movement", "", "D", "Left stick"),
		new ControlDef("walk", "Walk", "Movement", "Hold with the movement keys", "Left Alt", null),
		new ControlDef("roll", "Dodge / sprint", "Movement", "Tap to dodge, hold to sprint", "Space", "Circle"),
		new ControlDef("l3", "Left stick click", "Movement", "", "Left Ctrl", "L3"),
		new ControlDef("attack", "Attack", "Combat", "With the strong modifier held: strong attack", "Mouse1", "R1"),
		new ControlDef("strong_attack", "Strong attack", "Combat", "Hold to charge", "V", "R2"),
		new ControlDef("strong_modifier", "Strong attack modifier", "Combat", "Held with Attack", "Left Shift", null),
		new ControlDef("transform", "Transform weapon", "Combat", "", "C, Mouse3", "L1"),
		new ControlDef("firearm", "Firearm / parry", "Combat", "", "X, Mouse2", "L2"),
		new ControlDef("lock_on", "Lock on", "Combat", "Or reset the camera", "Q", "R3"),
		new ControlDef("blood_vial", "Blood Vial", "Items", "", "R", "Triangle"),
		new ControlDef("use_item", "Use item", "Items", "The selected quick item", "F", "Square"),
		new ControlDef("interact", "Interact", "Items", "Talk, examine, pick up, open", "E", "Cross"),
		new ControlDef("switch_item", "Switch item", "Items", "Cycle the quick items", "Down", "D-pad down"),
		new ControlDef("switch_right", "Switch right weapon", "Items", "", "Right", "D-pad right"),
		new ControlDef("switch_left", "Switch left weapon", "Items", "", "Left", "D-pad left"),
		new ControlDef("dpad_up", "D-pad up", "Items", "", "Up", "D-pad up"),
		new ControlDef("gestures", "Gestures", "Menus", "", "G", "Touchpad (left)"),
		new ControlDef("personal_effects", "Personal effects", "Menus", "", "T", "Touchpad (right)"),
		new ControlDef("menu", "Menu", "Menus", "", "Escape", "Options"),
		new ControlDef("confirm", "Confirm in menus", "Menus", "", "Return", null),
		new ControlDef("back", "Back in menus", "Menus", "", "Backspace", null),
		new ControlDef("look_up", "Look up", "Camera keys", "The camera without the mouse", "I", "Right stick"),
		new ControlDef("look_down", "Look down", "Camera keys", "", "K", "Right stick"),
		new ControlDef("look_left", "Look left", "Camera keys", "", "J", "Right stick"),
		new ControlDef("look_right", "Look right", "Camera keys", "", "L", "Right stick")
	};

	public static readonly Dictionary<string, string> Reserved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Insert"] = "Insert opens the port's settings menu" };

	public static readonly string[] MouseTokens = new string[7] { "Mouse1", "Mouse2", "Mouse3", "Mouse4", "Mouse5", "WheelUp", "WheelDown" };

	private static readonly Lazy<HashSet<string>> KeyNames = new Lazy<HashSet<string>>(delegate
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (Key value in Enum.GetValues(typeof(Key)))
		{
			string text = SdlKeyName(value);
			if (text != null)
			{
				hashSet.Add(text);
			}
		}
		return hashSet;
	});

	public static bool IsMouse(string name)
	{
		return MouseTokens.Contains<string>(name, StringComparer.OrdinalIgnoreCase);
	}

	public static string Display(string name)
	{
		return name.ToLowerInvariant() switch
		{
			"mouse1" => "Left click", 
			"mouse2" => "Middle click", 
			"mouse3" => "Right click", 
			"mouse4" => "Mouse 4", 
			"mouse5" => "Mouse 5", 
			"wheelup" => "Wheel up", 
			"wheeldown" => "Wheel down", 
			_ => name, 
		};
	}

	public static List<string> Split(string value)
	{
		return (from n in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			where !n.Equals("none", StringComparison.OrdinalIgnoreCase)
			select n).ToList();
	}

	public static string Join(IEnumerable<string> names)
	{
		return string.Join(", ", names);
	}

	public static string? SdlKeyName(Key key)
	{
		if (key >= Key.A && key <= Key.Z)
		{
			return key.ToString();
		}
		if (key >= Key.D0 && key <= Key.D9)
		{
			return ((char)(48 + (key - 34))).ToString();
		}
		if (key >= Key.NumPad0 && key <= Key.NumPad9)
		{
			return "Keypad " + (char)(48 + (key - 74));
		}
		if (key >= Key.F1 && key <= Key.F24)
		{
			return "F" + (int)(key - 90 + 1);
		}
		return key switch
		{
			Key.Space => "Space", 
			Key.Return => "Return", 
			Key.Escape => "Escape", 
			Key.Back => "Backspace", 
			Key.Tab => "Tab", 
			Key.Capital => "CapsLock", 
			Key.LeftShift => "Left Shift", 
			Key.RightShift => "Right Shift", 
			Key.LeftCtrl => "Left Ctrl", 
			Key.RightCtrl => "Right Ctrl", 
			Key.LeftAlt => "Left Alt", 
			Key.RightAlt => "Right Alt", 
			Key.LWin => "Left GUI", 
			Key.RWin => "Right GUI", 
			Key.Apps => "Application", 
			Key.Up => "Up", 
			Key.Down => "Down", 
			Key.Left => "Left", 
			Key.Right => "Right", 
			Key.Insert => "Insert", 
			Key.Delete => "Delete", 
			Key.Home => "Home", 
			Key.End => "End", 
			Key.Prior => "PageUp", 
			Key.Next => "PageDown", 
			Key.Snapshot => "PrintScreen", 
			Key.Scroll => "ScrollLock", 
			Key.Pause => "Pause", 
			Key.NumLock => "Numlock", 
			Key.Divide => "Keypad /", 
			Key.Multiply => "Keypad *", 
			Key.Subtract => "Keypad -", 
			Key.Add => "Keypad +", 
			Key.Decimal => "Keypad .", 
			Key.OemMinus => "-", 
			Key.OemPlus => "=", 
			Key.Oem4 => "[", 
			Key.Oem6 => "]", 
			Key.Oem5 => "\\", 
			Key.Oem1 => ";", 
			Key.Oem7 => "'", 
			Key.Oem3 => "`", 
			Key.OemComma => ",", 
			Key.OemPeriod => ".", 
			Key.Oem2 => "/", 
			Key.Oem102 => "NonUSBackslash", 
			_ => null, 
		};
	}

	public static string? MouseToken(MouseButton button)
	{
		return button switch
		{
			MouseButton.Left => "Mouse1", 
			MouseButton.Middle => "Mouse2", 
			MouseButton.Right => "Mouse3", 
			MouseButton.XButton1 => "Mouse4", 
			MouseButton.XButton2 => "Mouse5", 
			_ => null, 
		};
	}

	public static string? Canonical(string name)
	{
		name = name.Trim();
		string text = MouseTokens.FirstOrDefault((string t) => t.Equals(name, StringComparison.OrdinalIgnoreCase));
		if (text != null)
		{
			return text;
		}
		return KeyNames.Value.FirstOrDefault((string k) => k.Equals(name, StringComparison.OrdinalIgnoreCase));
	}
}
