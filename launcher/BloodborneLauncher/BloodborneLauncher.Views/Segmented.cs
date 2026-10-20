using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BloodborneLauncher.Core;

namespace BloodborneLauncher.Views;

public sealed class Segmented : Border
{
	private readonly List<Border> _items = new List<Border>();

	private readonly IReadOnlyList<Choice> _choices;

	public int SelectedIndex { get; private set; } = -1;

	public event Action<int>? Changed;

	public Segmented(IReadOnlyList<Choice> choices, Func<Choice, bool>? enabled = null)
	{
		_choices = choices;
		base.CornerRadius = new CornerRadius(14.0);
		base.Padding = new Thickness(3.0);
		SetResourceReference(Border.BackgroundProperty, "PillTrackBrush");
		Grid grid = new Grid();
		for (int i = 0; i < choices.Count; i++)
		{
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			int index = i;
			Choice choice = choices[i];
			TextBlock textBlock = new TextBlock
			{
				Text = Short(choice.Label),
				FontSize = 12.5,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				TextTrimming = TextTrimming.CharacterEllipsis
			};
			Border item = new Border
			{
				CornerRadius = new CornerRadius(11.0),
				Padding = new Thickness(8.0, 5.0, 8.0, 5.0),
				Child = textBlock,
				Cursor = Cursors.Hand,
				Background = Brushes.Transparent,
				ToolTip = ((choice.Label != Short(choice.Label) || choice.Note != null) ? string.Join("\n", new string[2] { choice.Label, choice.Note }.Where((string t) => t != null)) : null)
			};
			bool flag = enabled?.Invoke(choice) ?? true;
			item.IsEnabled = flag;
			if (!flag)
			{
				textBlock.Opacity = 0.35;
			}
			item.MouseLeftButtonUp += delegate
			{
				Select(index, notify: true);
			};
			item.MouseEnter += delegate
			{
				if (index != SelectedIndex)
				{
					item.SetResourceReference(Border.BackgroundProperty, "SidebarItemHoverBrush");
				}
			};
			item.MouseLeave += delegate
			{
				if (index != SelectedIndex)
				{
					item.Background = Brushes.Transparent;
				}
			};
			Grid.SetColumn(item, i);
			grid.Children.Add(item);
			_items.Add(item);
		}
		Child = grid;
	}

	public static string Short(string label)
	{
		return label.Split(" (")[0];
	}

	public void Select(int index, bool notify)
	{
		if (index < 0 || index >= _items.Count || !_items[index].IsEnabled)
		{
			return;
		}
		SelectedIndex = index;
		for (int i = 0; i < _items.Count; i++)
		{
			TextBlock textBlock = (TextBlock)_items[i].Child;
			if (i == index)
			{
				_items[i].SetResourceReference(Border.BackgroundProperty, "SegmentSelectedBrush");
				textBlock.Foreground = Brushes.White;
				textBlock.FontWeight = FontWeights.SemiBold;
			}
			else
			{
				_items[i].Background = Brushes.Transparent;
				textBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
				textBlock.FontWeight = FontWeights.Normal;
			}
		}
		if (notify)
		{
			this.Changed?.Invoke(index);
		}
	}
}
