using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BloodborneLauncher.Build;

namespace BloodborneLauncher.Core;

public static class Paths
{
	public static readonly string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "bbport-launcher");

	public static readonly string ConfigFile = Path.Combine(ConfigDir, "settings.json");

	public static string PortDir { get; set; } = "";

	public static string DataDir
	{
		get
		{
			string environmentVariable = Environment.GetEnvironmentVariable("BB_DATA_DIR");
			if (environmentVariable == null || environmentVariable.Length <= 0)
			{
				return PortDir;
			}
			return environmentVariable;
		}
	}

	public static string IniPath
	{
		get
		{
			string environmentVariable = Environment.GetEnvironmentVariable("BB_CONFIG");
			if (environmentVariable == null || environmentVariable.Length <= 0)
			{
				return Path.Combine(DataDir, "bbport.ini");
			}
			return environmentVariable;
		}
	}

	public static string EngineDir => EngineDirOf(PortDir);

	public static string BinariesDir => Path.Combine(EngineDir, "out");

	public static string Start => Path.Combine(EngineDir, "out", "bb-start.exe");

	public static string Python => Path.Combine(EngineDir, "tools", "python", "python.exe");

	public static string? SourceRoot => SourceStamp.LocateSources(AppContext.BaseDirectory);

	public static string EngineDirOf(string dir)
	{
		if (!File.Exists(Path.Combine(dir, "engine", "out", "bb-probe.exe")) && !File.Exists(Path.Combine(dir, "engine", "scripts", "run_windows.py")))
		{
			return dir;
		}
		return Path.Combine(dir, "engine");
	}

	public static bool IsPortDir(string? dir)
	{
		if (string.IsNullOrWhiteSpace(dir))
		{
			return false;
		}
		string path = EngineDirOf(dir);
		if (File.Exists(Path.Combine(path, "out", "bb-probe.exe")))
		{
			if (!File.Exists(Path.Combine(path, "out", "bb-start.exe")))
			{
				return File.Exists(Path.Combine(path, "scripts", "run_windows.py"));
			}
			return true;
		}
		return false;
	}

	public static string? PortBesideGame(string gameDir)
	{
		try
		{
			string text = (string.IsNullOrWhiteSpace(gameDir) ? null : Directory.GetParent(gameDir)?.FullName);
			return IsPortDir(text) ? text : null;
		}
		catch
		{
			return null;
		}
	}

	public static string? PortNearGame(string gameDir)
	{
		try
		{
			string text = (string.IsNullOrWhiteSpace(gameDir) ? null : Directory.GetParent(gameDir)?.FullName);
			if (text == null)
			{
				return null;
			}
			return (from dir in Directory.GetDirectories(text).Where(IsPortDir)
				orderby (from b in new string[2] { "bin", "out" }
					select new FileInfo(Path.Combine(EngineDirOf(dir), b, "bb-probe.exe")) into f
					where f.Exists
					select f.LastWriteTimeUtc).DefaultIfEmpty(DateTime.MinValue).Max() descending
				select dir).FirstOrDefault();
		}
		catch
		{
			return null;
		}
	}

	public static string? DetectPortDir()
	{
		string[] array = new string[2]
		{
			AppContext.BaseDirectory,
			Environment.CurrentDirectory
		};
		for (int i = 0; i < array.Length; i++)
		{
			for (DirectoryInfo directoryInfo = new DirectoryInfo(array[i]); directoryInfo != null; directoryInfo = directoryInfo.Parent)
			{
				if (IsPortDir(directoryInfo.FullName))
				{
					return directoryInfo.FullName.TrimEnd(Path.DirectorySeparatorChar);
				}
			}
		}
		return null;
	}

	public static string Expand(string path)
	{
		string name;
		if (!path.StartsWith("~"))
		{
			name = path;
		}
		else
		{
			name = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path.Substring(1, path.Length - 1);
		}
		return Environment.ExpandEnvironmentVariables(name);
	}

	public static void OpenFolder(string path)
	{
		try
		{
			Directory.CreateDirectory(path);
			Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"")
			{
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}
}
