using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BloodborneLauncher.Core;

public static class MemoryCheck
{
	private struct MemoryStatus
	{
		public uint Length;

		public uint Load;

		public ulong TotalPhys;

		public ulong AvailPhys;

		public ulong TotalPageFile;

		public ulong AvailPageFile;

		public ulong TotalVirtual;

		public ulong AvailVirtual;

		public ulong AvailExtended;
	}

	private static readonly double[] PresetScales = new double[5] { 1.0, 1.5, 1.7, 2.0, 3.0 };

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

	public static long PoolMiB(IniFile ini)
	{
		return ((ini.Get("live_resolution") != "1" && LargeScene(ini)) ? 9152 : 5056) + 1024;
	}

	public static long NeededMiB(IniFile ini)
	{
		return PoolMiB(ini) + 3072;
	}

	private static bool LargeScene(IniFile ini)
	{
		(int W, int H) tuple = Choices.OutputSize(ini.Get("output_res", "1920x1080"));
		int item = tuple.W;
		int item2 = tuple.H;
		string text = ini.Get("upscaler", "fsr3");
		int num = item2;
		if ((item == 1920 && num == 1080) || text == "taa")
		{
			return false;
		}
		double num2 = 1.0;
		if (text != "off")
		{
			int.TryParse(ini.Get("preset", "0"), out var result);
			num2 = PresetScales[Math.Clamp(result, 0, PresetScales.Length - 1)];
		}
		long num3 = (long)Math.Round((double)item / num2 / 4.0) * 4;
		long num4 = (long)Math.Round((double)item2 / num2 / 4.0) * 4;
		return num3 * num4 > 2073600;
	}

	public static string? Problem(IniFile ini)
	{
		MemoryStatus status = new MemoryStatus
		{
			Length = (uint)Marshal.SizeOf<MemoryStatus>()
		};
		if (!GlobalMemoryStatusEx(ref status))
		{
			return null;
		}
		var (flag, val) = PageFile();
		if (flag)
		{
			long num = (long)(status.AvailPageFile >> 20);
			long num2 = PoolMiB(ini);
			if (num >= num2)
			{
				return null;
			}
			return $"The game takes about {(double)num2 / 1024.0:0.#} GB of memory (RAM plus page file) at start, and Windows can give about {(double)num / 1024.0:0.#} GB right now: the game would not start (Windows error 1450).\n\n" + "Close other programs (browsers, recorders), or turn Live resolution On (Display) or choose a lower upscaler preset or output resolution: a scene up to 1080p needs about 4 GB less.";
		}
		long num3 = (long)(status.TotalPhys >> 20);
		long num4 = (long)(status.TotalPageFile >> 20);
		long num5 = num4 - (long)(status.AvailPageFile >> 20);
		long num6 = Math.Max(val, num4 - num3);
		long num7 = num3 + num6 - num5;
		long num8 = NeededMiB(ini);
		if (num7 >= num8)
		{
			return null;
		}
		string value = ((num6 < 256) ? "the page file is off" : $"the page file is fixed at {(double)num6 / 1024.0:0.#} GB");
		return $"The game needs about {(double)num8 / 1024.0:0.#} GB of memory (RAM plus page file). With the programs open now, about {(double)Math.Max(0L, num7) / 1024.0:0.#} GB is left, because {value}. Windows can run out during play, and the game then crashes.\n\n" + "To fix it, set the page file to System managed size: Windows Settings › System › About › Advanced system settings › Performance Settings › Advanced › Virtual memory › Change. Closing other programs also helps.";
	}

	private static (bool Managed, long MaxMiB) PageFile()
	{
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management");
			if (!(registryKey?.GetValue("PagingFiles") is string[] source))
			{
				return (Managed: true, MaxMiB: 0L);
			}
			long num = 0L;
			foreach (string item in source.Where((string f) => !string.IsNullOrWhiteSpace(f)))
			{
				string[] array = item.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (array[0].StartsWith("?:", StringComparison.Ordinal))
				{
					return (Managed: true, MaxMiB: 0L);
				}
				if (array.Length < 3)
				{
					return (Managed: true, MaxMiB: 0L);
				}
				if (!long.TryParse(array[2], out var result))
				{
					return (Managed: true, MaxMiB: 0L);
				}
				if (result == 0L)
				{
					return (Managed: true, MaxMiB: 0L);
				}
				num += result;
			}
			return (Managed: false, MaxMiB: num);
		}
		catch
		{
			return (Managed: true, MaxMiB: 0L);
		}
	}
}
