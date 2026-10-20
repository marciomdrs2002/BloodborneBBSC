using System;
using System.Collections.Generic;
using System.Linq;

namespace BloodborneLauncher.Core;

public static class Bindings
{
	public static string IniKey(ControlDef c)
	{
		return "bind." + c.Name;
	}

	public static List<string> Get(IniFile ini, ControlDef c)
	{
		return InputMap.Split(ini.Raw(IniKey(c)) ?? c.Default);
	}

	public static bool IsDefault(IniFile ini, ControlDef c)
	{
		return Same(Get(ini, c), InputMap.Split(c.Default));
	}

	private static bool Same(List<string> a, List<string> b)
	{
		if (a.Count == b.Count)
		{
			return a.Zip(b).All(((string First, string Second) p) => p.First.Equals(p.Second, StringComparison.OrdinalIgnoreCase));
		}
		return false;
	}

	public static void Set(IniFile ini, ControlDef c, IEnumerable<string> names)
	{
		List<string> source = names.ToList();
		string text = source.LastOrDefault((string n) => !InputMap.IsMouse(n));
		string text2 = source.LastOrDefault(InputMap.IsMouse);
		string text3 = InputMap.Join(new string[2] { text, text2 }.Where((string n) => n != null));
		ini.Set(IniKey(c), (text3.Length == 0) ? "none" : text3);
	}

	public static void Assign(IniFile ini, ControlDef c, string name, bool keepOtherKind)
	{
		IEnumerable<string> enumerable2;
		if (!keepOtherKind)
		{
			IEnumerable<string> enumerable = Enumerable.Empty<string>();
			enumerable2 = enumerable;
		}
		else
		{
			enumerable2 = from n in Get(ini, c)
				where InputMap.IsMouse(n) != InputMap.IsMouse(name)
				select n;
		}
		IEnumerable<string> source = enumerable2;
		Set(ini, c, source.Append(name));
	}

	public static void ResetAll(IniFile ini)
	{
		ControlDef[] controls = InputMap.Controls;
		foreach (ControlDef controlDef in controls)
		{
			ini.Set(IniKey(controlDef), controlDef.Default);
		}
	}

	public static Dictionary<string, string> Current(IniFile ini)
	{
		return InputMap.Controls.ToDictionary((ControlDef c) => c.Name, (ControlDef c) => InputMap.Join(Get(ini, c)));
	}
}
