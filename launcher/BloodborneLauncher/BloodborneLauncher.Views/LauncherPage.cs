using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BloodborneLauncher.Core;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public abstract class LauncherPage : Page
{
	private bool _sectioned;

	protected static Workspace Ws => Workspace.Current;

	protected static LauncherSettings S => Ws.Settings;

	protected static IniFile Ini => Ws.Ini;

	protected StackPanel Body { get; }

	protected ScrollViewer Scroller { get; }

	protected LauncherPage(string title, string subtitle)
	{
		base.Background = Brushes.Transparent;
		SetResourceReference(Page.ForegroundProperty, "TextFillColorPrimaryBrush");
		SetResourceReference(TextElement.ForegroundProperty, "TextFillColorPrimaryBrush");
		Body = new StackPanel
		{
			Margin = new Thickness(20.0, 6.0, 20.0, 24.0),
			MaxWidth = 880.0
		};
		Scroller = new ScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			Content = Body
		};
		base.Content = Scroller;
		PageScroll.Attach(Scroller);
		if (SettingRow.Classic)
		{
			Body.MaxWidth = double.PositiveInfinity;
		}
		base.Loaded += delegate
		{
			if (!_sectioned && !SettingRow.Classic)
			{
				_sectioned = true;
				Sectionize();
			}
		};
	}

	public virtual void OnShown()
	{
	}

	private void Sectionize()
	{
		List<UIElement> list = Body.Children.Cast<UIElement>().ToList();
		Body.Children.Clear();
		StackPanel stackPanel = null;
		foreach (UIElement item in list)
		{
			if (item is System.Windows.Controls.TextBlock { Tag: string tag } && tag == "group")
			{
				Body.Children.Add(item);
				stackPanel = new StackPanel
				{
					Margin = new Thickness(0.0, -1.0, 0.0, 0.0)
				};
				Border frame = new Border
				{
					CornerRadius = new CornerRadius(16.0),
					BorderThickness = new Thickness(1.0),
					Child = stackPanel
				};
				frame.SizeChanged += delegate(object _, SizeChangedEventArgs e)
				{
					frame.Clip = new RectangleGeometry(new Rect(0.0, 0.0, e.NewSize.Width, e.NewSize.Height), 16.0, 16.0);
				};
				frame.SetResourceReference(Border.BackgroundProperty, "GlassPaneBrush");
				frame.SetResourceReference(Border.BorderBrushProperty, "GlassPaneStrokeBrush");
				Body.Children.Add(frame);
				continue;
			}
			bool flag = item is SettingRow || item is StackPanel || item is WrapPanel || (item is System.Windows.Controls.TextBlock && stackPanel != null);
			if (stackPanel != null && flag)
			{
				if (item is System.Windows.Controls.TextBlock textBlock2)
				{
					textBlock2.Margin = new Thickness(14.0, 10.0, 14.0, 6.0);
				}
				if (item is WrapPanel wrapPanel)
				{
					wrapPanel.Margin = new Thickness(10.0, 4.0, 10.0, 10.0);
				}
				stackPanel.Children.Add(item);
			}
			else
			{
				stackPanel = null;
				Body.Children.Add(item);
			}
		}
	}

	public static System.Windows.Controls.TextBlock Text(string text, string style)
	{
		System.Windows.Controls.TextBlock textBlock = new System.Windows.Controls.TextBlock();
		textBlock.Text = text;
		textBlock.SetResourceReference(FrameworkElement.StyleProperty, style);
		return textBlock;
	}

	protected System.Windows.Controls.TextBlock Group(string text, Panel? into = null)
	{
		System.Windows.Controls.TextBlock textBlock = Text(text, "GroupHeader");
		textBlock.Tag = "group";
		(into ?? Body).Children.Add(textBlock);
		return textBlock;
	}

	public static SymbolIcon Icon(SymbolRegular symbol)
	{
		return new SymbolIcon
		{
			Symbol = symbol
		};
	}

	public static SettingRow Card(SymbolRegular icon, string title, string? info, object? control, out System.Windows.Controls.TextBlock hintBlock, bool wide = false)
	{
		SettingRow settingRow = new SettingRow(icon, title, info, control, wide);
		hintBlock = settingRow.Hint;
		return settingRow;
	}

	public static void SetHint(System.Windows.Controls.TextBlock hint, string? text)
	{
		hint.Text = text ?? "";
		hint.Visibility = (string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible);
	}

	protected SettingRow Add(SettingRow row, Panel? into = null)
	{
		(into ?? Body).Children.Add(row);
		return row;
	}

	public static Wpf.Ui.Controls.Button SmallButton(string text, SymbolRegular icon, RoutedEventHandler click, ControlAppearance look = ControlAppearance.Secondary)
	{
		Wpf.Ui.Controls.Button button = new Wpf.Ui.Controls.Button();
		button.Content = text;
		button.Icon = Icon(icon);
		button.Appearance = look;
		button.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		button.Padding = new Thickness(10.0, 5.0, 10.0, 5.0);
		button.VerticalAlignment = VerticalAlignment.Center;
		button.Click += click;
		return button;
	}

	public static Wpf.Ui.Controls.Button IconButton(SymbolRegular icon, string tooltip, RoutedEventHandler click)
	{
		Wpf.Ui.Controls.Button button = new Wpf.Ui.Controls.Button();
		button.Icon = Icon(icon);
		button.ToolTip = tooltip;
		button.Appearance = ControlAppearance.Transparent;
		button.Padding = new Thickness(6.0);
		button.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
		button.VerticalAlignment = VerticalAlignment.Center;
		button.Click += click;
		return button;
	}

	public static StackPanel Row(params UIElement[] children)
	{
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		foreach (UIElement element in children)
		{
			stackPanel.Children.Add(element);
		}
		return stackPanel;
	}

	protected FrameworkElement ComboRow(SymbolRegular icon, string title, string? hint, IReadOnlyList<Choice> choices, Func<string> get, Action<string> set, Panel? into = null, double width = 240.0, Action? changed = null, Func<Choice, bool>? enabled = null)
	{
		System.Windows.Controls.TextBlock note = null;
		int num = choices.Sum((Choice c) => Segmented.Short(c.Label).Length);
		if (!SettingRow.Classic)
		{
			int count = choices.Count;
			if (count >= 2 && count <= 6 && num <= 72)
			{
				Segmented segmented = new Segmented(choices, enabled);
				SettingRow settingRow = Add(Card(icon, title, hint, segmented, out note, wide: true), into);
				segmented.Select(Choices.IndexOf(choices, get()), notify: false);
				ShowNote(segmented.SelectedIndex);
				segmented.Changed += Picked;
				settingRow.Tag = note;
				return segmented;
			}
		}
		ComboBox box = new ComboBox
		{
			Width = width,
			VerticalAlignment = VerticalAlignment.Center
		};
		foreach (Choice choice in choices)
		{
			box.Items.Add(new ComboBoxItem
			{
				Content = choice.Label,
				Tag = choice.Value
			});
		}
		SettingRow settingRow2 = Add(Card(icon, title, hint, box, out note), into);
		box.SelectedIndex = Choices.IndexOf(choices, get());
		ShowNote(box.SelectedIndex);
		box.SelectionChanged += delegate
		{
			if (box.SelectedIndex >= 0)
			{
				Picked(box.SelectedIndex);
			}
		};
		settingRow2.Tag = note;
		return box;
		void Picked(int index)
		{
			set(choices[index].Value);
			ShowNote(index);
			Ws.SaveSoon();
			changed?.Invoke();
		}
		void ShowNote(int index)
		{
			SetHint(note, (index >= 0) ? choices[index].Note : null);
		}
	}

	protected FrameworkElement SettingCombo(SymbolRegular icon, string title, string? hint, IReadOnlyList<Choice> choices, string key, Panel? into = null, Action? changed = null)
	{
		return ComboRow(icon, title, hint, choices, () => S.Str(key), delegate(string v)
		{
			S.Set(key, v);
		}, into, 240.0, changed);
	}

	protected FrameworkElement IniCombo(SymbolRegular icon, string title, string? hint, IReadOnlyList<Choice> choices, string key, Panel? into = null, Action? changed = null, Func<Choice, bool>? enabled = null)
	{
		return ComboRow(icon, title, hint, choices, () => Ini.Get(key), delegate(string v)
		{
			Ini.Set(key, v);
		}, into, 240.0, changed, enabled);
	}

	protected ToggleSwitch ToggleRow(SymbolRegular icon, string title, string? hint, Func<bool> get, Action<bool> set, Panel? into = null, Action? changed = null)
	{
		ToggleSwitch toggle = new ToggleSwitch
		{
			IsChecked = get(),
			VerticalAlignment = VerticalAlignment.Center
		};
		toggle.Click += delegate
		{
			set(toggle.IsChecked == true);
			Ws.SaveSoon();
			changed?.Invoke();
		};
		Add(Card(icon, title, hint, toggle, out System.Windows.Controls.TextBlock _), into);
		return toggle;
	}

	protected ToggleSwitch SettingToggle(SymbolRegular icon, string title, string? hint, string key, Panel? into = null, Action? changed = null)
	{
		return ToggleRow(icon, title, hint, () => S.Bool(key), delegate(bool v)
		{
			S.Set(key, v);
		}, into, changed);
	}

	protected ToggleSwitch IniToggle(SymbolRegular icon, string title, string? hint, string key, Panel? into = null, Action? changed = null)
	{
		return ToggleRow(icon, title, hint, () => Ini.Flag(key), delegate(bool v)
		{
			Ini.Set(key, v ? "1" : "0");
		}, into, changed);
	}

	protected Slider SliderRow(SymbolRegular icon, string title, string? hint, double min, double max, double step, Func<double> get, Action<double> set, string format = "0.00", Panel? into = null, double valueWidth = 44.0)
	{
		Slider slider = new Slider
		{
			Minimum = min,
			Maximum = max,
			SmallChange = step,
			LargeChange = step * 4.0,
			TickFrequency = step,
			IsSnapToTickEnabled = true,
			Width = 260.0,
			VerticalAlignment = VerticalAlignment.Center,
			Value = Math.Clamp(get(), min, max)
		};
		System.Windows.Controls.TextBlock value = new System.Windows.Controls.TextBlock
		{
			Width = valueWidth,
			TextAlignment = TextAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
		};
		value.SetResourceReference(System.Windows.Controls.TextBlock.FontFamilyProperty, "MonoFont");
		value.Text = slider.Value.ToString(format, CultureInfo.InvariantCulture);
		slider.ValueChanged += delegate
		{
			value.Text = slider.Value.ToString(format, CultureInfo.InvariantCulture);
			set(slider.Value);
			Ws.SaveSoon();
		};
		Add(Card(icon, title, hint, Row(value, slider), out System.Windows.Controls.TextBlock _), into);
		return slider;
	}

	protected Wpf.Ui.Controls.TextBox TextRow(SymbolRegular icon, string title, string? hint, string placeholder, Func<string> get, Action<string> set, Panel? into = null, double width = 300.0)
	{
		Wpf.Ui.Controls.TextBox box = new Wpf.Ui.Controls.TextBox
		{
			Text = get(),
			PlaceholderText = placeholder,
			PlaceholderEnabled = true,
			Width = width,
			VerticalAlignment = VerticalAlignment.Center
		};
		box.TextChanged += delegate
		{
			set(box.Text);
			Ws.SaveSoon();
		};
		Add(Card(icon, title, hint, box, out System.Windows.Controls.TextBlock _), into);
		return box;
	}

	protected static string Fmt(double value)
	{
		return value.ToString("0.00", CultureInfo.InvariantCulture);
	}

	protected static double ParseDouble(string text, double fallback)
	{
		if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return fallback;
		}
		return result;
	}

	protected static string? PickFolder(string title, string current)
	{
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = title
		};
		try
		{
			if (Directory.Exists(current))
			{
				openFolderDialog.InitialDirectory = Path.GetFullPath(current);
			}
			return (openFolderDialog.ShowDialog() == true) ? openFolderDialog.FolderName : null;
		}
		catch (ArgumentException)
		{
			openFolderDialog = new OpenFolderDialog
			{
				Title = title
			};
			return (openFolderDialog.ShowDialog() == true) ? openFolderDialog.FolderName : null;
		}
	}
}
