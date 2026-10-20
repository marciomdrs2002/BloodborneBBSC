using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BloodborneLauncher.Core;

public static class SaveBackups
{
	public sealed record Backup(string Dir, DateTime When, long Bytes, bool MadeBeforeRestore);

	public const int Keep = 10;

	private const string Stamp = "yyyy-MM-dd_HH-mm-ss";

	private const string BeforeRestore = "_before-restore";

	private const string FingerprintFile = "fingerprint.txt";

	public static string SaveData(LauncherSettings s)
	{
		return Path.Combine(GameEnvironment.UserDir(s), "savedata");
	}

	public static string Folder(LauncherSettings s)
	{
		return Path.Combine(GameEnvironment.SavesRoot(s), "launcher-backups", PlayerProfiles.Current(s));
	}

	public static List<Backup> List(LauncherSettings s)
	{
		string path = Folder(s);
		if (!Directory.Exists(path))
		{
			return new List<Backup>();
		}
		List<Backup> list = new List<Backup>();
		string[] directories = Directory.GetDirectories(path);
		foreach (string text in directories)
		{
			string text2 = Path.GetFileName(text);
			bool flag = text2.EndsWith("_before-restore", StringComparison.Ordinal);
			if (flag)
			{
				string text3 = text2;
				int length = "_before-restore".Length;
				text2 = text3.Substring(0, text3.Length - length);
			}
			if (DateTime.TryParseExact(text2, "yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
			{
				list.Add(new Backup(text, result, Size(text), flag));
			}
		}
		return list.OrderByDescending((Backup b) => b.When).ToList();
	}

	public static string? Create(LauncherSettings s, bool beforeRestore = false)
	{
		string text = SaveData(s);
		if (!Directory.Exists(text) || !Directory.EnumerateFiles(text, "*", SearchOption.AllDirectories).Any())
		{
			return null;
		}
		string text2 = Fingerprint(text);
		if (!beforeRestore)
		{
			Backup backup = List(s).FirstOrDefault();
			if ((object)backup != null && ReadFingerprint(backup.Dir) == text2)
			{
				return null;
			}
		}
		string text3 = Path.Combine(Folder(s), DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + (beforeRestore ? "_before-restore" : ""));
		if (Directory.Exists(text3))
		{
			return null;
		}
		Copy(text, Path.Combine(text3, "savedata"));
		File.WriteAllText(Path.Combine(text3, "fingerprint.txt"), text2);
		foreach (Backup item in List(s).Skip(10))
		{
			try
			{
				Directory.Delete(item.Dir, recursive: true);
			}
			catch
			{
			}
		}
		return text3;
	}

	public static void Restore(LauncherSettings s, Backup backup)
	{
		string text = Path.Combine(backup.Dir, "savedata");
		if (!Directory.Exists(text))
		{
			throw new DirectoryNotFoundException("This backup has no savedata folder.");
		}
		Create(s, beforeRestore: true);
		string text2 = SaveData(s);
		string text3 = text2 + ".restoring";
		if (Directory.Exists(text3))
		{
			Directory.Delete(text3, recursive: true);
		}
		Copy(text, text3);
		if (Directory.Exists(text2))
		{
			Directory.Delete(text2, recursive: true);
		}
		Directory.Move(text3, text2);
	}

	private static bool Staging(string from, string path)
	{
		return Path.GetRelativePath(from, path).Split(Path.DirectorySeparatorChar).Any((string part) => part.EndsWith(".importing", StringComparison.OrdinalIgnoreCase));
	}

	private static void Copy(string from, string to)
	{
		Directory.CreateDirectory(to);
		foreach (string item in from d in Directory.GetDirectories(@from, "*", SearchOption.AllDirectories)
			where !Staging(@from, d)
			select d)
		{
			Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, item)));
		}
		foreach (string item2 in from f in Directory.GetFiles(@from, "*", SearchOption.AllDirectories)
			where !Staging(@from, f)
			select f)
		{
			File.Copy(item2, Path.Combine(to, Path.GetRelativePath(from, item2)), overwrite: true);
		}
	}

	private static string Fingerprint(string dir)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (string item in (from f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
			where !Staging(dir, f)
			select f).OrderBy<string, string>((string f) => f, StringComparer.Ordinal))
		{
			FileInfo fileInfo = new FileInfo(item);
			stringBuilder.Append(Path.GetRelativePath(dir, item)).Append('|').Append(fileInfo.Length)
				.Append('|')
				.Append(fileInfo.LastWriteTimeUtc.Ticks)
				.Append('\n');
		}
		return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(stringBuilder.ToString())));
	}

	private static string? ReadFingerprint(string backup)
	{
		try
		{
			return File.ReadAllText(Path.Combine(backup, "fingerprint.txt")).Trim();
		}
		catch
		{
			return null;
		}
	}

	private static long Size(string dir)
	{
		try
		{
			return Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Sum((string f) => new FileInfo(f).Length);
		}
		catch
		{
			return 0L;
		}
	}
}
