using System;
using System.Collections.Generic;
using System.Globalization;

namespace BloodborneLauncher.Core;

public static class Choices
{
	public static readonly Choice[] Upscalers = new Choice[6]
	{
		new Choice("FSR 4", "fsr4"),
		new Choice("FSR 4.1.1", "fsr411"),
		new Choice("FSR 3", "fsr3"),
		new Choice("DLSS (NVIDIA RTX)", "dlss"),
		new Choice("TAA (native anti-aliasing)", "taa"),
		new Choice("Off", "off")
	};

	public static readonly Choice[] Presets = new Choice[5]
	{
		new Choice("Native AA (no upscaling; DLAA with DLSS)", "0"),
		new Choice("Quality (x1.5)", "1"),
		new Choice("Balanced (x1.7)", "2"),
		new Choice("Performance (x2)", "3"),
		new Choice("Ultra Performance (x3)", "4")
	};

	public static readonly Choice[] OutputResolutions = new Choice[13]
	{
		new Choice("1280 × 720 (Steam Deck)", "1280x720"),
		new Choice("1600 × 900", "1600x900"),
		new Choice("1920 × 1080", "1920x1080"),
		new Choice("2560 × 1440", "2560x1440"),
		new Choice("3840 × 2160", "3840x2160"),
		new Choice("2560 × 1080 (21:9)", "2560x1080"),
		new Choice("3440 × 1440 (21:9)", "3440x1440"),
		new Choice("3840 × 1600 (24:10)", "3840x1600"),
		new Choice("5120 × 2160 (21:9)", "5120x2160"),
		new Choice("3840 × 1080 (32:9)", "3840x1080"),
		new Choice("5120 × 1440 (32:9)", "5120x1440"),
		new Choice("7680 × 2160 (32:9)", "7680x2160"),
		new Choice("Display (auto)", "auto", "Your display's own resolution, whatever its shape: the picture fills the screen")
	};

	public static readonly Effect[] Effects = new Effect[10]
	{
		new Effect("effect_chromatic_aberration", "Chromatic aberration", Default: false),
		new Effect("effect_dof", "Depth of field", Default: false),
		new Effect("effect_motion_blur", "Motion blur", Default: false),
		new Effect("effect_ssao", "Ambient occlusion (SSAO)", Default: true),
		new Effect("effect_game_aa", "Game's own anti-aliasing", Default: false),
		new Effect("effect_dynamic_shadows", "Shadows from dynamic lights", Default: true),
		new Effect("effect_ssr", "Screen-space reflections", Default: false, "Not in the original game"),
		new Effect("skip_intro", "Skip intro videos", Default: false),
		new Effect("debug_camera", "Free camera", Default: false, "Toggle with Cross + L3 / Space + Z"),
		new Effect("debug_menu", "Debug menu", Default: false, "Left touchpad / Tab. Needs the adhoc folder from Nexus mod #253 (adhoc/font) in the game's dvdroot_ps4 or as a mod; without it the patch is not applied")
	};

	public static readonly Choice[] CameraTuning = new Choice[2]
	{
		new Choice("Original (the game's camera)", "original", "As the game has it, at any frame rate"),
		new Choice("FPS++ retune", "fps++", "Kyo's 60 FPS++ camera: further back and slower to follow")
	};

	public static readonly Choice[] ModelLod = new Choice[4]
	{
		new Choice("As in the game", "0"),
		new Choice("Highest (-2)", "-2"),
		new Choice("Lower (1)", "1"),
		new Choice("Lowest (2)", "2")
	};

	public static readonly Choice[] FpsModes = new Choice[6]
	{
		new Choice("60 FPS", "60"),
		new Choice("90 FPS", "90", "Includes the sprint fix for high frame rates"),
		new Choice("120 FPS", "120", "The 90 FPS patch made for 120"),
		new Choice("144 FPS", "144", "The 90 FPS patch made for 144"),
		new Choice("Uncapped", "uncap", "The community uncapped patch, no limit. It can freeze at the end of a boss intro (Vicar Amelia's); 120 and 144 use another patch"),
		new Choice("30 FPS (as on PS4)", "30")
	};

	public static readonly Choice[] MenuLanguages = new Choice[9]
	{
		new Choice("English", "en"),
		new Choice("Español", "es"),
		new Choice("Português (Brasil)", "pt_BR"),
		new Choice("Français", "fr"),
		new Choice("Deutsch", "de"),
		new Choice("Italiano", "it"),
		new Choice("Polski", "pl"),
		new Choice("Türkçe", "tr"),
		new Choice("Русский", "ru")
	};

	public static readonly Choice[] PresentModes = new Choice[4]
	{
		new Choice("Mailbox", "Mailbox", "Low latency without tearing"),
		new Choice("FIFO (VSync)", "Fifo", "Classic vertical sync"),
		new Choice("FIFO Relaxed", "FifoRelaxed", "VSync that tears instead of stuttering when a frame is late"),
		new Choice("Immediate", "Immediate", "Lowest latency; may tear")
	};

