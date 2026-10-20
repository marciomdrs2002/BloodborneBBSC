using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace BloodborneLauncher.Core;

public static class SdlPad
{
	public sealed record Device(string Name, bool Supported);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.U1)]
	private delegate bool SetHintFn([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.U1)]
	private delegate bool InitFn(uint flags);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void QuitFn(uint flags);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate nint ListFn(out int count);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void FreeFn(nint p);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate nint ById(uint id);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.U1)]
	private delegate bool IsById(uint id);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void VoidFn();

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.U1)]
	private delegate bool ButtonFn(nint pad, int button);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate short AxisFn(nint pad, int axis);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate nint ButtonNameFn(int button);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void CloseFn(nint pad);

	private const uint InitGamepad = 8192u;

	private const int ButtonCount = 26;

	private const int AxisLeftTrigger = 4;

	private const int AxisRightTrigger = 5;

	private static bool _loaded;

	private static bool _failed;

	private static bool _initialised;

	private static SetHintFn? _setHint;

	private static InitFn? _init;

	private static QuitFn? _quit;

	private static ListFn? _gamepads;

	private static ListFn? _joysticks;

	private static FreeFn? _free;

	private static ById? _open;

	private static ById? _nameForId;

	private static ById? _joystickNameForId;

	private static IsById? _isGamepad;

	private static VoidFn? _update;

	private static ButtonFn? _button;

	private static AxisFn? _axis;

	private static ButtonNameFn? _buttonName;

	private static CloseFn? _close;

	private static readonly Dictionary<uint, nint> Open = new Dictionary<uint, nint>();

	public static bool Available => Load();

	private static bool Load()
	{
		if (_loaded || _failed)
		{
			return _loaded;
		}
		try
		{
			string text = Path.Combine(Paths.BinariesDir, "SDL3.dll");
			if (!File.Exists(text))
			{
				_failed = true;
				return false;
			}
			nint lib = NativeLibrary.Load(text);
			_setHint = Get<SetHintFn>("SDL_SetHint");
			_init = Get<InitFn>("SDL_InitSubSystem");
			_quit = Get<QuitFn>("SDL_QuitSubSystem");
			_gamepads = Get<ListFn>("SDL_GetGamepads");
			_joysticks = Get<ListFn>("SDL_GetJoysticks");
			_free = Get<FreeFn>("SDL_free");
			_open = Get<ById>("SDL_OpenGamepad");
			_nameForId = Get<ById>("SDL_GetGamepadNameForID");
			_joystickNameForId = Get<ById>("SDL_GetJoystickNameForID");
			_isGamepad = Get<IsById>("SDL_IsGamepad");
			_update = Get<VoidFn>("SDL_UpdateGamepads");
			_button = Get<ButtonFn>("SDL_GetGamepadButton");
			_axis = Get<AxisFn>("SDL_GetGamepadAxis");
			_buttonName = Get<ButtonNameFn>("SDL_GetGamepadStringForButton");
			_close = Get<CloseFn>("SDL_CloseGamepad");
			_loaded = true;
			T Get<T>(string name) where T : notnull, Delegate
			{
				return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(lib, name));
			}
		}
		catch
		{
			_failed = true;
		}
		return _loaded;
	}

	private static bool Start()
	{
		if (_initialised)
		{
			return true;
		}
		if (!Load())
		{
			return false;
		}
		foreach (var (name, value) in GameEnvironment.SdlHints())
		{
			_setHint(name, value);
		}
		_setHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
		_initialised = _init(8192u);
		return _initialised;
	}

	public static void Stop()
	{
		if (!_initialised)
		{
			return;
		}
		foreach (nint value in Open.Values)
		{
			_close(value);
		}
		Open.Clear();
		_quit(8192u);
		_initialised = false;
	}

	private static List<uint> Ids(ListFn list)
	{
		List<uint> list2 = new List<uint>();
		int count;
		nint num = list(out count);
		if (num == IntPtr.Zero)
		{
			return list2;
		}
		for (int i = 0; i < count; i++)
		{
			list2.Add((uint)Marshal.ReadInt32(num, i * 4));
		}
		_free(num);
		return list2;
	}

	public static List<Device>? Devices()
	{
		if (!Start())
		{
			return null;
		}
		_update();
		List<Device> list = new List<Device>();
		foreach (uint item in Ids(_joysticks))
		{
			bool flag = _isGamepad(item);
			string text = Marshal.PtrToStringUTF8(flag ? _nameForId(item) : _joystickNameForId(item));
			list.Add(new Device(string.IsNullOrWhiteSpace(text) ? "Game controller" : text, flag));
		}
		return list;
	}

	public static string State()
	{
		HashSet<string> source = Held();
		StringBuilder stringBuilder = new StringBuilder(string.Join(",", source.OrderBy((string h) => h)));
		foreach (nint value in Open.Values)
		{
			for (int num = 0; num < 4; num++)
			{
				stringBuilder.Append('|').Append(_axis(value, num) / 8192);
			}
		}
		return stringBuilder.ToString();
	}

	public static HashSet<string> Held()
	{
		HashSet<string> hashSet = new HashSet<string>();
		if (!Start())
		{
			return hashSet;
		}
		_update();
		List<uint> list = Ids(_gamepads);
		foreach (uint item in Open.Keys.Except(list).ToList())
		{
			_close(Open[item]);
			Open.Remove(item);
		}
		foreach (uint item2 in list)
		{
			if (!Open.TryGetValue(item2, out var value))
			{
				value = _open(item2);
				if (value == IntPtr.Zero)
				{
					continue;
				}
				Open[item2] = value;
			}
			for (int i = 0; i < 26; i++)
			{
				if (_button(value, i))
				{
					string text = Marshal.PtrToStringUTF8(_buttonName(i));
					if (text != null && text.Length > 0)
					{
						hashSet.Add(text);
					}
				}
			}
			if (_axis(value, 4) > 16000)
			{
				hashSet.Add("lefttrigger");
			}
			if (_axis(value, 5) > 16000)
			{
				hashSet.Add("righttrigger");
			}
		}
		return hashSet;
	}
}
