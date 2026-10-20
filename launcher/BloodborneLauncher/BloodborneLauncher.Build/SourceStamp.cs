using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BloodborneLauncher.Build;

public static class SourceStamp
{
	public sealed class Stamp
	{
		public string Launcher { get; set; } = "";

		public string Builder { get; set; } = "";

		public string Source { get; set; } = "";

		public DateTime Built { get; set; }
	}

	public const string LauncherProject = "BloodborneLauncher";

	public const string BuilderProject = "LauncherBuilder";

	public const string SharedFolder = "Shared";

	public const string AppFolder = "LauncherApp";

	public const string StampName = "build-stamp.json";

	public const string LauncherExe = "Bloodborne Launcher.exe";

	public const string BuilderExe = "Build Launcher.exe";

	private static readonly HashSet<string> SkippedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".vs", ".idea" };

	private static readonly HashSet<string> SkippedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".user", ".suo", ".swp", ".tmp" };

	public static string? FindSourceRoot(string start)
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(start); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string text = Path.Combine(directoryInfo.FullName, "launcher", "windows");
			if (File.Exists(Path.Combine(text, "BloodborneLauncher", "BloodborneLauncher.csproj")))
			{
				return text;
			}
		}
		return null;
	}

	public static string RepoOf(string sourceRoot)
	{
		return Directory.GetParent(Directory.GetParent(sourceRoot).FullName).FullName;
	}

	public static string AppDirOf(string sourceRoot)
	{
		string path = Path.Combine(sourceRoot, "install.txt");
		if (File.Exists(path))
		{
			string text = File.ReadAllText(path).Trim();
			if (text != null && text.Length > 0)
			{
				return Environment.ExpandEnvironmentVariables(text);
			}
		}
		return Path.Combine(RepoOf(sourceRoot), "LauncherApp");
	}

	public static string? LocateSources(string start)
	{
		string text = FindSourceRoot(start);
		if (text != null)
		{
			return text;
		}
		string source = Read(start).Source;
		if (source.Length <= 0 || !File.Exists(Path.Combine(source, "BloodborneLauncher", "BloodborneLauncher.csproj")))
		{
			return null;
		}
		return source;
	}

	public static string Compute(string sourceRoot, string project)
	{
		using SHA256 sHA = SHA256.Create();
		string[] array = new string[2] { project, "Shared" };
		foreach (string text in array)
		{
			string text2 = Path.Combine(sourceRoot, text);
			if (!Directory.Exists(text2))
			{
				continue;
			}
			foreach (string item in Files(text2).OrderBy<string, string>((string f) => f, StringComparer.Ordinal))
			{
				string text3 = text + "/" + Path.GetRelativePath(text2, item).Replace('\\', '/');
				byte[] bytes = Encoding.UTF8.GetBytes(text3 + "\n");
				sHA.TransformBlock(bytes, 0, bytes.Length, null, 0);
				byte[] array2 = Normalise(File.ReadAllBytes(item));
				sHA.TransformBlock(array2, 0, array2.Length, null, 0);
			}
		}
		sHA.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
		return Convert.ToHexString(sHA.Hash).ToLowerInvariant();
	}

	private static IEnumerable<string> Files(string root)
	{
		foreach (string item in Directory.EnumerateFiles(root))
		{
			if (!SkippedExtensions.Contains(Path.GetExtension(item)))
			{
				yield return item;
			}
		}
		foreach (string item2 in Directory.EnumerateDirectories(root))
		{
			if (SkippedFolders.Contains(Path.GetFileName(item2)))
			{
				continue;
			}
			foreach (string item3 in Files(item2))
			{
				yield return item3;
			}
		}
	}

	private static byte[] Normalise(byte[] bytes)
	{
		List<byte> list = new List<byte>(bytes.Length);
		for (int i = 0; i < bytes.Length; i++)
		{
			if (bytes[i] != 13 || i + 1 >= bytes.Length || bytes[i + 1] != 10)
			{
				list.Add(bytes[i]);
			}
		}
		return list.ToArray();
	}

	public static Stamp Read(string appDir)
	{
		try
		{
			string path = Path.Combine(appDir, "build-stamp.json");
			return File.Exists(path) ? (JsonSerializer.Deserialize<Stamp>(File.ReadAllText(path)) ?? new Stamp()) : new Stamp();
		}
		catch
		{
			return new Stamp();
		}
	}

	public static void Write(string appDir, Stamp stamp)
	{
		File.WriteAllText(Path.Combine(appDir, "build-stamp.json"), JsonSerializer.Serialize(stamp, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
	}
}
