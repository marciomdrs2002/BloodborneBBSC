using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using BloodborneLauncher.Core;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public sealed class TrophiesPage : LauncherPage
{
	private readonly System.Windows.Controls.TextBlock _percent;

	private readonly System.Windows.Controls.TextBlock _count;

	private readonly System.Windows.Controls.TextBlock _points;

	private readonly System.Windows.Controls.TextBlock _baseText;

	private readonly System.Windows.Controls.TextBlock _dlcText;

	private readonly ProgressBar _overall;

	private readonly ProgressBar _base;

	private readonly ProgressBar _dlc;

	private readonly UniformGrid _grades = new UniformGrid
	{
		Rows = 1,
		Margin = new Thickness(0.0, 16.0, 0.0, 0.0)
	};

	private readonly StackPanel _list = new StackPanel();

	private readonly InfoBar _support;

	private readonly System.Windows.Controls.TextBlock _logHint;

	private string _filter = "all";

	public TrophiesPage()
		: base("Trophies", "Bloodborne's 40 trophies, your completion rate, and the notification shown in game when one unlocks.")
	{
		_support = new InfoBar
		{
			IsClosable = false,
			Severity = InfoBarSeverity.Warning,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
		};
		base.Body.Children.Add(_support);
		Card card = new Card
		{
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0)
		};
		StackPanel stackPanel = new StackPanel();
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = GridLength.Auto
				},
				new ColumnDefinition()
			}
		};
		_percent = new System.Windows.Controls.TextBlock
		{
			FontSize = 40.0,
			FontWeight = FontWeights.SemiBold,
			VerticalAlignment = VerticalAlignment.Center
		};
		_percent.SetResourceReference(System.Windows.Controls.TextBlock.FontFamilyProperty, "MonoFont");
		grid.Children.Add(_percent);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(18.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel2.Children.Add(LauncherPage.Text("COMPLETION", "StatLabel"));
		_count = LauncherPage.Text("", "RowTitle");
		_points = LauncherPage.Text("", "Faint");
		stackPanel2.Children.Add(_count);
		stackPanel2.Children.Add(_points);
		Grid.SetColumn(stackPanel2, 1);
		grid.Children.Add(stackPanel2);
		stackPanel.Children.Add(grid);
		_overall = Bar(10.0);
		_overall.Margin = new Thickness(0.0, 14.0, 0.0, 0.0);
		stackPanel.Children.Add(_overall);
		UniformGrid uniformGrid = new UniformGrid
		{
			Rows = 1,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
		};
		uniformGrid.Children.Add(Part("Bloodborne", out _baseText, out _base));
		uniformGrid.Children.Add(Part("The Old Hunters", out _dlcText, out _dlc));
		stackPanel.Children.Add(uniformGrid);
		stackPanel.Children.Add(_grades);
		card.Content = stackPanel;
		base.Body.Children.Add(card);
		Grid grid2 = new Grid
		{
			Margin = new Thickness(2.0, 24.0, 0.0, 8.0)
		};
		grid2.ColumnDefinitions.Add(new ColumnDefinition());
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		System.Windows.Controls.TextBlock textBlock = LauncherPage.Text("All trophies", "GroupHeader");
		textBlock.Margin = new Thickness(0.0);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		grid2.Children.Add(textBlock);
		ComboBox filter = new ComboBox
		{
			Width = 150.0
		};
		(string, string)[] array = new(string, string)[3]
		{
			("All", "all"),
			("Earned", "earned"),
			("Locked", "locked")
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (content, tag) = array[i];
			filter.Items.Add(new ComboBoxItem
			{
				Content = content,
				Tag = tag
			});
		}
		filter.SelectedIndex = 0;
		filter.SelectionChanged += delegate
		{
			_filter = (string)((ComboBoxItem)filter.SelectedItem).Tag;
			Refresh();
		};
		Grid.SetColumn(filter, 1);
		grid2.Children.Add(filter);
		base.Body.Children.Add(grid2);
		base.Body.Children.Add(_list);
		Group("Notification");
		SettingToggle(SymbolRegular.Alert24, "Show in game", "A small banner in a corner of the game's screen, without taking focus. In fullscreen it needs Display › Borderless fullscreen", "trophy_notifications");
		ComboRow(SymbolRegular.Pin24, "Position", null, new Choice[4]
		{
			new Choice("Top right", "top-right"),
			new Choice("Top left", "top-left"),
			new Choice("Bottom right", "bottom-right"),
			new Choice("Bottom left", "bottom-left")
		}, () => LauncherPage.S.Str("trophy_position"), delegate(string v)
		{
			LauncherPage.S.Set("trophy_position", v);
		});
		SettingToggle(SymbolRegular.Speaker224, "Play a sound", null, "trophy_sound");
		SliderRow(SymbolRegular.Speaker124, "Volume", null, 0.0, 100.0, 5.0, () => LauncherPage.S.Int("trophy_volume"), delegate(double v)
		{
			LauncherPage.S.Set("trophy_volume", (int)v);
		}, "0");
		Add(LauncherPage.Card(SymbolRegular.MusicNote224, "Sound file", "A .wav of your own; empty uses the built-in chime", LauncherPage.Row(LauncherPage.SmallButton("Choose…", SymbolRegular.FolderOpen24, delegate
		{
			ChooseSound();
		}), LauncherPage.IconButton(SymbolRegular.ArrowUndo24, "Use the built-in chime", delegate
		{
			LauncherPage.S.Set("trophy_sound_file", "");
			LauncherPage.Ws.SaveSoon();
			LauncherPage.Ws.Log("Trophy sound: built-in chime.", LogLevel.Step);
		})), out System.Windows.Controls.TextBlock hintBlock));
		Add(LauncherPage.Card(SymbolRegular.PlayCircle24, "Try it", "Shows a sample notification and plays the sound", LauncherPage.SmallButton("Test", SymbolRegular.Trophy24, delegate
		{
			TrophyToast.Announce(TrophyTracker.ById[14]);
		}), out hintBlock));
		Add(LauncherPage.Card(SymbolRegular.Folder24, "Trophy log", "", LauncherPage.IconButton(SymbolRegular.Open24, "Open the saves folder", delegate
		{
			Paths.OpenFolder(GameEnvironment.UserDir(LauncherPage.S));
		}), out _logHint));
		Add(LauncherPage.Card(SymbolRegular.ArrowReset24, "Reset trophy progress", "Starts trophies and completion over, for a fresh run. The old log is kept as a backup", LauncherPage.SmallButton("Reset…", SymbolRegular.ArrowReset24, delegate
		{
			ResetProgress();
		}), out hintBlock));
		LauncherPage.Ws.TrophiesChanged += Refresh;
	}

	private void ResetProgress()
	{
		if (LauncherPage.Ws.Trophies.Count == 0)
		{
			Prompt.Tell("Reset trophy progress", "There is no trophy progress to reset.");
		}
		else if (Prompt.Confirm("Reset trophy progress", $"All {LauncherPage.Ws.Trophies.Count} unlocked trophies and the completion rate start over. Your saves are not touched, " + "and the current trophy log is kept as a backup in the saves folder.", "Reset"))
		{
			try
			{
				string text = LauncherPage.Ws.Trophies.Reset();
				LauncherPage.Ws.Log((text == null) ? "Trophy progress reset." : ("Trophy progress reset. Backup: " + text), LogLevel.Good);
			}
			catch (Exception ex)
			{
				LauncherPage.Ws.Log("Could not reset trophy progress: " + ex.Message, LogLevel.Bad);
			}
			Refresh();
		}
	}

	private static ProgressBar Bar(double height)
	{
		ProgressBar progressBar = new ProgressBar();
		progressBar.Height = height;
		progressBar.Minimum = 0.0;
		progressBar.Maximum = 1.0;
		progressBar.SetResourceReference(Page.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
		return progressBar;
	}

	private static StackPanel Part(string name, out System.Windows.Controls.TextBlock text, out ProgressBar bar)
	{
		StackPanel obj = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 18.0, 0.0)
		};
		Grid grid = new Grid
		{
			Children = { (UIElement)LauncherPage.Text(name, "Muted") }
		};
		text = LauncherPage.Text("", "Faint");
		text.HorizontalAlignment = HorizontalAlignment.Right;
		grid.Children.Add(text);
		obj.Children.Add(grid);
		bar = Bar(5.0);
		bar.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		obj.Children.Add(bar);
		return obj;
	}

	public override void OnShown()
	{
		string text = Path.Combine(Paths.BinariesDir, "bb-probe.exe");
		bool flag = File.Exists(text) && TrophyTracker.RuntimeReportsTrophies(text);
		bool flag2 = File.Exists(LauncherPage.Ws.Trophies.LogPath);
		_support.IsOpen = !flag && !flag2;
		_support.Title = "This build of the game does not report trophies";
		_support.Message = "Its bb-probe.exe accepts unlocks silently, so nothing can be tracked. A build with trophy hooks (for example the Windows release of github.com/pyL1nx/bloodborne_pc) writes trophies.log, and the launcher picks it up from there.";
		Refresh();
	}

	private void Refresh()
	{
		TrophyTracker t = LauncherPage.Ws.Trophies;
		LauncherPage.SetHint(_logHint, "trophies.log in the saves folder of the " + PlayerProfiles.Current(LauncherPage.S) + " profile: one line per unlock (id and time)");
		_percent.Text = $"{t.Completion * 100.0:0}%";
		_count.Text = $"{t.Count} of {TrophyTracker.All.Length} trophies";
		_points.Text = $"{t.EarnedPoints} of {t.TotalPoints} points (weighted by grade, as on PlayStation)";
		_overall.Value = t.Completion;
		var (num, num2) = t.Progress(dlc: false);
		var (num3, num4) = t.Progress(dlc: true);
		_baseText.Text = $"{num} / {num2}";
		_base.Value = (double)num / (double)num2;
		_dlcText.Text = $"{num3} / {num4}";
		_dlc.Value = (double)num3 / (double)num4;
		_grades.Children.Clear();
		Grade[] array = new Grade[4]
		{
			Grade.Platinum,
			Grade.Gold,
			Grade.Silver,
			Grade.Bronze
		};
		for (int i = 0; i < array.Length; i++)
		{
			Grade grade = array[i];
			List<Trophy> list = TrophyTracker.All.Where((Trophy x) => x.Grade == grade).ToList();
			int value = list.Count((Trophy x) => t.Unlocked.ContainsKey(x.Id));
			SymbolIcon symbolIcon = new SymbolIcon
			{
				Symbol = SymbolRegular.Trophy24,
				Filled = true,
				FontSize = 18.0,
				Foreground = new SolidColorBrush(TrophyToast.GradeColor(grade))
			};
			System.Windows.Controls.TextBlock textBlock = LauncherPage.Text($"{value} / {list.Count}", "StatValue");
			textBlock.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
			System.Windows.Controls.TextBlock element = LauncherPage.Text(grade.ToString(), "StatLabel");
			StackPanel stackPanel = new StackPanel();
			stackPanel.Children.Add(element);
			stackPanel.Children.Add(LauncherPage.Row(symbolIcon, textBlock));
			_grades.Children.Add(stackPanel);
		}
		_list.Children.Clear();
		Trophy[] all = TrophyTracker.All;
		foreach (Trophy trophy in all)
		{
			long value2;
			bool flag = t.Unlocked.TryGetValue(trophy.Id, out value2);
			if ((!(_filter == "earned") || flag) && !(_filter == "locked" && flag))
			{
				string text = trophy.Description + (trophy.Dlc ? "  ·  The Old Hunters" : "");
				System.Windows.Controls.TextBlock textBlock2 = LauncherPage.Text(flag ? DateTimeOffset.FromUnixTimeSeconds(value2).LocalDateTime.ToString("d MMM yyyy, HH:mm") : "Locked", flag ? "Muted" : "Faint");
				textBlock2.VerticalAlignment = VerticalAlignment.Center;
				textBlock2.TextAlignment = TextAlignment.Right;
				textBlock2.MinWidth = 130.0;
				System.Windows.Controls.TextBlock hintBlock;
				SettingRow settingRow = LauncherPage.Card(SymbolRegular.Trophy24, trophy.Name, text, textBlock2, out hintBlock);
				SymbolIcon icon = settingRow.Icon;
				settingRow.ShowIcon = true;
				LauncherPage.SetHint(settingRow.Hint, text);
				icon.Filled = flag;
				icon.Foreground = (flag ? new SolidColorBrush(TrophyToast.GradeColor(trophy.Grade)) : ((Brush)FindResource("TextFillColorTertiaryBrush")));
				settingRow.Opacity = (flag ? 1.0 : 0.62);
				settingRow.ToolTip = $"{trophy.Grade} · {trophy.Points} points";
				_list.Children.Add(settingRow);
			}
		}
		if (_list.Children.Count == 0)
		{
			_list.Children.Add(LauncherPage.Text((_filter == "earned") ? "No trophies earned yet." : "Every trophy is earned. Hats off!", "Faint"));
		}
	}

	private void ChooseSound()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Trophy sound",
			Filter = "WAV sound|*.wav"
		};
		if (openFileDialog.ShowDialog() == true)
		{
			LauncherPage.S.Set("trophy_sound_file", openFileDialog.FileName);
			LauncherPage.Ws.SaveSoon();
			Chime.Play(LauncherPage.S.Int("trophy_volume"), openFileDialog.FileName);
		}
	}
}
