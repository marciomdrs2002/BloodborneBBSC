using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BloodborneLauncher.Core;

public static class PlayerProfiles
{
	public const string Main = "Main";

	public static List<string> All(LauncherSettings s)
	{
		List<string> list = new List<string> { "Main" };
		string[] array = s.Str("player_profiles").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		foreach (string text in array)
		{
			if (!list.Contains<string>(text, StringComparer.OrdinalIgnoreCase))
			{
				list.Add(text);
			}
		}
		return list;
	}

	public static string Current(LauncherSettings s)
	{
		string text = s.Str("player_profile");
		if (text.Length <= 0 || !All(s).Contains<string>(text, StringComparer.OrdinalIgnoreCase))
		{
			return "Main";
		}
		return text;
	}

	public static string Folder(LauncherSettings s, string profile)
	{
		if (!IsMain(profile))
		{
			return Path.Combine(GameEnvironment.SavesRoot(s), "profiles", SafeName(profile));
		}
		return GameEnvironment.SavesRoot(s);
	}

	public static bool IsMain(string profile)
	{
		return string.Equals(profile, "Main", StringComparison.OrdinalIgnoreCase);
	}

	public static string? Validate(LauncherSettings s, string name, out string error, string? renaming = null)
	{
		name = name.Trim();
		error = "";
		if (name.Length == 0)
		{
			error = "Enter a name.";
			return null;
		}
		if (name.Length > 32)
		{
			error = "Use 32 characters or fewer.";
			return null;
		}
		if (SafeName(name) != name)
		{
			error = "Use letters, numbers, spaces, - or _ only.";
			return null;
		}
		if (All(s).Any((string p) => p.Equals(name, StringComparison.OrdinalIgnoreCase) && !p.Equals(renaming, StringComparison.OrdinalIgnoreCase)))
		{
			error = "There is already a profile called " + name + ".";
			return null;
		}
		return name;
	}

	public static void Add(LauncherSettings s, string name)
	{
		IEnumerable<string> values = (from p in All(s)
			where !IsMain(p)
			select p).Append(name);
		s.Set("player_profiles", string.Join("|", values));
		Directory.CreateDirectory(Folder(s, name));
	}

	public static void Rename(LauncherSettings s, string from, string to)
	{
		string text = Folder(s, from);
		string text2 = Folder(s, to);
		if (Directory.Exists(text) && !Directory.Exists(text2))
		{
			Directory.Move(text, text2);
		}
		s.Set("player_profiles", string.Join("|", from p in All(s)
			where !IsMain(p)
			select (!p.Equals(@from, StringComparison.OrdinalIgnoreCase)) ? p : to));
		if (Current(s).Equals(from, StringComparison.OrdinalIgnoreCase) || s.Str("player_profile").Equals(from, StringComparison.OrdinalIgnoreCase))
		{
			s.Set("player_profile", to);
		}
	}

	public static void Remove(LauncherSettings s, string name)
	{
		s.Set("player_profiles", string.Join("|", from p in All(s)
			where !IsMain(p) && !p.Equals(name, StringComparison.OrdinalIgnoreCase)
			select p));
		if (Current(s).Equals("Main") || s.Str("player_profile").Equals(name, StringComparison.OrdinalIgnoreCase))
		{
			s.Set("player_profile", "");
		}
	}

	private static string SafeName(string name)
	{
		return new string(name.Where(delegate(char c)
		{
			bool flag = char.IsLetterOrDigit(c);
			if (!flag)
			{
				bool flag2 = ((c == ' ' || c == '-' || c == '_') ? true : false);
				flag = flag2;
			}
			return flag;
		}).ToArray()).Trim();
	}
}
