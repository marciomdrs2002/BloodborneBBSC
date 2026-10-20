using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BloodborneLauncher.Core;

public sealed class LauncherSettings
{
	public const string OnlineCommunity = "https://thehuntersdream.com";

	public const string Discord = "https://discord.gg/d3Uv3Cfr5x";

	public const string ShadnetServer = "srv.shadps4.net:31313";

	private JsonObject _data = new JsonObject();

	public string OnlinePassword
	{
		get
		{
			return Secret.Unprotect(Str("online_password"));
		}
		set
		{
			string text = Secret.Protect(value);
			Set("online_password", (text != null && text.Length > 0) ? text : value);
		}
	}

	public string HuntersDreamToken
	{
		get
		{
			return Secret.Unprotect(Str("hd_token"));
		}
		set
		{
			object value2;
			if (value.Length != 0)
			{
				string text = Secret.Protect(value);
				value2 = ((text != null && text.Length > 0) ? text : value);
			}
			else
			{
				value2 = "";
			}
			Set("hd_token", (string)value2);
		}
	}

	public IEnumerable<KeyValuePair<string, JsonNode?>> All => _data;

	private static Dictionary<string, JsonNode?> Defaults()
	{
		return new Dictionary<string, JsonNode>
		{
			["ui_language"] = "en",
			["game_dir"] = "",
			["user_dir"] = "",
			["dlc_dir"] = "",
			["player_profile"] = "",
			["player_profiles"] = "",
			["mods_dir"] = "",
			["mods_enabled"] = true,
			["patches_dir"] = "",
			["language"] = "1",
			["fullscreen"] = false,
			["display_device"] = "",
			["present_mode"] = "Mailbox",
			["fps_mode"] = "60",
			["draw_pipe"] = "",
			["readbacks"] = "",
			["frame_stats"] = false,
			["gpu_profile"] = false,
			["vk_validation"] = false,
			["extra_env"] = "",
			["dlc_enabled"] = true,
			["online_password"] = "",
			["port_dir"] = "",
			["launch_method"] = "windows",
			["custom_command"] = "",
			["custom_args"] = "",
			["switch_to_output"] = true,
			["check_updates"] = true,
			["save_backups"] = true,
			["safe_mode_next"] = false,
			["memory_check_off"] = false,
			["discord_hidden"] = false,
			["hardware_offered"] = false,
			["cheat_vials_amount"] = 20,
			["cheat_bullets_amount"] = 20,
			["update_channel"] = "",
			["ui_layout"] = "modern",
			["ui_theme"] = "default",
			["ui_theme_colour"] = "#C41E3A",
			["ui_background"] = "game",
			["ui_background_image"] = "",
			["ui_background_blur"] = "strong",
			["ui_window_buttons"] = "mac",
			["ui_window_buttons_side"] = "usual",
			["fsr4_balanced"] = false,
			["exp_vram_cleanup"] = true,
			["exp_frame_pacing"] = false,
			["exp_thread_priority"] = false,
			["exp_sleep_spin"] = false,
			["exp_async_pipelines"] = true,
			["exp_fast_loading"] = false,
			["exp_hot_pages"] = false,
			["exp_sfx_workaround"] = false,
			["exp_camera_easing"] = false,
			["borderless_fullscreen"] = false,
			["exp_occlusion"] = Updater.CurrentBeta > 0,
			["exp_fast_draws"] = Updater.CurrentBeta > 0,
			["exp_compile_skip"] = Updater.CurrentBeta > 0,
			["exp_vram_budget"] = Updater.CurrentBeta > 0,
			["audio_priority"] = false,
			["sound_output"] = "",
			["load_profile"] = false,
			["occlusion_default_on"] = true,
			["vram_budget_default_on"] = true,
			["section_stats"] = false,
			["trophy_notifications"] = true,
			["trophy_sound"] = true,
			["trophy_volume"] = 70,
			["trophy_position"] = "top-right"
		};
	}

