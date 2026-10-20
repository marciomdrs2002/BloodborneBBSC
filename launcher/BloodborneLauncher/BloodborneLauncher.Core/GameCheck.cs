using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BloodborneLauncher.Core;

public static class GameCheck
{
	public static readonly string[] SupportedTitles = new string[9] { "CUSA00900", "CUSA00207", "CUSA00208", "CUSA00299", "CUSA01363", "CUSA03179", "CUSA03173", "CUSA03014", "CUSA03023" };

	public const string SupportedVersion = "01.09";

	public static GameStatus Inspect(string gameDir)
	{
		if (string.IsNullOrWhiteSpace(gameDir))
		{
			return new GameStatus(Health.Bad, "No game folder chosen", "", "");
		}
		if (!File.Exists(Path.Combine(gameDir, "eboot.bin")))
		{
			return new GameStatus(Health.Bad, "No eboot.bin in this folder: choose the folder that contains it", "", "");
		}
		Dictionary<string, string> dictionary = ReadSfo(Path.Combine(gameDir, "sce_sys", "param.sfo"));
		string valueOrDefault = dictionary.GetValueOrDefault("TITLE_ID", "?");
		string text = dictionary.GetValueOrDefault("APP_VER", "?");
		string text2 = UpdateFolder(gameDir);
		if (text2 != null)
		{
			string valueOrDefault2 = ReadSfo(Path.Combine(text2, "sce_sys", "param.sfo")).GetValueOrDefault("APP_VER", "?");
			if (valueOrDefault2 != "?")
			{
				text = valueOrDefault2;
			}
		}
		if (valueOrDefault != "?" && !SupportedTitles.Contains(valueOrDefault))
		{
			return new GameStatus(Health.Bad, "This is not a retail Bloodborne (found " + valueOrDefault + "): any region with update 1.09 is needed", valueOrDefault, text);
		}
		if (text != "?" && text != "01.09")
		{
			return new GameStatus(Health.Warning, $"Update 1.09 is needed: put the dumped 1.09 update beside the game as {Path.GetFileName(Path.TrimEndingDirectorySeparator(gameDir))}-UPDATE, or copy it into the game folder (found {text})", valueOrDefault, text);
		}
		if (valueOrDefault == "?")
		{
			return new GameStatus(Health.Warning, "sce_sys/param.sfo is missing or unreadable; the game may not start", valueOrDefault, text);
		}
		return new GameStatus(Health.Good, (text2 != null) ? $"Bloodborne {valueOrDefault}, version {text} (update from {Path.GetFileName(text2)})" : ("Bloodborne " + valueOrDefault + ", version " + text), valueOrDefault, text);
	}

	public static string? UpdateFolder(string gameDir)
	{
		try
		{
			DirectoryInfo game = new DirectoryInfo(Path.TrimEndingDirectorySeparator(gameDir));
			return (from d in game.Parent?.EnumerateDirectories()
				where d.Name.Equals(game.Name + "-UPDATE", StringComparison.OrdinalIgnoreCase) || d.Name.Equals(game.Name + "-patch", StringComparison.OrdinalIgnoreCase)
				select d).FirstOrDefault((DirectoryInfo d) => File.Exists(Path.Combine(d.FullName, "eboot.bin")))?.FullName;
		}
		catch (Exception)
		{
			return null;
		}
	}

	public static Dictionary<string, string> ReadSfo(string path)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		try
		{
			byte[] array = File.ReadAllBytes(path);
			if (array.Length < 20 || array[0] != 0 || array[1] != 80 || array[2] != 83 || array[3] != 70)
			{
				return dictionary;
			}
			int num = BitConverter.ToInt32(array, 8);
			int num2 = BitConverter.ToInt32(array, 12);
			int num3 = BitConverter.ToInt32(array, 16);
			for (int i = 0; i < num3; i++)
			{
				int num4 = 20 + i * 16;
				ushort num5 = BitConverter.ToUInt16(array, num4);
				ushort num6 = BitConverter.ToUInt16(array, num4 + 2);
				int count = BitConverter.ToInt32(array, num4 + 4);
				int num7 = BitConverter.ToInt32(array, num4 + 12);
				int num8 = num + num5;
				int num9 = Array.IndexOf(array, (byte)0, num8);
				string key = Encoding.ASCII.GetString(array, num8, num9 - num8);
				int num10 = num2 + num7;
				dictionary[key] = ((num6 == 1028) ? BitConverter.ToUInt32(array, num10).ToString() : Encoding.UTF8.GetString(array, num10, count).TrimEnd('\0'));
			}
		}
		catch
		{
		}
		return dictionary;
	}
}
