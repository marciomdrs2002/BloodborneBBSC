using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace BloodborneLauncher.Core;

public static class WindowsMonitors
{
	public sealed record Monitor(string Device, string Name, int X, int Y, int Width, int Height, bool Primary)
	{
		public string Number
		{
			get
			{
				if (!Device.StartsWith("\\\\.\\DISPLAY", StringComparison.OrdinalIgnoreCase))
				{
					return Device;
				}
				string device = Device;
				return device.Substring(11, device.Length - 11);
			}
		}

		public string Label => "Monitor " + Number + ": " + Name + (Primary ? " (primary)" : "");

		public string Bounds => FormattableString.Invariant($"{X},{Y},{Width},{Height}");
	}

	private struct Rect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct MonitorInfo
	{
		public int Size;

		public Rect Monitor;

		public Rect Work;

		public uint Flags;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string Device;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct DisplayDevice
	{
		public int Size;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string Device;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string Description;

		public uint Flags;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string Id;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string Key;
	}

	private delegate bool MonitorCallback(nint handle, nint dc, nint rect, nint data);

	public static List<Monitor> All()
	{
		List<Monitor> result = new List<Monitor>();
		nint num = SetThreadDpiAwarenessContext(new IntPtr(-4));
		try
		{
			EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(nint handle, nint _, nint _, nint _)
			{
				MonitorInfo info = new MonitorInfo
				{
					Size = Marshal.SizeOf<MonitorInfo>()
				};
				if (!GetMonitorInfo(handle, ref info))
				{
					return true;
				}
				DisplayDevice info2 = new DisplayDevice
				{
					Size = Marshal.SizeOf<DisplayDevice>()
				};
				string name = ((EnumDisplayDevices(info.Device, 0u, ref info2, 0u) && !string.IsNullOrWhiteSpace(info2.Description)) ? info2.Description : "Display");
				Rect monitor = info.Monitor;
				if (monitor.Right > monitor.Left && monitor.Bottom > monitor.Top)
				{
					result.Add(new Monitor(info.Device, name, monitor.Left, monitor.Top, monitor.Right - monitor.Left, monitor.Bottom - monitor.Top, (info.Flags & 1) != 0));
				}
				return true;
			}, IntPtr.Zero);
		}
		finally
		{
			if (num != IntPtr.Zero)
			{
				SetThreadDpiAwarenessContext(num);
			}
		}
		int result2;
		return result.OrderBy((Monitor m) => (!int.TryParse(m.Number, out result2)) ? int.MaxValue : result2).ToList();
	}

	public static Monitor? Resolve(string device, IReadOnlyList<Monitor> monitors)
	{
		return monitors.FirstOrDefault((Monitor m) => string.Equals(m.Device, device, StringComparison.OrdinalIgnoreCase)) ?? monitors.FirstOrDefault((Monitor m) => m.Primary) ?? monitors.FirstOrDefault();
	}

	public static void ApplyEnvironment(string device, IReadOnlyList<Monitor> monitors, IDictionary<string, string> env)
	{
		env["BB_MONITOR_BOUNDS"] = "";
		env["BB_MONITOR_LABEL"] = "";
		if (!string.IsNullOrWhiteSpace(device))
		{
			Monitor monitor = Resolve(device, monitors);
			if (!(monitor == null))
			{
				env["BB_MONITOR_BOUNDS"] = monitor.Bounds;
				env["BB_MONITOR_LABEL"] = (string.Equals(device, monitor.Device, StringComparison.OrdinalIgnoreCase) ? monitor.Label : ("Selected monitor disconnected; using " + monitor.Label));
				env["BB_DISPLAY_SIZE"] = FormattableString.Invariant($"{monitor.Width - monitor.Width % 2}x{monitor.Height - monitor.Height % 2}");
			}
		}
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool EnumDisplayMonitors(nint dc, nint rect, MonitorCallback callback, nint data);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplayDevicesW")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice info, uint flags);

	[DllImport("user32.dll")]
	private static extern nint SetThreadDpiAwarenessContext(nint value);
}
