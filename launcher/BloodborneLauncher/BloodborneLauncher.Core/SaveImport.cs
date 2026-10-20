using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace BloodborneLauncher.Core;

public static class SaveImport
{
	public sealed record Source(string Dir, DateTime Saved, string From);

	public const string SaveDir = "SPRJ0005";

	public const string SlotFile = "userdata0010";

	public static string GameId(LauncherSettings s)
	{
		Dictionary<string, string> dictionary = GameCheck.ReadSfo(Path.Combine(GameEnvironment.GameDir(s), "sce_sys", "param.sfo"));
		if (dictionary.TryGetValue("INSTALL_DIR_SAVEDATA", out var value) && value.Length > 0)
		{
			return value;
		}
		if (dictionary.TryGetValue("TITLE_ID", out value) && value.Length > 0)
		{
			return value;
		}
		string user = Path.Combine(SaveBackups.SaveData(s), "1");
		if (!Directory.Exists(user))
		{
			return "CUSA00207";
		}
		return Directory.GetDirectories(user).Select(Path.GetFileName).FirstOrDefault((string d) => Directory.Exists(Path.Combine(user, d, "SPRJ0005"))) ?? "CUSA00207";
	}

	public static string Target(LauncherSettings s)
	{
		return Path.Combine(SaveBackups.SaveData(s), "1", GameId(s), "SPRJ0005");
	}

	public static bool HasSaves(string dir)
	{
		if (Directory.Exists(dir))
		{
			return Directory.EnumerateFiles(dir, "userdata*").Any();
		}
		return false;
	}

	public static bool IsComplete(string dir)
	{
		return File.Exists(Path.Combine(dir, "userdata0010"));
	}

	public static bool IsSaveFile(string path)
	{
		string fileName = Path.GetFileName(path);
		if (!Regex.IsMatch(fileName, "^(userdata|backup)\\d{4}$", RegexOptions.IgnoreCase) && !fileName.Equals("icon0.png", StringComparison.OrdinalIgnoreCase) && !fileName.Equals("param.bin", StringComparison.OrdinalIgnoreCase))
		{
			return fileName.Equals("sce_param.bin", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static string FolderOfFile(string file)
	{
		if (!file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
		{
			return Path.GetDirectoryName(file);
		}
		string text = Path.Combine(Path.GetTempPath(), "bbport-save-import-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
		ZipFile.ExtractToDirectory(file, text);
		return text;
	}

	public static List<Source> Known(LauncherSettings s)
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		(string, string)[] source = new(string, string)[2]
		{
			(Path.Combine(folderPath, "shadPS4"), "shadPS4"),
			(Path.Combine(folderPath2, "shadPS4"), "shadPS4")
		};
		string ours = Path.GetFullPath(Target(s));
		return (from f in source.Where(((string, string) r) => Directory.Exists(r.Item1)).SelectMany(((string, string) r) => Find(r.Item1, r.Item2, 5))
			where !string.Equals(Path.GetFullPath(f.Dir), ours, StringComparison.OrdinalIgnoreCase)
			orderby f.Saved descending
			select f).ToList();
	}

	public static List<Source> Find(string root, string from, int depth = 7)
	{
		List<Source> found = new List<Source>();
		Walk(root, depth);
		return found.OrderByDescending((Source f) => f.Saved).ToList();
		void Walk(string dir, int left)
		{
			if ((Path.GetFileName(dir).EndsWith("SPRJ0005", StringComparison.OrdinalIgnoreCase) || File.Exists(Path.Combine(dir, "userdata0010"))) && HasSaves(dir))
			{
				found.Add(new Source(dir, ((IEnumerable<string>)Directory.GetFiles(dir, "userdata*")).Max((Func<string, DateTime>)File.GetLastWriteTime), from));
			}
			else if (left != 0)
			{
				string[] directories;
				try
				{
					directories = Directory.GetDirectories(dir);
				}
				catch
				{
					return;
				}
				string[] array = directories;
				foreach (string text in array)
				{
					string fileName = Path.GetFileName(text);
					if (!fileName.Equals("dvdroot_ps4", StringComparison.OrdinalIgnoreCase) && !fileName.Equals("launcher-backups", StringComparison.OrdinalIgnoreCase) && !fileName.Equals("save-backups", StringComparison.OrdinalIgnoreCase))
					{
						Walk(text, left - 1);
					}
				}
			}
		}
	}

	public static bool HasEncryptedPs4Save(string root, int depth = 7)
	{
		return Walk(root, depth);
		static bool Walk(string dir, int left)
		{
			try
			{
				if (Directory.EnumerateFiles(dir, "sdimg_*").Any())
				{
					return true;
				}
				return left > 0 && Directory.GetDirectories(dir).Any((string child) => Walk(child, left - 1));
			}
			catch
			{
				return false;
			}
		}
	}

	public static void Import(LauncherSettings s, string from)
	{
		if (!HasSaves(from))
		{
			throw new IOException("That folder has no Bloodborne save files (userdata).");
		}
		if (!IsComplete(from))
		{
			throw new IOException("That save is incomplete: it has no userdata0010, the file the game needs to find the save slots (importing it would make the game crash). Import the whole SPRJ0005 folder.");
		}
		SaveBackups.Create(s, beforeRestore: true);
		string text = Target(s);
		string text2 = text + ".importing";
		if (Directory.Exists(text2))
		{
			Directory.Delete(text2, recursive: true);
		}
		Directory.CreateDirectory(text2);
		try
		{
			foreach (string item in Directory.GetFiles(from).Where(IsSaveFile))
			{
				File.Copy(item, Path.Combine(text2, Path.GetFileName(item)));
			}
			if (Directory.Exists(text))
			{
				Directory.Delete(text, recursive: true);
			}
			Directory.Move(text2, text);
		}
		finally
		{
			try
			{
				if (Directory.Exists(text2) && Directory.Exists(text))
				{
					Directory.Delete(text2, recursive: true);
				}
			}
			catch (Exception)
			{
			}
		}
	}
}
