using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BloodborneLauncher.Core;

public static class Mods
{
	public sealed record Entry(string Name, bool Enabled);

	private static readonly HashSet<string> GameFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"action", "adhoc", "chr", "event", "facegen", "font", "map", "menu", "movie", "msg",
		"mtd", "obj", "other", "param", "paramdef", "parts", "remo", "script", "sfx", "shader",
		"sound"
	};

	private static readonly string[] Serials = new string[9] { "CUSA03173", "CUSA00900", "CUSA00207", "CUSA00208", "CUSA00299", "CUSA01363", "CUSA03179", "CUSA03014", "CUSA03023" };

	private static readonly string[] LooseFileTypes = new string[5] { ".txt", ".md", ".jpg", ".png", ".ini" };

	public static string ProfilePath => Path.Combine(Paths.DataDir, "mods.json");

	public static List<string> Discover(string root)
	{
		if (!Directory.Exists(root))
		{
			return new List<string>();
		}
		return Directory.GetDirectories(root).Where(HasContent).Select(Path.GetFileName)
			.OfType<string>()
			.OrderBy<string, string>((string n) => n, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static bool HasContent(string folder)
	{
		try
		{
			return ContentRoot(folder);
		}
		catch
		{
			return false;
		}
	}

	private static string Child(string folder, string name)
	{
		if (!Directory.Exists(folder))
		{
			return Path.Combine(folder, name);
		}
		return Directory.EnumerateFileSystemEntries(folder).FirstOrDefault((string e) => string.Equals(Path.GetFileName(e), name, StringComparison.OrdinalIgnoreCase)) ?? Path.Combine(folder, name);
	}

	private static bool ContentRoot(string folder)
	{
		if (!Directory.Exists(folder))
		{
			return false;
		}
		foreach (string item in new string[2] { "", "app0" }.Concat(Serials))
		{
			if (Directory.Exists(Child((item.Length > 0) ? Child(folder, item) : folder, "dvdroot_ps4")))
			{
				return true;
			}
		}
		List<string> source = (from e in Directory.EnumerateFileSystemEntries(folder)
			where !Path.GetFileName(e).StartsWith('.')
			select e).ToList();
		List<string> list = source.Where(Directory.Exists).ToList();
		if (list.Count > 0 && list.All((string f) => GameFolders.Contains(Path.GetFileName(f))))
		{
			return true;
		}
		if (list.Count == 1 && !source.Any((string e) => File.Exists(e) && !LooseFileTypes.Contains<string>(Path.GetExtension(e), StringComparer.OrdinalIgnoreCase)))
		{
			return ContentRoot(list[0]);
		}
		return false;
	}

	public static List<Entry> Load(string root)
	{
		List<string> list = Discover(root);
		List<string> list2 = new List<string>();
		HashSet<string> disabled = new HashSet<string>();
		try
		{
			if (File.Exists(ProfilePath) && JsonNode.Parse(File.ReadAllText(ProfilePath)) is JsonObject jsonObject)
			{
				list2 = (from n in jsonObject["order"]?.AsArray()
					select n?.GetValue<string>() ?? "").ToList() ?? list2;
				disabled = (from n in jsonObject["disabled"]?.AsArray()
					select n?.GetValue<string>() ?? "").ToHashSet() ?? disabled;
			}
		}
		catch
		{
		}
		return (from n in list2.Concat(list).Distinct().Where(list.Contains)
			select new Entry(n, !disabled.Contains(n))).ToList();
	}

	public static void Save(IEnumerable<Entry> mods)
	{
		List<Entry> source = mods.ToList();
		JsonObject jsonObject = new JsonObject
		{
			["order"] = new JsonArray(((IEnumerable<Entry>)source).Select((Func<Entry, JsonNode>)((Entry m) => m.Name)).ToArray()),
			["disabled"] = new JsonArray(source.Where((Entry m) => !m.Enabled).Select((Func<Entry, JsonNode>)((Entry m) => m.Name)).ToArray())
		};
		Directory.CreateDirectory(Paths.DataDir);
		File.WriteAllText(ProfilePath, jsonObject.ToJsonString(new JsonSerializerOptions
		{
			WriteIndented = true
		}) + "\n");
	}
}
