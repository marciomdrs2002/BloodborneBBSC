using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using BloodborneLauncher.Core;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public sealed class OutputPage : LauncherPage
{
	private readonly System.Windows.Controls.TextBox _output;

	private readonly ToggleSwitch _follow;

	private int _lines;

	private bool _stale;

	public OutputPage()
		: base("Game output", "What run.py and the game print. Copy or export it when reporting a problem.")
	{
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 18.0, 0.0, 8.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		_follow = new ToggleSwitch
		{
			IsChecked = true,
			Content = "Follow new output",
			VerticalAlignment = VerticalAlignment.Center
		};
		grid.Children.Add(_follow);
		StackPanel element = LauncherPage.Row(LauncherPage.SmallButton("Copy", SymbolRegular.Copy24, delegate
		{
			Copy();
		}), LauncherPage.SmallButton("Export…", SymbolRegular.Save24, delegate
		{
			Export();
		}), LauncherPage.SmallButton("Clear", SymbolRegular.Broom24, delegate
		{
			LauncherPage.Ws.ClearOutput();
		}));
		Grid.SetColumn(element, 1);
		grid.Children.Add(element);
		base.Body.Children.Add(grid);
		SettingToggle(SymbolRegular.WindowConsole20, "Show this page when the game starts", null, "switch_to_output");
		_output = new System.Windows.Controls.TextBox
		{
			Height = 520.0,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		_output.SetResourceReference(FrameworkElement.StyleProperty, "CodeBox");
		base.Body.Children.Add(_output);
		LauncherPage.Ws.GameOutput += delegate(IReadOnlyList<string> batch)
		{
			if (!base.IsVisible)
			{
				_stale = true;
			}
			else
			{
				_lines += batch.Count;
				if (_lines > 5500)
				{
					Rebuild();
				}
				else
				{
					_output.AppendText(string.Join("\n", batch) + "\n");
					if (_follow.IsChecked == true)
					{
						_output.ScrollToEnd();
					}
				}
			}
		};
		LauncherPage.Ws.OutputCleared += delegate
		{
			_output.Clear();
			_lines = 0;
		};
		base.IsVisibleChanged += delegate
		{
			if (base.IsVisible && _stale)
			{
				Rebuild();
			}
		};
		Rebuild();
	}

	private void Rebuild()
	{
		_output.Text = ((LauncherPage.Ws.OutputLines.Count > 0) ? (string.Join("\n", LauncherPage.Ws.OutputLines) + "\n") : "");
		_lines = LauncherPage.Ws.OutputLines.Count;
		_stale = false;
		if (_follow.IsChecked == true)
		{
			_output.ScrollToEnd();
		}
	}

	public override void OnShown()
	{
		if (_stale)
		{
			Rebuild();
		}
		else if (_follow.IsChecked == true)
		{
			_output.ScrollToEnd();
		}
	}

	private void Copy()
	{
		if (_output.Text.Length != 0)
		{
			Clipboard.SetText(_output.Text);
			LauncherPage.Ws.Log("Game output copied.", LogLevel.Good);
		}
	}

	private void Export()
	{
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Title = "Export the game output",
			FileName = $"bbport-{DateTime.Now:yyyyMMdd_HHmmss}.log",
			Filter = "Log files|*.log|Text files|*.txt|All files|*.*"
		};
		if (saveFileDialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			if (File.Exists(GameRunner.SessionLog))
			{
				using FileStream fileStream = new FileStream(GameRunner.SessionLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
				using FileStream destination = File.Create(saveFileDialog.FileName);
				fileStream.CopyTo(destination);
			}
			else
			{
				File.WriteAllText(saveFileDialog.FileName, _output.Text);
			}
			LauncherPage.Ws.Log("Game output saved: " + saveFileDialog.FileName, LogLevel.Good);
		}
		catch (Exception ex)
		{
			LauncherPage.Ws.Log("Could not save the output: " + ex.Message, LogLevel.Bad);
		}
	}
}
