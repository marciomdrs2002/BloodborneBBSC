using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BloodborneLauncher.Core;

public static class SharedSettings
{
	public enum Part
	{
		Settings,
		Controls,
		Cheats
	}

	public sealed record Snapshot(SortedDictionary<string, string> Ini, SortedDictionary<string, JsonNode> Launcher, string From);

	public sealed record Change(string Key, string? Old, string? New, Part Part, bool Launcher = false);

	public const string CodePrefix = "BBPC1:";

	private const int MaxChars = 65536;

	private static readonly string[] IniKeys = new string[20]
	{
		"upscaler", "preset", "sharpen", "sharpness", "show_fps", "show_fps_rate", "show_fps_frame_time", "show_fps_upscaler", "live_resolution", "model_lod",
		"object_motion", "jitter", "reactive", "widescreen", "pc_controls", "camera_controls", "graphics_controls", "change_appearance", "rebirth", "skip_intro"
	};

	private static readonly string[] IniPrefixes = new string[9] { "graphics_", "camera_", "effect_", "reactive_", "fsr4_", "cheat_", "bind.", "mouse_", "pad_" };

	private static readonly string[] LauncherKeys = new string[5] { "present_mode", "fps_mode", "draw_pipe", "readbacks", "fsr4_balanced" };

	private static readonly string[] LauncherPrefixes = new string[2] { "exp_", "cheat_" };

	public static Part PartOf(string key)
	{
		if (!key.StartsWith("cheat_", StringComparison.Ordinal))
		{
			if (!key.StartsWith("bind.", StringComparison.Ordinal) && !key.StartsWith("mouse_", StringComparison.Ordinal) && !key.StartsWith("pad_", StringComparison.Ordinal) && !(key == "pc_controls"))
			{
				return Part.Settings;
			}
			return Part.Controls;
		}
		return Part.Cheats;
	}

	private static bool Shared(string key, string[] keys, string[] prefixes)
	{
		if (!keys.Contains(key))
		{
			return prefixes.Any((string p) => key.StartsWith(p, StringComparison.Ordinal));
		}
		return true;
	}

	public static Snapshot Take(IniFile ini, LauncherSettings s)
	{
		SortedDictionary<string, string> sortedDictionary = new SortedDictionary<string, string>(StringComparer.Ordinal);
		string key2;
		foreach (KeyValuePair<string, string> value3 in ini.Values)
		{
			value3.Deconstruct(out var key, out key2);
			string key3 = key;
			string value = key2;
			if (Shared(key3, IniKeys, IniPrefixes))
			{
				sortedDictionary[key3] = value;
			}
		}
		SortedDictionary<string, JsonNode> sortedDictionary2 = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
		foreach (KeyValuePair<string, JsonNode> item in s.All)
		{
			item.Deconstruct(out key2, out var value2);
			string key4 = key2;
			JsonNode jsonNode = value2;
			if (jsonNode is JsonValue && Shared(key4, LauncherKeys, LauncherPrefixes))
			{
				sortedDictionary2[key4] = jsonNode.DeepClone();
			}
		}
		return new Snapshot(sortedDictionary, sortedDictionary2, Updater.CurrentLabel);
	}

	public static string ToJson(Snapshot snapshot, bool indented = true)
	{
		JsonObject jsonObject = new JsonObject();
		string key2;
		foreach (KeyValuePair<string, string> item in snapshot.Ini)
		{
			item.Deconstruct(out var key, out key2);
			string propertyName = key;
			string text = key2;
			jsonObject[propertyName] = text;
		}
		JsonObject jsonObject2 = new JsonObject();
		foreach (KeyValuePair<string, JsonNode> item2 in snapshot.Launcher)
		{
			item2.Deconstruct(out key2, out var value);
			string propertyName2 = key2;
			JsonNode jsonNode = value;
			jsonObject2[propertyName2] = jsonNode.DeepClone();
		}
		return new JsonObject
		{
			["bbpc_settings"] = 1,
			["from"] = snapshot.From,
			["ini"] = jsonObject,
			["launcher"] = jsonObject2
		}.ToJsonString(new JsonSerializerOptions
		{
			WriteIndented = indented
		});
	}

