using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BloodborneLauncher.Core;

public static class ControlProfiles
{
	public sealed record Profile(string Name, string Author, string Description, Dictionary<string, string> Bindings, bool BuiltIn = false, string File = "", Dictionary<string, string>? Pad = null);

	public sealed record Parsed(Profile Profile, List<string> Notes);

	public const string Format = "bbport-controls";

	public const int Version = 2;

	private const int MaxBytes = 65536;

	public static readonly Profile[] BuiltIns = new Profile[3]
	{
		new Profile("PC default", "Bloodborne PC", "The engine's own layout: attack on left click, firearm and transform on the mouse too.", DefaultBindings(), BuiltIn: true),
		new Profile("EldenRingStyle", "Bloodborne PC", "Elden Ring's PC layout (the package default): left click attacks, Shift + left click strong attack, right click left hand (firearm / parry), Q or middle click lock on, R Blood Vial, E interact, arrows switch.", new Dictionary<string, string>(DefaultBindings())
		{
			["attack"] = "Mouse1",
			["strong_modifier"] = "Left Shift",
			["strong_attack"] = "Mouse4",
			["firearm"] = "X, Mouse3",
			["transform"] = "C, Mouse5",
			["lock_on"] = "Q, Mouse2",
			["roll"] = "Space",
			["blood_vial"] = "R",
			["use_item"] = "F",
			["interact"] = "E",
			["switch_item"] = "Down",
			["switch_right"] = "Right",
			["switch_left"] = "Left",
			["dpad_up"] = "Up",
			["walk"] = "Left Alt"
		}, BuiltIn: true),
		new Profile("Mouse focus", "Bloodborne PC", "Both attacks and lock on on the mouse: left click attack, right click strong attack, middle click lock on; firearm F, quick item V.", new Dictionary<string, string>(DefaultBindings())
		{
			["attack"] = "Mouse1",
			["strong_attack"] = "Mouse3",
			["lock_on"] = "Mouse2",
			["firearm"] = "F",
			["use_item"] = "V",
			["transform"] = "C"
		}, BuiltIn: true)
	};

	public static string Folder => Path.Combine(Paths.ConfigDir, "profiles");

	public static Dictionary<string, string> DefaultBindings()
	{
		return InputMap.Controls.ToDictionary((ControlDef c) => c.Name, (ControlDef c) => c.Default);
	}

