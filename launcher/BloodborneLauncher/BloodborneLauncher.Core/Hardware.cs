using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BloodborneLauncher.Core;

public sealed record Hardware(string Gpu, string Vendor, long VramBytes, string Cpu, int Threads, long RamBytes, int ScreenWidth, int ScreenHeight, int RefreshHz)
{
	public enum Tier
	{
		Low,
		Mid,
		High
	}

	public sealed record Change(string Label, string From, string To, Action Apply);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct DevMode
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string dmDeviceName;

		public short dmSpecVersion;

		public short dmDriverVersion;

		public short dmSize;

		public short dmDriverExtra;

		public int dmFields;

		public int dmPositionX;

		public int dmPositionY;

		public int dmDisplayOrientation;

		public int dmDisplayFixedOutput;

		public short dmColor;

		public short dmDuplex;

		public short dmYResolution;

		public short dmTTOption;

		public short dmCollate;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string dmFormName;

		public short dmLogPixels;

		public int dmBitsPerPel;

		public int dmPelsWidth;

		public int dmPelsHeight;

		public int dmDisplayFlags;

		public int dmDisplayFrequency;

		public int dmICMMethod;

		public int dmICMIntent;

		public int dmMediaType;

		public int dmDitherType;

		public int dmReserved1;

		public int dmReserved2;

		public int dmPanningWidth;

