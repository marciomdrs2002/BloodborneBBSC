using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace BloodborneLauncher.Core;

public sealed class SessionStats
{
	private static readonly Regex FrameStats = new Regex("^Frame stats: ([\\d.]+) FPS, worst frame ([\\d.]+) ms", RegexOptions.Compiled);

	private static readonly Regex Stall = new Regex("^Stall: ([\\d.]+) ms frame", RegexOptions.Compiled);

	private static readonly Regex Memory = new Regex("^Memory: game (\\d+) MiB in RAM", RegexOptions.Compiled);

	private readonly List<double> _fps = new List<double>();

	public DateTime Started { get; } = DateTime.Now;

	public TimeSpan Length { get; private set; }

	public double WorstFrameMs { get; private set; }

	public int Stutters { get; private set; }

	public double AverageFps
	{
		get
		{
			if (_fps.Count <= 0)
			{
				return 0.0;
			}
			return _fps.Average();
		}
	}

	public double LowestFps
	{
		get
		{
			if (_fps.Count <= 0)
			{
				return 0.0;
			}
			return _fps.Min();
		}
	}

	public bool HasFrameStats => _fps.Count > 0;

	public long RamStartMiB { get; private set; }

	public long RamEndMiB { get; private set; }

	public string Summary => Duration(Length) + (HasFrameStats ? ($" · {AverageFps:0} FPS average (lowest {LowestFps:0}) · worst frame {WorstFrameMs:0} ms · " + ((Stutters == 1) ? "1 stutter" : $"{Stutters} stutters")) : " · turn on Frame statistics (Settings) for FPS and stutters") + ((RamEndMiB > 0) ? $" · RAM {(double)RamStartMiB / 1024.0:0.0} → {(double)RamEndMiB / 1024.0:0.0} GB" : "");

	public void Observe(string line)
	{
		if (line.StartsWith("Frame stats: ", StringComparison.Ordinal))
		{
			Match match = FrameStats.Match(line);
			if (match != null && match.Success)
			{
				_fps.Add(Parse(match.Groups[1].Value));
				WorstFrameMs = Math.Max(WorstFrameMs, Parse(match.Groups[2].Value));
				return;
			}
		}
		if (line.StartsWith("Memory: game ", StringComparison.Ordinal))
		{
			Match match2 = Memory.Match(line);
			if (match2 != null && match2.Success)
			{
				RamEndMiB = long.Parse(match2.Groups[1].Value, CultureInfo.InvariantCulture);
				if (RamStartMiB == 0L)
				{
					RamStartMiB = RamEndMiB;
				}
				return;
			}
		}
		if (line.StartsWith("Stall: ", StringComparison.Ordinal))
		{
			Match match3 = Stall.Match(line);
			if (match3 != null && match3.Success)
			{
				Stutters++;
				WorstFrameMs = Math.Max(WorstFrameMs, Parse(match3.Groups[1].Value));
			}
		}
	}

	public void Finish()
	{
		Length = DateTime.Now - Started;
	}

	public static string Duration(TimeSpan time)
	{
		if (!(time.TotalHours >= 1.0))
		{
			if (time.TotalMinutes >= 1.0)
			{
				return $"{(int)time.TotalMinutes} min";
			}
			return $"{Math.Max(0, (int)time.TotalSeconds)} s";
		}
		return $"{(int)time.TotalHours} h {time.Minutes} min";
	}

	public static TimeSpan Playtime(LauncherSettings s, string profile)
	{
		return TimeSpan.FromSeconds(Math.Max(0, s.Int(Key(profile))));
	}

	public static void AddPlaytime(LauncherSettings s, string profile, TimeSpan time)
	{
		s.Set(Key(profile), (int)Math.Min(2147483647.0, Playtime(s, profile).TotalSeconds + time.TotalSeconds));
	}

	private static string Key(string profile)
	{
		return "playtime." + profile.ToLowerInvariant();
	}

	private static double Parse(string value)
	{
		return double.Parse(value, CultureInfo.InvariantCulture);
	}
}
