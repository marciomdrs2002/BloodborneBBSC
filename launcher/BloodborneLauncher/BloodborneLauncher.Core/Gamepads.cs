using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace BloodborneLauncher.Core;

public static class Gamepads
{
	public sealed record Pad(string Name, bool Bluetooth);

	private struct HidCaps
	{
		public ushort Usage;

		public ushort UsagePage;

		public ushort InputReportByteLength;

		public ushort OutputReportByteLength;

		public ushort FeatureReportByteLength;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
		public ushort[] Reserved;

		public ushort NumberLinkCollectionNodes;

		public ushort NumberInputButtonCaps;

		public ushort NumberInputValueCaps;

		public ushort NumberInputDataIndices;

		public ushort NumberOutputButtonCaps;

		public ushort NumberOutputValueCaps;

		public ushort NumberOutputDataIndices;

		public ushort NumberFeatureButtonCaps;

		public ushort NumberFeatureValueCaps;

		public ushort NumberFeatureDataIndices;
	}

	private struct InterfaceData
	{
		public int Size;

		public Guid Class;

		public int Flags;

		public nint Reserved;
	}

	private static readonly Dictionary<(int Vid, int Pid), string> Known = new Dictionary<(int, int), string>
	{
		[(1356, 1476)] = "DualShock 4",
		[(1356, 2508)] = "DualShock 4",
		[(1356, 2976)] = "DualShock 4 (wireless adapter)",
		[(1356, 3302)] = "DualSense",
		[(1356, 3570)] = "DualSense Edge",
		[(1356, 616)] = "DualShock 3",
		[(1406, 8201)] = "Switch Pro Controller",
		[(8380, 21760)] = "Betop BFM"
	};

	private static readonly Regex VidPid = new Regex("vid[_&](?:0002)?([0-9a-f]{4}).*?pid[_&]([0-9a-f]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static Guid HidInterface = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");

	private const int DIGCF_PRESENT = 2;

	private const int DIGCF_DEVICEINTERFACE = 16;

	public static List<Pad> Detect()
	{
		List<Pad> list = new List<Pad>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string item2 in HidPaths())
		{
			Match match = VidPid.Match(item2);
			if (!match.Success)
			{
				continue;
			}
			(int, int) key = (Convert.ToInt32(match.Groups[1].Value, 16), Convert.ToInt32(match.Groups[2].Value, 16));
			if (!item2.Contains("ig_", StringComparison.OrdinalIgnoreCase) && (Known.TryGetValue(key, out string value) || (value = GenericGamepadName(item2)) != null))
			{
				string text = item2.Split('#').ElementAtOrDefault(2) ?? item2;
				string item = ((key.Item1 == 1356 || key.Item1 == 1406) ? text.Split('&').Take(2).Aggregate(value, (string a, string b) => a + b) : $"{key.Item1:x4}:{key.Item2:x4}");
				if (hashSet.Add(item))
				{
					list.Add(new Pad(value, item2.Contains("{00001124-", StringComparison.OrdinalIgnoreCase)));
				}
			}
		}
		foreach (int item3 in XInput.Connected())
		{
			_ = item3;
			list.Add(new Pad("Xbox controller", Bluetooth: false));
		}
		return list;
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint creation, uint flags, nint template);

	[DllImport("hid.dll")]
	private static extern bool HidD_GetPreparsedData(SafeFileHandle device, out nint data);

	[DllImport("hid.dll")]
	private static extern bool HidD_FreePreparsedData(nint data);

	[DllImport("hid.dll")]
	private static extern int HidP_GetCaps(nint data, out HidCaps caps);

	[DllImport("hid.dll", CharSet = CharSet.Unicode)]
	private static extern bool HidD_GetProductString(SafeFileHandle device, char[] buffer, int length);

	private static string? GenericGamepadName(string path)
	{
		try
		{
			using SafeFileHandle safeFileHandle = CreateFileW(path, 0u, 3u, IntPtr.Zero, 3u, 0u, IntPtr.Zero);
			if (safeFileHandle.IsInvalid || !HidD_GetPreparsedData(safeFileHandle, out var data))
			{
				return null;
			}
			try
			{
				HidCaps caps;
				bool flag = HidP_GetCaps(data, out caps) != 1114112 || caps.UsagePage != 1;
				if (!flag)
				{
					ushort usage = caps.Usage;
					bool flag2 = (uint)(usage - 4) <= 1u;
					flag = !flag2;
				}
				if (flag)
				{
					return null;
				}
			}
			finally
			{
				HidD_FreePreparsedData(data);
			}
			char[] array = new char[127];
			string text = (HidD_GetProductString(safeFileHandle, array, array.Length * 2) ? new string(array).TrimEnd('\0').Trim() : "");
			return (text.Length > 0) ? text : "Game controller";
		}
		catch
		{
			return null;
		}
	}

	[DllImport("setupapi.dll", SetLastError = true)]
	private static extern nint SetupDiGetClassDevsW(ref Guid guid, nint enumerator, nint parent, int flags);

	[DllImport("setupapi.dll", SetLastError = true)]
	private static extern bool SetupDiEnumDeviceInterfaces(nint set, nint info, ref Guid guid, int index, ref InterfaceData data);

	[DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool SetupDiGetDeviceInterfaceDetailW(nint set, ref InterfaceData data, nint detail, int size, out int required, nint info);

	[DllImport("setupapi.dll")]
	private static extern bool SetupDiDestroyDeviceInfoList(nint set);

	private static List<string> HidPaths()
	{
		List<string> list = new List<string>();
		nint num = SetupDiGetClassDevsW(ref HidInterface, IntPtr.Zero, IntPtr.Zero, 18);
		if (num == IntPtr.Zero || num == new IntPtr(-1))
		{
			return list;
		}
		try
		{
			InterfaceData data = new InterfaceData
			{
				Size = Marshal.SizeOf<InterfaceData>()
			};
			for (int i = 0; SetupDiEnumDeviceInterfaces(num, IntPtr.Zero, ref HidInterface, i, ref data); i++)
			{
				SetupDiGetDeviceInterfaceDetailW(num, ref data, IntPtr.Zero, 0, out var required, IntPtr.Zero);
				if (required <= 0)
				{
					continue;
				}
				nint num2 = Marshal.AllocHGlobal(required);
				try
				{
					Marshal.WriteInt32(num2, (IntPtr.Size == 8) ? 8 : 6);
					if (SetupDiGetDeviceInterfaceDetailW(num, ref data, num2, required, out var _, IntPtr.Zero))
					{
						list.Add(Marshal.PtrToStringUni(num2 + 4) ?? "");
					}
				}
				finally
				{
					Marshal.FreeHGlobal(num2);
				}
			}
		}
		catch
		{
		}
		finally
		{
			SetupDiDestroyDeviceInfoList(num);
		}
		return list;
	}
}
