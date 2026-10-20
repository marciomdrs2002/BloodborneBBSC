using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BloodborneLauncher.Core;

public static class SteamShortcut
{
	private static class Vdf
	{
		public sealed class Node
		{
			public List<KeyValuePair<string, object>> Children { get; } = new List<KeyValuePair<string, object>>();

			public object? Get(string key)
			{
				return Children.FirstOrDefault<KeyValuePair<string, object>>((KeyValuePair<string, object> c) => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
			}

			public void Set(string key, object value)
			{
				int num = Children.FindIndex((KeyValuePair<string, object> c) => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
				if (num >= 0)
				{
					Children[num] = new KeyValuePair<string, object>(Children[num].Key, value);
				}
				else
				{
					Children.Add(new KeyValuePair<string, object>(key, value));
				}
			}

			public Node Child(string key)
			{
				if (Get(key) is Node result)
				{
					return result;
				}
				Node node = new Node();
				Set(key, node);
				return node;
			}
		}

		public static Node Read(byte[] data)
		{
			int at = 0;
			return ReadNode(data, ref at);
		}

		private static Node ReadNode(byte[] d, ref int at)
		{
			Node node = new Node();
			while (at < d.Length)
			{
				byte b = d[at++];
				if (b == 8)
				{
					break;
				}
				string key = ReadString(d, ref at);
				switch (b)
				{
				case 0:
					node.Children.Add(new KeyValuePair<string, object>(key, ReadNode(d, ref at)));
					break;
				case 1:
					node.Children.Add(new KeyValuePair<string, object>(key, ReadString(d, ref at)));
					break;
				case 2:
					node.Children.Add(new KeyValuePair<string, object>(key, BitConverter.ToInt32(d, at)));
					at += 4;
					break;
				case 7:
					node.Children.Add(new KeyValuePair<string, object>(key, BitConverter.ToUInt64(d, at)));
					at += 8;
					break;
				default:
					throw new InvalidDataException($"shortcuts.vdf: unknown value type {b}");
				}
			}
			return node;
		}

		private static string ReadString(byte[] d, ref int at)
		{
			int num = Array.IndexOf(d, (byte)0, at);
			if (num < 0)
			{
				throw new InvalidDataException("shortcuts.vdf: unterminated string");
			}
			string result = Encoding.UTF8.GetString(d, at, num - at);
			at = num + 1;
			return result;
		}

		public static byte[] Write(Node root)
		{
			using MemoryStream memoryStream = new MemoryStream();
			WriteNode(memoryStream, root);
			memoryStream.WriteByte(8);
			return memoryStream.ToArray();
		}

		private static void WriteNode(Stream s, Node node)
		{
			foreach (var (text2, obj2) in node.Children)
			{
				if (!(obj2 is Node node2))
				{
					if (!(obj2 is string text3))
					{
						if (!(obj2 is int value))
						{
							if (obj2 is ulong value2)
							{
								s.WriteByte(7);
								WriteString(s, text2);
								s.Write(BitConverter.GetBytes(value2));
							}
						}
						else
						{
							s.WriteByte(2);
							WriteString(s, text2);
							s.Write(BitConverter.GetBytes(value));
						}
					}
					else
					{
						s.WriteByte(1);
						WriteString(s, text2);
						WriteString(s, text3);
					}
				}
				else
				{
					s.WriteByte(0);
					WriteString(s, text2);
					WriteNode(s, node2);
					s.WriteByte(8);
				}
			}
		}

		private static void WriteString(Stream s, string text)
		{
			s.Write(Encoding.UTF8.GetBytes(text));
			s.WriteByte(0);
		}
	}

	public const string AppName = "Bloodborne";

	public static string? SteamDir()
	{
		if (!(Registry.GetValue("HKEY_CURRENT_USER\\Software\\Valve\\Steam", "SteamPath", null) is string { Length: >0 } text) || !Directory.Exists(text))
		{
			return null;
		}
		return Path.GetFullPath(text);
	}

	public static bool SteamRunning()
	{
		return Process.GetProcessesByName("steam").Length != 0;
	}

