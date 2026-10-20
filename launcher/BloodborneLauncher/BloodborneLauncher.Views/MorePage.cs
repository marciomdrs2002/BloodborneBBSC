using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using BloodborneLauncher.Core;

namespace BloodborneLauncher.Views;

public sealed class MorePage : LauncherPage
{
	private static readonly (string Key, string Label, Type Page)[] Sections = new(string, string, Type)[3]
	{
		("mods", "Mods", typeof(ModsPage)),
		("cheats", "Cheats", typeof(CheatsPage)),
		("output", "Log", typeof(OutputPage))
	}.Where(((string, string, Type) s) => s.Item1 != "cheats" || CheatsPage.Supported).ToArray();

	private readonly Dictionary<Type, LauncherPage> _pages = new Dictionary<Type, LauncherPage>();

	private readonly Frame _frame = new Frame
	{
		NavigationUIVisibility = NavigationUIVisibility.Hidden,
		Focusable = false
	};

	private readonly Segmented _tabs;

	private int _current = -1;

	public MorePage()
		: base("More", "")
	{
		_tabs = new Segmented(Sections.Select(((string Key, string Label, Type Page) s) => new Choice(s.Label, s.Key)).ToArray())
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Width = Sections.Length * 104,
			Margin = new Thickness(0.0, 6.0, 0.0, 2.0)
		};
		_tabs.Changed += Open;
		_frame.Navigated += delegate
		{
			while (_frame.CanGoBack)
			{
				_frame.RemoveBackEntry();
			}
		};
		Grid grid = new Grid
		{
			RowDefinitions = 
			{
				new RowDefinition
				{
					Height = GridLength.Auto
				},
				new RowDefinition()
			},
			Children = { (UIElement)_tabs }
		};
		Grid.SetRow(_frame, 1);
		grid.Children.Add(_frame);
		base.Content = grid;
		Open(0);
	}

	public void ShowSection(string key)
	{
		int num = Array.FindIndex(Sections, ((string Key, string Label, Type Page) s) => s.Key == key);
		if (num >= 0)
		{
			Open(num);
		}
	}

	private void Open(int index)
	{
		_tabs.Select(index, notify: false);
		Type item = Sections[index].Page;
		if (!_pages.TryGetValue(item, out LauncherPage value))
		{
			value = (_pages[item] = (LauncherPage)Activator.CreateInstance(item));
		}
		if (_current != index)
		{
			_frame.Navigate(value);
		}
		_current = index;
		value.OnShown();
	}

	public override void OnShown()
	{
		if (_current >= 0 && _pages.TryGetValue(Sections[_current].Page, out LauncherPage value))
		{
			value.OnShown();
		}
	}
}
