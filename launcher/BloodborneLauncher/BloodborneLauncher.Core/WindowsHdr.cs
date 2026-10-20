using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace BloodborneLauncher.Core;

public static class WindowsHdr
{
	public sealed record Display(uint AdapterLow, int AdapterHigh, uint Target, bool Supported, bool On);

	private struct Luid
	{
		public uint Low;

		public int High;
	}

	private struct PathInfo
	{
		public Luid SourceAdapter;

		public uint SourceId;

		public uint SourceModeIndex;

		public uint SourceFlags;

		public Luid TargetAdapter;

		public uint TargetId;

		public uint TargetModeIndex;

		public uint OutputTechnology;

		public uint Rotation;

		public uint Scaling;

		public uint RefreshNumerator;

		public uint RefreshDenominator;

		public uint ScanLineOrdering;

		public int TargetAvailable;

		public uint TargetFlags;

		public uint Flags;
	}

	[StructLayout(LayoutKind.Sequential, Size = 64)]
	private struct ModeInfo
	{
		public uint InfoType;

		public uint Id;

		public Luid Adapter;
	}

	private struct DeviceInfoHeader
	{
		public uint Type;

		public uint Size;

		public Luid Adapter;

		public uint Id;
	}

	private struct AdvancedColorInfo
	{
		public DeviceInfoHeader Header;

		public uint Value;

		public uint ColorEncoding;

		public uint BitsPerColorChannel;
	}

	private struct AdvancedColorInfo2
	{
		public DeviceInfoHeader Header;

		public uint Value;

		public uint ColorEncoding;

		public uint BitsPerColorChannel;

		public uint ActiveColorMode;
	}

	private struct SetState
	{
		public DeviceInfoHeader Header;

		public uint Value;
	}

	private const uint QdcOnlyActivePaths = 2u;

	private const uint InfoAdvancedColor = 9u;

	private const uint SetAdvancedColorState = 10u;

	private const uint InfoAdvancedColor2 = 15u;

	private const uint SetHdrState = 16u;

	public static bool AnyOn()
	{
		return Displays().Any((Display d) => d.On);
	}

	public static bool AnySupported()
	{
		return Displays().Any((Display d) => d.Supported);
	}

	public static List<Display> Displays()
	{
		List<Display> list = new List<Display>();
		try
		{
			if (GetDisplayConfigBufferSizes(2u, out var paths, out var modes) != 0)
			{
				return list;
			}
			PathInfo[] array = new PathInfo[paths];
			ModeInfo[] modeInfo = new ModeInfo[modes];
			if (QueryDisplayConfig(2u, ref paths, array, ref modes, modeInfo, IntPtr.Zero) != 0)
			{
				return list;
			}
			foreach (PathInfo item in array.Take((int)paths))
			{
				Luid targetAdapter = item.TargetAdapter;
				AdvancedColorInfo2 info = new AdvancedColorInfo2
				{
					Header = Header(15u, Marshal.SizeOf<AdvancedColorInfo2>(), targetAdapter, item.TargetId)
				};
				if (DisplayConfigGetDeviceInfo(ref info) == 0)
				{
					list.Add(new Display(targetAdapter.Low, targetAdapter.High, item.TargetId, (info.Value & 0x10) != 0, info.ActiveColorMode == 2));
					continue;
				}
				AdvancedColorInfo info2 = new AdvancedColorInfo
				{
					Header = Header(9u, Marshal.SizeOf<AdvancedColorInfo>(), targetAdapter, item.TargetId)
				};
				if (DisplayConfigGetDeviceInfo(ref info2) == 0)
				{
					list.Add(new Display(targetAdapter.Low, targetAdapter.High, item.TargetId, (info2.Value & 1) != 0, (info2.Value & 2) != 0 && (info2.Value & 4) == 0));
				}
			}
		}
		catch
		{
		}
		return list;
	}

	public static int Restore(IReadOnlyList<Display> before)
	{
		int num = 0;
		List<Display> source = Displays();
		foreach (Display was in before.Where((Display d) => d.On))
		{
			Display display = source.FirstOrDefault((Display d) => d.AdapterLow == was.AdapterLow && d.AdapterHigh == was.AdapterHigh && d.Target == was.Target);
			if (display == null || display.On)
			{
				continue;
			}
			Luid adapter = new Luid
			{
				Low = was.AdapterLow,
				High = was.AdapterHigh
			};
			SetState info = new SetState
			{
				Header = Header(16u, Marshal.SizeOf<SetState>(), adapter, was.Target),
				Value = 1u
			};
			if (DisplayConfigSetDeviceInfo(ref info) == 0)
			{
				num++;
				continue;
			}
			SetState info2 = new SetState
			{
				Header = Header(10u, Marshal.SizeOf<SetState>(), adapter, was.Target),
				Value = 1u
			};
			if (DisplayConfigSetDeviceInfo(ref info2) == 0)
			{
				num++;
			}
		}
		return num;
	}

	private static DeviceInfoHeader Header(uint type, int size, Luid adapter, uint id)
	{
		return new DeviceInfoHeader
		{
			Type = type,
			Size = (uint)size,
			Adapter = adapter,
			Id = id
		};
	}

	[DllImport("user32.dll")]
	private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);

	[DllImport("user32.dll")]
	private static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] PathInfo[] pathInfo, ref uint modes, [Out] ModeInfo[] modeInfo, nint topology);

	[DllImport("user32.dll")]
	private static extern int DisplayConfigGetDeviceInfo(ref AdvancedColorInfo info);

	[DllImport("user32.dll")]
	private static extern int DisplayConfigGetDeviceInfo(ref AdvancedColorInfo2 info);

	[DllImport("user32.dll")]
	private static extern int DisplayConfigSetDeviceInfo(ref SetState info);
}