		public int dmPanningHeight;
	}

	public int VramGb => (int)Math.Round((double)VramBytes / 1073741824.0);

	public bool Rtx
	{
		get
		{
			if (Vendor == "NVIDIA")
			{
				return Gpu.Contains("RTX", StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
	}

	public bool Rdna4
	{
		get
		{
			if (Vendor == "AMD")
			{
				return Regex.IsMatch(Gpu, "RX\\s*9\\d{3}");
			}
			return false;
		}
	}

	public bool Integrated
	{
		get
		{
			if (VramBytes >= 2147483648u && !Gpu.Contains("UHD", StringComparison.OrdinalIgnoreCase) && !Gpu.Contains("Iris", StringComparison.OrdinalIgnoreCase) && !Gpu.Contains("Custom GPU", StringComparison.OrdinalIgnoreCase))
			{
				return Gpu.EndsWith("Radeon(TM) Graphics", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
	}

	public Tier GpuTier
	{
		get
		{
			if (!Integrated)
			{
				if (VramGb < 12)
				{
					if (VramGb < 8)
					{
						return Tier.Low;
					}
					return Tier.Mid;
				}
				return Tier.High;
			}
			return Tier.Low;
		}
	}

	public string Summary => $"{Gpu}{((VramBytes > 0) ? $" ({VramGb} GB)" : "")} · {Cpu} ({Threads} threads) · " + ((ScreenWidth > 0) ? $"{ScreenWidth} × {ScreenHeight} at {RefreshHz} Hz" : "display unknown");

	private static Hardware? _detected;

	private static readonly Choice[] OnOff = new Choice[2]
	{
		new Choice("On", "1"),
		new Choice("Off", "0")
	};

	private const int EnumCurrentSettings = -1;

	public static Hardware Detect()
	{
		return _detected ?? (_detected = Probe());
	}

	private static Hardware Probe()
	{
		(string Name, string Vendor, long Vram) tuple = DetectGpu();
		string item = tuple.Name;
		string item2 = tuple.Vendor;
		long item3 = tuple.Vram;
		string cpu = "CPU";
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0");
			if (registryKey?.GetValue("ProcessorNameString") is string text && text.Trim().Length > 0)
			{
				cpu = text.Trim();
			}
		}
		catch
		{
		}
		long totalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
		DevMode devMode = new DevMode
		{
			dmSize = (short)Marshal.SizeOf<DevMode>()
		};
		int screenWidth;
		int screenHeight;
		int val;
		if (!EnumDisplaySettings(null, -1, ref devMode))
		{
			screenWidth = 0;
			screenHeight = 0;
			val = 60;
		}
		else
		{
			int dmPelsWidth = devMode.dmPelsWidth;
			int dmPelsHeight = devMode.dmPelsHeight;
			int dmDisplayFrequency = devMode.dmDisplayFrequency;
			val = dmDisplayFrequency;
			screenHeight = dmPelsHeight;
			screenWidth = dmPelsWidth;
		}
		return new Hardware(item, item2, item3, cpu, Environment.ProcessorCount, totalAvailableMemoryBytes, screenWidth, screenHeight, Math.Max(val, 1));
	}

	private static (string Name, string Vendor, long Vram) DetectGpu()
	{
		(string, string, long) result = ("GPU", "", 0L);
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}");
			string[] array = registryKey?.GetSubKeyNames() ?? Array.Empty<string>();
			foreach (string text in array)
			{
				if (text.Length != 4 || !text.All(char.IsDigit))
				{
					continue;
				}
				using RegistryKey registryKey2 = registryKey.OpenSubKey(text);
				if (!(registryKey2?.GetValue("DriverDesc") is string text2) || text2.Contains("Basic Display", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				string text3 = ((registryKey2.GetValue("MatchingDeviceId") as string) ?? "").ToUpperInvariant();
				string item = (text3.Contains("VEN_10DE") ? "NVIDIA" : (text3.Contains("VEN_1002") ? "AMD" : (text3.Contains("VEN_8086") ? "Intel" : "")));
				object value = registryKey2.GetValue("HardwareInformation.qwMemorySize");
				long num2;
				if (!(value is long num))
				{
					if (value is byte[] array2 && array2.Length >= 8)
					{
						num2 = BitConverter.ToInt64(array2, 0);
					}
					else
					{
						object value2 = registryKey2.GetValue("HardwareInformation.MemorySize");
						long num3 = ((value2 is int num4) ? ((uint)num4) : ((!(value2 is byte[] array3) || array3.Length < 4) ? 0 : BitConverter.ToUInt32(array3, 0)));
						num2 = num3;
					}
				}
				else
				{
					num2 = num;
				}
				long num5 = num2;
				if (num5 > result.Item3 || result.Item1 == "GPU")
				{
					result = (text2.Trim(), item, num5);
				}
			}
		}
		catch
		{
		}
		return result;
	}

	public List<Change> Recommend(LauncherSettings s, IniFile ini)
	{
		List<Change> changes = new List<Change>();
		Tier gpuTier = GpuTier;
		bool flag = File.Exists(Path.Combine(Paths.BinariesDir, "nvngx_dlss.dll"));
		Ini("Upscaler", "upscaler", (Rtx && flag) ? "dlss" : (Rdna4 ? "fsr4" : "fsr3"), Choices.Upscalers);
		string text = ((ScreenWidth == 0) ? "1920x1080" : (Choices.OutputResolutions.Select((Choice c) => c.Value).FirstOrDefault((string v) => v == $"{ScreenWidth}x{ScreenHeight}") ?? "auto"));
		Ini("Output resolution", "output_res", text, Choices.OutputResolutions, "1920x1080");
		int item = Choices.OutputSize(text).H;
		int num = ((item <= 1080) ? ((item > 720) ? (gpuTier switch
		{
			Tier.High => 0, 
			Tier.Mid => 1, 
			_ => 2, 
		}) : ((gpuTier == Tier.Low) ? 1 : 0)) : ((item > 1440) ? (gpuTier switch
		{
			Tier.High => 2, 
			Tier.Mid => 3, 
			_ => 4, 
		}) : (gpuTier switch
		{
			Tier.High => 1, 
			Tier.Mid => 2, 
			_ => 3, 
		})));
		int num2 = num;
		Ini("Quality preset", "preset", num2.ToString(), Choices.Presets, "0");
		string to = ((RefreshHz >= 119 && gpuTier != Tier.Low && Threads >= 12) ? "120" : "60");
		Setting("Frame rate", "fps_mode", to, Choices.FpsModes);
		if (gpuTier == Tier.High)
		{
			Ini("Object motion vectors", "object_motion", "1", OnOff, "1");
		}
		if (gpuTier == Tier.Low)
		{
			Ini("Object motion vectors", "object_motion", "0", OnOff, "1");
		}
		return changes;
		void Ini(string label, string key, string text3, IReadOnlyList<Choice>? names = null, string fallback = "")
		{
			string text2 = ini.Get(key, fallback);
			if (!(text2 == text3))
			{
				changes.Add(new Change(label, Name(names, text2), Name(names, text3), delegate
				{
					ini.Set(key, text3);
				}));
			}
		}
		void Setting(string label, string key, string text3, IReadOnlyList<Choice>? names = null)
		{
			string text2 = s.Str(key);
			if (!(text2 == text3))
			{
				changes.Add(new Change(label, Name(names, text2), Name(names, text3), delegate
				{
					s.Set(key, text3);
				}));
			}
		}
	}

	private static string Name(IReadOnlyList<Choice>? names, string value)
	{
		object obj;
		if (names == null)
		{
			obj = null;
		}
		else
		{
			Choice? choice = names.FirstOrDefault((Choice c) => c.Value == value);
			obj = (((object)choice != null) ? choice.Label.Split(" (")[0] : null);
		}
		if (obj == null)
		{
			if (value.Length <= 0)
			{
				return "default";
			}
			obj = value;
		}
		return (string)obj;
	}

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

	[CompilerGenerated]
	private Hardware(Hardware original)
	{
		Gpu = original.Gpu;
		Vendor = original.Vendor;
		VramBytes = original.VramBytes;
		Cpu = original.Cpu;
		Threads = original.Threads;
		RamBytes = original.RamBytes;
		ScreenWidth = original.ScreenWidth;
		ScreenHeight = original.ScreenHeight;
		RefreshHz = original.RefreshHz;
	}
}
