using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BloodborneLauncher.Core;

public static class CrashReport
{
	private static readonly string[] Experimental = new string[13]
	{
		"exp_vram_cleanup", "exp_frame_pacing", "exp_vram_budget", "exp_thread_priority", "exp_sleep_spin", "exp_async_pipelines", "exp_occlusion", "exp_fast_draws", "exp_compile_skip", "exp_fast_loading",
		"exp_hot_pages", "exp_sfx_workaround", "exp_camera_easing"
	};

	private static readonly Regex FaultLine = new Regex("^(Host|Guest) fault 0x([0-9a-fA-F]{8}) at ([^,\\s]+)", RegexOptions.Compiled);

	private static readonly Regex ExitLine = new Regex("bb-probe exited with code (-?\\d+)", RegexOptions.Compiled);

	private static readonly Regex MemoryLine = new Regex("^Memory: game (\\d+) MiB in RAM \\((\\d+) MiB committed, peak (\\d+) MiB\\); PC (\\d+) of (\\d+) MiB free, (\\d+) MiB commit left", RegexOptions.Compiled);

	public static string Explain(int code)
	{
		List<string> tail = Tail(GameRunner.SessionLog, 120).ToList();
		string text = Cause(tail, EngineCode(tail, code));
		string text2 = LowMemory(tail);
		if (text2 != null)
		{
			return text + " " + text2;
		}
		return text;
	}

	private static string Cause(List<string> tail, long code)
	{
		if (Has("creating the memory pool section") || Has("mapping the memory pool"))
		{
			return "Windows could not give the game its memory at start (about 10 GB for a scene above 1080p, else 6 GB, RAM plus page file). A game left running in the background holds it: end bb-probe.exe in Task Manager (Details) or restart the PC. Otherwise set the page file to System managed size (Windows Settings › System › About › Advanced system settings › Performance Settings › Advanced › Virtual memory › Change), close other programs, or turn Live resolution On (Display) or choose a lower upscaler preset: a scene up to 1080p needs about 4 GB less.";
		}
		if (Has("gxshader.shaderbnd.dcx) -> not found") || Has("systex.tpf.dcx) -> not found"))
		{
			return "The game's own files are missing (dvdroot_ps4\\shader, dvdroot_ps4\\other): the game folder is incomplete. Turn Mods off and start again; if it still crashes, dump or extract the game again (base game plus the 1.09 update).";
		}
		if (Has("FileTransferTask.cpp"))
		{
			return "The game could not load one of its files and stopped itself (FileTransferTask). A damaged or incomplete game folder does this: dump or extract the game again (base game plus the 1.09 update), keep it out of OneDrive, and turn Mods off to rule them out. A \"Runtime: reading ... failed\" line in the log names the file.";
		}
		if (Has("Out of order flip IRQ"))
		{
			return "A frame reached the screen out of order (a timing race in the engine; Precise frame pacing makes it likelier).";
		}
		if (Has("the game's memory pool is full"))
		{
			return "The game ran out of its memory pool: a patch from Mods → Third-party patches enlarges the game's memory (a resolution patch above 1080p, for example). Turn it off and choose the resolution in Display → Output resolution instead.";
		}
		if (Has("ErrorOutOfDeviceMemory") || Has("ErrorOutOfHostMemory"))
		{
			return "The graphics card ran out of video memory. A lower output resolution or upscaler preset (Graphics), fewer texture or model mods, and closing other programs (browsers, recorders) leave it more room; keep \"Use more VRAM for textures\" off on cards with 4-6 GB.";
		}
		if (Has("DEVICE_LOST") || Has("device lost"))
		{
			return "The graphics driver stopped responding: the GPU hung or lost the game's memory.";
		}
		int num = tail.FindLastIndex((string l) => l.Contains("<Critical>", StringComparison.Ordinal));
		if (num >= 0)
		{
			string text = tail[num];
			int num2 = tail[num].IndexOf("<Critical>", StringComparison.Ordinal) + 10;
			string text2 = text.Substring(num2, text.Length - num2).Trim();
			string text3 = ((num + 1 < tail.Count) ? tail[num + 1].Trim() : "");
			if (text3.Length > 0 && !text3.StartsWith("STOP:") && !text3.Contains("exited with code"))
			{
				text2 = text2 + " (" + text3 + ")";
			}
			return "The engine stopped on an internal check: " + text2;
		}
		string text4 = tail.LastOrDefault((string l) => FaultLine.IsMatch(l));
		Match match = ((text4 != null) ? FaultLine.Match(text4) : null);
		if (match != null && match.Groups[1].Value == "Guest" && match.Groups[3].Value.StartsWith("guest+0x", StringComparison.Ordinal))
		{
			string text = match.Groups[3].Value;
			if (ulong.TryParse(text.Substring(8, text.Length - 8), NumberStyles.HexNumber, null, out var result) && result >= 42788784 && result < 42792320)
			{
				return "The game crashed drawing an effect trail (" + match.Groups[3].Value + "), a known crash on newer Intel CPUs. Turn on Graphics › Experimental › Effect trail crash fix: it stops this crash, and that kind of effect trail is no longer drawn.";
			}
		}
		if (match != null)
		{
			if (!(match.Groups[1].Value == "Guest"))
			{
				return $"The engine crashed in {match.Groups[3].Value} ({Name(Convert.ToUInt32(match.Groups[2].Value, 16))}).";
			}
			return "The game's own code crashed (" + match.Groups[3].Value + "), usually a rare timing race in the game.";
		}
		if (code > 65535)
		{
			return code switch
			{
				3221225477L => "The engine crashed on a bad memory access (0xC0000005).", 
				3221226505L => "The engine stopped on a failed safety check (0xC0000409).", 
				3221225725L => "The engine ran out of stack (0xC00000FD).", 
				_ => $"The game closed unexpectedly (0x{code:X8}).", 
			};
		}
		if (code == 23)
		{
			return "The engine stopped itself on an error (exit code 23); the report has the details.";
		}
		return $"The game closed unexpectedly (exit code {code}).";
		bool Has(string value)
		{
			return tail.Any((string l) => l.Contains(value, StringComparison.OrdinalIgnoreCase));
		}
	}

