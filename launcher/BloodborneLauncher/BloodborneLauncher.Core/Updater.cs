using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace BloodborneLauncher.Core;

public static class Updater
{
	public sealed record Release(Version Version, int Beta, string Tag, string Name, string Notes, string Page, string? ExeUrl, string? ZipUrl, string? PackageUrl, long ExeSize, long ZipSize, DateTime Published = default(DateTime))
	{
		public bool IsCurrent => Compare(Version, Beta, Current, CurrentBeta) == 0;

		public bool IsNewerThan(Version version, int beta)
		{
			return Compare(Version, Beta, version, beta) > 0;
		}
	}

	public const string Repository = "Awakenchan/BloodBorneButPc";

	public const string EngineId = "bbhost-0.4.0-fps66";

	private const string ExeName = "Bloodborne Launcher.exe";

	private const string PackageFile = "package.json";

	private static readonly string[] Kept = new string[8] { "bbport.ini", "mods.json", "patches.json", "user", "mods", "logs", "network", "engine-assets" };

	private static readonly string[] Optional = new string[1] { "bbbridge" };

	public static string ReleasesPage => "https://github.com/Awakenchan/BloodBorneButPc/releases";

	public static Version Current
	{
		get
		{
			Version version = typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0, 0);
			return new Version(version.Major, version.Minor, Math.Max(0, version.Build));
		}
	}

	public static int CurrentBeta { get; } = TryVersion(typeof(Updater).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), inherit: false).OfType<AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "", out Version _, out int beta) ? beta : 0;

	public static string CurrentLabel => Label(Current, CurrentBeta);

	public static string? OwnPackage
	{
		get
		{
			string text = Paths.DetectPortDir();
			if (text != null)
			{
				string processPath = Environment.ProcessPath;
				if (processPath != null && string.Equals(Path.GetDirectoryName(processPath)?.TrimEnd('\\'), text.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
				{
					return text;
				}
			}
			return null;
		}
	}

	public static bool EngineOutdated
	{
		get
		{
			if (OwnPackage != null)
			{
				return LocalEngine() != "bbhost-0.4.0-fps66";
			}
			return false;
		}
	}

	public static bool BetaChannel(LauncherSettings s)
	{
		string text = s.Str("update_channel");
		if (!(text == "beta"))
		{
			if (text == "stable")
			{
				return false;
			}
			return CurrentBeta > 0;
		}
		return true;
	}

	public static string Label(Version version, int beta)
	{
		if (beta > 0)
		{
			return $"v{version} beta {beta}";
		}
		return $"v{version}";
	}

	public static int Compare(Version a, int betaA, Version b, int betaB)
	{
		int num = a.CompareTo(b);
		if (num != 0)
		{
			return num;
		}
		return ((betaA == 0) ? int.MaxValue : betaA).CompareTo((betaB == 0) ? int.MaxValue : betaB);
	}

	public static string? LocalEngine()
	{
		try
		{
			string path = Path.Combine(Paths.PortDir, "package.json");
			return (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject jsonObject) ? ((string?)jsonObject["engine"]) : null;
		}
		catch
		{
			return null;
		}
	}

	public static async Task<Release?> LatestAsync(bool beta = false)
	{
		using HttpClient http = Client(TimeSpan.FromSeconds(10.0));
		using HttpResponseMessage response = await http.GetAsync(beta ? "https://api.github.com/repos/Awakenchan/BloodBorneButPc/releases?per_page=30" : "https://api.github.com/repos/Awakenchan/BloodBorneButPc/releases/latest");
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
		response.EnsureSuccessStatusCode();
		using JsonDocument jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		if (!beta)
		{
			return Parse(jsonDocument.RootElement);
		}
		Release release = null;
		foreach (JsonElement item in jsonDocument.RootElement.EnumerateArray())
		{
			if (!item.TryGetProperty("draft", out var value) || !value.GetBoolean())
			{
				Release release2 = Parse(item);
				if ((object)release2 != null && (release == null || release2.IsNewerThan(release.Version, release.Beta)))
				{
					release = release2;
				}
			}
		}
		return release;
	}

	public static async Task<IReadOnlyList<Release>> AllAsync()
	{
		using HttpClient http = Client(TimeSpan.FromSeconds(15.0));
		using HttpResponseMessage response = await http.GetAsync("https://api.github.com/repos/Awakenchan/BloodBorneButPc/releases?per_page=100");
		response.EnsureSuccessStatusCode();
		using JsonDocument jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		List<Release> list = new List<Release>();
		foreach (JsonElement item in jsonDocument.RootElement.EnumerateArray())
		{
			if (!item.TryGetProperty("draft", out var value) || !value.GetBoolean())
			{
				Release release = Parse(item);
				if ((object)release != null && (release.ZipUrl != null || release.ExeUrl != null))
				{
					list.Add(release);
				}
			}
		}
		list.Sort((Release a, Release b) => Compare(b.Version, b.Beta, a.Version, a.Beta));
		return list;
	}

	public static async Task<bool> SwitchNeedsPackageAsync(Release release)
	{
		if (OwnPackage == null || release.ZipUrl == null)
		{
			return false;
		}
		if (release.ExeUrl == null)
		{
			return true;
		}
		string text = await RemoteEngineAsync(release);
		return text == null || text != LocalEngine();
	}

	private static Release? Parse(JsonElement root)
	{
		string text = root.GetProperty("tag_name").GetString() ?? "";
		if (!TryVersion(text, out Version version, out int beta))
		{
			return null;
		}
		string exeUrl = null;
		string text2 = null;
		string packageUrl = null;
		long exeSize = 0L;
		long zipSize = 0L;
		foreach (JsonElement item in root.GetProperty("assets").EnumerateArray())
		{
			string text3 = item.GetProperty("name").GetString() ?? "";
			string text4 = item.GetProperty("browser_download_url").GetString();
			long @int = item.GetProperty("size").GetInt64();
			if (text3.Replace(".", " ").Replace(" exe", ".exe").Equals("Bloodborne Launcher.exe", StringComparison.OrdinalIgnoreCase))
			{
				exeSize = @int;
				exeUrl = text4;
			}
			else if (text3.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && text2 == null)
			{
				zipSize = @int;
				text2 = text4;
			}
			else if (text3.Equals("package.json", StringComparison.OrdinalIgnoreCase))
			{
				packageUrl = text4;
			}
		}
		Version version2 = version;
		int beta2 = beta;
		string name;
		if (root.TryGetProperty("name", out var value))
		{
			string text5 = value.GetString();
			if (text5 != null && text5.Length > 0)
			{
				name = text5;
				goto IL_016a;
			}
		}
		name = text;
		goto IL_016a;
		IL_016a:
		JsonElement value2;
		JsonElement value3;
		DateTime result;
		return new Release(version2, beta2, text, name, root.TryGetProperty("body", out value2) ? (value2.GetString() ?? "") : "", root.GetProperty("html_url").GetString() ?? ReleasesPage, exeUrl, text2, packageUrl, exeSize, zipSize, (root.TryGetProperty("published_at", out value3) && value3.ValueKind == JsonValueKind.String && DateTime.TryParse(value3.GetString(), null, DateTimeStyles.RoundtripKind, out result)) ? result.ToLocalTime() : default(DateTime));
	}

	public static bool TryVersion(string tag, out Version version, out int beta)
	{
		string text = tag.Trim().TrimStart('v', 'V');
		int num = text.IndexOf('+');
		if (num > 0)
		{
			text = text.Substring(0, num);
		}
		beta = 0;
		int num2 = text.IndexOf("-beta", StringComparison.OrdinalIgnoreCase);
		if (num2 > 0)
		{
			string text2 = text;
			int num3 = num2 + 5;
			string s = new string(text2.Substring(num3, text2.Length - num3).Where(char.IsDigit).ToArray());
			beta = ((!int.TryParse(s, out var result) || result <= 0) ? 1 : result);
		}
		int num4 = text.IndexOfAny(new char[3] { '-', '+', ' ' });
		if (num4 > 0)
		{
			text = text.Substring(0, num4);
		}
		if (Version.TryParse(text.Contains('.') ? text : (text + ".0"), out Version result2))
		{
			version = new Version(result2.Major, result2.Minor, Math.Max(0, result2.Build));
			return true;
		}
		version = new Version(0, 0, 0);
		return false;
	}

	public static async Task<string?> RemoteEngineAsync(Release release)
	{
		if (release.PackageUrl == null)
		{
			return null;
		}
		try
		{
			using HttpClient http = Client(TimeSpan.FromSeconds(10.0));
			return (JsonNode.Parse(await http.GetStringAsync(release.PackageUrl)) is JsonObject jsonObject) ? ((string?)jsonObject["engine"]) : null;
		}
		catch
		{
			return null;
		}
	}

	public static async Task<bool> NeedsPackageAsync(Release release)
	{
		if (OwnPackage == null || release.ZipUrl == null)
		{
			return false;
		}
		string text = await RemoteEngineAsync(release);
		return (text != null) ? (text != LocalEngine()) : (LocalEngine() == null && release.ExeUrl == null);
	}

	public static async Task<IReadOnlyList<string>> InstallAsync(Release release, bool package, IProgress<double>? progress = null)
	{
		string self = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot tell where this launcher is.");
		string folder = Path.GetDirectoryName(self);
		string text = Path.Combine(Path.GetTempPath(), "bbport-launcher-update");
		if (Directory.Exists(text))
		{
			foreach (string item in Directory.GetDirectories(text).Append(text))
			{
				try
				{
					if (item != text)
					{
						Directory.Delete(item, recursive: true);
					}
				}
				catch (Exception)
				{
				}
			}
			string[] files = Directory.GetFiles(text);
			foreach (string path in files)
			{
				try
				{
					File.Delete(path);
				}
				catch (Exception)
				{
				}
			}
		}
		string work = Path.Combine(text, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
		Directory.CreateDirectory(work);
		List<string> skipped = new List<string>();
		if (package)
		{
			if (Process.GetProcessesByName("bb-probe").Length != 0)
			{
				throw new InvalidOperationException("Close the game first: the update replaces the engine.");
			}
			string zip = Path.Combine(work, "package.zip");
			await DownloadAsync(release.ZipUrl, zip, release.ZipSize, progress);
			string text2 = Path.Combine(work, "unpacked");
			Extract(zip, text2, skipped);
			CopyPackage(Directory.GetFiles(text2, "Bloodborne Launcher.exe", SearchOption.AllDirectories).Select(Path.GetDirectoryName).FirstOrDefault() ?? throw new InvalidOperationException("The package has no Bloodborne Launcher.exe."), folder, self, skipped);
		}
		else
		{
			string? url = release.ExeUrl ?? throw new InvalidOperationException("This release has no launcher download attached.");
			string zip = Path.Combine(work, "Bloodborne Launcher.exe");
			await DownloadAsync(url, zip, release.ExeSize, progress);
			if (new FileInfo(zip).Length < 100000)
			{
				throw new InvalidOperationException("The download is too small to be the launcher.");
			}
			ReplaceSelf(self, zip);
		}
		Process.Start(new ProcessStartInfo(self)
		{
			UseShellExecute = true,
			WorkingDirectory = folder
		});
		return skipped;
	}

	private static bool IsOptional(string relative)
	{
		return Optional.Any((string o) => relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains<string>(o, StringComparer.OrdinalIgnoreCase));
	}

	private static void Retry(Action action)
	{
		int num = 1;
		bool flag = default(bool);
		while (true)
		{
			try
			{
				action();
				break;
			}
			catch (Exception ex) when (((Func<bool>)delegate
			{
				// Could not convert BlockContainer to single expression
				flag = num < 6;
				if (flag)
				{
					flag = ((ex is IOException || ex is UnauthorizedAccessException) ? true : false);
				}
				return flag;
			}).Invoke())
			{
				Thread.Sleep(500 * num);
			}
			num++;
		}
	}

	private static Exception Blocked(string file, Exception e)
	{
		return new InvalidOperationException(e.Message + "\n\nAn antivirus is most likely holding or removing " + Path.GetFileName(file) + ". Allow it (or the Bloodborne PC folder) in the antivirus and update again.", e);
	}

	private static void Extract(string zip, string to, List<string> skipped)
	{
		using ZipArchive zipArchive = ZipFile.OpenRead(zip);
		string value = Path.GetFullPath(to) + Path.DirectorySeparatorChar;
		foreach (ZipArchiveEntry entry in zipArchive.Entries)
		{
			string target = Path.GetFullPath(Path.Combine(to, entry.FullName));
			if (!target.StartsWith(value, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
			{
				Directory.CreateDirectory(target);
				continue;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(target));
			try
			{
				Retry(delegate
				{
					entry.ExtractToFile(target, overwrite: true);
				});
			}
			catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
			{
				if (!IsOptional(entry.FullName))
				{
					throw Blocked(target, ex);
				}
				skipped.Add(entry.FullName.Replace('\\', '/'));
			}
		}
	}

	private static void CopyPackage(string from, string to, string self, List<string> skipped)
	{
		string[] files = Directory.GetFiles(from, "*", SearchOption.AllDirectories);
		foreach (string file in files)
		{
			string relativePath = Path.GetRelativePath(from, file);
			string value = relativePath.Split(Path.DirectorySeparatorChar)[0];
			if (Kept.Contains<string>(value, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}
			string target = Path.Combine(to, relativePath);
			if (string.Equals(Path.GetFullPath(target), Path.GetFullPath(self), StringComparison.OrdinalIgnoreCase))
			{
				ReplaceSelf(self, file);
				continue;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(target));
			try
			{
				Retry(delegate
				{
					File.Copy(file, target, overwrite: true);
				});
			}
			catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
			{
				if (!IsOptional(relativePath))
				{
					throw Blocked(file, ex);
				}
				skipped.Add(relativePath.Replace('\\', '/'));
			}
		}
	}

	private static void ReplaceSelf(string self, string replacement)
	{
		string text = self + ".old";
		if (File.Exists(text))
		{
			File.Delete(text);
		}
		File.Move(self, text);
		try
		{
			File.Copy(replacement, self);
		}
		catch
		{
			File.Move(text, self);
			throw;
		}
	}

	private static async Task DownloadAsync(string url, string path, long expected, IProgress<double>? progress)
	{
		using HttpClient http = Client(TimeSpan.FromMinutes(30.0));
		using HttpResponseMessage response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
		response.EnsureSuccessStatusCode();
		long total = response.Content.Headers.ContentLength ?? expected;
		await using Stream input = await response.Content.ReadAsStreamAsync();
		await using FileStream output = File.Create(path);
		byte[] buffer = new byte[81920];
		long done = 0L;
		while (true)
		{
			int num;
			int read = (num = await input.ReadAsync(buffer));
			if (num <= 0)
			{
				break;
			}
			await output.WriteAsync(buffer.AsMemory(0, read));
			done += read;
			if (total > 0)
			{
				progress?.Report((double)done / (double)total);
			}
		}
	}

	public static void CleanUp()
	{
		try
		{
			string processPath = Environment.ProcessPath;
			if (processPath != null && File.Exists(processPath + ".old"))
			{
				File.Delete(processPath + ".old");
			}
		}
		catch
		{
		}
	}

	private static HttpClient Client(TimeSpan timeout)
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = timeout;
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"BloodborneLauncher/{Current}");
		httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
		return httpClient;
	}
}
