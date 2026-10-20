using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BloodborneLauncher.Core;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public sealed class DisplayPage : LauncherPage
{
	private readonly ComboBox _monitor = new ComboBox
	{
		Width = 360.0,
		VerticalAlignment = VerticalAlignment.Center
	};

	private bool _refreshingMonitors;

	public override void OnShown()
	{
		RefreshMonitors();
	}

	private void RefreshMonitors()
	{
		_refreshingMonitors = true;
		try
		{
			string saved = LauncherPage.S.Str("display_device");
			_monitor.Items.Clear();
			_monitor.Items.Add(new ComboBoxItem
			{
				Content = "Default (Windows primary monitor)",
				Tag = ""
			});
			foreach (WindowsMonitors.Monitor item in WindowsMonitors.All())
			{
				_monitor.Items.Add(new ComboBoxItem
				{
					Content = item.Label,
					Tag = item.Device,
					ToolTip = $"{item.Width} × {item.Height}; {item.Device}"
				});
			}
			ComboBoxItem comboBoxItem = _monitor.Items.Cast<ComboBoxItem>().FirstOrDefault((ComboBoxItem i) => string.Equals(i.Tag as string, saved, StringComparison.OrdinalIgnoreCase));
			if (comboBoxItem == null && saved.Length > 0)
			{
				comboBoxItem = new ComboBoxItem
				{
					Content = "Disconnected monitor (" + saved + ")",
					Tag = saved
				};
				_monitor.Items.Add(comboBoxItem);
			}
			_monitor.SelectedItem = comboBoxItem ?? _monitor.Items[0];
		}
		finally
		{
			_refreshingMonitors = false;
		}
	}

	public DisplayPage()
		: base("Display", "Resolution, window mode, frame rate and menu language. Applied when the game starts.")
	{
		_monitor.SetResourceReference(FrameworkElement.StyleProperty, "DefaultComboBoxStyle");
		Group("Language");
		ComboRow(SymbolRegular.LocalLanguage24, "In-game menu language", "The port's settings menu (Insert or L3 + R3)", Choices.MenuLanguages, () => LauncherPage.Ini.Get("menu_language", "en"), delegate(string v)
		{
			LauncherPage.Ini.Set("menu_language", v);
		});
		SettingCombo(SymbolRegular.Translate24, "Game language", "The game's own text and voices", Choices.GameLanguages, "language");
		Group("Screen");
		Add(LauncherPage.Card(SymbolRegular.Desktop24, "Monitor", "The game opens here from the start, including fullscreen and HDR. If this monitor is disconnected, the Windows primary monitor is used. Applied on the next launch.", _monitor, out System.Windows.Controls.TextBlock _));
		RefreshMonitors();
		_monitor.SelectionChanged += delegate
		{
			if (!_refreshingMonitors && _monitor.SelectedItem is ComboBoxItem comboBoxItem)
			{
				LauncherPage.S.Set("display_device", (comboBoxItem.Tag as string) ?? "");
				LauncherPage.Ws.SaveSoon();
			}
		};
		IniCombo(SymbolRegular.ResizeLarge24, "Output resolution", "The upscaler renders below this and fills it in", Choices.OutputResolutions, "output_res");
		IniCombo(SymbolRegular.ArrowSync24, "Live resolution change", "Change the resolution in the game menu without restarting. Off is faster; turn on only if you switch resolutions in game", Choices.LiveResolution, "live_resolution");
		IniToggle(SymbolRegular.FullScreenMaximize24, "Fullscreen", "The game covers the whole screen", "fullscreen");
		SettingToggle(SymbolRegular.WindowNew24, "Borderless fullscreen", "Keeps fullscreen a borderless window, so trophy notifications and other windows show on top. Off: the driver may switch to exclusive fullscreen (lowest latency, G-SYNC fullscreen-only mode), which hides them", "borderless_fullscreen");
		ToggleRow(SymbolRegular.RatioOneToOne24, "Widescreen", "21:9 and 32:9 outputs (3440 × 1440, 5120 × 1440) with the right field of view", () => LauncherPage.Ini.Get("widescreen", "1") != "0", delegate(bool v)
		{
			LauncherPage.Ini.Set("widescreen", v ? "1" : "0");
		});
		SettingCombo(SymbolRegular.Image24, "Presentation mode", null, Choices.PresentModes, "present_mode");
		Group("HDR");
		IniToggle(SymbolRegular.Hdr24, "HDR output", WindowsHdr.AnyOn() ? "The game's picture on an HDR screen, with brighter highlights. Applied when the game starts" : (WindowsHdr.AnySupported() ? "Turn on Use HDR in Windows' display settings first (Settings › System › Display)" : "This display shows no HDR"), "hdr");
		ComboRow(SymbolRegular.Lightbulb24, "HDR source", "Native: highlights as bright as they are in the game's scene. Expanded: the SDR picture's bright parts made brighter", Choices.HdrMode, () => LauncherPage.Ini.Get("hdr_mode", "native"), delegate(string v)
		{
			LauncherPage.Ini.Set("hdr_mode", v);
		});
		SliderRow(SymbolRegular.BrightnessHigh24, "Paper white", "How bright the game's white is, in nits. Range: 10–500", 10.0, 500.0, 1.0, () => LauncherPage.ParseDouble(LauncherPage.Ini.Get("hdr_paper_white", "200"), 200.0), delegate(double v)
		{
			LauncherPage.Ini.Set("hdr_paper_white", v.ToString("0", CultureInfo.InvariantCulture));
		}, "0' nits'", null, 82.0);
		SliderRow(SymbolRegular.WeatherSunny24, "Peak brightness", "Your display's peak brightness, in nits. Range: 10–4000", 10.0, 4000.0, 10.0, () => LauncherPage.ParseDouble(LauncherPage.Ini.Get("hdr_peak", "1000"), 1000.0), delegate(double v)
		{
			LauncherPage.Ini.Set("hdr_peak", v.ToString("0", CultureInfo.InvariantCulture));
		}, "0' nits'", null, 82.0);
		ComboRow(SymbolRegular.Sparkle24, "Highlight boost", "Off keeps the game's own look; higher makes lamps, fire and the sky brighter", Choices.HdrBoost, () => LauncherPage.Ini.Get("hdr_boost", "0.25"), delegate(string v)
		{
			LauncherPage.Ini.Set("hdr_boost", v);
		});
		ComboRow(SymbolRegular.TextFont24, "UI brightness", "Native source only: how bright the HUD, menus and subtitles are, against paper white", Choices.HdrUiBrightness, () => LauncherPage.Ini.Get("hdr_ui_brightness", "1"), delegate(string v)
		{
			LauncherPage.Ini.Set("hdr_ui_brightness", v);
		});
		ComboRow(SymbolRegular.Color24, "UI colour", "Native source only: Vivid shows the HUD and menus with the colours a wide-gamut monitor gives SDR; Exact keeps sRGB's", Choices.HdrUiColor, () => LauncherPage.Ini.Get("hdr_ui_color", "1"), delegate(string v)
		{
			LauncherPage.Ini.Set("hdr_ui_color", v);
		});
		Group("Frame rate");
		SettingCombo(SymbolRegular.Gauge24, "Frame rate", "30, 60, 90, 120 and 144 are the game's frame-rate patches for that rate; Uncapped uses the community uncapped patch", Choices.FpsModes, "fps_mode");
	}
}