	public static List<Profile> All()
	{
		List<Profile> list = new List<Profile>(BuiltIns);
		try
		{
			if (Directory.Exists(Folder))
			{
				foreach (string item in Directory.GetFiles(Folder, "*.json").OrderBy<string, string>((string f) => f, StringComparer.OrdinalIgnoreCase))
				{
					try
					{
						Parsed parsed = Parse(File.ReadAllText(item));
						list.Add(parsed.Profile with
						{
							File = item
						});
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	public static string Save(Profile profile)
	{
		Directory.CreateDirectory(Folder);
		string text = Path.Combine(Folder, SafeFileName(profile.Name) + ".json");
		File.WriteAllText(text, ToJson(profile), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		return text;
	}

	public static void Delete(Profile profile)
	{
		if (!profile.BuiltIn && File.Exists(profile.File))
		{
			File.Delete(profile.File);
		}
	}

	private static string SafeFileName(string name)
	{
		char[] invalid = Path.GetInvalidFileNameChars();
		string text = new string(name.Select((char c) => (!invalid.Contains(c)) ? c : '_').ToArray()).Trim().TrimEnd('.');
		if (text.Length != 0)
		{
			return text.Substring(0, Math.Min(text.Length, 60));
		}
		return "profile";
	}

	public static Dictionary<string, string> Current(IniFile ini)
	{
		return Bindings.Current(ini);
	}

	public static void Apply(LauncherSettings s, IniFile ini, Profile profile)
	{
		ControlDef[] controls = InputMap.Controls;
		foreach (ControlDef controlDef in controls)
		{
			string value2;
			string value = (profile.Bindings.TryGetValue(controlDef.Name, out value2) ? value2 : controlDef.Default);
			Bindings.Set(ini, controlDef, InputMap.Split(value));
		}
		if (profile.Pad != null)
		{
			PadMap.Apply(ini, profile.Pad);
		}
		s.Set("controls_profile", profile.Name);
	}

	public static string ToJson(Profile profile)
	{
		JsonObject jsonObject = new JsonObject();
		ControlDef[] controls = InputMap.Controls;
		foreach (ControlDef controlDef in controls)
		{
			jsonObject[controlDef.Name] = (profile.Bindings.TryGetValue(controlDef.Name, out string value) ? value : controlDef.Default);
		}
		JsonObject jsonObject2 = new JsonObject
		{
			["format"] = "bbport-controls",
			["version"] = 2,
			["name"] = profile.Name,
			["author"] = profile.Author,
			["description"] = profile.Description,
			["bindings"] = jsonObject
		};
		if (profile.Pad != null)
		{
			JsonObject jsonObject3 = new JsonObject();
			foreach (KeyValuePair<string, string> item in profile.Pad)
			{
				item.Deconstruct(out var key, out var value2);
				string propertyName = key;
				string text = value2;
				jsonObject3[propertyName] = text;
			}
			jsonObject2["pad"] = jsonObject3;
		}
		return jsonObject2.ToJsonString(new JsonSerializerOptions
		{
			WriteIndented = true
		}) + "\n";
	}

	public static Parsed Parse(string text)
	{
		if (Encoding.UTF8.GetByteCount(text) > 65536)
		{
			throw new FormatException("The file is too large to be a control profile.");
		}
		JsonObject jsonObject;
		try
		{
			string json = text.Trim().TrimStart('\ufeff');
			JsonDocumentOptions documentOptions = new JsonDocumentOptions
			{
				AllowTrailingCommas = true,
				CommentHandling = JsonCommentHandling.Skip
			};
			jsonObject = (JsonNode.Parse(json, null, documentOptions) as JsonObject) ?? throw new FormatException("This is not a control profile (expected a JSON object).");
		}
		catch (JsonException ex)
		{
			throw new FormatException("This is not valid JSON: " + ex.Message);
		}
		string text2 = Str(jsonObject, "format");
		if (text2 != null && text2 != "bbport-controls")
		{
			throw new FormatException("This is a \"" + text2 + "\" file, not a control profile.");
		}
		JsonObject obj = (jsonObject["bindings"] as JsonObject) ?? throw new FormatException("This file has no \"bindings\" section.");
		List<string> list = new List<string>();
		Dictionary<string, ControlDef> dictionary = InputMap.Controls.ToDictionary<ControlDef, string>((ControlDef c) => c.Name, StringComparer.OrdinalIgnoreCase);
		Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
		string key;
		JsonNode value;
		foreach (KeyValuePair<string, JsonNode> item in obj)
		{
			item.Deconstruct(out key, out value);
			string text3 = key;
			JsonNode jsonNode = value;
			if (!dictionary.TryGetValue(text3, out var value2))
			{
				list.Add("Skipped unknown input \"" + Clip(text3, 30) + "\".");
				continue;
			}
			string value3;
			string text4 = ((jsonNode is JsonValue jsonValue && jsonValue.TryGetValue<string>(out value3)) ? value3 : ((jsonNode is JsonArray source) ? string.Join(", ", source.Select((JsonNode n) => n?.ToString() ?? "")) : null));
			if (text4 == null)
			{
				list.Add("Skipped " + value2.Label + ": its binding is not text.");
				continue;
			}
			List<string> list2 = new List<string>();
			foreach (string item2 in InputMap.Split(text4))
			{
				string text5 = InputMap.Canonical(item2);
				if (text5 == null)
				{
					list.Add(value2.Label + ": skipped unknown key \"" + Clip(item2, 30) + "\".");
				}
				else if (!list2.Contains<string>(text5, StringComparer.OrdinalIgnoreCase))
				{
					list2.Add(text5);
				}
			}
			string text6 = list2.FirstOrDefault((string n) => !InputMap.IsMouse(n));
			string text7 = list2.FirstOrDefault(InputMap.IsMouse);
			if (list2.Count > ((text6 != null) ? 1 : 0) + ((text7 != null) ? 1 : 0))
			{
				list.Add(value2.Label + ": kept one key and one mouse input.");
			}
			list2 = (from n in new string[2] { text6, text7 }
				where n != null
				select (n)).ToList();
			dictionary2[value2.Name] = InputMap.Join(list2);
		}
		if (dictionary2.Count == 0)
		{
			throw new FormatException("This profile binds nothing the launcher knows.");
		}
		Dictionary<string, string> dictionary3 = null;
		if (jsonObject["pad"] is JsonObject jsonObject2)
		{
			dictionary3 = new Dictionary<string, string>();
			HashSet<string> hashSet = PadMap.Inputs.ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> buttons = (from b in PadMap.Buttons
				where b.Sdl.Length > 0
				select b.Sdl).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (KeyValuePair<string, JsonNode> item3 in jsonObject2)
			{
				item3.Deconstruct(out key, out value);
				string text8 = key;
				JsonNode jsonNode2 = value;
				if (!hashSet.Contains(text8))
				{
					list.Add("Controller: skipped unknown input \"" + Clip(text8, 30) + "\".");
					continue;
				}
				string value4;
				List<string> values = (from n in ((jsonNode2 is JsonValue jsonValue2 && jsonValue2.TryGetValue<string>(out value4)) ? value4 : "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
					where buttons.Contains(n)
					select n.ToLowerInvariant()).Distinct().Take(4).ToList();
				dictionary3[text8.ToLowerInvariant()] = string.Join(", ", values);
			}
		}
		string text9 = Clip(Str(jsonObject, "name") ?? "", 60).Trim();
		return new Parsed(new Profile((text9.Length > 0) ? text9 : "Imported profile", Clip(Str(jsonObject, "author") ?? "", 60).Trim(), Clip(Str(jsonObject, "description") ?? "", 400).Trim(), dictionary2, BuiltIn: false, "", dictionary3), list);
	}

	private static string? Str(JsonObject root, string key)
	{
		if (!(root[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return null;
		}
		return value;
	}

	private static string Clip(string text, int n)
	{
		string text2 = new string(text.Where((char c) => !char.IsControl(c)).ToArray());
		if (text2.Length > n)
		{
			return text2.Substring(0, n) + "…";
		}
		return text2;
	}

	public static Uri RawUrl(string link)
	{
		link = link.Trim();
		if (!link.Contains("://"))
		{
			link = "https://" + link;
		}
		if (!Uri.TryCreate(link, UriKind.Absolute, out Uri result) || (result.Scheme != "https" && result.Scheme != "http"))
		{
			throw new FormatException("That is not a web link.");
		}
		string text = result.Host.ToLowerInvariant();
		string text2 = result.AbsolutePath.TrimEnd('/');
		string text3 = null;
		Match match;
		if (text == "pastebin.com" && (match = Regex.Match(text2, "^/(?:raw/)?([A-Za-z0-9]+)$")).Success)
		{
			text3 = "https://pastebin.com/raw/" + match.Groups[1].Value;
		}
		else if (text == "gist.github.com" && !text2.Contains("/raw"))
		{
			text3 = "https://gist.github.com" + text2 + "/raw";
		}
		else if (text == "github.com" && (match = Regex.Match(text2, "^/([^/]+)/([^/]+)/blob/(.+)$")).Success)
		{
			text3 = $"https://raw.githubusercontent.com/{match.Groups[1].Value}/{match.Groups[2].Value}/{match.Groups[3].Value}";
		}
		else
		{
			bool flag = ((text == "rentry.co" || text == "rentry.org") ? true : false);
			if (flag && !text2.EndsWith("/raw"))
			{
				text3 = "https://" + text + text2 + "/raw";
			}
			else
			{
				switch (text)
				{
				case "hastebin.com":
				case "hastebin.skyra.pw":
				case "toptal.com":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (flag && (match = Regex.Match(text2, "/(?:raw/)?([A-Za-z0-9]+)(?:\\.\\w+)?$")).Success)
				{
					text3 = ((text == "toptal.com") ? ("https://www.toptal.com/developers/hastebin/raw/" + match.Groups[1].Value) : ("https://" + text + "/raw/" + match.Groups[1].Value));
				}
				else if (text == "pastes.dev" && (match = Regex.Match(text2, "^/([A-Za-z0-9]+)$")).Success)
				{
					text3 = "https://api.pastes.dev/" + match.Groups[1].Value;
				}
				else if (text == "dpaste.org" && !text2.EndsWith("/raw"))
				{
					text3 = "https://dpaste.org" + text2 + "/raw";
				}
				else if (text == "dpaste.com" && !text2.EndsWith(".txt"))
				{
					text3 = "https://dpaste.com" + text2 + ".txt";
				}
			}
		}
		if (text3 == null)
		{
			return result;
		}
		return new Uri(text3);
	}

	public static async Task<Parsed> DownloadAsync(string link)
	{
		Uri requestUri = RawUrl(link);
		using HttpClient http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(12.0)
		};
		http.DefaultRequestHeaders.UserAgent.ParseAdd("BloodborneLauncher/1.0");
		using HttpResponseMessage response = await http.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead);
		if (!response.IsSuccessStatusCode)
		{
			throw new FormatException($"The link answered {response.StatusCode} {response.ReasonPhrase}.");
		}
		if (response.Content.Headers.ContentLength > 65536)
		{
			throw new FormatException("That link is too large to be a control profile.");
		}
		Parsed result;
		await using (Stream stream = await response.Content.ReadAsStreamAsync())
		{
			byte[] buffer = new byte[65537];
			int read = 0;
			int n = default(int);
			while (true)
			{
				bool flag = read < buffer.Length;
				if (flag)
				{
					int num;
					n = (num = await stream.ReadAsync(buffer.AsMemory(read)));
					flag = num > 0;
				}
				if (!flag)
				{
					break;
				}
				read += n;
			}
			if (read > 65536)
			{
				throw new FormatException("That link is too large to be a control profile.");
			}
			string text = Encoding.UTF8.GetString(buffer, 0, read);
			if (text.TrimStart().StartsWith('<'))
			{
				throw new FormatException("That link shows a web page, not the raw profile. Use the paste's \"raw\" link.");
			}
			result = Parse(text);
		}
		return result;
	}
}
