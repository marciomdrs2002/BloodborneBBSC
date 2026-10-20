using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BloodborneLauncher.Build;
using BloodborneLauncher.Core;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public sealed class SettingsPage : LauncherPage
{
	private readonly System.Windows.Controls.TextBlock _portHint;

	private readonly System.Windows.Controls.TextBlock _buildHint;

	private readonly StackPanel _customRows = new StackPanel();

	private readonly System.Windows.Controls.TextBlock _updateHint;

	private readonly Wpf.Ui.Controls.Button _updateNow;

	private readonly ComboBox _versions = new ComboBox
	{
		Width = 300.0,
		VerticalAlignment = VerticalAlignment.Center
	};

	private readonly Wpf.Ui.Controls.Button _installVersion;

	private readonly System.Windows.Controls.TextBlock _versionHint;

	private IReadOnlyList<Updater.Release>? _releases;

	private bool _versionsLoading;

	private readonly StackPanel _themeColour = new StackPanel();

	private readonly StackPanel _picture = new StackPanel();

	private System.Windows.Controls.TextBlock _pictureHint;

	public SettingsPage()
		: base("Settings", "The Bloodborne PC engine, how the game is started, and keeping this launcher up to date.")
	{
		Group("Appearance");
		FrameworkElement layout = null;
		bool reverting = false;
		layout = ComboRow(SymbolRegular.PanelLeft24, "Layout", "Modern: tabs and compact rows. Classic: the sidebar and one card per setting", new Choice[2]
		{
			new Choice("Modern", "modern"),
			new Choice("Classic", "classic")
		}, () => (!(LauncherPage.S.Str("ui_layout") == "classic")) ? "modern" : "classic", delegate(string v)
		{
			LauncherPage.S.Set("ui_layout", v);
		}, null, 240.0, delegate
		{
			if (!reverting && !RestartForLayout())
			{
				reverting = true;
				string text = (SettingRow.Classic ? "classic" : "modern");
				LauncherPage.S.Set("ui_layout", text);
				LauncherPage.Ws.SaveNow();
				int num2 = ((text == "classic") ? 1 : 0);
				if (layout is Segmented segmented)
				{
					segmented.Select(num2, notify: false);
				}
				else if (layout is ComboBox comboBox)
				{
					comboBox.SelectedIndex = num2;
				}
				reverting = false;
			}
		});
		SettingCombo(SymbolRegular.PaintBrush24, "Theme", "The accent colour, and a faint tint of it in the launcher's surfaces", LauncherTheme.Themes, "ui_theme", null, delegate
		{
			ShowAppearance();
			MainWindow.Instance?.ApplyTheme();
		});
		Border swatch = new Border
		{
			Width = 22.0,
			Height = 22.0,
			CornerRadius = new CornerRadius(11.0),
			BorderThickness = new Thickness(1.0),
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		swatch.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
		Wpf.Ui.Controls.TextBox colour = new Wpf.Ui.Controls.TextBox
		{
			Text = LauncherPage.S.Str("ui_theme_colour"),
			PlaceholderText = "#C41E3A",
			PlaceholderEnabled = true,
			Width = 120.0,
			VerticalAlignment = VerticalAlignment.Center
		};
		ShowSwatch();
		colour.TextChanged += delegate
		{
			ShowSwatch();
			if (LauncherTheme.ParseColour(colour.Text).HasValue)
			{
				LauncherPage.S.Set("ui_theme_colour", colour.Text.Trim());
				LauncherPage.Ws.SaveSoon();
				MainWindow.Instance?.ApplyTheme();
			}
		};
		StackPanel stackPanel = LauncherPage.Row(swatch, colour);
		string[] array = new string[10] { "#C41E3A", "#E0643A", "#D99A2B", "#3FA86B", "#2BB3B3", "#4C9DFF", "#8FB8E8", "#8E63D6", "#D65DA8", "#B8B8BE" };
		foreach (string preset in array)
		{
			Border border = new Border
			{
				Width = 16.0,
				Height = 16.0,
				CornerRadius = new CornerRadius(8.0),
				Margin = new Thickness(6.0, 0.0, 0.0, 0.0),
				Background = new SolidColorBrush(LauncherTheme.ParseColour(preset).Value),
				Cursor = Cursors.Hand,
				ToolTip = preset,
				VerticalAlignment = VerticalAlignment.Center
			};
			border.MouseLeftButtonUp += delegate
			{
				colour.Text = preset;
			};
			stackPanel.Children.Add(border);
		}
		Add(LauncherPage.Card(SymbolRegular.ColorBackground24, "Theme colour", "Pick a dot or type a colour as #RRGGBB", stackPanel, out System.Windows.Controls.TextBlock hintBlock), _themeColour);
		base.Body.Children.Add(_themeColour);
		SettingCombo(SymbolRegular.ImageEdit24, "Background", SettingRow.Classic ? "Shown in the Modern layout (Classic's panes are solid)" : "The picture behind the glass panes", new Choice[3]
		{
			new Choice("Game artwork", "game"),
			new Choice("Your picture", "custom"),
			new Choice("None", "none")
		}, "ui_background", null, delegate
		{
			ShowAppearance();
			MainWindow.Instance?.ApplyBackdrop();
		});
		Add(LauncherPage.Card(SymbolRegular.ImageEdit24, "Picture", "A PNG, JPG or BMP; it fills the window", LauncherPage.SmallButton("Choose", SymbolRegular.FolderOpen24, delegate
		{
			ChoosePicture();
		}), out _pictureHint), _picture);
		base.Body.Children.Add(_picture);
		SettingCombo(SymbolRegular.Blur24, "Background blur", null, new Choice[3]
		{
			new Choice("Strong", "strong"),
			new Choice("Light", "light"),
			new Choice("Off", "off")
		}, "ui_background_blur", null, delegate
		{
			MainWindow.Instance?.ApplyBackdrop();
		});
		SettingCombo(SymbolRegular.Window24, "Window buttons", "macOS: the red, yellow and green dots. Windows: – □ ✕", new Choice[2]
		{
			new Choice("macOS", "mac"),
			new Choice("Windows", "windows")
		}, "ui_window_buttons", null, delegate
		{
			MainWindow.Instance?.ApplyWindowButtons();
		});
		SettingCombo(SymbolRegular.ArrowBidirectionalLeftRight24, "Window buttons side", null, new Choice[3]
		{
			new Choice("Usual", "usual", "macOS on the left, Windows on the right"),
			new Choice("Left", "left"),
			new Choice("Right", "right")
		}, "ui_window_buttons_side", null, delegate
		{
			MainWindow.Instance?.ApplyWindowButtons();
		});
		ShowAppearance();
		Group("Bloodborne PC");
		Add(LauncherPage.Card(SymbolRegular.FolderLink24, "Engine folder", "", LauncherPage.Row(LauncherPage.SmallButton("Browse", SymbolRegular.FolderOpen24, delegate
		{
			ChoosePort();
		}), LauncherPage.IconButton(SymbolRegular.Open24, "Open in Explorer", delegate
		{
			Paths.OpenFolder(Paths.PortDir);
		}), LauncherPage.IconButton(SymbolRegular.ArrowUndo24, "Detect automatically", delegate
		{
			LauncherPage.S.Set("port_dir", "");
			LauncherPage.Ws.ResolvePort();
			LauncherPage.Ws.SaveSoon();
			Refresh();
		})), out _portHint));
		Group("Launch");
		SettingCombo(SymbolRegular.Rocket24, "Start the game with", null, Choices.LaunchMethods, "launch_method", null, delegate
		{
			ShowMethod();
			LauncherPage.Ws.SaveSoon();
		});
		TextRow(SymbolRegular.Code24, "Command", "A program to run instead of Bloodborne.exe --run, with the settings as environment variables", "C:\\path\\to\\game.exe", () => LauncherPage.S.Str("custom_command"), delegate(string v)
		{
			LauncherPage.S.Set("custom_command", v);
		}, _customRows, 380.0);
		TextRow(SymbolRegular.TextDescription24, "Arguments", null, "Optional", () => LauncherPage.S.Str("custom_args"), delegate(string v)
		{
			LauncherPage.S.Set("custom_args", v);
		}, _customRows, 380.0);
		base.Body.Children.Add(_customRows);
		Group("Performance");
		SettingCombo(SymbolRegular.Flow20, "Two-stage GPU pipeline", "20–30% faster; turn off if the game is unstable", Choices.DrawPipe, "draw_pipe");
		SettingCombo(SymbolRegular.ArrowSwap24, "CPU reads of GPU data", "Relaxed is the default", Choices.Readbacks, "readbacks");
		SettingToggle(SymbolRegular.Speaker224, "Sound threads first", "For CPUs with many slower cores (older Xeons and similar): the game's sound runs before its other threads, against late or stuttering sound. Off by default", "audio_priority");
		SettingCombo(SymbolRegular.Speaker224, "Sound output", "If the sound jitters or crackles, try DirectSound. The larger buffer is steadier but the sound comes a little later", Choices.SoundOutput, "sound_output");
		Group("Diagnostics");
		SettingToggle(SymbolRegular.TextBulletListSquare24, "Frame statistics in the log", "BB_FRAME_STATS", "frame_stats");
		SettingToggle(SymbolRegular.DeveloperBoard24, "FSR 4 profile in the log", "BB_FSR4_PROFILE", "fsr_profile");
		SettingToggle(SymbolRegular.Speaker224, "Audio statistics in the log", "BB_AUDIO_STATS", "audio_stats");
		SettingToggle(SymbolRegular.HourglassHalf24, "Loading profile in the log", "BB_SAMPLE_THREADS: where the game's main, render and file threads spend their time, every 5 seconds. For slow loading; costs a little speed", "load_profile");
		SettingToggle(SymbolRegular.Timer24, "Render stage timings in the log", "BB_SECTIONS: where each frame's CPU time goes (with frame statistics). It costs FPS, so it applies to the next game only", "section_stats");
		TextRow(SymbolRegular.Code24, "Extra variables", "NAME=value pairs separated by spaces, passed to the game", "BB_EXAMPLE=1", () => LauncherPage.S.Str("extra_env"), delegate(string v)
		{
			LauncherPage.S.Set("extra_env", v.Trim());
		});
		Group("Files");
		Add(LauncherPage.Card(SymbolRegular.DocumentSettings20, "Launcher settings", Paths.ConfigFile, LauncherPage.IconButton(SymbolRegular.Open24, "Open in Explorer", delegate
		{
			Paths.OpenFolder(Paths.ConfigDir);
		}), out hintBlock));
		Add(LauncherPage.Card(SymbolRegular.DocumentSettings20, "Port settings (bbport.ini)", Paths.IniPath, LauncherPage.IconButton(SymbolRegular.Open24, "Open in Explorer", delegate
		{
			Paths.OpenFolder(Path.GetDirectoryName(Paths.IniPath));
		}), out hintBlock));
		Add(LauncherPage.Card(SymbolRegular.Broom24, "Shader cache", "The game's compiled shaders, kept between starts (a new engine version clears them itself). Clear them if something draws wrong in every save, such as missing skin or hair; the next start compiles them again and stutters for a while", LauncherPage.SmallButton("Clear", SymbolRegular.Broom24, delegate
		{
			ClearShaderCache();
		}), out hintBlock));
		Group("Share settings");
		Add(LauncherPage.Card(SymbolRegular.Share24, "Share your settings", "Graphics, effects, camera, controls, cheats and engine options, as a code to paste in Discord or a file. Not included: display and HDR, the online account, folders and languages", LauncherPage.Row(LauncherPage.SmallButton("Copy code", SymbolRegular.Copy24, delegate
		{
			CopySettingsCode();
		}), LauncherPage.SmallButton("Save as file", SymbolRegular.Save24, delegate
		{
			SaveSettingsFile();
		})), out hintBlock));
		Add(LauncherPage.Card(SymbolRegular.ArrowImport24, "Import settings", "Someone's code or file. You see what changes before anything is applied", LauncherPage.Row(LauncherPage.SmallButton("Paste code", SymbolRegular.ClipboardPaste24, delegate
		{
			ImportSettingsCode();
		}), LauncherPage.SmallButton("Open file", SymbolRegular.FolderOpen24, delegate
		{
			ImportSettingsFile();
		})), out hintBlock));
		Group("Community");
		Add(LauncherPage.Card(SymbolRegular.PeopleCommunity24, "Discord", "Help, news, co-op partners and beta feedback", LauncherPage.SmallButton("Join", SymbolRegular.Open24, delegate
		{
			Process.Start(new ProcessStartInfo("https://discord.gg/d3Uv3Cfr5x")
			{
				UseShellExecute = true
			});
		}), out hintBlock));
		Group("Updates");
		_updateNow = LauncherPage.SmallButton("Update now", SymbolRegular.ArrowDownload24, async delegate
		{
			await MainWindow.Instance.InstallUpdateAsync();
		}, ControlAppearance.Primary);
		Add(LauncherPage.Card(SymbolRegular.ArrowDownload24, "Launcher updates", "", LauncherPage.Row(LauncherPage.SmallButton("Check now", SymbolRegular.ArrowClockwise24, async delegate
		{
			await LauncherPage.Ws.CheckForUpdatesAsync(quiet: false);
		}), _updateNow, LauncherPage.IconButton(SymbolRegular.Open24, "Open the releases page", delegate
		{
			Process.Start(new ProcessStartInfo(Updater.ReleasesPage)
			{
				UseShellExecute = true
			});
		})), out _updateHint));
		ComboRow(SymbolRegular.Branch24, "Update channel", "Stable: releases that have been tested. Beta: every change as soon as it is done, before it is tested", new Choice[2]
		{
			new Choice("Stable", "stable"),
			new Choice("Beta", "beta")
		}, () => (!Updater.BetaChannel(LauncherPage.S)) ? "stable" : "beta", delegate(string v)
		{
			if (!(v == (Updater.BetaChannel(LauncherPage.S) ? "beta" : "stable")))
			{
				LauncherPage.S.Set("update_channel", v);
				LauncherPage.Ws.SaveSoon();
				LauncherPage.Ws.Log((v == "beta") ? "Update channel: beta." : "Update channel: stable.", LogLevel.Step);
				LauncherPage.Ws.CheckForUpdatesAsync(quiet: false);
			}
		});
		SettingToggle(SymbolRegular.Alert24, "Check for updates at start", "Looks at github.com/Awakenchan/BloodBorneButPc/releases", "check_updates");
		_installVersion = LauncherPage.SmallButton("Install", SymbolRegular.ArrowDownload24, async delegate
		{
			await InstallChosenVersionAsync();
		});
		_installVersion.IsEnabled = false;
		_versions.SelectionChanged += delegate
		{
			_installVersion.IsEnabled = _versions.SelectedItem is ComboBoxItem { Tag: Updater.Release tag } && !tag.IsCurrent;
		};
		Add(LauncherPage.Card(SymbolRegular.History24, "Version", "Every published release, stable and beta, newest first. Install any of them, older ones too, to go back if a version gives you trouble. Your settings, saves, mods and profiles stay", LauncherPage.Row(_versions, _installVersion, LauncherPage.IconButton(SymbolRegular.ArrowClockwise24, "Reload the list", async delegate
		{
			await LoadVersionsAsync(force: true);
		})), out _versionHint));
		LauncherPage.SetHint(_versionHint, "You have " + Updater.CurrentLabel);
		LauncherPage.Ws.UpdateStateChanged += UpdateUpdates;
		UpdateUpdates();
		SettingRow row = LauncherPage.Card(SymbolRegular.ArrowSync24, "Build", "", LauncherPage.Row(LauncherPage.SmallButton("Rebuild", SymbolRegular.ArrowClockwise24, delegate
		{
			Rebuild(force: false);
		}), LauncherPage.SmallButton("Force rebuild", SymbolRegular.Wrench24, delegate
		{
			Rebuild(force: true);
		})), out _buildHint);
		if (Paths.SourceRoot != null)
		{
			Group("Build from source");
			Add(row);
		}
		LauncherPage.Ws.PortChanged += Refresh;
		void ShowSwatch()
		{
			Border border2 = swatch;
			Color? color = LauncherTheme.ParseColour(colour.Text);
			SolidColorBrush background;
			if (color.HasValue)
			{
				Color valueOrDefault = color.GetValueOrDefault();
				background = new SolidColorBrush(valueOrDefault);
			}
			else
			{
				background = Brushes.Transparent;
			}
			border2.Background = background;
		}
	}

	public override void OnShown()
	{
		Refresh();
		ShowMethod();
		LoadVersionsAsync(force: false);
	}

	private async Task LoadVersionsAsync(bool force)
	{
		if (_versionsLoading || (_releases != null && !force))
		{
			return;
		}
		_versionsLoading = true;
		_versions.IsEnabled = false;
		_installVersion.IsEnabled = false;
		_versions.Items.Clear();
		_versions.Items.Add(new ComboBoxItem
		{
			Content = "Loading…"
		});
		_versions.SelectedIndex = 0;
		try
		{
			_releases = await Updater.AllAsync();
			_versions.Items.Clear();
			ComboBoxItem comboBoxItem = null;
			foreach (Updater.Release release in _releases)
			{
				string content = Updater.Label(release.Version, release.Beta) + ((release.Beta > 0) ? "" : "  · stable") + (release.IsCurrent ? "  (installed)" : "");
				ComboBoxItem comboBoxItem2 = new ComboBoxItem
				{
					Content = content,
					Tag = release
				};
				_versions.Items.Add(comboBoxItem2);
				if (release.IsCurrent)
				{
					comboBoxItem = comboBoxItem2;
				}
			}
			_versions.SelectedItem = comboBoxItem ?? ((_versions.Items.Count > 0) ? _versions.Items[0] : null);
			_versions.IsEnabled = _versions.Items.Count > 0;
			LauncherPage.SetHint(_versionHint, (_releases.Count == 0) ? ("You have " + Updater.CurrentLabel + "; no release is published yet") : $"You have {Updater.CurrentLabel}. {_releases.Count} versions to choose from");
		}
		catch (Exception ex)
		{
			_releases = null;
			_versions.Items.Clear();
			LauncherPage.SetHint(_versionHint, "You have " + Updater.CurrentLabel + ". The list could not be loaded: " + ex.GetBaseException().Message);
		}
		finally
		{
			_versionsLoading = false;
		}
	}

	private async Task InstallChosenVersionAsync()
	{
		if (_versions.SelectedItem is ComboBoxItem { Tag: Updater.Release tag })
		{
			await MainWindow.Instance.InstallVersionAsync(tag);
		}
	}

	private void Refresh()
	{
		LauncherPage.SetHint(_portHint, Paths.IsPortDir(Paths.PortDir) ? (Paths.PortDir + ((Paths.DetectPortDir() == Paths.PortDir) ? "  ·  the package this launcher is in" : "") + "\nThe engine that runs your game: out\\bb-probe.exe, its scripts and Python. Your game files stay where they are") : "Not found. It is normally the folder this launcher is in (with bin\\bb-probe.exe and run.py)");
		LauncherPage.SetHint(_buildHint, BuildStatus());
	}

	private void UpdateUpdates()
	{
		Updater.Release update = LauncherPage.Ws.Update;
		Updater.Release latest = LauncherPage.Ws.Latest;
		_updateNow.Visibility = ((!(update != null)) ? Visibility.Collapsed : Visibility.Visible);
		LauncherPage.SetHint(_updateHint, (update != null) ? (update.Name + " is available (you have " + Updater.CurrentLabel + ")" + (update.IsNewerThan(Updater.Current, Updater.CurrentBeta) ? "" : ": back to stable")) : ((!LauncherPage.Ws.CheckedAt.HasValue) ? ("You have " + Updater.CurrentLabel) : ((latest == null) ? ("You have " + Updater.CurrentLabel + "; no release is published yet") : $"You have {Updater.CurrentLabel}, the latest ({latest.Tag})")));
	}

	private void ShowAppearance()
	{
		_themeColour.Visibility = ((!(LauncherPage.S.Str("ui_theme") == "custom")) ? Visibility.Collapsed : Visibility.Visible);
		_picture.Visibility = ((!(LauncherPage.S.Str("ui_background") == "custom")) ? Visibility.Collapsed : Visibility.Visible);
		string text = LauncherPage.S.Str("ui_background_image");
		LauncherPage.SetHint(_pictureHint, (text.Length == 0) ? "None chosen yet: the game artwork shows" : (File.Exists(text) ? text : (text + " is missing: the game artwork shows")));
	}

	private void ChoosePicture()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Background picture",
			Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*"
		};
		if (openFileDialog.ShowDialog() == true)
		{
			LauncherPage.S.Set("ui_background_image", openFileDialog.FileName);
			LauncherPage.Ws.SaveSoon();
			ShowAppearance();
			MainWindow.Instance?.ApplyBackdrop();
		}
	}

	private static bool RestartForLayout()
	{
		if (!Prompt.Confirm("Switch layout", "The launcher restarts to change its layout.", "Restart now"))
		{
			return false;
		}
		Restart();
		return true;
	}

	private static void Restart()
	{
		LauncherPage.Ws.SaveNow();
		string processPath = Environment.ProcessPath;
		if (processPath != null)
		{
			Process.Start(new ProcessStartInfo(processPath)
			{
				UseShellExecute = true,
				WorkingDirectory = Path.GetDirectoryName(processPath)
			});
		}
		Application.Current.Shutdown();
	}

	private static void ClearShaderCache()
	{
		if (Process.GetProcessesByName("bb-probe").Length != 0)
		{
			Prompt.Tell("Shader cache", "Close the game first: it is using the cache.");
			return;
		}
		string text = Path.Combine(Paths.PortDir, "user", "cache");
		int num = 0;
		if (Directory.Exists(text))
		{
			foreach (FileSystemInfo item in new DirectoryInfo(text).EnumerateFileSystemInfos())
			{
				if (item.Name == "engine.txt")
				{
					continue;
				}
				try
				{
					if (item is DirectoryInfo directoryInfo)
					{
						directoryInfo.Delete(recursive: true);
					}
					else
					{
						item.Delete();
					}
				}
				catch (Exception)
				{
					num++;
				}
			}
		}
		LauncherPage.Ws.Log((num == 0) ? "Shader cache cleared: the next start compiles the shaders again." : $"Shader cache: {num} entries could not be removed ({text}).", (num == 0) ? LogLevel.Good : LogLevel.Warn);
	}

	private void CopySettingsCode()
	{
		Clipboard.SetText(SharedSettings.ToCode(SharedSettings.Take(LauncherPage.Ini, LauncherPage.S)));
		LauncherPage.Ws.Log("Settings code copied. Paste it in Discord; others import it in Settings › Share settings.", LogLevel.Good);
		Prompt.Tell("Settings code copied", "Paste it in Discord or anywhere else. Others use Import settings › Paste code.");
	}

	private void SaveSettingsFile()
	{
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Title = "Save settings",
			FileName = "Bloodborne PC settings.json",
			Filter = "Settings|*.json"
		};
		if (saveFileDialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			File.WriteAllText(saveFileDialog.FileName, SharedSettings.ToJson(SharedSettings.Take(LauncherPage.Ini, LauncherPage.S)));
			LauncherPage.Ws.Log("Settings saved: " + saveFileDialog.FileName, LogLevel.Good);
		}
		catch (Exception ex)
		{
			LauncherPage.Ws.Log("Could not save the settings: " + ex.Message, LogLevel.Bad);
		}
	}

	private void ImportSettingsCode()
	{
		string text = (Clipboard.ContainsText() ? Clipboard.GetText() : "");
		string[] array = Prompt.Ask("Import settings", "Paste the code someone shared (it starts with BBPC1:).", new Prompt.Field("Code", text.Contains("BBPC1:", StringComparison.OrdinalIgnoreCase) ? text.Trim() : "", "BBPC1:…", Multiline: true));
		if (array != null && array[0].Trim().Length != 0)
		{
			OfferSettings(array[0]);
		}
	}

	private void ImportSettingsFile()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Import settings",
			Filter = "Settings|*.json;*.txt|All files|*.*"
		};
		if (openFileDialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			if (new FileInfo(openFileDialog.FileName).Length > 262144)
			{
				throw new FormatException("That file is far too large to be shared settings.");
			}
			OfferSettings(File.ReadAllText(openFileDialog.FileName));
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is FormatException) ? 1 : 0) != 0)
		{
			Prompt.Tell("Could not import", ex.Message);
		}
	}

	private void OfferSettings(string text)
	{
		if (LauncherPage.Ws.Game.Running)
		{
			Prompt.Tell("Close the game first", "The game changes these settings too while it runs.");
			return;
		}
		SharedSettings.Snapshot snapshot;
		try
		{
			snapshot = SharedSettings.Parse(text);
		}
		catch (FormatException ex)
		{
			Prompt.Tell("Could not import", ex.Message);
			return;
		}
		HashSet<SharedSettings.Part> hashSet = new HashSet<SharedSettings.Part>
		{
			SharedSettings.Part.Settings,
			SharedSettings.Part.Controls,
			SharedSettings.Part.Cheats
		};
		List<SharedSettings.Change> list = SharedSettings.Changes(snapshot, LauncherPage.Ini, LauncherPage.S, hashSet);
		if (list.Count == 0)
		{
			Prompt.Tell("Nothing to change", "These settings are the same as yours.");
			return;
		}
		int num = list.Count((SharedSettings.Change c) => c.Part == SharedSettings.Part.Settings);
		int num2 = list.Count((SharedSettings.Change c) => c.Part == SharedSettings.Part.Controls);
		int num3 = list.Count((SharedSettings.Change c) => c.Part == SharedSettings.Part.Cheats);
		string value = string.Join(", ", new string[3]
		{
			(num > 0) ? $"{num} graphics and engine" : null,
			(num2 > 0) ? $"{num2} controls" : null,
			(num3 > 0) ? $"{num3} cheats" : null
		}.Where((string p) => p != null));
		string value2 = string.Join("\n", list.Take(12).Select(SharedSettings.Describe)) + ((list.Count > 12) ? $"\n…and {list.Count - 12} more" : "");
		string message = $"From {snapshot.From}: {list.Count} changes ({value}).\n\n{value2}\n\n" + "Your current settings are saved first. The launcher restarts to show the new ones.";
		HashSet<SharedSettings.Part> parts;
		if (num > 0 && num2 + num3 > 0)
		{
			int num4 = Prompt.Choose("Import settings", message, "Cancel", "Keep my controls and cheats", "Import all");
			if (num4 <= 0)
			{
				return;
			}
			parts = ((num4 == 2) ? hashSet : new HashSet<SharedSettings.Part> { SharedSettings.Part.Settings });
		}
		else
		{
			if (!Prompt.Confirm("Import settings", message, "Import"))
			{
				return;
			}
			parts = hashSet;
		}
		try
		{
			string text2 = Path.Combine(Paths.ConfigDir, "settings-before-import.json");
			File.WriteAllText(text2, SharedSettings.ToJson(SharedSettings.Take(LauncherPage.Ini, LauncherPage.S)));
			SharedSettings.Apply(snapshot, LauncherPage.Ini, LauncherPage.S, parts);
			LauncherPage.Ws.Log($"Imported settings from {snapshot.From}. Your previous ones: {text2} (Import settings › Open file).", LogLevel.Good);
		}
		catch (Exception ex2) when (((ex2 is IOException || ex2 is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			Prompt.Tell("Could not import", ex2.Message);
			return;
		}
		Restart();
	}

	private void ShowMethod()
	{
		_customRows.Visibility = ((!GameRunner.Custom(LauncherPage.S)) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void ChoosePort()
	{
		string text = LauncherPage.PickFolder("Engine folder (with run.py and bin\\bb-probe.exe)", Paths.PortDir);
		if (text != null)
		{
			if (!Paths.IsPortDir(text))
			{
				System.Windows.MessageBox.Show(Application.Current.MainWindow, "That folder has no run.py with Bloodborne.exe or bin\\bb-probe.exe.", "Engine folder", System.Windows.MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			LauncherPage.S.Set("port_dir", text);
			LauncherPage.Ws.ResolvePort();
			LauncherPage.Ws.SaveSoon();
			Refresh();
		}
	}

	private static string? BuilderPath()
	{
		string text = Path.Combine(AppContext.BaseDirectory, "Build Launcher.exe");
		if (File.Exists(text))
		{
			return text;
		}
		string sourceRoot = Paths.SourceRoot;
		if (sourceRoot != null)
		{
			string text2 = Path.Combine(SourceStamp.AppDirOf(sourceRoot), "Build Launcher.exe");
			if (File.Exists(text2))
			{
				return text2;
			}
		}
		return null;
	}

	private static string BuildStatus()
	{
		Version version = typeof(SettingsPage).Assembly.GetName().Version;
		string sourceRoot = Paths.SourceRoot;
		if (sourceRoot == null)
		{
			return "Version " + version?.ToString(3) + ". The sources (launcher/windows) are not next to this copy, so it cannot rebuild itself.";
		}
		SourceStamp.Stamp stamp = SourceStamp.Read(SourceStamp.AppDirOf(sourceRoot));
		string text = SourceStamp.Compute(sourceRoot, "BloodborneLauncher");
		string value = ((stamp.Launcher == text) ? "Up to date with the sources" : "The sources changed since this build: rebuild to pick the changes up");
		string value2 = ((stamp.Built == default(DateTime)) ? "" : $" · built {stamp.Built.ToLocalTime():yyyy-MM-dd HH:mm}");
		return $"Version {version?.ToString(3)}{value2}. {value}.";
	}

	private void Rebuild(bool force)
	{
		string text = BuilderPath();
		if (text == null)
		{
			System.Windows.MessageBox.Show(Application.Current.MainWindow, "Build Launcher.exe was not found next to the launcher.", "Rebuild", System.Windows.MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (LauncherPage.Ws.Game.Running)
		{
			System.Windows.MessageBox.Show(Application.Current.MainWindow, "Stop the game before rebuilding the launcher.", "Rebuild", System.Windows.MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		LauncherPage.Ws.SaveNow();
		string arguments = $"--wait-pid {Environment.ProcessId}" + (force ? " --force" : "");
		Process.Start(new ProcessStartInfo(text, arguments)
		{
			UseShellExecute = true,
			WorkingDirectory = Path.GetDirectoryName(text)
		});
		Application.Current.Shutdown();
	}
}