	public static readonly Choice[] DrawPipe = new Choice[3]
	{
		new Choice("Auto", "", "On with 8 or more CPU threads"),
		new Choice("On", "1"),
		new Choice("Off", "0", "More stable, but slower")
	};

	public static readonly Choice[] GameLanguages = new Choice[7]
	{
		new Choice("English", "1"),
		new Choice("Russian", "8"),
		new Choice("Japanese", "0"),
		new Choice("French", "2"),
		new Choice("Spanish", "3"),
		new Choice("German", "4"),
		new Choice("Italian", "5")
	};

	public static readonly Choice[] HdrPaperWhite = new Choice[6]
	{
		new Choice("100 nits", "100"),
		new Choice("150 nits", "150"),
		new Choice("200 nits (default)", "200"),
		new Choice("250 nits", "250"),
		new Choice("300 nits", "300"),
		new Choice("400 nits", "400")
	};

	public static readonly Choice[] HdrPeak = new Choice[7]
	{
		new Choice("400 nits", "400"),
		new Choice("600 nits", "600"),
		new Choice("800 nits", "800"),
		new Choice("1000 nits (default)", "1000"),
		new Choice("1500 nits", "1500"),
		new Choice("2000 nits", "2000"),
		new Choice("4000 nits", "4000")
	};

	public static readonly Choice[] ButtonPrompts = new Choice[3]
	{
		new Choice("Automatic (default)", "auto"),
		new Choice("Keyboard", "keyboard"),
		new Choice("Controller", "controller")
	};

	public static readonly Choice[] PadIcons = new Choice[3]
	{
		new Choice("Automatic (default)", "auto"),
		new Choice("PlayStation", "playstation"),
		new Choice("Xbox", "xbox")
	};

	public static readonly Choice[] HdrMode = new Choice[2]
	{
		new Choice("Native (default)", "native"),
		new Choice("Expanded", "expand")
	};

	public static readonly Choice[] HdrBoost = new Choice[4]
	{
		new Choice("Off", "0"),
		new Choice("Low (default)", "0.25"),
		new Choice("Medium", "0.5"),
		new Choice("High", "1")
	};

	public static readonly Choice[] HdrUiBrightness = new Choice[4]
	{
		new Choice("Dimmer", "0.75"),
		new Choice("Paper white (default)", "1"),
		new Choice("Brighter", "1.25"),
		new Choice("Much brighter", "1.5")
	};

	public static readonly Choice[] HdrUiColor = new Choice[3]
	{
		new Choice("Exact (sRGB)", "0"),
		new Choice("In between", "0.5"),
		new Choice("Vivid (default)", "1")
	};

	public static readonly Choice[] OnlineServers = new Choice[1]
	{
		new Choice("The Hunter's Dream", "huntersdream")
	};

	public static readonly Choice[] SignTimeout = new Choice[3]
	{
		new Choice("30 seconds (default)", "30"),
		new Choice("60 seconds", "60"),
		new Choice("The game's own (180 seconds)", "0")
	};

	public static readonly Choice[] LiveResolution = new Choice[3]
	{
		new Choice("Auto (by GPU)", "auto"),
		new Choice("Off (faster)", "0"),
		new Choice("On", "1")
	};

	public static readonly Choice[] Readbacks = new Choice[3]
	{
		new Choice("Relaxed", "", "Default"),
		new Choice("Off", "0"),
		new Choice("Precise", "2")
	};

	public static readonly Choice[] SoundOutput = new Choice[3]
	{
		new Choice("WASAPI", "", "Default"),
		new Choice("DirectSound", "directsound"),
		new Choice("DirectSound, larger buffer", "directsound_large")
	};

	public static readonly Choice[] LaunchMethods = new Choice[2]
	{
		new Choice("Bloodborne PC engine", "windows", "Prepares mods and patches, then starts the engine (out\\bb-probe.exe)"),
		new Choice("Custom command", "custom", "Runs a program of your choice with the settings as environment variables")
	};

	public static (int W, int H) OutputSize(string value)
	{
		if (value == "auto")
		{
			Hardware hardware = Hardware.Detect();
			if (hardware.ScreenWidth < 640 || hardware.ScreenHeight < 360)
			{
				return (W: 1920, H: 1080);
			}
			return (W: hardware.ScreenWidth & -2, H: hardware.ScreenHeight & -2);
		}
		string[] array = value.Split('x');
		if (array.Length != 2 || !int.TryParse(array[0], out var result) || !int.TryParse(array[1], out var result2))
		{
			return (W: 1920, H: 1080);
		}
		return (W: result, H: result2);
	}

	public static int IndexOf(IReadOnlyList<Choice> choices, string value)
	{
		for (int i = 0; i < choices.Count; i++)
		{
			if (choices[i].Value == value)
			{
				return i;
			}
		}
		if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			for (int j = 0; j < choices.Count; j++)
			{
				if (double.TryParse(choices[j].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) && Math.Abs(result2 - result) < 1E-06)
				{
					return j;
				}
			}
		}
		return 0;
	}
}
