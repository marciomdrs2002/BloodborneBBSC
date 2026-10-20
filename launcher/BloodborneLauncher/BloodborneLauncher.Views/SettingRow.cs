using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public sealed class SettingRow : Border
{
	public SymbolIcon Icon { get; }

	public System.Windows.Controls.TextBlock Hint { get; }

	public bool ShowIcon
	{
		get
		{
			return Icon.Visibility == Visibility.Visible;
		}
		set
		{
			Icon.Visibility = ((!value) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	public static bool Classic { get; set; }

	public SettingRow(SymbolRegular icon, string title, string? info, object? control, bool wide)
	{
		base.Padding = (Classic ? new Thickness(16.0, 12.0, 16.0, 12.0) : new Thickness(14.0, 9.0, 14.0, 9.0));
		base.MinHeight = (Classic ? 64 : 42);
		base.BorderThickness = (Classic ? new Thickness(1.0) : new Thickness(0.0, 1.0, 0.0, 0.0));
		SetResourceReference(Border.BorderBrushProperty, Classic ? "CardStrokeColorDefaultBrush" : "GlassDividerBrush");
		if (Classic)
		{
			base.CornerRadius = new CornerRadius(6.0);
			base.Margin = new Thickness(0.0, 0.0, 0.0, 4.0);
			SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
		}
		Icon = new SymbolIcon
		{
			Symbol = icon,
			FontSize = (Classic ? 20 : 18),
			Margin = new Thickness(0.0, 0.0, Classic ? 16 : 12, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			Visibility = ((!Classic) ? Visibility.Collapsed : Visibility.Visible)
		};
		System.Windows.Controls.TextBlock element = new System.Windows.Controls.TextBlock
		{
			Text = title,
			FontSize = 13.5,
			VerticalAlignment = VerticalAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		stackPanel.Children.Add(element);
		if (!string.IsNullOrWhiteSpace(info) && !Classic)
		{
			SymbolIcon symbolIcon = new SymbolIcon
			{
				Symbol = SymbolRegular.Info16,
				FontSize = 12.0,
				Margin = new Thickness(6.0, 1.0, 0.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center,
				ToolTip = info,
				Cursor = Cursors.Help
			};
			symbolIcon.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorTertiaryBrush");
			ToolTipService.SetInitialShowDelay(symbolIcon, 150);
			stackPanel.Children.Add(symbolIcon);
		}
		Hint = new System.Windows.Controls.TextBlock
		{
			FontSize = 11.5,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0),
			Visibility = Visibility.Collapsed
		};
		Hint.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
		StackPanel stackPanel2 = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel2.Children.Add(stackPanel);
		if (Classic && !string.IsNullOrWhiteSpace(info))
		{
			System.Windows.Controls.TextBlock textBlock = new System.Windows.Controls.TextBlock
			{
				Text = info,
				FontSize = 12.0,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
			};
			textBlock.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
			stackPanel2.Children.Add(textBlock);
		}
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = GridLength.Auto
				},
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				},
				new ColumnDefinition
				{
					Width = GridLength.Auto
				}
			},
			Children = { (UIElement)Icon }
		};
		Grid.SetColumn(stackPanel2, 1);
		grid.Children.Add(stackPanel2);
		if (control is UIElement uIElement)
		{
			if (wide)
			{
				grid.RowDefinitions.Add(new RowDefinition
				{
					Height = GridLength.Auto
				});
				grid.RowDefinitions.Add(new RowDefinition
				{
					Height = GridLength.Auto
				});
				if (uIElement is FrameworkElement frameworkElement)
				{
					frameworkElement.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
				}
				Grid.SetRow(uIElement, 1);
				Grid.SetColumn(uIElement, 1);
				Grid.SetColumnSpan(uIElement, 2);
				grid.Children.Add(uIElement);
				grid.RowDefinitions.Add(new RowDefinition
				{
					Height = GridLength.Auto
				});
				Grid.SetRow(Hint, 2);
				Grid.SetColumn(Hint, 1);
				Grid.SetColumnSpan(Hint, 2);
				grid.Children.Add(Hint);
			}
			else
			{
				stackPanel2.Children.Add(Hint);
				if (uIElement is FrameworkElement frameworkElement2)
				{
					frameworkElement2.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
				}
				Grid.SetColumn(uIElement, 2);
				grid.Children.Add(uIElement);
			}
		}
		else
		{
			stackPanel2.Children.Add(Hint);
		}
		Child = grid;
	}
}