	public static string ToCode(Snapshot snapshot)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using (DeflateStream deflateStream = new DeflateStream(memoryStream, CompressionLevel.SmallestSize))
		{
			deflateStream.Write(Encoding.UTF8.GetBytes(ToJson(snapshot, indented: false)));
		}
		return "BBPC1:" + Convert.ToBase64String(memoryStream.ToArray()).TrimEnd('=').Replace('+', '-')
			.Replace('/', '_');
	}

	public static Snapshot Parse(string text)
	{
		if (text.Length > 131072)
		{
			throw new FormatException("That is far too long to be shared settings.");
		}
		Match match = Regex.Match(text, Regex.Escape("BBPC1:") + "\\s*([A-Za-z0-9_\\-]+)", RegexOptions.IgnoreCase);
		string text2 = (match.Success ? Unpack(match.Groups[1].Value) : text.Trim());
		if (!text2.StartsWith('{'))
		{
			throw new FormatException("That is not a settings code. Codes start with BBPC1:");
		}
		JsonObject jsonObject;
		try
		{
			jsonObject = (JsonNode.Parse(text2) as JsonObject) ?? throw new FormatException("");
		}
		catch (Exception ex) when (((ex is JsonException || ex is FormatException) ? 1 : 0) != 0)
		{
			throw new FormatException("The settings are damaged (copy the whole code).");
		}
		if (!(jsonObject["bbpc_settings"] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value) || value != 1)
		{
			throw new FormatException("These are not Bloodborne PC settings, or they come from a newer launcher.");
		}
		SortedDictionary<string, string> sortedDictionary = new SortedDictionary<string, string>(StringComparer.Ordinal);
		string key;
		JsonNode value2;
		if (jsonObject["ini"] is JsonObject jsonObject2)
		{
			foreach (KeyValuePair<string, JsonNode> item in jsonObject2)
			{
				item.Deconstruct(out key, out value2);
				string key2 = key;
				JsonNode jsonNode = value2;
				if (ValidKey(key2) && Shared(key2, IniKeys, IniPrefixes) && jsonNode is JsonValue jsonValue2 && jsonValue2.TryGetValue<string>(out string value3) && value3.Length <= 256 && !value3.Any(char.IsControl))
				{
					sortedDictionary[key2] = value3.Trim();
				}
			}
		}
		SortedDictionary<string, JsonNode> sortedDictionary2 = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
		if (jsonObject["launcher"] is JsonObject jsonObject3)
		{
			foreach (KeyValuePair<string, JsonNode> item2 in jsonObject3)
			{
				item2.Deconstruct(out key, out value2);
				string key3 = key;
				JsonNode jsonNode2 = value2;
				if (ValidKey(key3) && Shared(key3, LauncherKeys, LauncherPrefixes) && jsonNode2 is JsonValue jsonValue3 && (jsonValue3.TryGetValue<bool>(out var _) || jsonValue3.TryGetValue<double>(out var _) || (jsonValue3.TryGetValue<string>(out string value6) && value6.Length <= 256 && !value6.Any(char.IsControl))))
				{
					sortedDictionary2[key3] = jsonValue3.DeepClone();
				}
			}
		}
		if (sortedDictionary.Count == 0 && sortedDictionary2.Count == 0)
		{
			throw new FormatException("These settings are empty.");
		}
		string value7;
		string text3 = ((jsonObject["from"] is JsonValue jsonValue4 && jsonValue4.TryGetValue<string>(out value7) && value7.Length <= 40) ? value7 : "another launcher");
		return new Snapshot(sortedDictionary, sortedDictionary2, text3);
	}

	private static bool ValidKey(string key)
	{
		int length = key.Length;
		if (length > 0 && length <= 64)
		{
			return key.All(delegate(char c)
			{
				switch (c)
				{
				case '.':
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
				case '_':
				case 'a':
				case 'b':
				case 'c':
				case 'd':
				case 'e':
				case 'f':
				case 'g':
				case 'h':
				case 'i':
				case 'j':
				case 'k':
				case 'l':
				case 'm':
				case 'n':
				case 'o':
				case 'p':
				case 'q':
				case 'r':
				case 's':
				case 't':
				case 'u':
				case 'v':
				case 'w':
				case 'x':
				case 'y':
				case 'z':
					return true;
				default:
					return false;
				}
			});
		}
		return false;
	}

	private static string Unpack(string code)
	{
		byte[] buffer;
		try
		{
			string text = code.Replace('-', '+').Replace('_', '/');
			buffer = Convert.FromBase64String(text.PadRight(text.Length + (4 - text.Length % 4) % 4, '='));
		}
		catch (FormatException)
		{
			throw new FormatException("The code is damaged (copy the whole code).");
		}
		try
		{
			using DeflateStream deflateStream = new DeflateStream(new MemoryStream(buffer), CompressionMode.Decompress);
			byte[] array = new byte[65537];
			int i;
			int num;
			for (i = 0; i < array.Length; i += num)
			{
				if ((num = deflateStream.Read(array, i, array.Length - i)) <= 0)
				{
					break;
				}
			}
			if (i > 65536)
			{
				throw new FormatException("That code is far too large to be shared settings.");
			}
			return Encoding.UTF8.GetString(array, 0, i);
		}
		catch (InvalidDataException)
		{
			throw new FormatException("The code is damaged (copy the whole code).");
		}
	}

	public static List<Change> Changes(Snapshot shared, IniFile ini, LauncherSettings s, IReadOnlySet<Part> parts)
	{
		List<Change> list = new List<Change>();
		string value;
		string key;
		foreach (KeyValuePair<string, string> item in shared.Ini)
		{
			item.Deconstruct(out value, out key);
			string key2 = value;
			string text = key;
			if (parts.Contains(PartOf(key2)) && ini.Raw(key2) != text)
			{
				list.Add(new Change(key2, ini.Raw(key2), text, PartOf(key2)));
			}
		}
		if (parts.Contains(Part.Controls))
		{
			foreach (KeyValuePair<string, string> value3 in ini.Values)
			{
				value3.Deconstruct(out key, out value);
				string text2 = key;
				string old = value;
				if (text2.StartsWith("bind.", StringComparison.Ordinal) && !shared.Ini.ContainsKey(text2))
				{
					list.Add(new Change(text2, old, null, Part.Controls));
				}
			}
		}
		Dictionary<string, JsonNode> dictionary = s.All.ToDictionary<KeyValuePair<string, JsonNode>, string, JsonNode>((KeyValuePair<string, JsonNode> kv) => kv.Key, (KeyValuePair<string, JsonNode> kv) => kv.Value);
		foreach (KeyValuePair<string, JsonNode> item2 in shared.Launcher)
		{
			item2.Deconstruct(out value, out var value2);
			string key3 = value;
			JsonNode jsonNode = value2;
			JsonNode valueOrDefault = dictionary.GetValueOrDefault(key3);
			if (parts.Contains(PartOf(key3)) && !JsonNode.DeepEquals(valueOrDefault, jsonNode))
			{
				list.Add(new Change(key3, Plain(valueOrDefault), Plain(jsonNode), PartOf(key3), Launcher: true));
			}
		}
		return list;
	}

	public static void Apply(Snapshot shared, IniFile ini, LauncherSettings s, IReadOnlySet<Part> parts)
	{
		foreach (Change item in Changes(shared, ini, s, parts))
		{
			if (item.Launcher)
			{
				s.SetNode(item.Key, shared.Launcher[item.Key]);
			}
			else
			{
				ini.Set(item.Key, item.New);
			}
		}
	}

	private static string? Plain(JsonNode? node)
	{
		if (!(node is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return node?.ToJsonString();
		}
		return value;
	}

	public static string Describe(Change change)
	{
		return $"{change.Key}: {change.Old ?? "default"} → {change.New ?? "default"}";
	}
}
