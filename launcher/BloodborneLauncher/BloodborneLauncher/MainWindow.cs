using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using BloodborneLauncher.Core;
using BloodborneLauncher.Views;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace BloodborneLauncher;

public class MainWindow : FluentWindow, IComponentConnector
{
	private readonly ObservableCollection<LogItem> _log = new ObservableCollection<LogItem>();

	private readonly Dictionary<Type, LauncherPage> _pages = new Dictionary<Type, LauncherPage>();

	private bool _logExpanded;

	private bool _closing;

	private bool _directPlay;

	private bool _directCrashed;

	internal System.Windows.Controls.Image BackdropImage;

	internal Rectangle BackdropShade;

	internal StackPanel MacButtons;

	internal StackPanel WinButtons;

	internal System.Windows.Controls.Button WinMinimize;

	internal System.Windows.Controls.Button WinMaximize;

	internal System.Windows.Controls.Button WinClose;

	internal Run TitlePage;

	internal StackPanel TitleTools;

	internal System.Windows.Controls.Button TitleUpdate;

	internal System.Windows.Controls.TextBlock TitleUpdateText;

	internal System.Windows.Controls.Button TitlePlay;

	internal SymbolIcon TitlePlayIcon;

	internal System.Windows.Controls.TextBlock TitlePlayText;

	internal Border Sidebar;

	internal RadioButton ClassicPlay;

	internal RadioButton ClassicControls;

	internal RadioButton ClassicMods;

	internal RadioButton ClassicOnline;

	internal RadioButton ClassicSettings;

	internal RadioButton ClassicOutput;

	internal Border TabStrip;

	internal RadioButton PlayItem;

	internal RadioButton ControlsItem;

	internal RadioButton OnlineItem;

	internal RadioButton SettingsItem;

	internal RadioButton MoreItem;

	internal Frame PageFrame;

	internal Border LogPanel;

	internal ScrollViewer LogScroller;

	internal ItemsControl LogList;

	internal Ellipse StatusDot;

	internal System.Windows.Controls.TextBlock StatusText;

	internal System.Windows.Controls.TextBlock SavedText;

	internal System.Windows.Controls.TextBlock LogCount;

	internal System.Windows.Controls.TextBlock VersionText;

	private bool _contentLoaded;

	public static MainWindow? Instance => Application.Current.MainWindow as MainWindow;

