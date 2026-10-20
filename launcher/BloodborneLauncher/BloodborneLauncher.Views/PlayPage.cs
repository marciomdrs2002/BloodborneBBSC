using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using BloodborneLauncher.Core;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public sealed class PlayPage : LauncherPage
{
	private readonly InfoBar _problem;

	private readonly SettingRow _engineCard;

	private readonly Wpf.Ui.Controls.Button _profileButton;

	private readonly System.Windows.Controls.TextBlock _profileHint;

	private readonly System.Windows.Controls.TextBlock _backupHint;

	private readonly Wpf.Ui.Controls.Button _restoreButton;

	private readonly System.Windows.Controls.TextBlock _gameHint;

	private readonly System.Windows.Controls.TextBlock _dlcHint;

	private readonly System.Windows.Controls.TextBlock _userHint;

	private readonly System.Windows.Controls.TextBlock _statGame;

	private readonly System.Windows.Controls.TextBlock _statUpscaler;

	private readonly System.Windows.Controls.TextBlock _statResolution;

	private readonly System.Windows.Controls.TextBlock _statFps;

	private readonly System.Windows.Controls.TextBlock _statVia;

	private readonly SymbolIcon _gameIcon;

	private readonly Wpf.Ui.Controls.Button _play;

	private readonly Wpf.Ui.Controls.Button _resetUser;

	private readonly Wpf.Ui.Controls.Button _resetDlc;

	private readonly System.Windows.Controls.TextBlock _playStatus;

	private readonly DispatcherTimer _clock;

	public PlayPage()
		: base("Play", "Check the game folder, choose where saves go, and start Bloodborne.")
	{
		Border border = new Border
		{
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0),
			Padding = new Thickness(18.0, 16.0, 18.0, 16.0),
			CornerRadius = new CornerRadius(SettingRow.Classic ? 8 : 16),
			BorderThickness = new Thickness(1.0)
		};
		border.SetResourceReference(Border.BackgroundProperty, "GlassPaneBrush");
		border.SetResourceReference(Border.BorderBrushProperty, "GlassPaneStrokeBrush");
		StackPanel stackPanel = new StackPanel();
		UniformGrid uniformGrid = new UniformGrid
		{
			Rows = 1
		};
		uniformGrid.Children.Add(Stat("Game", out _statGame));
		uniformGrid.Children.Add(Stat("Upscaler", out _statUpscaler));
		uniformGrid.Children.Add(Stat("Output", out _statResolution));
		uniformGrid.Children.Add(Stat("Frame rate", out _statFps));
		uniformGrid.Children.Add(Stat("Trophies", out _statVia));
		stackPanel.Children.Add(uniformGrid);
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 16.0, 0.0, 0.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		_play = new Wpf.Ui.Controls.Button
		{
			Appearance = ControlAppearance.Primary,
			Content = "Play",
			Icon = LauncherPage.Icon(SymbolRegular.Play24),
			Padding = new Thickness(26.0, 10.0, 26.0, 10.0),
			FontWeight = FontWeights.SemiBold
		};
		_play.Click += async delegate
		{
			await MainWindow.Instance.PlayOrStopAsync();
		};
		grid.Children.Add(_play);
		_playStatus = LauncherPage.Text("", "Muted");
		_playStatus.Margin = new Thickness(16.0, 0.0, 0.0, 0.0);
		_playStatus.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumn(_playStatus, 1);
		grid.Children.Add(_playStatus);
		stackPanel.Children.Add(grid);
		border.Child = stackPanel;
		base.Body.Children.Add(border);
		_problem = new InfoBar
		{
			IsClosable = false,
			IsOpen = false,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		base.Body.Children.Add(_problem);
		_engineCard = LauncherPage.Card(SymbolRegular.ArrowDownload24, "Engine update available", "This package has an older engine than this launcher: the crash fixes since then are missing. Updating downloads the latest package and keeps your settings, saves and mods.", LauncherPage.SmallButton("Update engine", SymbolRegular.ArrowDownload24, async delegate
		{
			await MainWindow.Instance.UpdateEngineAsync();
		}, ControlAppearance.Primary), out System.Windows.Controls.TextBlock hintBlock);
		_engineCard.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		base.Body.Children.Add(_engineCard);
		if (!LauncherPage.S.Bool("discord_hidden"))
		{
			SettingRow discord = null;
			discord = LauncherPage.Card(SymbolRegular.PeopleCommunity24, "Join the Discord", "Help, news, co-op partners and beta feedback", LauncherPage.Row(LauncherPage.SmallButton("Join", SymbolRegular.Open24, delegate
			{
				OpenDiscord();
			}, ControlAppearance.Primary), LauncherPage.IconButton(SymbolRegular.Dismiss24, "Hide", delegate
			{
				LauncherPage.S.Set("discord_hidden", value: true);
				LauncherPage.Ws.SaveSoon();
				base.Body.Children.Remove(discord);
			})), out hintBlock);
			discord.Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
			base.Body.Children.Add(discord);
		}
		Group("Game");
		SettingRow settingRow = LauncherPage.Card(SymbolRegular.Folder24, "Game folder", "", LauncherPage.Row(LauncherPage.SmallButton("Browse", SymbolRegular.FolderOpen24, delegate
		{
			ChooseGame();
		}), LauncherPage.IconButton(SymbolRegular.Open24, "Open in Explorer", delegate
		{
			Paths.OpenFolder(GameEnvironment.GameDir(LauncherPage.S));
		})), out _gameHint);
		_gameIcon = settingRow.Icon;
		settingRow.ShowIcon = true;
		Add(settingRow);
		_resetDlc = LauncherPage.IconButton(SymbolRegular.Dismiss24, "No DLC folder", delegate
		{
			LauncherPage.S.Set("dlc_dir", "");
			LauncherPage.Ws.SaveSoon();
			Refresh();
		});
		ToggleRow(SymbolRegular.PuzzlePiece24, "The Old Hunters (DLC)", "Off: the game starts without its DLC. The Game of the Year edition (CUSA03173) always has it", () => LauncherPage.S.Bool("dlc_enabled"), delegate(bool v)
		{
			LauncherPage.S.Set("dlc_enabled", v);
			LauncherPage.Ws.SaveSoon();
			Refresh();
		});
		Add(LauncherPage.Card(SymbolRegular.FolderOpen24, "DLC folder", "", LauncherPage.Row(LauncherPage.SmallButton("Browse", SymbolRegular.FolderOpen24, delegate
		{
			ChooseDlc();
		}), LauncherPage.IconButton(SymbolRegular.Open24, "Open in Explorer", delegate
		{
			Paths.OpenFolder(GameEnvironment.DlcDir(LauncherPage.S));
		}), _resetDlc), out _dlcHint));
		Add(LauncherPage.Card(SymbolRegular.Games24, "Add to Steam", "Bloodborne as a non-Steam game in your Steam library: Steam Input, the overlay and Big Picture work with it", LauncherPage.SmallButton("Add to Steam", SymbolRegular.Add24, delegate
		{
			AddToSteam();
		}), out hintBlock));
		_profileButton = LauncherPage.SmallButton("Main", SymbolRegular.Person24, delegate
		{
			ShowProfileMenu();
		});
		Add(LauncherPage.Card(SymbolRegular.People24, "Player profile", "Each profile has its own save files and trophies, so several people can play their own runs on this PC", LauncherPage.Row(_profileButton, LauncherPage.SmallButton("New…", SymbolRegular.PersonAdd24, delegate
		{
			NewProfile();
		}), LauncherPage.IconButton(SymbolRegular.Rename24, "Rename this profile", delegate
		{
			RenameProfile();
		}), LauncherPage.IconButton(SymbolRegular.PersonDelete24, "Remove this profile from the list (its saves stay on disk)", delegate
		{
			RemoveProfile();
		})), out _profileHint));
		_resetUser = LauncherPage.IconButton(SymbolRegular.ArrowUndo24, "Back to the default folder", delegate
		{
			LauncherPage.S.Set("user_dir", "");
			LauncherPage.Ws.SaveSoon();
			Refresh();
		});
		Add(LauncherPage.Card(SymbolRegular.Save24, "Saves folder", "", LauncherPage.Row(LauncherPage.SmallButton("Browse", SymbolRegular.FolderOpen24, delegate
		{
			ChooseUser();
		}), LauncherPage.IconButton(SymbolRegular.Open24, "Open in Explorer", delegate
		{
			Paths.OpenFolder(GameEnvironment.SavesRoot(LauncherPage.S));
		}), _resetUser), out _userHint));
		_restoreButton = LauncherPage.SmallButton("Restore…", SymbolRegular.History24, delegate
		{
			ShowBackupMenu();
		});
		ToggleSwitch backupToggle = new ToggleSwitch
		{
			IsChecked = LauncherPage.S.Bool("save_backups"),
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		backupToggle.Click += delegate
		{
			LauncherPage.S.Set("save_backups", backupToggle.IsChecked == true);
			LauncherPage.Ws.SaveSoon();
			RefreshBackups();
		};
		Add(LauncherPage.Card(SymbolRegular.ArrowSync24, "Save backups", $"Before every start, when this profile's saves changed. The newest {10} are kept", LauncherPage.Row(_restoreButton, LauncherPage.IconButton(SymbolRegular.Open24, "Open the backups folder", delegate
		{
			Directory.CreateDirectory(SaveBackups.Folder(LauncherPage.S));
			Paths.OpenFolder(SaveBackups.Folder(LauncherPage.S));
		}), backupToggle), out _backupHint));
		Add(LauncherPage.Card(SymbolRegular.ArrowImport24, "Import saves", "From shadPS4, another Bloodborne port, or a PS4 save exported decrypted (Apollo Save Tool, Save Wizard). This profile's saves are backed up first", LauncherPage.SmallButton("Import…", SymbolRegular.FolderArrowRight24, delegate
		{
			ImportSaves();
		}), out hintBlock));
		ComboRow(SymbolRegular.LocalLanguage24, "In-game menu language", "The port's settings menu (Insert or L3 + R3)", Choices.MenuLanguages, () => LauncherPage.Ini.Get("menu_language", "en"), delegate(string v)
		{
			LauncherPage.Ini.Set("menu_language", v);
		});
		_clock = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_clock.Tick += delegate
		{
			UpdateRunning();
		};
		LauncherPage.Ws.PortChanged += Refresh;
		LauncherPage.Ws.GameStateChanged += delegate
		{
			RefreshProfile();
			RefreshBackups();
		};
		LauncherPage.Ws.Saved += RefreshSummary;
		LauncherPage.Ws.TrophiesChanged += RefreshSummary;
	}

	private static StackPanel Stat(string label, out System.Windows.Controls.TextBlock value)
	{
		StackPanel obj = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
			Children = { (UIElement)LauncherPage.Text(label, "StatLabel") }
		};
		value = LauncherPage.Text("—", "StatValue");
		obj.Children.Add(value);
		return obj;
	}

	public override void OnShown()
	{
		Refresh();
	}

	private void Refresh()
	{
		string text = GameEnvironment.GameDir(LauncherPage.S);
		GameStatus gameStatus = GameCheck.Inspect(text);
		LauncherPage.SetHint(_gameHint, string.IsNullOrEmpty(text) ? "Not chosen" : text);
		_gameIcon.Symbol = ((gameStatus.Health == Health.Good) ? SymbolRegular.CheckmarkCircle24 : SymbolRegular.Warning24);
		SymbolIcon gameIcon = _gameIcon;
		Health health = gameStatus.Health;
		gameIcon.SetResourceReference(Page.ForegroundProperty, health switch
		{
			Health.Good => "GoodTextBrush", 
			Health.Warning => "WarningTextBrush", 
			_ => "DangerTextBrush", 
		});
		_gameIcon.ToolTip = gameStatus.Summary;
		_problem.IsOpen = gameStatus.Health != Health.Good;
		_problem.Severity = ((gameStatus.Health == Health.Bad) ? InfoBarSeverity.Error : InfoBarSeverity.Warning);
		_problem.Title = ((gameStatus.Health == Health.Bad) ? "The game folder needs attention" : "Check the game");
		_problem.Message = gameStatus.Summary;
		string text2 = GameEnvironment.DlcDir(LauncherPage.S);
		bool flag = LauncherPage.S.Str("dlc_dir").Length > 0;
		LauncherPage.SetHint(_dlcHint, (text2.Length <= 0) ? "Only with a base-game dump: the folder of your dumped add-ons, one folder each (The Old Hunters: SPEXPANSIONDLC03). Found on its own when it is beside the game folder. The Game of the Year edition (CUSA03173) has it already" : (flag ? text2 : ("Found next to the game: " + text2)));
		_resetDlc.IsEnabled = flag;
		bool flag2 = LauncherPage.S.Str("user_dir").Length > 0;
		string text3 = GameEnvironment.SavesRoot(LauncherPage.S);
		LauncherPage.SetHint(_userHint, (flag2 ? text3 : ("Default: " + text3)) + "  ·  every profile's saves live here");
		_resetUser.IsEnabled = flag2;
		RefreshProfile();
		RefreshBackups();
		_engineCard.Visibility = ((!Updater.EngineOutdated) ? Visibility.Collapsed : Visibility.Visible);
		_statGame.Text = ((gameStatus.Health == Health.Bad) ? "Not found" : (gameStatus.TitleId + " " + gameStatus.Version).Trim());
		RefreshSummary();
		UpdateRunning();
	}

	private void RefreshSummary()
	{
		_statUpscaler.Text = Label(Choices.Upscalers, LauncherPage.Ini.Get("upscaler"));
		var (value, value2) = Choices.OutputSize(LauncherPage.Ini.Get("output_res", "1920x1080"));
		_statResolution.Text = $"{value} × {value2}";
		_statFps.Text = Label(Choices.FpsModes, GameEnvironment.Fps(LauncherPage.S));
		_statVia.Text = $"{LauncherPage.Ws.Trophies.Count} / {TrophyTracker.All.Length} · {LauncherPage.Ws.Trophies.Completion * 100.0:0}%";
	}

	private static string Label(IReadOnlyList<Choice> choices, string value)
	{
		Choice? choice = choices.FirstOrDefault((Choice c) => c.Value == value);
		return (((object)choice != null) ? choice.Label.Split(" (")[0] : null) ?? value;
	}

	public void UpdateRunning()
	{
		GameRunner game = LauncherPage.Ws.Game;
		_play.Content = (game.Running ? "Stop" : "Play");
		_play.Icon = LauncherPage.Icon(game.Running ? SymbolRegular.Stop24 : SymbolRegular.Play24);
		_play.Appearance = (game.Running ? ControlAppearance.Danger : ControlAppearance.Primary);
		if (game.Running)
		{
			TimeSpan timeSpan = DateTime.Now - game.StartedAt;
			_playStatus.Text = $"Running for {(int)timeSpan.TotalMinutes}:{timeSpan.Seconds:00}  ·  output on the Game output page";
			_clock.Start();
		}
		else
		{
			_clock.Stop();
			System.Windows.Controls.TextBlock playStatus = _playStatus;
			object text;
			if (GameCheck.Inspect(GameEnvironment.GameDir(LauncherPage.S)).Health != Health.Bad)
			{
				SessionStats session = LauncherPage.Ws.Session;
				text = ((session != null && session.Length.TotalSeconds > 10.0) ? ("Last session: " + session.Summary) : "Settings are saved as you change them");
			}
			else
			{
				text = "Choose the game folder first";
			}
			playStatus.Text = (string)text;
		}
	}

	private void OpenDiscord()
	{
		try
		{
			Process.Start(new ProcessStartInfo("https://discord.gg/d3Uv3Cfr5x")
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			LauncherPage.Ws.Log("Could not open the Discord link: " + ex.Message, LogLevel.Warn);
		}
	}

	private void ChooseGame()
	{
		string text = LauncherPage.PickFolder("Game folder (the one with eboot.bin)", GameEnvironment.GameDir(LauncherPage.S));
		if (text != null)
		{
			LauncherPage.S.Set("game_dir", text);
			if (!Paths.IsPortDir(Paths.PortDir))
			{
				LauncherPage.Ws.ResolvePort();
			}
			LauncherPage.Ws.SaveSoon();
			LauncherPage.Ws.Log("Game folder: " + text, LogLevel.Step);
			Refresh();
		}
	}

	private void AddToSteam()
	{
		string text = Path.Combine(Paths.PortDir, "Bloodborne.exe");
		if (!File.Exists(text))
		{
			Prompt.Tell("Add to Steam", "Bloodborne.exe is missing from the Bloodborne PC folder: reinstall the package.");
			return;
		}
		if (SteamShortcut.SteamDir() == null)
		{
			Prompt.Tell("Add to Steam", "Steam is not installed for this Windows user.");
			return;
		}
		while (SteamShortcut.SteamRunning())
		{
			if (Prompt.Choose("Close Steam first", "Steam rewrites its list of non-Steam games when it closes, so it has to be closed while Bloodborne is added. Exit Steam (Steam › Exit, or right-click its tray icon › Exit), then press Try again.", "Cancel", "Try again") <= 0)
			{
				return;
			}
		}
		try
		{
			SteamShortcut.Add(text);
			LauncherPage.Ws.Log("Added Bloodborne to Steam as a non-Steam game", LogLevel.Good);
			Prompt.Tell("Added to Steam", "Bloodborne is in your Steam library after Steam starts again. It starts the game with this launcher's settings; Steam shows it as running until the game ends.");
		}
		catch (Exception ex)
		{
			Prompt.Tell("Add to Steam", ex.Message);
		}
	}

	private void ChooseDlc()
	{
		string text = LauncherPage.PickFolder("DLC folder (your dumped add-ons, like SPEXPANSIONDLC03)", GameEnvironment.DlcDir(LauncherPage.S));
		if (text != null)
		{
			LauncherPage.S.Set("dlc_dir", text);
			LauncherPage.Ws.SaveSoon();
			LauncherPage.Ws.Log("DLC folder: " + text, LogLevel.Step);
			Refresh();
		}
	}

	private void RefreshProfile()
	{
		string text = PlayerProfiles.Current(LauncherPage.S);
		_profileButton.Content = text;
		string text2 = GameEnvironment.UserDir(LauncherPage.S);
		string path = Path.Combine(text2, "savedata");
		bool flag = Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
		TimeSpan time = SessionStats.Playtime(LauncherPage.S, text);
		LauncherPage.SetHint(_profileHint, text2 + (flag ? "  ·  saves found" : "  ·  no saves yet; the game creates them here") + $"  ·  {LauncherPage.Ws.Trophies.Count} / {TrophyTracker.All.Length} trophies" + ((time.TotalMinutes >= 1.0) ? ("  ·  " + SessionStats.Duration(time) + " played") : ""));
	}

	private void RefreshBackups()
	{
		List<SaveBackups.Backup> list;
		try
		{
			list = SaveBackups.List(LauncherPage.S);
		}
		catch
		{
			list = new List<SaveBackups.Backup>();
		}
		_restoreButton.IsEnabled = list.Count > 0;
		LauncherPage.SetHint(_backupHint, (!LauncherPage.S.Bool("save_backups")) ? "Off: no backups are made" : ((list.Count == 0) ? "None yet: the first is made when you next press Play" : $"{list.Count} for {PlayerProfiles.Current(LauncherPage.S)}  ·  newest {list[0].When:d MMM, HH:mm}"));
	}

	private void ShowBackupMenu()
	{
		ContextMenu contextMenu = new ContextMenu
		{
			PlacementTarget = _restoreButton,
			Placement = PlacementMode.Bottom
		};
		foreach (SaveBackups.Backup item in SaveBackups.List(LauncherPage.S))
		{
			System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem
			{
				Header = $"{item.When:d MMM yyyy, HH:mm}  ·  {(double)item.Bytes / 1048576.0:0.0} MB" + (item.MadeBeforeRestore ? "  ·  before a restore" : "")
			};
			SaveBackups.Backup chosen = item;
			menuItem.Click += delegate
			{
				RestoreBackup(chosen);
			};
			contextMenu.Items.Add(menuItem);
		}
		contextMenu.IsOpen = true;
	}

	private void ImportSaves()
	{
		if (LauncherPage.Ws.Game.Running)
		{
			Prompt.Tell("Import saves", "Close the game first: it has the saves open.");
			return;
		}
		string text = null;
		SaveImport.Source source = SaveImport.Known(LauncherPage.S).FirstOrDefault();
		if ((object)source != null)
		{
			int num = Prompt.Choose("Import saves", $"Bloodborne saves found ({source.From}), last saved {source.Saved:d MMM yyyy, HH:mm}:\n{source.Dir}", "Cancel", "Choose others…", "Import these");
			if (num <= 0)
			{
				return;
			}
			if (num == 2)
			{
				text = source.Dir;
			}
		}
		if (text == null)
		{
			int num2 = Prompt.Choose("Import saves", "Choose the folder with your saves (another port's whole folder is fine), or a save file: a .zip of the save, or one of its files (userdata0000).", "Cancel", "A folder…", "A file or .zip…");
			if (num2 <= 0)
			{
				return;
			}
			string text2;
			if (num2 == 1)
			{
				text2 = LauncherPage.PickFolder("The folder with your saves (another port's whole folder is fine)", "");
			}
			else
			{
				OpenFileDialog openFileDialog = new OpenFileDialog
				{
					Title = "A save file: a .zip of the save, or one of its files (userdata0000)",
					Filter = "Bloodborne saves (*.zip;userdata*)|*.zip;userdata*|All files|*.*"
				};
				try
				{
					text2 = ((openFileDialog.ShowDialog() == true) ? SaveImport.FolderOfFile(openFileDialog.FileName) : null);
				}
				catch (Exception ex)
				{
					Prompt.Tell("Import saves", "Could not open that file: " + ex.Message);
					return;
				}
			}
			if (text2 == null)
			{
				return;
			}
			SaveImport.Source source2 = SaveImport.Find(text2, "chosen folder").FirstOrDefault();
			if ((object)source2 == null)
			{
				Prompt.Tell("Import saves", SaveImport.HasEncryptedPs4Save(text2) ? "That is a PS4 save as the console copies it to USB: it is encrypted with that PS4's own keys, which only the console has, so no PC program can open it.\n\nExport it decrypted instead: on a jailbroken PS4 with Apollo Save Tool (Copy save game → decrypted, to USB), or with Save Wizard for PS4. Then import that folder here." : "No Bloodborne saves in that folder or the folders under it (a SPRJ0005 folder with userdata files).");
				return;
			}
			text = source2.Dir;
		}
		string text3 = SaveImport.Target(LauncherPage.S);
		if (string.Equals(Path.GetFullPath(text), Path.GetFullPath(text3), StringComparison.OrdinalIgnoreCase))
		{
			Prompt.Tell("Import saves", "Those are already this profile's saves.");
			return;
		}
		string value = (SaveImport.HasSaves(text3) ? "\n\nThis profile's current saves are backed up first: Save backups › Restore… brings them back." : "");
		if (Prompt.Confirm("Import saves", $"Copy the saves from\n{text}\ninto {PlayerProfiles.Current(LauncherPage.S)}'s saves?{value}", "Import"))
		{
			try
			{
				SaveImport.Import(LauncherPage.S, text);
				LauncherPage.Ws.Log("Saves imported from " + text + ".", LogLevel.Good);
				Prompt.Tell("Saves imported", "Start the game and choose Continue or Load Game.");
			}
			catch (Exception ex2)
			{
				LauncherPage.Ws.Log("Could not import the saves: " + ex2.Message, LogLevel.Bad);
				Prompt.Tell("Import saves", "Could not import: " + ex2.Message);
			}
			Refresh();
		}
	}

	private void RestoreBackup(SaveBackups.Backup backup)
	{
		if (LauncherPage.Ws.Game.Running)
		{
			Prompt.Tell("Restore saves", "Close the game first: it has the saves open.");
		}
		else if (Prompt.Confirm("Restore saves", $"{PlayerProfiles.Current(LauncherPage.S)}'s saves go back to {backup.When:d MMM yyyy, HH:mm}. " + "What is there now is backed up first, so you can undo this from the same list.", "Restore"))
		{
			try
			{
				SaveBackups.Restore(LauncherPage.S, backup);
				LauncherPage.Ws.Log($"Saves restored from {backup.When:d MMM yyyy, HH:mm}.", LogLevel.Good);
			}
			catch (Exception ex)
			{
				LauncherPage.Ws.Log("Could not restore the saves: " + ex.Message, LogLevel.Bad);
				Prompt.Tell("Restore saves", "Could not restore: " + ex.Message);
			}
			Refresh();
		}
	}

	private void ShowProfileMenu()
	{
		ContextMenu contextMenu = new ContextMenu
		{
			PlacementTarget = _profileButton,
			Placement = PlacementMode.Bottom
		};
		string value = PlayerProfiles.Current(LauncherPage.S);
		foreach (string item in PlayerProfiles.All(LauncherPage.S))
		{
			string name = item;
			System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem
			{
				Header = name,
				IsCheckable = true,
				IsChecked = name.Equals(value, StringComparison.OrdinalIgnoreCase)
			};
			menuItem.Click += delegate
			{
				SwitchProfile(name);
			};
			contextMenu.Items.Add(menuItem);
		}
		contextMenu.IsOpen = true;
	}

	private bool GameBlocksProfiles()
	{
		if (!LauncherPage.Ws.Game.Running)
		{
			return false;
		}
		Prompt.Tell("Player profile", "Close the game before changing profiles: it is using this profile's saves.");
		return true;
	}

	private void SwitchProfile(string name)
	{
		if (!name.Equals(PlayerProfiles.Current(LauncherPage.S), StringComparison.OrdinalIgnoreCase) && !GameBlocksProfiles())
		{
			LauncherPage.S.Set("player_profile", PlayerProfiles.IsMain(name) ? "" : name);
			Directory.CreateDirectory(GameEnvironment.UserDir(LauncherPage.S));
			LauncherPage.Ws.Trophies.SetUserDir(GameEnvironment.UserDir(LauncherPage.S));
			LauncherPage.Ws.SaveSoon();
			LauncherPage.Ws.Log("Player profile: " + name + ".", LogLevel.Step);
			Refresh();
		}
	}

	private void NewProfile()
	{
		if (GameBlocksProfiles())
		{
			return;
		}
		string[] array = Prompt.Ask("New player profile", "Its own save files and trophies. The game starts a new character the first time.", new Prompt.Field("Name", "", "e.g. Alex"));
		if (array != null)
		{
			string error;
			string text = PlayerProfiles.Validate(LauncherPage.S, array[0], out error);
			if (text == null)
			{
				Prompt.Tell("New player profile", error);
				return;
			}
			PlayerProfiles.Add(LauncherPage.S, text);
			SwitchProfile(text);
		}
	}

	private void RenameProfile()
	{
		string text = PlayerProfiles.Current(LauncherPage.S);
		if (PlayerProfiles.IsMain(text))
		{
			Prompt.Tell("Rename profile", "Main is the original saves folder and keeps its name.");
		}
		else
		{
			if (GameBlocksProfiles())
			{
				return;
			}
			string[] array = Prompt.Ask("Rename profile", null, new Prompt.Field("Name", text));
			if (array == null)
			{
				return;
			}
			string error;
			string text2 = PlayerProfiles.Validate(LauncherPage.S, array[0], out error, text);
			if (text2 == null)
			{
				Prompt.Tell("Rename profile", error);
				return;
			}
			try
			{
				PlayerProfiles.Rename(LauncherPage.S, text, text2);
			}
			catch (Exception ex)
			{
				Prompt.Tell("Rename profile", "Could not rename its folder: " + ex.Message);
				return;
			}
			LauncherPage.Ws.Trophies.SetUserDir(GameEnvironment.UserDir(LauncherPage.S));
			LauncherPage.Ws.SaveSoon();
			Refresh();
		}
	}

	private void RemoveProfile()
	{
		string text = PlayerProfiles.Current(LauncherPage.S);
		if (PlayerProfiles.IsMain(text))
		{
			Prompt.Tell("Remove profile", "Main is the original saves folder and can't be removed.");
		}
		else if (!GameBlocksProfiles() && Prompt.Confirm("Remove profile", $"{text} is taken off the list and the launcher switches to Main. Its saves and trophies are not deleted: they stay in {GameEnvironment.UserDir(LauncherPage.S)}, and adding a profile with the same name brings them back.", "Remove"))
		{
			PlayerProfiles.Remove(LauncherPage.S, text);
			LauncherPage.S.Set("player_profile", "");
			LauncherPage.Ws.Trophies.SetUserDir(GameEnvironment.UserDir(LauncherPage.S));
			LauncherPage.Ws.SaveSoon();
			Refresh();
		}
	}

	private void ChooseUser()
	{
		if (!GameBlocksProfiles())
		{
			string text = LauncherPage.PickFolder("Saves folder", GameEnvironment.SavesRoot(LauncherPage.S));
			if (text != null)
			{
				LauncherPage.S.Set("user_dir", text);
				LauncherPage.Ws.SaveSoon();
				Refresh();
			}
		}
	}
}
