using System;
using System.Collections.Generic;
using System.Linq;

namespace BloodborneLauncher.Core;

public static class PadMap
{
	public sealed record Button(string Sdl, string Label);

	public static readonly Button[] Buttons = new Button[23]
	{
		new Button("a", "Cross / A"),
		new Button("b", "Circle / B"),
		new Button("x", "Square / X"),
		new Button("y", "Triangle / Y"),
		new Button("leftshoulder", "L1 / LB"),
		new Button("rightshoulder", "R1 / RB"),
		new Button("lefttrigger", "L2 / LT"),
		new Button("righttrigger", "R2 / RT"),
		new Button("leftstick", "L3 (left stick click)"),
		new Button("rightstick", "R3 (right stick click)"),
		new Button("dpup", "D-pad up"),
		new Button("dpdown", "D-pad down"),
		new Button("dpleft", "D-pad left"),
		new Button("dpright", "D-pad right"),
		new Button("start", "Options / Start"),
		new Button("back", "Share / Back"),
		new Button("touchpad", "Touchpad click"),
		new Button("misc1", "Mute / Share (Xbox Series)"),
		new Button("paddle1", "Extra button 1 (Edge / Elite paddle, 8BitDo R4 or back button)"),
		new Button("paddle2", "Extra button 2 (Edge / Elite paddle, 8BitDo L4 or back button)"),
		new Button("paddle3", "Extra button 3 (Elite paddle, 8BitDo back button)"),
		new Button("paddle4", "Extra button 4 (Elite paddle, 8BitDo back button)"),
		new Button("", "Not bound")
	};

	private static readonly Dictionary<string, string> InputOfLabel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["Cross"] = "cross",
		["Circle"] = "circle",
		["Square"] = "square",
		["Triangle"] = "triangle",
		["L1"] = "l1",
		["R1"] = "r1",
		["L2"] = "l2",
		["R2"] = "r2",
		["L3"] = "l3",
		["R3"] = "r3",
		["Options"] = "options",
		["Touchpad (left)"] = "touchpad",
		["Touchpad (right)"] = "touchpad_right",
		["D-pad up"] = "up",
		["D-pad down"] = "down",
		["D-pad left"] = "left",
		["D-pad right"] = "right"
	};

	public static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>
	{
		["cross"] = "a",
		["circle"] = "b",
		["square"] = "x",
		["triangle"] = "y",
		["l1"] = "leftshoulder",
		["r1"] = "rightshoulder",
		["l2"] = "lefttrigger",
		["r2"] = "righttrigger",
		["l3"] = "leftstick",
		["r3"] = "rightstick",
		["options"] = "start",
		["touchpad"] = "back, touchpad",
		["touchpad_right"] = "",
		["up"] = "dpup",
		["down"] = "dpdown",
		["left"] = "dpleft",
		["right"] = "dpright"
	};

	public static IEnumerable<string> Inputs => Defaults.Keys;

	public static string? InputFor(ControlDef control)
	{
		if (control.Pad == null || !InputOfLabel.TryGetValue(control.Pad, out string value))
		{
			return null;
		}
		return value;
	}

	private static string Key(string input)
	{
		return "pad." + input;
	}

	public static List<string> Get(IniFile ini, string input)
	{
		return Split(ini.Raw(Key(input)) ?? Defaults[input]);
	}

	public static string Main(IniFile ini, string input)
	{
		return Get(ini, input).FirstOrDefault() ?? "";
	}

	public static bool IsDefault(IniFile ini, string input)
	{
		return Get(ini, input).SequenceEqual(Split(Defaults[input]));
	}

	public static string Label(string sdl)
	{
		return Buttons.FirstOrDefault((Button b) => b.Sdl.Equals(sdl, StringComparison.OrdinalIgnoreCase))?.Label ?? sdl;
	}

	public static void Assign(IniFile ini, string input, string button)
	{
		string text = Main(ini, input);
		if (button.Length > 0)
		{
			foreach (string item in Inputs.Where((string i) => i != input))
			{
				List<string> list = Get(ini, item);
				if (list.Remove(button))
				{
					if (text.Length > 0 && !list.Contains(text))
					{
						list.Insert(0, text);
					}
					Write(ini, item, list);
				}
			}
		}
		Write(ini, input, (button.Length > 0) ? new List<string> { button } : new List<string>());
	}

	public static void Reset(IniFile ini, string input)
	{
		Write(ini, input, Split(Defaults[input]));
	}

	public static void ResetAll(IniFile ini)
	{
		foreach (string input in Inputs)
		{
			Reset(ini, input);
		}
	}

	private static void Write(IniFile ini, string input, List<string> buttons)
	{
		if (buttons.SequenceEqual(Split(Defaults[input])))
		{
			ini.Set(Key(input), null);
		}
		else
		{
			ini.Set(Key(input), string.Join(", ", buttons));
		}
	}

	public static Dictionary<string, string> Current(IniFile ini)
	{
		return Inputs.Where((string i) => !IsDefault(ini, i)).ToDictionary((string i) => i, (string i) => string.Join(", ", Get(ini, i)));
	}

	public static void Apply(IniFile ini, IReadOnlyDictionary<string, string>? map)
	{
		foreach (string input in Inputs)
		{
			Write(ini, input, (map != null && map.TryGetValue(input, out string value)) ? Split(value) : Split(Defaults[input]));
		}
	}

	private static List<string> Split(string value)
	{
		return (from v in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			select v.ToLowerInvariant() into v
			where v != "none"
			select v).ToList();
	}
}
