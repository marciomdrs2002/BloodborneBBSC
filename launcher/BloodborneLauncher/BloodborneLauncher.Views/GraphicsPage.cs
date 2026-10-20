using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BloodborneLauncher.Core;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public sealed class GraphicsPage : LauncherPage
{
	private readonly System.Windows.Controls.TextBlock _upscalerHint;

	private readonly System.Windows.Controls.TextBlock _dlssHint;

	private readonly SettingRow _presetCard;

	private readonly SettingRow _sharpenCard;

	private readonly SettingRow _sharpnessCard;

	public GraphicsPage()
		: base("Graphics", "Stored in bbport.ini; the upscaler can also be changed in game (Insert or L3 + R3).")
	{
		Add(LauncherPage.Card(SymbolRegular.Gauge24, "Best settings for this PC", Hardware.Detect().Summary, LauncherPage.SmallButton("Recommend…", SymbolRegular.Sparkle24, delegate
		{
			MainWindow.Instance.OfferBestSettings(firstRun: false);
		}), out System.Windows.Controls.TextBlock _));
		Group("Upscaler");
		bool flag = Fsr4VkFound() || Directory.Exists(Fsr411Dir());
		if (!flag && LauncherPage.Ini.Get("upscaler") == "fsr411")
		{
			LauncherPage.Ini.Set("upscaler", "fsr4");
		}
		IniCombo(SymbolRegular.Sparkle24, "Upscaler", null, flag ? ((IReadOnlyList<Choice>)Choices.Upscalers) : ((IReadOnlyList<Choice>)Choices.Upscalers.Where((Choice c) => c.Value != "fsr411").ToArray()), "upscaler", null, UpdateUpscaler);
		UIElementCollection children = base.Body.Children;
		_upscalerHint = (System.Windows.Controls.TextBlock)((SettingRow)children[children.Count - 1]).Tag;
		IniCombo(SymbolRegular.ZoomIn24, "Quality preset", "How far below the output resolution the game renders", Choices.Presets, "preset", null, UpdateUpscaler);
		UIElementCollection children2 = base.Body.Children;
		_presetCard = (SettingRow)children2[children2.Count - 1];
		IniToggle(SymbolRegular.Eyedropper24, "Sharpening (RCAS)", "Works with every option above, TAA included: turn it up if the picture looks soft", "sharpen");
		UIElementCollection children3 = base.Body.Children;
		_sharpenCard = (SettingRow)children3[children3.Count - 1];
		SliderRow(SymbolRegular.SlideSize24, "Sharpening strength", "Up to 1: the upscaler's own sharpening. Above 1: an extra pass", 0.0, 2.0, 0.05, () => LauncherPage.ParseDouble(LauncherPage.Ini.Get("sharpness"), 0.5), delegate(double v)
		{
			LauncherPage.Ini.Set("sharpness", LauncherPage.Fmt(v));
		});
		UIElementCollection children4 = base.Body.Children;
		_sharpnessCard = (SettingRow)children4[children4.Count - 1];
		IniToggle(SymbolRegular.ArrowMove24, "Object motion vectors", "Less ghosting on characters; costs about 10% FPS", "object_motion");
		IniToggle(SymbolRegular.DataUsage24, "Show FPS", null, "show_fps");
		(SymbolRegular, string, string)[] array = new(SymbolRegular, string, string)[5]
		{
			(SymbolRegular.TopSpeed24, "show_fps_rate", "Frame rate"),
			(SymbolRegular.Timer24, "show_fps_frame_time", "Frame time"),
			(SymbolRegular.ResizeLarge24, "show_fps_upscaler", "Upscaler"),
			(SymbolRegular.DeveloperBoard24, "show_fps_cpu", "CPU usage"),
			(SymbolRegular.Gauge24, "show_fps_gpu", "GPU usage")
		};
		for (int num = 0; num < array.Length; num++)
		{
			(SymbolRegular, string, string) tuple = array[num];
			SymbolRegular item = tuple.Item1;
			string key = tuple.Item2;
			string item2 = tuple.Item3;
			string text = key;
			bool flag2 = ((text == "show_fps_cpu" || text == "show_fps_gpu") ? true : false);
			string fallback = (flag2 ? "0" : "1");
			ToggleRow(item, item2, "Shown in the FPS counter", () => LauncherPage.Ini.Get(key, fallback) == "1", delegate(bool v)
			{
				LauncherPage.Ini.Set(key, v ? "1" : "0");
			});
			UIElementCollection children5 = base.Body.Children;
			((FrameworkElement)children5[children5.Count - 1]).Margin = new Thickness(24.0, 0.0, 0.0, 4.0);
		}
		Add(LauncherPage.Card(SymbolRegular.DeveloperBoard24, "DLSS", "", null, out _dlssHint));
		Group("Picture");
		StackPanel stackPanel = new StackPanel();
		IniToggle(SymbolRegular.Color24, "Picture tuning", "Turns the settings below on, live. Also in the game's System menu (PC Graphics, PC Effects, PC Colour) and the Insert menu", "graphics_controls");
		IniToggle(SymbolRegular.CircleHalfFill24, "Vignette", null, "graphics_vignette", stackPanel);
		IniSlider(SymbolRegular.DarkTheme24, "Ambient occlusion strength", 0.0, 3.0, "graphics_ao_strength", stackPanel);
		IniSlider(SymbolRegular.WeatherSunny24, "Shadow distance", 0.5, 4.0, "graphics_shadow_scale", stackPanel);
		IniSlider(SymbolRegular.Lightbulb24, "Bloom", 0.0, 3.0, "graphics_bloom", stackPanel);
		IniSlider(SymbolRegular.Color24, "Saturation", 0.0, 3.0, "graphics_saturation", stackPanel);
		IniSlider(SymbolRegular.BrightnessHigh24, "Brightness", 0.0, 3.0, "graphics_brightness", stackPanel);
		IniSlider(SymbolRegular.DarkTheme24, "Contrast", 0.8, 1.2, "graphics_contrast", stackPanel, 0.01);
		IniSlider(SymbolRegular.Color24, "Hue", -180.0, 180.0, "graphics_hue", stackPanel, 1.0, 0.0);
		IniSlider(SymbolRegular.WeatherFog24, "Fog", 0.0, 3.0, "graphics_fog", stackPanel);
		base.Body.Children.Add(stackPanel);
		Group("Camera");
		IniToggle(SymbolRegular.Camera24, "Camera tuning", "Turns the three settings below on", "camera_controls");
		IniSlider(SymbolRegular.ZoomOut24, "Field of view", 1.0, 1.5, "camera_fov_scale");
		IniSlider(SymbolRegular.ArrowMaximize24, "Distance", 0.5, 1.5, "camera_distance_scale");
		IniSlider(SymbolRegular.ArrowBidirectionalUpDown24, "Height", 0.5, 1.5, "camera_height_scale");
		Group("Experimental");
		SettingToggle(SymbolRegular.Broom24, "VRAM cleanup", "Frees video memory held by textures the GPU drew, so long sessions don't fill up VRAM and slow down. Recommended: on", "exp_vram_cleanup");
		SettingToggle(SymbolRegular.Timer24, "Precise frame pacing", "A high-resolution timer and even spacing between frames: steadier frame times and less VRR flicker. Uses a little CPU. Off by default", "exp_frame_pacing");
		SettingToggle(SymbolRegular.Storage24, "Use more VRAM for textures", "Keeps textures in video memory up to 80% of the card instead of about 40%, so they aren't thrown out and loaded again (the cause of most stutters on 8 GB cards). On by default in betas while it is tested", "exp_vram_budget");
		SettingToggle(SymbolRegular.TopSpeed24, "Graphics threads first", "Runs the engine's graphics threads at a higher CPU priority than the game's other threads, so a frame isn't held up behind them. Off by default", "exp_thread_priority");
		SettingToggle(SymbolRegular.Sleep24, "Shorter sleep spin", "The game's many short sleeps busy-wait only as long as the timer needs (about 0.2–0.4 ms instead of 0.6 ms), leaving that CPU time to the graphics threads. Off by default", "exp_sleep_spin");
		SettingToggle(SymbolRegular.FlashFlow24, "Background shader compiling", "New shaders compile on helper threads instead of freezing the frame: much shorter hitches when you reach a new area. On by default", "exp_async_pipelines");
		SettingToggle(SymbolRegular.EyeOff24, "Occlusion culling", "The game skips objects hidden behind walls instead of drawing them all: much higher FPS in dense areas. Turn it off if lamps or glare flicker", "exp_occlusion");
		SettingToggle(SymbolRegular.FastForward24, "Faster draw thread", "Remembers texture and sampler lookups and hands the end of each frame to the draw thread: higher FPS in busy areas. New: turn it off if the game hangs or crashes", "exp_fast_draws");
		SettingToggle(SymbolRegular.Shapes24, "No shader freezes", "While a new shader compiles, the objects that need it appear a moment later instead of the game freezing. New: turn it off if something stays missing", "exp_compile_skip");
		SettingToggle(SymbolRegular.ArrowForward24, "Fast loading", "Loading screens run at 30 FPS: fewer frames leave the CPU to the loading work itself. Not before cutscenes. New: off by default while it is tested", "exp_fast_loading");
		SettingToggle(SymbolRegular.Flash24, "Fewer memory write checks", "Memory the game rewrites before almost every use is no longer watched for changes, saving up to about 2 ms a frame in busy areas. New: off by default while it is tested", "exp_hot_pages");
		SettingToggle(SymbolRegular.ShieldCheckmark24, "Effect trail crash fix", "For crashes while drawing effect trails on newer Intel CPUs (Witch of Hemwick, for example): stops the crash, and that kind of trail is not drawn. Off by default", "exp_sfx_workaround");
		SettingToggle(SymbolRegular.Camera24, "Camera easing for high FPS", "With the FPS++ follow camera only: its eases converted for 60 FPS and above. Off by default", "exp_camera_easing");
		SettingToggle(SymbolRegular.Run24, "Sharper motion (FSR 4)", "Uses FSR 4's balanced model at Performance and Ultra Performance: fewer trails behind moving characters, a little more GPU time", "fsr4_balanced");
		Group("Game effects");
		base.Body.Children.Add(LauncherPage.Text("Patches to the game, applied when it starts.", "Faint"));
		IniCombo(SymbolRegular.Cube24, "Model detail", null, Choices.ModelLod, "model_lod");
		IniCombo(SymbolRegular.Camera24, "Follow camera", "Original keeps the game's camera distance, angle and follow speed at 60 FPS and above", Choices.CameraTuning, "camera_tuning");
		Effect[] effects = Choices.Effects;
		foreach (Effect effect in effects)
		{
			IniToggle(EffectIcon(effect.Key), effect.Title, effect.Hint, effect.Key);
		}
		LauncherPage.Ws.PortChanged += UpdateUpscaler;
	}

	private void IniSlider(SymbolRegular icon, string title, double min, double max, string key, Panel? into = null, double step = 0.05, double fallback = 1.0)
	{
		SliderRow(icon, title, null, min, max, step, () => LauncherPage.ParseDouble(LauncherPage.Ini.Get(key, LauncherPage.Fmt(fallback)), fallback), delegate(double v)
		{
			LauncherPage.Ini.Set(key, LauncherPage.Fmt(v));
		}, "0.00", into);
	}

	private static SymbolRegular EffectIcon(string key)
	{
		return key switch
		{
			"skip_intro" => SymbolRegular.FastForward24, 
			"debug_camera" => SymbolRegular.Camera24, 
			"debug_menu" => SymbolRegular.Bug24, 
			_ => SymbolRegular.Color24, 
		};
	}

	public override void OnShown()
	{
		UpdateUpscaler();
	}

	private static bool DlssFound()
	{
		return File.Exists(Path.Combine(Paths.BinariesDir, "nvngx_dlss.dll"));
	}

	private void UpdateUpscaler()
	{
		string text = LauncherPage.Ini.Get("upscaler");
		string text2;
		switch (text)
		{
		case "fsr4":
			text2 = (Directory.Exists(Path.Combine(Paths.EngineDir, "fsr4_shaders")) ? "Assets found. GPUs without the needed shader features fall back to FSR 3.1" : "fsr4_shaders is missing from the port folder: the game falls back to FSR 3.1");
			break;
		case "fsr411":
		{
			if (Fsr4VkFound())
			{
				text2 = "Through fsr4vk (included, experimental): runs on any GPU with the needed Vulkan features. Prefer FSR 4 if it looks wrong";
				break;
			}
			string text3 = Fsr411Problem();
			text2 = ((text3 != null) ? (text3 + ": the game falls back to FSR 4") : "Assets for the chosen resolution and preset found");
			break;
		}
		case "dlss":
			text2 = (DlssFound() ? ("Needs an NVIDIA GeForce RTX GPU; others fall back to FSR 3.1" + ((LauncherPage.Ini.Get("preset", "0") == "0") ? ". With Native AA this is DLAA: DLSS as anti-aliasing at full resolution" : "")) : "DLSS files are missing: the game falls back to FSR 3.1");
			break;
		case "taa":
			text2 = "Anti-aliasing at the output resolution, without an FSR model. Softer than DLAA or FSR Native AA: add sharpening below";
			break;
		default:
			text2 = null;
			break;
		}
		string text4 = text2;
		LauncherPage.SetHint(_upscalerHint, text4);
		SettingRow presetCard = _presetCard;
		bool flag = ((text == "taa" || text == "off") ? true : false);
		presetCard.IsEnabled = !flag;
		_sharpenCard.IsEnabled = text != "off";
		_sharpnessCard.IsEnabled = text != "off";
		string fileName = Path.Combine(Paths.BinariesDir, "nvngx_dlss.dll");
		LauncherPage.SetHint(_dlssHint, DlssFound() ? ("NVIDIA DLSS " + FileVersionInfo.GetVersionInfo(fileName).FileVersion + " is included (out\\nvngx_dlss.dll). Choose it as the upscaler above") : "out\\nvngx_dlss.dll is missing from the engine folder");
	}

	private static bool Fsr4VkFound()
	{
		return File.Exists(Path.Combine(Paths.EngineDir, "fsr4vk", "amd_fidelityfx_upscaler_vk.dll"));
	}

	private static string Fsr411Dir()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("BB_FSR411_DIR");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			return environmentVariable;
		}
		string text = Path.Combine(Paths.EngineDir, "fsr4_411");
		if (!Directory.Exists(text))
		{
			return Path.Combine(Paths.DataDir, "fsr4_411");
		}
		return text;
	}

	private static string? Fsr411Problem()
	{
		(int W, int H) tuple = Choices.OutputSize(LauncherPage.Ini.Get("output_res", "1920x1080"));
		int item = tuple.W;
		int item2 = tuple.H;
		string text = ((item > 1920 || item2 > 1080) ? "t2160" : "t1080") + ((LauncherPage.Ini.Get("preset", "4") == "4") ? "_m1" : "_m0");
		List<string> list = new List<string> { "spd", "prepass", "pass0_post" };
		for (int i = 1; i <= 12; i++)
		{
			list.Add($"pass{i}");
			list.Add($"pass{i}_post");
		}
		list.Add("postpass");
		list.Add("rcas");
		string path = Path.Combine(Fsr411Dir(), text);
		foreach (string item3 in list)
		{
			FileInfo fileInfo = new FileInfo(Path.Combine(path, item3 + ".spv"));
			if (!fileInfo.Exists || fileInfo.Length < 20 || fileInfo.Length % 4 != 0L)
			{
				return $"Missing or damaged fsr4_411\\{text}\\{item3}.spv";
			}
		}
		FileInfo fileInfo2 = new FileInfo(Path.Combine(path, "initializer.bin"));
		if (!fileInfo2.Exists || fileInfo2.Length != 131072)
		{
			return "Missing or damaged fsr4_411\\" + text + "\\initializer.bin";
		}
		return null;
	}
}