	private static string Name(uint code)
	{
		return code switch
		{
			3221225477u => "bad memory access", 
			3221225501u => "illegal instruction", 
			3221225620u => "division by zero", 
			_ => $"0x{code:X8}", 
		};
	}

	private static long EngineCode(List<string> tail, int code)
	{
		string text = tail.LastOrDefault((string l) => ExitLine.IsMatch(l));
		long result;
		long num = ((text != null && long.TryParse(ExitLine.Match(text).Groups[1].Value, out result)) ? result : code);
		if (num >= 0)
		{
			return num;
		}
		return num & 0xFFFFFFFFu;
	}

	private static string? LowMemory(List<string> tail)
	{
		string text = tail.LastOrDefault((string l) => l.StartsWith("Memory: game ", StringComparison.Ordinal));
		if (text != null)
		{
			Match match = MemoryLine.Match(text);
			if (match != null && match.Success)
			{
				long num = long.Parse(match.Groups[4].Value);
				long num2 = long.Parse(match.Groups[5].Value);
				long num3 = long.Parse(match.Groups[6].Value);
				if (num * 100 < num2 * 4 || num3 < 1024)
				{
					return $"Windows was nearly out of memory ({num} MiB of {num2} MiB free, {num3} MiB commit left): closing other programs or a larger page file helps.";
				}
				return null;
			}
		}
		return null;
	}

	private static string? RamTrend()
	{
		string text = null;
		string text2 = null;
		foreach (string item in Tail(GameRunner.SessionLog, int.MaxValue))
		{
			if (item.StartsWith("Memory: game ", StringComparison.Ordinal) && MemoryLine.IsMatch(item))
			{
				if (text == null)
				{
					text = item;
				}
				text2 = item;
			}
		}
		if (text == null || text2 == null)
		{
			return null;
		}
		Match match = MemoryLine.Match(text);
		Match match2 = MemoryLine.Match(text2);
		return $"RAM: game {match.Groups[1].Value} MiB at the start, {match2.Groups[1].Value} MiB at the end ({match.Groups[2].Value} → {match2.Groups[2].Value} MiB committed, peak {match2.Groups[3].Value} MiB); PC {match2.Groups[4].Value} of {match2.Groups[5].Value} MiB free, {match2.Groups[6].Value} MiB commit left";
	}

