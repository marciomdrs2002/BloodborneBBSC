using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace BloodborneLauncher.Core;

public static class Patches
{
	public sealed record Patch(string Key, string Name, string File, string Author, string Note, bool DefaultOn, bool ChangesRegion = false, bool SetsResolution = false);

	private const long RegionStart = 37458320L;

	private const long RegionEnd = 37458403L;

	private const string ResolutionTemplate = "Resolution Patch 1280x720 (16:9)";

	private static readonly HashSet<string> ManagedNames = new HashSet<string>(StringComparer.Ordinal)
	{
		"Sprint Fix (High FPS)", "60FPS (no deltatime)", "Intel Black Tonemap Fix", "Intel 12th Gen+ SFX workaround", "Disable Chromatic Aberration", "Disable DoF", "Disable Motion Blur (perf increase)", "Disable SSAO", "Disable AA", "Disable Dynamic Light Shadows (perf increase)",
		"Enable Screen Space Reflections (READ NOTE)", "Skip Intro", "Restore Debug Camera", "Restore Debug Menu (READ NOTES)", "Increased Graphics Heap Sizes"
	};

	public static string ProfilePath => Path.Combine(Paths.DataDir, "patches.json");

	private static (long Start, long End)? Range(XElement line)
	{
		string text = (((string?)line.Attribute("Address")) ?? "").Trim();
		if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			string text2 = text;
			text = text2.Substring(2, text2.Length - 2);
		}
		if (!long.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
		{
			return null;
		}
		string text3 = (((string?)line.Attribute("Value")) ?? "").Trim();
		int num;
		switch (((string?)line.Attribute("Type")) ?? "bytes")
		{
		case "bytes16":
			num = 2;
			break;
		case "bytes32":
		case "float32":
			num = 4;
			break;
		case "bytes64":
		case "float64":
			num = 8;
			break;
		default:
			num = Math.Max(1, text3.Replace(" ", "").Length / 2);
			break;
		}
		int num2 = num;
		return (result, result + num2);
	}

	private static bool WritesRegion(XElement line)
	{
		(long, long)? tuple = Range(line);
		if (tuple.HasValue)
		{
			(long, long) valueOrDefault = tuple.GetValueOrDefault();
			if (valueOrDefault.Item1 < 37458403)
			{
				return 37458320 < valueOrDefault.Item2;
			}
		}
		return false;
	}

	private static List<(long Start, long End)> ResolutionFootprint()
	{
		try
		{
			return (from r in XDocument.Load(Path.Combine(Paths.EngineDir, "patches", "Bloodborne.xml")).Descendants("Metadata").FirstOrDefault((XElement m) => (string?)m.Attribute("Name") == "Resolution Patch 1280x720 (16:9)")?.Descendants("Line").Select(Range)
				where r.HasValue
				select r.Value).ToList() ?? new List<(long, long)>();
		}
		catch
		{
			return new List<(long, long)>();
		}
	}

	public static List<Patch> ResolutionSetting(string directory)
	{
		(HashSet<string> Enabled, HashSet<string> Disabled) profile = LoadProfile();
		return (from p in Discover(directory)
			where p.SetsResolution && IsOn(p, profile)
			select p).ToList();
	}

	public static List<Patch> RegionChanging(string directory)
	{
		(HashSet<string> Enabled, HashSet<string> Disabled) profile = LoadProfile();
		return (from p in Discover(directory)
			where p.ChangesRegion && IsOn(p, profile)
			select p).ToList();
	}

	private static bool IsSupportedVersion(string? value)
	{
		string[] array = (value ?? "").Trim().Split('.');
		if (array.Length == 2 && int.TryParse(array[0], out var result) && int.TryParse(array[1], out var result2) && result == 1)
		{
			return result2 == 9;
		}
		return false;
	}

	public static bool ManagedBuiltIn(string name)
	{
		if (!name.Contains("FPS++", StringComparison.Ordinal) && !name.StartsWith("Resolution Patch", StringComparison.Ordinal) && !name.StartsWith("Model LOD", StringComparison.Ordinal))
		{
			return ManagedNames.Contains(name);
		}
		return true;
	}

