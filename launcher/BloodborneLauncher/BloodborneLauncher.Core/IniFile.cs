using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BloodborneLauncher.Core;

public sealed class IniFile
{
	public static readonly (string Key, string Value)[] Defaults = new(string, string)[10]
	{
		("upscaler", "fsr4"),
		("preset", "4"),
		("sharpen", "1"),
		("sharpness", "0.50"),
		("object_motion", "1"),
		("show_fps", "1"),
		("output_res", "1920x1080"),
		("model_lod", "0"),
		("camera_tuning", "original"),
		("live_resolution", "0")
	};

	private readonly Dictionary<string, string?> _values = new Dictionary<string, string>(StringComparer.Ordinal);

	private List<string> _lines = new List<string>();

	private readonly HashSet<string> _changed = new HashSet<string>(StringComparer.Ordinal);

	public IEnumerable<KeyValuePair<string, string>> Values => from kv in _values
		where kv.Value != null
		select new KeyValuePair<string, string>(kv.Key, kv.Value);

	public void Load()
	{
		_changed.Clear();
		_values.Clear();
		(string, string)[] defaults = Defaults;
		for (int i = 0; i < defaults.Length; i++)
		{
			var (key, value) = defaults[i];
			_values[key] = value;
		}
		Effect[] effects = Choices.Effects;
		foreach (Effect effect in effects)
		{
			_values[effect.Key] = (effect.Default ? "1" : "0");
		}
		_lines = new List<string>();
		try
		{
			if (File.Exists(Paths.IniPath))
			{
				_lines = File.ReadAllLines(Paths.IniPath).ToList();
			}
		}
		catch
		{
		}
		foreach (string line in _lines)
		{
			if (line.Contains('=') && !line.TrimStart().StartsWith('#'))
			{
				int num = line.IndexOf('=');
				Dictionary<string, string?> values = _values;
				string key2 = line.Substring(0, num).Trim();
				string text = line;
				int i = num + 1;
				values[key2] = text.Substring(i, text.Length - i).Trim();
			}
		}
	}

	public void Save()
	{
		string value4;
		Dictionary<string, string> dictionary = _changed.ToDictionary<string, string, string>((string k) => k, (string k) => (!_values.TryGetValue(k, out value4)) ? null : value4, StringComparer.Ordinal);
		Load();
		string value;
		string key;
		foreach (KeyValuePair<string, string> item in dictionary)
		{
			item.Deconstruct(out value, out key);
			string key2 = value;
			string value2 = key;
			_values[key2] = value2;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		List<string> list = new List<string>();
		foreach (string line in _lines)
		{
			if (line.Contains('=') && !line.TrimStart().StartsWith('#'))
			{
				string text = line.Substring(0, line.IndexOf('=')).Trim();
				if (_values.TryGetValue(text, out string value3))
				{
					if (value3 != null && hashSet.Add(text))
					{
						list.Add(text + "=" + value3);
					}
					hashSet.Add(text);
					continue;
				}
			}
			list.Add(line);
		}
		if (_lines.Count == 0)
		{
			list.Add("# bbport settings (in-game menu: Insert / L3+R3)");
		}
		foreach (KeyValuePair<string, string> value5 in _values)
		{
			value5.Deconstruct(out key, out value);
			string text2 = key;
			string text3 = value;
			if (!hashSet.Contains(text2) && text3 != null)
			{
				list.Add(text2 + "=" + text3);
			}
		}
		Directory.CreateDirectory(Path.GetDirectoryName(Paths.IniPath));
		File.WriteAllText(Paths.IniPath, string.Join("\n", list) + "\n");
		_lines = list;
	}

	public string? Raw(string key)
	{
		if (!_values.TryGetValue(key, out string value))
		{
			return null;
		}
		return value;
	}

	public string Get(string key, string fallback = "")
	{
		return Raw(key) ?? fallback;
	}

	public bool Flag(string key)
	{
		return Get(key) == "1";
	}

	public void Set(string key, string? value)
	{
		if (!_values.TryGetValue(key, out string value2) || !(value2 == value))
		{
			_values[key] = value;
			_changed.Add(key);
		}
	}
}