	public MainWindow()
	{
		InitializeComponent();
		LogList.ItemsSource = _log;
		Workspace ws = Workspace.Current;
		ws.Logged += OnLogged;
		ws.Saved += delegate
		{
			SavedText.Text = $"Saved {DateTime.Now:HH:mm:ss}";
		};
		ws.GameStateChanged += UpdatePlayButton;
		ws.GameCrashed += OnGameCrashed;
		ws.Initialise();
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "--layout");
		SettingRow.Classic = (((num >= 0 && num + 1 < commandLineArgs.Length) ? commandLineArgs[num + 1] : null) ?? ws.Settings.Str("ui_layout")) == "classic";
		int num2 = Array.IndexOf(commandLineArgs, "--test-prompt");
		if (num2 >= 0 && num2 + 1 < commandLineArgs.Length)
		{
			Prompt.SnapshotPath = commandLineArgs[num2 + 1];
			base.ShowActivated = false;
			base.ShowInTaskbar = false;
			base.WindowStartupLocation = WindowStartupLocation.Manual;
			base.Left = -32000.0;
			base.Top = -32000.0;
		}
		_directPlay = commandLineArgs.Contains("--play");
		if (_directPlay)
		{
			base.WindowState = WindowState.Minimized;
		}
		Sidebar.Visibility = ((!SettingRow.Classic) ? Visibility.Collapsed : Visibility.Visible);
		TabStrip.Visibility = (SettingRow.Classic ? Visibility.Collapsed : Visibility.Visible);
		if (!SettingRow.Classic)
		{
			ApplyGlass();
		}
		LauncherTheme.Apply(ws.Settings, !SettingRow.Classic);
		ApplyWindowButtons();
		Version version = typeof(MainWindow).Assembly.GetName().Version;
		VersionText.Text = ((version == null) ? "" : Updater.CurrentLabel);
		ws.UpdateStateChanged += delegate
		{
			TitleUpdate.Visibility = ((!(ws.Update != null)) ? Visibility.Collapsed : Visibility.Visible);
			if (ws.Update != null)
			{
				TitleUpdateText.Text = "Update " + ws.Update.Tag;
			}
		};
		base.Loaded += async delegate
		{
			Updater.CleanUp();
			if (Updater.CurrentBeta > 0 && ws.Settings.Str("update_channel").Length == 0)
			{
				ws.Settings.Set("update_channel", "beta");
				ws.SaveSoon();
			}
			if (ws.Settings.Bool("check_updates"))
			{
				ws.CheckForUpdatesAsync(quiet: true);
			}
			await Task.Yield();
			(SettingRow.Classic ? ClassicPlay : PlayItem).IsChecked = true;
			UpdatePlayButton();
			if (Environment.GetCommandLineArgs().Contains("--test-trophy"))
			{
				TrophyToast.Announce(TrophyTracker.ById[14]);
			}
			if (Prompt.SnapshotPath != null && Environment.GetCommandLineArgs().Contains("--test-import"))
			{
				base.Dispatcher.BeginInvoke((Func<string[]>)(() => Prompt.Ask("Import settings", "Paste the code someone shared (it starts with BBPC1:).", new Prompt.Field("Code", SharedSettings.ToCode(SharedSettings.Take(ws.Ini, ws.Settings)), "", Multiline: true))), DispatcherPriority.ApplicationIdle);
			}
			else if (Prompt.SnapshotPath != null)
			{
				base.Dispatcher.BeginInvoke((Func<bool>)(() => Prompt.Confirm("Switch layout", "The launcher restarts to change its layout.", "Restart now")), DispatcherPriority.ApplicationIdle);
			}
			ws.Log(Paths.IsPortDir(Paths.PortDir) ? ("Bloodborne Launcher ready. Engine: " + Paths.PortDir) : "Bloodborne Launcher ready. Choose the engine folder in Settings.", Paths.IsPortDir(Paths.PortDir) ? LogLevel.Good : LogLevel.Warn);
			if (_directPlay)
			{
				base.Dispatcher.BeginInvoke((Func<Task>)async delegate
				{
					await PlayDirectAsync();
				}, DispatcherPriority.ApplicationIdle);
			}
			else if (!ws.Settings.Bool("hardware_offered") && Prompt.SnapshotPath == null && Paths.IsPortDir(Paths.PortDir))
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					OfferBestSettings(firstRun: true);
				}, DispatcherPriority.ApplicationIdle);
			}
		};
		base.Closing += OnClosing;
	}

	private async Task PlayDirectAsync()
	{
		Workspace ws = Workspace.Current;
		Hide();
		await PlayOrStopAsync();
		if (!ws.Game.Running)
		{
			_directPlay = false;
			ShowLauncher();
			return;
		}
		ws.GameCrashed += delegate
		{
			_directCrashed = true;
		};
		ws.GameStateChanged += delegate
		{
			if (_directPlay && !ws.Game.Running)
			{
				_directPlay = false;
				if (_directCrashed)
				{
					ShowLauncher();
				}
				else
				{
					Close();
				}
			}
		};
	}

	private void ShowLauncher()
	{
		Show();
		base.WindowState = WindowState.Normal;
		Activate();
	}

	public void OfferBestSettings(bool firstRun)
	{
		Workspace current = Workspace.Current;
		current.Settings.Set("hardware_offered", value: true);
		current.SaveSoon();
		Hardware hardware = Hardware.Detect();
		List<Hardware.Change> list = hardware.Recommend(current.Settings, current.Ini);
		if (list.Count == 0)
		{
			if (!firstRun)
			{
				Prompt.Tell("Best settings for this PC", hardware.Summary + "\n\nYour settings already suit this PC.");
			}
			return;
		}
		string text = string.Join("\n", list.Select((Hardware.Change c) => $"{c.Label}: {c.From} → {c.To}"));
		if (!Prompt.Confirm(firstRun ? "Settings for this PC" : "Best settings for this PC", hardware.Summary + "\n\n" + text + "\n\nYou can change any of them later.", "Apply"))
		{
			return;
		}
		foreach (Hardware.Change item in list)
		{
			item.Apply();
		}
		current.SaveSoon();
		current.Log("Settings for this PC applied: " + string.Join(", ", list.Select((Hardware.Change c) => c.Label.ToLowerInvariant() + " " + c.To)) + ".", LogLevel.Good);
		ReloadPages();
	}

	public void ReloadPages()
	{
		LauncherPage launcherPage = PageFrame.Content as LauncherPage;
		_pages.Clear();
		if (launcherPage != null)
		{
			LauncherPage launcherPage2 = (_pages[launcherPage.GetType()] = (LauncherPage)Activator.CreateInstance(launcherPage.GetType()));
			LauncherPage launcherPage4 = launcherPage2;
			PageFrame.Navigate(launcherPage4);
			launcherPage4.OnShown();
		}
	}

	private void OnGameCrashed(int code, SessionStats session)
	{
		Workspace current = Workspace.Current;
		switch (Prompt.Choose("The game crashed", CrashReport.Explain(code) + " It ran for " + SessionStats.Duration(session.Length) + ".\n\nSafe mode starts the next run with the experimental engine options and mods off. Copy report puts the details and the end of the log on the clipboard, for a bug report.", "Close", "Copy report", "Use safe mode"))
		{
		case 1:
			try
			{
				Clipboard.SetText(CrashReport.Build(current.Settings, current.Ini, code));
				current.Log("Crash report copied to the clipboard.", LogLevel.Good);
				break;
			}
			catch (Exception ex)
			{
				current.Log("Could not copy the report: " + ex.Message, LogLevel.Warn);
				break;
			}
		case 2:
			current.Settings.Set("safe_mode_next", value: true);
			current.SaveSoon();
			current.Log("The next start uses safe mode (once).", LogLevel.Step);
			break;
		}
	}

	private void ApplyGlass()
	{
		Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
		{
			Source = new Uri("pack://application:,,,/Themes/Glass.xaml", UriKind.Absolute)
		});
		ApplyBackdrop();
	}

	public void ApplyBackdrop()
	{
		if (SettingRow.Classic)
		{
			return;
		}
		LauncherSettings settings = Workspace.Current.Settings;
		Color c = LauncherTheme.ShadeColour(settings);
		BackdropShade.Fill = new LinearGradientBrush(new GradientStopCollection
		{
			new GradientStop(LauncherTheme.WithAlpha(c, 89), 0.0),
			new GradientStop(LauncherTheme.WithAlpha(c, 128), 0.5),
			new GradientStop(LauncherTheme.WithAlpha(c, 179), 1.0)
		}, 90.0);
		string text = settings.Str("ui_background");
		if (text == "none")
		{
			BackdropImage.Visibility = Visibility.Collapsed;
			BackdropShade.Visibility = Visibility.Collapsed;
			return;
		}
		string text2 = settings.Str("ui_background_blur");
		int num = ((!(text2 == "off")) ? ((text2 == "light") ? 14 : 48) : 0);
		BackdropImage.Effect = ((num == 0) ? null : new BlurEffect
		{
			Radius = num,
			KernelType = KernelType.Gaussian,
			RenderingBias = RenderingBias.Performance
		});
		BackdropImage.Margin = new Thickness((num != 0) ? (-60) : 0);
		RenderOptions.SetBitmapScalingMode(BackdropImage, (num != 0) ? BitmapScalingMode.LowQuality : BitmapScalingMode.HighQuality);
		try
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.DecodePixelWidth = ((num == 0) ? 2560 : ((num < 20) ? 1600 : 960));
			string text3 = settings.Str("ui_background_image");
			string text4;
			if (text == "custom" && File.Exists(text3))
			{
				text4 = text3;
			}
			else
			{
				string game = GameEnvironment.GameDir(settings);
				text4 = new string[2] { "pic1.png", "pic0.png" }.Select((string n) => System.IO.Path.Combine(game, "sce_sys", n)).FirstOrDefault(File.Exists);
			}
			bitmapImage.UriSource = ((text4 != null) ? new Uri(text4, UriKind.Absolute) : new Uri("pack://application:,,,/Assets/backdrop.jpg", UriKind.Absolute));
			bitmapImage.EndInit();
			BackdropImage.Source = bitmapImage;
			BackdropImage.Visibility = Visibility.Visible;
			BackdropShade.Visibility = Visibility.Visible;
		}
		catch
		{
			BackdropImage.Visibility = Visibility.Collapsed;
			BackdropShade.Visibility = Visibility.Visible;
		}
	}

	public void ApplyTheme()
	{
		LauncherTheme.Apply(Workspace.Current.Settings, !SettingRow.Classic);
		ApplyBackdrop();
	}

	public void ApplyWindowButtons()
	{
		LauncherSettings settings = Workspace.Current.Settings;
		bool flag = settings.Str("ui_window_buttons") == "windows";
		string text = settings.Str("ui_window_buttons_side");
		bool flag2 = text == "right" || (text != "left" && flag);
		MacButtons.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		WinButtons.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		List<System.Windows.Controls.Button> source = MacButtons.Children.OfType<System.Windows.Controls.Button>().ToList();
		System.Windows.Controls.Button button = source.First((System.Windows.Controls.Button b) => (string)b.ToolTip == "Close");
		System.Windows.Controls.Button button2 = source.First((System.Windows.Controls.Button b) => (string)b.ToolTip == "Minimize");
		System.Windows.Controls.Button button3 = source.First((System.Windows.Controls.Button b) => (string)b.ToolTip == "Maximize");
		if (flag2)
		{
			Order(MacButtons, new UIElement[3] { button2, button3, button });
			Order(WinButtons, new UIElement[3] { WinMinimize, WinMaximize, WinClose });
		}
		else
		{
			Order(MacButtons, new UIElement[3] { button, button2, button3 });
			Order(WinButtons, new UIElement[3] { WinClose, WinMaximize, WinMinimize });
		}
		foreach (FrameworkElement item in MacButtons.Children.OfType<FrameworkElement>())
		{
			item.Margin = (flag2 ? new Thickness(8.0, 0.0, 0.0, 0.0) : new Thickness(0.0, 0.0, 8.0, 0.0));
		}
		MacButtons.HorizontalAlignment = (flag2 ? HorizontalAlignment.Right : HorizontalAlignment.Left);
		MacButtons.Margin = (flag2 ? new Thickness(0.0, 0.0, 14.0, 0.0) : new Thickness(14.0, 0.0, 0.0, 0.0));
		WinButtons.HorizontalAlignment = (flag2 ? HorizontalAlignment.Right : HorizontalAlignment.Left);
		int num = (flag2 ? (flag ? 138 : 74) : 0);
		TitleTools.Margin = new Thickness(0.0, 0.0, 10 + num, 0.0);
		static void Order(Panel panel, UIElement[] order)
		{
			panel.Children.Clear();
			foreach (UIElement element in order)
			{
				panel.Children.Add(element);
			}
		}
	}

	protected override void SetWindowChrome()
	{
		base.SetWindowChrome();
		WindowChrome windowChrome = WindowChrome.GetWindowChrome(this);
		if (windowChrome != null)
		{
			if (base.ResizeMode != ResizeMode.NoResize)
			{
				windowChrome.ResizeBorderThickness = new Thickness(8.0);
			}
			windowChrome.CaptionHeight = 34.0;
		}
	}

	private async void OnClosing(object? sender, CancelEventArgs e)
	{
		Workspace.Current.SaveNow();
		if (!_closing && Workspace.Current.Game.Running)
		{
			switch (System.Windows.MessageBox.Show(this, "The game is still running. Stop it and close the launcher?", "Bloodborne Launcher", System.Windows.MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
			{
			case System.Windows.MessageBoxResult.Cancel:
				e.Cancel = true;
				break;
			case System.Windows.MessageBoxResult.Yes:
				e.Cancel = true;
				_closing = true;
				await Workspace.Current.StopGameAsync();
				Close();
				break;
			}
		}
	}

	private void CloseWindow_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}

	private void SidebarItem_Checked(object sender, RoutedEventArgs e)
	{
		if (sender is RadioButton { Tag: Type tag } radioButton)
		{
			if (!_pages.TryGetValue(tag, out LauncherPage value))
			{
				value = (_pages[tag] = (LauncherPage)Activator.CreateInstance(tag));
			}
			PageFrame.Navigate(value);
			value.OnShown();
			string text = (radioButton.Content as string) ?? (radioButton.Content as Panel)?.Children.OfType<System.Windows.Controls.TextBlock>().FirstOrDefault()?.Text;
			TitlePage.Text = (string.IsNullOrEmpty(text) ? "" : (" — " + text));
		}
	}

	public void Show(string page)
	{
		RadioButton radioButton;
		if (SettingRow.Classic)
		{
			radioButton = page switch
			{
				"output" => ClassicOutput, 
				"online" => ClassicOnline, 
				"advanced" => ClassicSettings, 
				"mods" => ClassicMods, 
				"controls" => ClassicControls, 
				_ => ClassicPlay, 
			};
		}
		else
		{
			RadioButton radioButton2;
			switch (page)
			{
			case "online":
				radioButton2 = OnlineItem;
				break;
			case "advanced":
				radioButton2 = SettingsItem;
				break;
			case "output":
			case "mods":
				radioButton2 = MoreItem;
				break;
			case "controls":
				radioButton2 = ControlsItem;
				break;
			default:
				radioButton2 = PlayItem;
				break;
			}
			radioButton = radioButton2;
		}
		RadioButton radioButton3 = radioButton;
		if (radioButton3.IsChecked == true)
		{
			SidebarItem_Checked(radioButton3, new RoutedEventArgs());
		}
		radioButton3.IsChecked = true;
		if (radioButton3 == MoreItem && _pages.TryGetValue(typeof(MorePage), out LauncherPage value))
		{
			((MorePage)value).ShowSection(page);
		}
	}

	private void PageFrame_Navigated(object sender, NavigationEventArgs e)
	{
		while (PageFrame.CanGoBack)
		{
			PageFrame.RemoveBackEntry();
		}
	}

	private async void TitlePlay_Click(object sender, RoutedEventArgs e)
	{
		await PlayOrStopAsync();
	}

	private async void TitleUpdate_Click(object sender, RoutedEventArgs e)
	{
		await InstallUpdateAsync();
	}

	public async Task InstallUpdateAsync()
	{
		Updater.Release update = Workspace.Current.Update;
		if ((object)update != null)
		{
			Updater.Release release = update;
			await InstallAsync(release, await Updater.NeedsPackageAsync(update));
		}
	}

	public async Task UpdateEngineAsync()
	{
		Workspace current = Workspace.Current;
		Updater.Release release = current.Latest;
		if ((object)release == null)
		{
			release = await Updater.LatestAsync(Updater.BetaChannel(current.Settings));
		}
		Updater.Release release2 = release;
		if ((object)release2 == null || release2.ZipUrl == null)
		{
			Prompt.Tell("Update engine", "No package is published yet. See " + Updater.ReleasesPage);
		}
		else
		{
			await InstallAsync(release2, package: true);
		}
	}

	public async Task InstallVersionAsync(Updater.Release release)
	{
		if (release.IsCurrent)
		{
			Prompt.Tell("Already installed", "You already have " + Updater.CurrentLabel + ".");
		}
		else
		{
			await InstallAsync(release, await Updater.SwitchNeedsPackageAsync(release), chosen: true);
		}
	}

	private async Task InstallAsync(Updater.Release release, bool package, bool chosen = false)
	{
		Workspace ws = Workspace.Current;
		if (package && ws.Game.Running)
		{
			Prompt.Tell("Close the game first", "This update replaces the engine, so the game has to be closed.");
			return;
		}
		string text = release.Notes.Trim();
		if (text.Length > 1200)
		{
			text = text.Substring(0, 1200) + "…";
		}
		long num = (package ? release.ZipSize : release.ExeSize);
		bool flag = !release.IsNewerThan(Updater.Current, Updater.CurrentBeta);
		string message = "You have " + Updater.CurrentLabel + ". " + (package ? $"{(chosen ? "This version has a different engine" : "This update includes a new engine")}, so the whole package is downloaded ({num / 1048576} MB) and replaced in place." : $"The launcher downloads {release.Tag} ({num / 1048576} MB), replaces itself and restarts.") + " Your settings, saves, mods and profiles stay." + ((chosen && flag) ? "\n\nThis is an older version: settings it doesn't know are kept but not used, and the launcher may offer the newest version again at start." : "") + ((text.Length > 0) ? ("\n\n" + ((chosen && flag) ? "This version's notes" : "What's new") + ":\n" + text) : "");
		string text2 = (chosen ? "Switch" : "Update");
		if (!Prompt.Confirm(text2 + " to " + release.Name + "?", message, text2 + " now"))
		{
			return;
		}
		TitleUpdate.Visibility = Visibility.Visible;
		TitleUpdate.IsEnabled = false;
		Progress<double> progress = new Progress<double>(delegate(double p)
		{
			TitleUpdateText.Text = $"Downloading {p:P0}";
		});
		try
		{
			ws.SaveNow();
			IReadOnlyList<string> readOnlyList = await Updater.InstallAsync(release, package, progress);
			if (readOnlyList.Count > 0)
			{
				Prompt.Tell("Updated, with files left out", "Windows or an antivirus would not let these through, so the old ones were kept:\n" + string.Join("\n", readOnlyList.Take(5)) + "\n\nThe game runs without them; online play may need them. Allow the Bloodborne PC folder in the antivirus and update again, or take them from " + Updater.ReleasesPage);
			}
			Application.Current.Shutdown();
		}
		catch (Exception ex)
		{
			TitleUpdate.IsEnabled = true;
			TitleUpdateText.Text = "Update " + release.Tag;
			ws.Log("Update failed: " + ex.GetBaseException().Message, LogLevel.Bad);
			Prompt.Tell("Update failed", ex.GetBaseException().Message + "\n\nYou can download it by hand from " + Updater.ReleasesPage);
		}
	}

	public async Task PlayOrStopAsync()
	{
		Workspace ws = Workspace.Current;
		if (ws.Game.Running)
		{
			await ws.StopGameAsync();
			return;
		}
		if (Updater.EngineOutdated)
		{
			int num = Prompt.Choose("Engine update needed", $"This launcher is {Updater.CurrentLabel}, but the engine in this folder is older ({Updater.LocalEngine() ?? "unknown"}): " + "the crash fixes since then are missing. Updating downloads the latest package and keeps your settings, saves and mods.", "Cancel", "Play anyway", "Update engine");
			if (num <= 0)
			{
				return;
			}
			if (num == 2)
			{
				await UpdateEngineAsync();
				return;
			}
		}
		LauncherSettings s = ws.Settings;
		IniFile ini = ws.Ini;
		ini.Set("online_pool", (s.HuntersDreamToken.Length > 0 && s.Str("hd_name").Length > 0) ? "huntersdream" : "");
		if (!HuntersDream.OnlinePaused && ini.Get("online_enabled") == "1" && ini.Get("online_pool") != "huntersdream")
		{
			Prompt.Tell("Online", "Connect your Hunter's Dream account on the Online page first (with Discord, or without it).\n\nOr turn \"Play online\" off to play offline.");
			Show("online");
			return;
		}
		if (!GameRunner.Custom(s) && !Paths.IsPortDir(Paths.PortDir))
		{
			ws.ResolvePort();
			if (!Paths.IsPortDir(Paths.PortDir))
			{
				if (!Prompt.Confirm("Where is the engine?", "The launcher needs the Bloodborne PC engine (the folder with run.py and bin\\bb-probe.exe). It is normally the folder the launcher came in.", "Choose folder"))
				{
					return;
				}
				OpenFolderDialog openFolderDialog = new OpenFolderDialog
				{
					Title = "Port folder (with Bloodborne.exe and run.py)"
				};
				if (openFolderDialog.ShowDialog(this) != true)
				{
					return;
				}
				if (!Paths.IsPortDir(openFolderDialog.FolderName))
				{
					Prompt.Tell("Not the port folder", "That folder has no run.py with Bloodborne.exe or bin\\bb-probe.exe.");
					return;
				}
				s.Set("port_dir", openFolderDialog.FolderName);
				ws.ResolvePort();
				ws.SaveNow();
			}
		}
		GameEnvironment.RegionPatchOnline = null;
		if (!HuntersDream.OnlinePaused && ini.Get("online_enabled") == "1" && ini.Get("online_connected", "1") == "1")
		{
			List<Patches.Patch> list = Patches.RegionChanging(GameEnvironment.PatchesDir(s));
			if (list != null && list.Count > 0)
			{
				string text = string.Join(", ", list.Select((Patches.Patch p) => "\"" + p.Name + "\""));
				int num2 = Prompt.Choose("Play online with " + text + "?", text + " moves the game to a region that has no Bloodborne server, so Play online fails with \"Failure to acquire information from the Bloodborne server\".\n\nLeave it out for this session? It stays on for offline play.", "Cancel", "Keep it on", "Leave it out");
				if (num2 <= 0)
				{
					return;
				}
				GameEnvironment.RegionPatchOnline = ((num2 == 1) ? "keep" : "skip");
				ws.Log((num2 == 1) ? (text + ": kept on while online; Play online will not reach the server.") : (text + ": left out while online; it applies again with online off."), LogLevel.Step);
			}
		}
		if (!HuntersDream.OnlinePaused && ini.Get("online_enabled") == "1")
		{
			await Online.PrepareTrustAsync();
		}
		GameEnvironment.ResolutionPatchChoice = null;
		(int W, int H) tuple = Choices.OutputSize(ini.Get("output_res", "1920x1080"));
		int num3 = tuple.W;
		int num4 = tuple.H;
		int num5 = num3;
		int num6 = num4;
		if (num5 == 1920 && num6 == 1080)
		{
			List<Patches.Patch> list2 = Patches.ResolutionSetting(GameEnvironment.PatchesDir(s));
			if (list2 != null && list2.Count > 0)
			{
				Match match = list2.Select((Patches.Patch p) => Regex.Match(p.Name, "(\\d{3,5})x(\\d{3,5})")).FirstOrDefault((Match m) => m.Success);
				if (match != null)
				{
					int num7 = int.Parse(match.Groups[1].Value);
					int num8 = int.Parse(match.Groups[2].Value);
					num6 = num8;
					if (num7 != 1920 || num6 != 1080)
					{
						string text2 = string.Join(", ", list2.Select((Patches.Patch p) => "\"" + p.Name + "\""));
						if (s.Str("resolution_patch_native") != text2)
						{
							int num9 = Prompt.Choose($"Use {num7} × {num8} as the output resolution?", text2 + $" makes the game draw at {num7} × {num8} by itself, so the upscaler preset can't lower " + "the render resolution (the upscaler runs as DLAA / native AA).\n\n" + $"Setting the output resolution to {num7} × {num8} instead gives the same picture and shape, " + "and the presets (Quality, Balanced, Performance) work. The patch is then left out while that output is set.", "Cancel", $"Use {num7} × {num8}", "Keep the patch");
							if (num9 <= 0)
							{
								return;
							}
							if (num9 == 1)
							{
								ini.Set("output_res", $"{num7}x{num8}");
								ini.Save();
								num4 = num8;
								num3 = num7;
								GameEnvironment.ResolutionPatchChoice = "skip";
								ws.Log($"Output resolution {num7} × {num8}; {text2} left out (presets work).", LogLevel.Step);
							}
							else
							{
								s.Set("resolution_patch_native", text2);
								ws.SaveNow();
							}
						}
					}
				}
			}
		}
		int num10 = num3;
		num6 = num4;
		if ((num10 != 1920 || num6 != 1080) && GameEnvironment.ResolutionPatchChoice == null)
		{
			List<Patches.Patch> list3 = Patches.ResolutionSetting(GameEnvironment.PatchesDir(s));
			if (list3 != null && list3.Count > 0)
			{
				string text3 = string.Join(", ", list3.Select((Patches.Patch p) => "\"" + p.Name + "\""));
				int num11 = ((s.Str("resolution_patch_always") == text3) ? 1 : Prompt.Choose("Play with " + text3 + "?", $"The output resolution ({num3} × {num4}) already sets the game's resolution and widens the picture and UI to fill the screen. " + text3 + " sets its own on top: the picture keeps the patch's shape, with black bars.\n\nLeave it out? It still applies with a 1920 × 1080 output. \"Always on\" keeps it and does not ask again until the resolution patches switched on in Mods change.", "Cancel", "Keep on", "Leave out", "Always on"));
				if (num11 <= 0)
				{
					return;
				}
				if (num11 == 3)
				{
					s.Set("resolution_patch_always", text3);
					ws.SaveNow();
					num11 = 1;
				}
				GameEnvironment.ResolutionPatchChoice = ((num11 == 1) ? "keep" : "skip");
				ws.Log((num11 == 1) ? $"{text3}: kept on with the {num3} × {num4} output." : (text3 + ": left out; the output resolution sets the picture."), LogLevel.Step);
			}
		}
		Process[] processesByName = Process.GetProcessesByName("bb-probe");
		if (processesByName.Length != 0)
		{
			if (Prompt.Choose("The game is still running in the background", "bb-probe.exe from an earlier start is still running and holds the game's memory, so a new start would fail. Close it and start?", "Cancel", "Close it and start") <= 0)
			{
				return;
			}
			Process[] array = processesByName;
			foreach (Process process in array)
			{
				try
				{
					process.Kill(entireProcessTree: true);
					process.WaitForExit(5000);
				}
				catch
				{
				}
				process.Dispose();
			}
			ws.Log("Closed the game left running in the background.", LogLevel.Step);
		}
		if (!s.Bool("memory_check_off"))
		{
			string text4 = MemoryCheck.Problem(ws.Ini);
			if (text4 != null)
			{
				int num12 = Prompt.Choose("Windows may run out of memory", text4, "Cancel", "Don't ask again", "Start anyway");
				if (num12 <= 0)
				{
					return;
				}
				if (num12 == 1)
				{
					s.Set("memory_check_off", value: true);
				}
			}
		}
		string text5 = ws.StartGame();
		if (text5 != null)
		{
			System.Windows.MessageBox.Show(this, text5, "Could not start the game", System.Windows.MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
		else if (s.Bool("switch_to_output"))
		{
			Show("output");
		}
	}

	private void UpdatePlayButton()
	{
		bool running = Workspace.Current.Game.Running;
		TitlePlayText.Text = (running ? "Stop" : "Play");
		TitlePlayIcon.Symbol = (running ? SymbolRegular.Stop24 : SymbolRegular.Play24);
		TitlePlayIcon.Foreground = (Brush)FindResource(running ? "DangerTextBrush" : "AccentTextFillColorPrimaryBrush");
		TitlePlay.ToolTip = (running ? "Stop the game" : "Start the game");
		foreach (LauncherPage value in _pages.Values)
		{
			(value as PlayPage)?.UpdateRunning();
		}
	}

	private void OnLogged(string message, LogLevel level)
	{
		_log.Add(new LogItem
		{
			Time = DateTime.Now.ToString("HH:mm:ss"),
			Message = message,
			Color = BrushFor(level)
		});
		LogCount.Text = $"{_log.Count} events";
		LogScroller.ScrollToEnd();
		StatusText.Text = message;
		StatusText.ToolTip = message;
		StatusDot.Fill = ((level == LogLevel.Info) ? ((Brush)FindResource("StatusDotIdleBrush")) : BrushFor(level));
	}

	private Brush BrushFor(LogLevel level)
	{
		return (Brush)FindResource(level switch
		{
			LogLevel.Good => "GoodTextBrush", 
			LogLevel.Bad => "DangerTextBrush", 
			LogLevel.Warn => "WarningTextBrush", 
			LogLevel.Step => "AccentTextFillColorPrimaryBrush", 
			_ => "TextFillColorSecondaryBrush", 
		});
	}

	private void ClearLog_Click(object sender, RoutedEventArgs e)
	{
		_log.Clear();
		LogCount.Text = "";
		StatusText.Text = "Ready";
		StatusText.ToolTip = null;
		StatusDot.Fill = (Brush)FindResource("StatusDotIdleBrush");
	}

	private void ToggleLog_Click(object sender, RoutedEventArgs e)
	{
		ToggleLog();
	}

	private void StatusBar_Click(object sender, MouseButtonEventArgs e)
	{
		ToggleLog();
	}

	private void ToggleLog()
	{
		_logExpanded = !_logExpanded;
		LogPanel.Visibility = ((!_logExpanded) ? Visibility.Collapsed : Visibility.Visible);
		if (_logExpanded)
		{
			LogScroller.ScrollToEnd();
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.30.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/Bloodborne Launcher;component/mainwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.30.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			BackdropImage = (System.Windows.Controls.Image)target;
			break;
		case 2:
			BackdropShade = (Rectangle)target;
			break;
		case 3:
			MacButtons = (StackPanel)target;
			break;
		case 4:
			((System.Windows.Controls.Button)target).Click += CloseWindow_Click;
			break;
		case 5:
			((System.Windows.Controls.Button)target).Click += MinimizeWindow_Click;
			break;
		case 6:
			((System.Windows.Controls.Button)target).Click += MaximizeWindow_Click;
			break;
		case 7:
			WinButtons = (StackPanel)target;
			break;
		case 8:
			WinMinimize = (System.Windows.Controls.Button)target;
			WinMinimize.Click += MinimizeWindow_Click;
			break;
		case 9:
			WinMaximize = (System.Windows.Controls.Button)target;
			WinMaximize.Click += MaximizeWindow_Click;
			break;
		case 10:
			WinClose = (System.Windows.Controls.Button)target;
			WinClose.Click += CloseWindow_Click;
			break;
		case 11:
			TitlePage = (Run)target;
			break;
		case 12:
			TitleTools = (StackPanel)target;
			break;
		case 13:
			TitleUpdate = (System.Windows.Controls.Button)target;
			TitleUpdate.Click += TitleUpdate_Click;
			break;
		case 14:
			TitleUpdateText = (System.Windows.Controls.TextBlock)target;
			break;
		case 15:
			TitlePlay = (System.Windows.Controls.Button)target;
			TitlePlay.Click += TitlePlay_Click;
			break;
		case 16:
			TitlePlayIcon = (SymbolIcon)target;
			break;
		case 17:
			TitlePlayText = (System.Windows.Controls.TextBlock)target;
			break;
		case 18:
			((System.Windows.Controls.Button)target).Click += ToggleLog_Click;
			break;
		case 19:
			Sidebar = (Border)target;
			break;
		case 20:
			((RadioButton)target).Checked += SidebarItem_Checked;
			break;
		case 21:
			ClassicPlay = (RadioButton)target;
			ClassicPlay.Checked += SidebarItem_Checked;
			break;
		case 22:
			((RadioButton)target).Checked += SidebarItem_Checked;
			break;
		case 23:
			((RadioButton)target).Checked += SidebarItem_Checked;
			break;
		case 24:
			ClassicControls = (RadioButton)target;
			ClassicControls.Checked += SidebarItem_Checked;
			break;
		case 25:
			((RadioButton)target).Checked += SidebarItem_Checked;
			break;
		case 26:
			ClassicMods = (RadioButton)target;
			ClassicMods.Checked += SidebarItem_Checked;
			break;
		case 27:
			ClassicOnline = (RadioButton)target;
			ClassicOnline.Checked += SidebarItem_Checked;
			break;
		case 28:
			ClassicSettings = (RadioButton)target;
			ClassicSettings.Checked += SidebarItem_Checked;
			break;
		case 29:
			ClassicOutput = (RadioButton)target;
			ClassicOutput.Checked += SidebarItem_Checked;
			break;
		case 30:
			TabStrip = (Border)target;
			break;
		case 31:
			PlayItem = (RadioButton)target;
			PlayItem.Checked += SidebarItem_Checked;
			break;
		case 32:
			((RadioButton)target).Checked += SidebarItem_Checked;
			break;
		case 33:
			((RadioButton)target).Checked += SidebarItem_Checked;
			break;
		case 34:
			ControlsItem = (RadioButton)target;
			ControlsItem.Checked += SidebarItem_Checked;
			break;
		case 35:
			((RadioButton)target).Checked += SidebarItem_Checked;
			break;
		case 36:
			OnlineItem = (RadioButton)target;
			OnlineItem.Checked += SidebarItem_Checked;
			break;
		case 37:
			SettingsItem = (RadioButton)target;
			SettingsItem.Checked += SidebarItem_Checked;
			break;
		case 38:
			MoreItem = (RadioButton)target;
			MoreItem.Checked += SidebarItem_Checked;
			break;
		case 39:
			PageFrame = (Frame)target;
			PageFrame.Navigated += PageFrame_Navigated;
			break;
		case 40:
			LogPanel = (Border)target;
			break;
		case 41:
			LogScroller = (ScrollViewer)target;
			break;
		case 42:
			LogList = (ItemsControl)target;
			break;
		case 43:
			((System.Windows.Controls.Button)target).Click += ClearLog_Click;
			break;
		case 44:
			((Border)target).MouseLeftButtonUp += StatusBar_Click;
			break;
		case 45:
			StatusDot = (Ellipse)target;
			break;
		case 46:
			StatusText = (System.Windows.Controls.TextBlock)target;
			break;
		case 47:
			SavedText = (System.Windows.Controls.TextBlock)target;
			break;
		case 48:
			LogCount = (System.Windows.Controls.TextBlock)target;
			break;
		case 49:
			VersionText = (System.Windows.Controls.TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