	public static List<string> Add(string exe)
	{
		string text = SteamDir() ?? throw new InvalidOperationException("Steam is not installed for this Windows user.");
		if (SteamRunning())
		{
			throw new InvalidOperationException("Close Steam first (Steam › Exit): it rewrites its shortcut list when it closes.");
		}
		List<string> list = Accounts(text);
		if (list.Count == 0)
		{
			throw new InvalidOperationException("No Steam account has signed in on this PC yet: start Steam once and sign in.");
		}
		List<string> list2 = new List<string>();
		foreach (string item in list)
		{
			string text2 = Path.Combine(text, "userdata", item, "config");
			Directory.CreateDirectory(text2);
			string text3 = Path.Combine(text2, "shortcuts.vdf");
			Vdf.Node node = (File.Exists(text3) ? Vdf.Read(File.ReadAllBytes(text3)) : new Vdf.Node());
			Vdf.Node node2 = node.Child("shortcuts");
			string quoted = "\"" + exe + "\"";
			KeyValuePair<string, object> keyValuePair = node2.Children.FirstOrDefault<KeyValuePair<string, object>>((KeyValuePair<string, object> c) => c.Value is Vdf.Node node4 && string.Equals((node4.Get("Exe") ?? node4.Get("exe")) as string, quoted, StringComparison.OrdinalIgnoreCase));
			Vdf.Node node3 = (keyValuePair.Value as Vdf.Node) ?? new Vdf.Node();
			if (keyValuePair.Key == null)
			{
				int result;
				int num = node2.Children.Select<KeyValuePair<string, object>, int>((KeyValuePair<string, object> e) => int.TryParse(e.Key, out result) ? (result + 1) : 0).DefaultIfEmpty(0).Max();
				node2.Children.Add(new KeyValuePair<string, object>(num.ToString(), node3));
			}
			node3.Set("appid", AppId(quoted, "Bloodborne"));
			node3.Set("AppName", "Bloodborne");
			node3.Set("Exe", quoted);
			node3.Set("StartDir", "\"" + Path.GetDirectoryName(exe) + "\\\"");
			node3.Set("icon", exe);
			node3.Set("ShortcutPath", "");
			node3.Set("LaunchOptions", "");
			node3.Set("IsHidden", 0);
			node3.Set("AllowDesktopConfig", 1);
			node3.Set("AllowOverlay", 1);
			node3.Set("OpenVR", 0);
			node3.Set("Devkit", 0);
			node3.Set("DevkitGameID", "");
			node3.Set("DevkitOverrideAppID", 0);
			node3.Set("LastPlayTime", node3.Get("LastPlayTime") ?? ((object)0));
			node3.Set("FlatpakAppID", "");
			node3.Child("tags");
			if (File.Exists(text3))
			{
				File.Copy(text3, text3 + ".bak", overwrite: true);
			}
			File.WriteAllBytes(text3, Vdf.Write(node));
			list2.Add(item);
		}
		return list2;
	}

	private static List<string> Accounts(string steam)
	{
		List<string> list = (Directory.Exists(Path.Combine(steam, "userdata")) ? (from n in Directory.GetDirectories(Path.Combine(steam, "userdata")).Select(Path.GetFileName)
			where n != null && n.Length > 0 && n.All(char.IsDigit) && n != "0"
			select (n)).ToList() : new List<string>());
		try
		{
			foreach (Match item2 in Regex.Matches(File.ReadAllText(Path.Combine(steam, "config", "loginusers.vdf")), "\"(7656\\d{13})\"\\s*\\{([^}]*)\\}"))
			{
				if (item2.Groups[2].Value.Contains("\"MostRecent\"\t\t\"1\"") || Regex.IsMatch(item2.Groups[2].Value, "\"MostRecent\"\\s*\"1\""))
				{
					string item = (ulong.Parse(item2.Groups[1].Value) - 76561197960265728L).ToString();
					if (list.Contains(item))
					{
						return new List<string> { item };
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return list;
	}

	private static int AppId(string exe, string name)
	{
		uint num = uint.MaxValue;
		byte[] bytes = Encoding.UTF8.GetBytes(exe + name);
		foreach (byte b in bytes)
		{
			num ^= b;
			for (int j = 0; j < 8; j++)
			{
				num = (num >> 1) ^ (0xEDB88320u & (0 - (num & 1)));
			}
		}
		return (int)(~num) | int.MinValue;
	}
}