	public void Load()
	{
		_data = new JsonObject();
		string key;
		JsonNode value;
		foreach (KeyValuePair<string, JsonNode> item in Defaults())
		{
			item.Deconstruct(out key, out value);
			string propertyName = key;
			JsonNode value2 = value;
			_data[propertyName] = value2;
		}
		try
		{
			if (!File.Exists(Paths.ConfigFile) || !(JsonNode.Parse(File.ReadAllText(Paths.ConfigFile)) is JsonObject jsonObject))
			{
				return;
			}
			foreach (KeyValuePair<string, JsonNode> item2 in jsonObject)
			{
				item2.Deconstruct(out key, out value);
				string propertyName2 = key;
				JsonNode jsonNode = value;
				_data[propertyName2] = jsonNode?.DeepClone();
			}
			if (Updater.CurrentBeta > 0 && !jsonObject.ContainsKey("occlusion_default_on"))
			{
				_data["exp_occlusion"] = true;
			}
			if (Updater.CurrentBeta > 0 && !jsonObject.ContainsKey("vram_budget_default_on"))
			{
				_data["exp_vram_budget"] = true;
			}
			string text = Str("online_password");
			if (text != null && text.Length > 0 && !Secret.IsProtected(text))
			{
				OnlinePassword = text;
				Save();
			}
		}
		catch
		{
		}
	}

	public void Save()
	{
		Directory.CreateDirectory(Paths.ConfigDir);
		File.WriteAllText(Paths.ConfigFile, _data.ToJsonString(new JsonSerializerOptions
		{
			WriteIndented = true
		}));
	}

	public string Str(string key)
	{
		if (_data[key] is JsonValue jsonValue)
		{
			if (jsonValue.TryGetValue<string>(out string value))
			{
				return value;
			}
			return jsonValue.ToJsonString().Trim('"');
		}
		return "";
	}

	public bool Bool(string key)
	{
		if (_data[key] is JsonValue jsonValue)
		{
			if (jsonValue.TryGetValue<bool>(out var value))
			{
				return value;
			}
			if (jsonValue.TryGetValue<int>(out var value2))
			{
				return value2 != 0;
			}
			if (jsonValue.TryGetValue<string>(out string value3))
			{
				switch (value3)
				{
				case "1":
				case "true":
				case "True":
					return true;
				default:
					return false;
				}
			}
		}
		return false;
	}

	public int Int(string key)
	{
		if (_data[key] is JsonValue jsonValue)
		{
			if (jsonValue.TryGetValue<int>(out var value))
			{
				return value;
			}
			if (jsonValue.TryGetValue<double>(out var value2))
			{
				return (int)value2;
			}
			if (jsonValue.TryGetValue<string>(out string value3) && int.TryParse(value3, out value))
			{
				return value;
			}
		}
		return 0;
	}

	public Dictionary<string, string> Map(string key)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		if (_data[key] is JsonObject jsonObject)
		{
			foreach (var (key2, jsonNode2) in jsonObject)
			{
				if (jsonNode2 is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value))
				{
					dictionary[key2] = value;
				}
			}
		}
		return dictionary;
	}

	public void SetMap(string key, IReadOnlyDictionary<string, string> map)
	{
		JsonObject jsonObject = new JsonObject();
		foreach (KeyValuePair<string, string> item in map.OrderBy<KeyValuePair<string, string>, string>((KeyValuePair<string, string> p) => p.Key, StringComparer.Ordinal))
		{
			item.Deconstruct(out var key2, out var value);
			string propertyName = key2;
			string text = value;
			jsonObject[propertyName] = text;
		}
		_data[key] = jsonObject;
	}

	public void Set(string key, string value)
	{
		_data[key] = value;
	}

	public void SetNode(string key, JsonNode value)
	{
		_data[key] = value.DeepClone();
	}

	public void Set(string key, bool value)
	{
		_data[key] = value;
	}

	public void Set(string key, int value)
	{
		_data[key] = value;
	}
}
