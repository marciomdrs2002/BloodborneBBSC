using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BloodborneLauncher.Core;

public static class GameEnvironment
{
	private static (string Game, string? Found) _foundDlc = (Game: "", Found: null);

	public static string? RegionPatchOnline { get; set; }

	public static string? ResolutionPatchChoice { get; set; }

	public static string GameDir(LauncherSettings s)
	{
		return Paths.Expand(s.Str("game_dir"));
	}

	public static string SavesRoot(LauncherSettings s)
	{
		string text = s.Str("user_dir");
		if (text == null || text.Length <= 0)
		{
			return Path.Combine(Paths.DataDir, "user");
		}
		return Paths.Expand(text);
	}

	public static string UserDir(LauncherSettings s)
	{
		return PlayerProfiles.Folder(s, PlayerProfiles.Current(s));
	}

	public static string DlcDir(LauncherSettings s)
	{
		string text = s.Str("dlc_dir");
		string text2;
		if (text == null || text.Length <= 0)
		{
			text2 = FoundDlc(GameDir(s));
			if (text2 == null)
			{
				return "";
			}
		}
		else
		{
			text2 = Paths.Expand(text);
		}
		return text2;
	}

	public static string? FoundDlc(string game)
	{
		if (game.Length == 0 || !Directory.Exists(game))
		{
			return null;
		}
		if (_foundDlc.Game == game)
		{
			return _foundDlc.Found;
		}
		string text = null;
		try
		{
			string text2 = Directory.GetParent(Path.GetFullPath(game).TrimEnd('\\', '/'))?.FullName;
			List<string> list = Children(game).ToList();
			if (text2 != null)
			{
				foreach (string item in Children(text2).Take(200))
				{
					list.Add(item);
					list.AddRange(Children(item).Take(50));
					if (!string.Equals(Path.GetFileName(item), "addcont", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					foreach (string item2 in Children(item))
					{
						list.AddRange(Children(item2));
					}
				}
			}
			string text3 = list.FirstOrDefault(IsAddon);
			if (text3 != null)
			{
				string directoryName = Path.GetDirectoryName(text3);
				text = ((text2 != null && string.Equals(directoryName, text2, StringComparison.OrdinalIgnoreCase)) ? text3 : directoryName);
			}
		}
		catch (Exception)
		{
		}
		_foundDlc = (Game: game, Found: text);
		return text;
		static IEnumerable<string> Children(string dir)
		{
			try
			{
				return Directory.EnumerateDirectories(dir);
			}
			catch (Exception)
			{
				return Array.Empty<string>();
			}
		}
	}

	private static bool IsAddon(string dir)
	{
		try
		{
			string text = Path.Combine(dir, "sce_sys", "param.sfo");
			if (!File.Exists(text) || new FileInfo(text).Length > 65536)
			{
				return false;
			}
			byte[] sfo = File.ReadAllBytes(text);
			if (sfo.Length < 20 || sfo[0] != 0 || sfo[1] != 80 || sfo[2] != 83 || sfo[3] != 70)
			{
				return false;
			}
			int num = U(8);
			int num2 = U(12);
			int num3 = U(16);
			for (int i = 0; i < num3 && 20 + 16 * (i + 1) <= sfo.Length; i++)
			{
				int num4 = 20 + 16 * i;
				int num5 = num + (sfo[num4] | (sfo[num4 + 1] << 8));
				int num6 = num2 + U(num4 + 12);
				if (num5 + 9 <= sfo.Length && num6 + 2 <= sfo.Length && Encoding.ASCII.GetString(sfo, num5, 9) == "CATEGORY\0")
				{
					return sfo[num6] == 97 && sfo[num6 + 1] == 99;
				}
			}
			int U(int at)
			{
				return BitConverter.ToInt32(sfo, at);
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	public static string ModsDir(LauncherSettings s)
	{
		string text = s.Str("mods_dir");
		if (text == null || text.Length <= 0)
		{
			return Path.Combine(Paths.DataDir, "mods");
		}
		return Paths.Expand(text);
	}

	public static string PatchesDir(LauncherSettings s)
	{
		string text = s.Str("patches_dir");
		if (text == null || text.Length <= 0)
		{
			return Path.Combine(Paths.DataDir, "patches");
		}
		return Paths.Expand(text);
	}

	public static string Fps(LauncherSettings s)
	{
		bool flag;
		switch (s.Str("fps_mode"))
		{
		case "30":
		case "90":
		case "120":
		case "144":
		case "uncap":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return "60";
		}
		return s.Str("fps_mode");
	}

	public static Dictionary<string, string> Build(LauncherSettings s)
	{
		string text = Fps(s);
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		dictionary["BB_PREBUILT"] = "1";
		dictionary["BB_DATA_DIR"] = Paths.DataDir;
		dictionary["BB_CONFIG"] = Paths.IniPath;
		dictionary["BB_GAME_DIR"] = GameDir(s);
		dictionary["BB_USER_DIR"] = UserDir(s);
		dictionary["BB_MODS_DIR"] = ModsDir(s);
		dictionary["BB_MODS_CONFIG"] = Path.Combine(Paths.DataDir, "mods.json");
		dictionary["BB_MODS_ENABLED"] = (s.Bool("mods_enabled") ? "1" : "0");
		dictionary["BB_PATCHES_DIR"] = PatchesDir(s);
		dictionary["BB_PATCHES_CONFIG"] = Path.Combine(Paths.DataDir, "patches.json");
		dictionary["BB_LANGUAGE"] = s.Str("language");
		dictionary["BB_FPS"] = text;
		dictionary["BB_FPS_LIMIT"] = ((text == "uncap") ? "0" : text);
		Dictionary<string, string> dictionary2 = dictionary;
		string value;
		switch (text)
		{
		case "90":
			value = "90";
			break;
		case "30":
		case "60":
			value = "60";
			break;
		default:
			value = "0";
			break;
		}
		dictionary2["BB_VBLANK_HZ"] = value;
		dictionary["PYTHONUNBUFFERED"] = "1";
		Dictionary<string, string> dictionary3 = dictionary;
		dictionary3["BB_DLC"] = (s.Bool("dlc_enabled") ? "1" : "0");
		if (s.Bool("dlc_enabled"))
		{
			string text2 = DlcDir(s);
			if (text2 != null && text2.Length > 0)
			{
				dictionary3["BB_DLC_DIR"] = text2;
			}
		}
		string regionPatchOnline = RegionPatchOnline;
		if (regionPatchOnline != null)
		{
			dictionary3["BB_REGION_PATCH_ONLINE"] = regionPatchOnline;
		}
		string resolutionPatchChoice = ResolutionPatchChoice;
		if (resolutionPatchChoice != null)
		{
			dictionary3["BB_RESOLUTION_PATCH"] = resolutionPatchChoice;
		}
		if (s.Str("present_mode").Length > 0)
		{
			dictionary3["BB_PRESENT_MODE"] = s.Str("present_mode");
		}
		if (s.Bool("frame_stats"))
		{
			dictionary3["BB_FRAME_STATS"] = "1";
		}
		if (s.Bool("fsr_profile"))
		{
			dictionary3["BB_FSR4_PROFILE"] = "1";
		}
		if (s.Bool("audio_stats"))
		{
			dictionary3["BB_AUDIO_STATS"] = "1";
		}
		if (s.Bool("load_profile"))
		{
			dictionary3["BB_SAMPLE_THREADS"] = "load";
		}
		if (s.Bool("fsr4_balanced"))
		{
			dictionary3["BB_FSR4_BALANCED"] = "1";
		}
		dictionary3["BB_PRECISE_PACING"] = (s.Bool("exp_frame_pacing") ? "1" : "0");
		dictionary3["BB_IMAGE_WRITEBACK_GC"] = (s.Bool("exp_vram_cleanup") ? "1" : "0");
		foreach (var item3 in SdlHints())
		{
			string item = item3.Key;
			string item2 = item3.Value;
			dictionary3[item] = item2;
		}
		if (!s.Bool("exp_vram_budget"))
		{
			dictionary3["BB_GC_PRESSURE_PERCENT"] = "0";
			dictionary3["BB_GC_PRESSURE_SECONDS"] = "0";
		}
		if (!s.Bool("exp_thread_priority"))
		{
			dictionary3["BB_GPU_THREAD_PRIORITY"] = "0";
		}
		if (!s.Bool("exp_sleep_spin"))
		{
			dictionary3["BB_SLEEP_SPIN_US"] = "600";
		}
		if (!s.Bool("exp_async_pipelines"))
		{
			dictionary3["BB_ASYNC_PIPELINES"] = "0";
		}
		if (!s.Bool("exp_occlusion"))
		{
			dictionary3["BB_OCCLUSION"] = "0";
		}
		if (!s.Bool("exp_fast_draws"))
		{
			dictionary3["BB_SUBMIT_END_TASKS"] = "0";
			dictionary3["BB_MEMO_REVALIDATE"] = "0";
			dictionary3["BB_SAMPLER_MEMO"] = "0";
			dictionary3["BB_DRAIN_SLEEP"] = "0";
			dictionary3["BB_FAULT_SKIP_TEXTURES"] = "0";
		}
		if (!s.Bool("audio_priority") || s.Bool("safe_mode_next"))
		{
			dictionary3["BB_AUDIO_THREAD_PRIORITY"] = "0";
		}
		string obj = (s.Bool("safe_mode_next") ? "" : s.Str("sound_output"));
		if (obj.StartsWith("directsound"))
		{
			dictionary3["SDL_AUDIO_DRIVER"] = "directsound";
		}
		if (obj == "directsound_large")
		{
			dictionary3["SDL_AUDIO_DEVICE_SAMPLE_FRAMES"] = "2048";
		}
		if (s.Bool("exp_fast_loading"))
		{
			dictionary3["BB_FAST_LOADING"] = "1";
		}
		if (s.Bool("exp_hot_pages"))
		{
			dictionary3["BB_HOT_PAGES"] = "2";
		}
		if (s.Bool("exp_sfx_workaround"))
		{
			dictionary3["BB_SFX_WORKAROUND"] = "1";
		}
		if (s.Bool("exp_camera_easing"))
		{
			dictionary3["BB_CAMERA_FIX"] = "1";
		}
		if (s.Bool("borderless_fullscreen"))
		{
			dictionary3["BB_EXCLUSIVE_FULLSCREEN"] = "0";
		}
		if (!s.Bool("exp_compile_skip"))
		{
			dictionary3["BB_SKIP_UNCOMPILED"] = "0";
			dictionary3["BB_LATE_PIPELINE_BIND"] = "0";
			dictionary3["BB_PIPELINE_TAKE"] = "0";
		}
		if (s.Bool("section_stats"))
		{
			dictionary3["BB_SECTIONS"] = "1";
		}
		if (s.Bool("safe_mode_next"))
		{
			dictionary3.Remove("BB_FAST_LOADING");
			dictionary3.Remove("BB_HOT_PAGES");
			dictionary3.Remove("BB_CAMERA_FIX");
			dictionary3["BB_PRECISE_PACING"] = "0";
			dictionary3["BB_IMAGE_WRITEBACK_GC"] = "0";
			dictionary3["BB_GC_PRESSURE_PERCENT"] = "0";
			dictionary3["BB_GC_PRESSURE_SECONDS"] = "0";
			dictionary3["BB_GPU_THREAD_PRIORITY"] = "0";
			dictionary3["BB_SLEEP_SPIN_US"] = "600";
			dictionary3["BB_ASYNC_PIPELINES"] = "0";
			dictionary3["BB_OCCLUSION"] = "0";
			dictionary3["BB_SUBMIT_END_TASKS"] = "0";
			dictionary3["BB_MEMO_REVALIDATE"] = "0";
			dictionary3["BB_SAMPLER_MEMO"] = "0";
			dictionary3["BB_DRAIN_SLEEP"] = "0";
			dictionary3["BB_FAULT_SKIP_TEXTURES"] = "0";
			dictionary3["BB_SKIP_UNCOMPILED"] = "0";
			dictionary3["BB_LATE_PIPELINE_BIND"] = "0";
			dictionary3["BB_PIPELINE_TAKE"] = "0";
			dictionary3["BB_MODS_ENABLED"] = "0";
		}
		string huntersDreamToken = s.HuntersDreamToken;
		if (huntersDreamToken != null && huntersDreamToken.Length > 0)
		{
			string text3 = s.Str("hd_name");
			if (text3 != null && text3.Length > 0)
			{
				dictionary3["BB_THD_TOKEN"] = huntersDreamToken;
				dictionary3["BB_THD_ONLINE_ID"] = text3;
			}
		}
		// The BBSC local server: the host's shadNet over Tailscale and the local game server.
		IniFile ini = Workspace.Current.Ini;
		if (Bbsc.Selected(s) && ini.Get("online_enabled") == "1")
		{
			Bbsc.ApplyEnvironment(s, ini, dictionary3);
		}
		dictionary3["BB_CLIENT_VERSION"] = "shadPS4 Awakenchan/v0.20.0";
		string[] array = s.Str("extra_env").Split(' ', StringSplitOptions.RemoveEmptyEntries);
		foreach (string text4 in array)
		{
			int num = text4.IndexOf('=');
			if (num > 0)
			{
				string key = text4.Substring(0, num);
				value = text4;
				int num2 = num + 1;
				dictionary3[key] = value.Substring(num2, value.Length - num2);
			}
		}
		WindowsMonitors.ApplyEnvironment(s.Str("display_device"), WindowsMonitors.All(), dictionary3);
		return dictionary3;
	}

	public static IEnumerable<(string Key, string Value)> SdlHints()
	{
		yield return (Key: "SDL_JOYSTICK_HIDAPI_PS3", Value: "1");
		yield return (Key: "SDL_JOYSTICK_HIDAPI_PS3_SIXAXIS_DRIVER", Value: "1");
		string text = Path.Combine(Paths.EngineDir, "gamecontrollerdb.txt");
		if (File.Exists(text))
		{
			yield return (Key: "SDL_GAMECONTROLLERCONFIG_FILE", Value: text);
		}
	}
}