	public static List<Patch> Discover(string directory)
	{
		List<Patch> list = new List<Patch>();
		try
		{
			Directory.CreateDirectory(directory);
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
		}
		string builtIn = Path.GetFullPath(Path.Combine(Paths.EngineDir, "patches", "Bloodborne.xml"));
		List<(long Start, long End)> footprint = ResolutionFootprint();
		IEnumerable<string> source = (Directory.Exists(directory) ? ((IEnumerable<string>)Directory.GetFiles(directory, "*.xml")) : ((IEnumerable<string>)Array.Empty<string>())).Where((string p) => !string.Equals(Path.GetFileName(p), Path.GetFileName(builtIn), StringComparison.OrdinalIgnoreCase));
		if (File.Exists(builtIn))
		{
			source = source.Prepend(builtIn);
		}
		foreach (string item in source.Distinct().ToList())
		{
			bool flag = string.Equals(Path.GetFullPath(item), builtIn, StringComparison.OrdinalIgnoreCase);
			XElement root;
			try
			{
				root = XDocument.Load(item).Root;
			}
			catch
			{
				continue;
			}
			HashSet<string> hashSet = (from e in root.Descendants("ID")
				select e.Value.Trim() into v
				where v.Length > 0
				select v).ToHashSet();
			if (hashSet.Count > 0 && !hashSet.Overlaps(GameCheck.SupportedTitles))
			{
				continue;
			}
			foreach (XElement item2 in root.Descendants("Metadata"))
			{
				if (!IsSupportedVersion((string?)item2.Attribute("AppVer")) || (((string?)item2.Attribute("AppElf")) ?? "eboot.bin") != "eboot.bin")
				{
					continue;
				}
				string text = ((string?)item2.Attribute("Name")) ?? "";
				if (flag && ManagedBuiltIn(text))
				{
					continue;
				}
				string fileName = Path.GetFileName(item);
				list.Add(new Patch(fileName + "/" + text, (text.Length > 0) ? text : (fileName + "/" + text), fileName, ((string?)item2.Attribute("Author")) ?? "", (((string?)item2.Attribute("Note")) ?? "").Replace("\\n", "\n"), !flag && string.Equals((string?)item2.Attribute("isEnabled"), "true", StringComparison.OrdinalIgnoreCase), item2.Descendants("Line").Any(WritesRegion), item2.Descendants("Line").Select(Range).Any(delegate((long Start, long End)? r)
				{
					if (r.HasValue)
					{
						(long Start, long End) w = r.GetValueOrDefault();
						return footprint.Any(((long Start, long End) f) => f.Start < w.End && w.Start < f.End);
					}
					return false;
				})));
			}
		}
		return list;
	}

	public static (HashSet<string> Enabled, HashSet<string> Disabled) LoadProfile()
	{
		try
		{
			if (File.Exists(ProfilePath) && JsonNode.Parse(File.ReadAllText(ProfilePath)) is JsonObject jsonObject)
			{
				return (Enabled: Set(jsonObject["enabled"]), Disabled: Set(jsonObject["disabled"]));
			}
		}
		catch
		{
		}
		return (Enabled: new HashSet<string>(), Disabled: new HashSet<string>());
		static HashSet<string> Set(JsonNode? node)
		{
			return (from n in node?.AsArray()
				select n?.GetValue<string>() ?? "").ToHashSet() ?? new HashSet<string>();
		}
	}

	public static bool IsOn(Patch patch, (HashSet<string> Enabled, HashSet<string> Disabled) profile)
	{
		if (!profile.Enabled.Contains(patch.Key))
		{
			if (patch.DefaultOn)
			{
				return !profile.Disabled.Contains(patch.Key);
			}
			return false;
		}
		return true;
	}

	public static void Save(IEnumerable<(Patch Patch, bool On)> shown)
	{
		List<(Patch Patch, bool On)> list = shown.ToList();
		HashSet<string> hashSet = list.Select<(Patch, bool), string>(((Patch Patch, bool On) p) => p.Patch.Key).ToHashSet();
		var (hashSet2, hashSet3) = LoadProfile();
		hashSet2.RemoveWhere(hashSet.Contains);
		hashSet3.RemoveWhere(hashSet.Contains);
		foreach (var (patch, flag) in list)
		{
			if (flag != patch.DefaultOn)
			{
				(flag ? hashSet2 : hashSet3).Add(patch.Key);
			}
		}
		JsonObject jsonObject = new JsonObject
		{
			["enabled"] = new JsonArray(((IEnumerable<string>)hashSet2.OrderBy<string, string>((string k) => k, StringComparer.Ordinal)).Select((Func<string, JsonNode>)((string k) => k)).ToArray()),
			["disabled"] = new JsonArray(((IEnumerable<string>)hashSet3.OrderBy<string, string>((string k) => k, StringComparer.Ordinal)).Select((Func<string, JsonNode>)((string k) => k)).ToArray())
		};
		Directory.CreateDirectory(Paths.DataDir);
		File.WriteAllText(ProfilePath, jsonObject.ToJsonString(new JsonSerializerOptions
		{
			WriteIndented = true
		}) + "\n");
	}
}