	public static string Build(LauncherSettings s, IniFile ini, int code, int logLines = 150)
	{
		Hardware hardware = Hardware.Detect();
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(29, 2, stringBuilder2);
		handler.AppendLiteral("Bloodborne Launcher ");
		handler.AppendFormatted(Updater.CurrentLabel);
		handler.AppendLiteral(", engine ");
		handler.AppendFormatted(Updater.LocalEngine() ?? "unknown");
		stringBuilder3.AppendLine(ref handler);
		List<string> tail = Tail(GameRunner.SessionLog, 120).ToList();
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(14, 2, stringBuilder2);
		handler.AppendLiteral("Exit code 0x");
		handler.AppendFormatted(EngineCode(tail, code), "X");
		handler.AppendLiteral(": ");
		handler.AppendFormatted(Explain(code));
		stringBuilder4.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(19, 3, stringBuilder2);
		handler.AppendFormatted(hardware.Summary);
		handler.AppendLiteral(", ");
		handler.AppendFormatted(hardware.RamBytes >> 30);
		handler.AppendLiteral(" GB RAM, Windows ");
		handler.AppendFormatted(Environment.OSVersion.Version);
		stringBuilder5.AppendLine(ref handler);
		string text = RamTrend();
		if (text != null)
		{
			stringBuilder.AppendLine(text);
		}
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(58, 6, stringBuilder2);
		handler.AppendLiteral("Upscaler ");
		handler.AppendFormatted(ini.Get("upscaler"));
		handler.AppendLiteral(", preset ");
		handler.AppendFormatted(ini.Get("preset"));
		handler.AppendLiteral(", output ");
		handler.AppendFormatted(ini.Get("output_res"));
		handler.AppendLiteral(", ");
		handler.AppendLiteral("FPS ");
		handler.AppendFormatted(s.Str("fps_mode"));
		handler.AppendLiteral(", live resolution ");
		handler.AppendFormatted(ini.Get("live_resolution"));
		handler.AppendLiteral(", mods ");
		handler.AppendFormatted(s.Bool("mods_enabled") ? "on" : "off");
		stringBuilder6.AppendLine(ref handler);
		stringBuilder.AppendLine("Experimental: " + string.Join(", ", Experimental.Select((string k) => k.Substring(4, k.Length - 4).Replace('_', ' ') + " " + (s.Bool(k) ? "on" : "off"))));
		stringBuilder.AppendLine();
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(54, 1, stringBuilder2);
		handler.AppendLiteral("Last ");
		handler.AppendFormatted(logLines);
		handler.AppendLiteral(" lines of logs\\last_session.log (repeats folded):");
		stringBuilder7.AppendLine(ref handler);
		stringBuilder.AppendLine("```");
		List<(string, int)> list = new List<(string, int)>();
		foreach (string item in Tail(GameRunner.SessionLog, 2000))
		{
			if (list.Count > 0)
			{
				if (list[list.Count - 1].Item1 == item)
				{
					list[list.Count - 1] = (item, list[list.Count - 1].Item2 + 1);
					continue;
				}
			}
			list.Add((item, 1));
		}
		foreach (var (text2, num) in list.Skip(Math.Max(0, list.Count - logLines)))
		{
			stringBuilder.AppendLine((num > 1) ? $"{text2}  (x{num})" : text2);
		}
		stringBuilder.AppendLine("```");
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (folderPath.Length <= 3)
		{
			return stringBuilder.ToString();
		}
		return stringBuilder.ToString().Replace(folderPath, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
	}

	private static IEnumerable<string> Tail(string path, int count)
	{
		try
		{
			using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using StreamReader streamReader = new StreamReader(stream);
			Queue<string> queue = new Queue<string>();
			while (true)
			{
				string text = streamReader.ReadLine();
				if (text == null)
				{
					break;
				}
				queue.Enqueue(text);
				if (queue.Count > count)
				{
					queue.Dequeue();
				}
			}
			return queue;
		}
		catch (Exception ex)
		{
			return new string[1] { "(the session log could not be read: " + ex.Message + ")" };
		}
	}
}
