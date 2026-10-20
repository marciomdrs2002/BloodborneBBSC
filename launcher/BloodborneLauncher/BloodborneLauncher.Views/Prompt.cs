using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public sealed class Prompt : Window
{
	public sealed record Field(string Label, string Value = "", string Placeholder = "", bool Multiline = false);

	private readonly List<Wpf.Ui.Controls.TextBox> _boxes = new List<Wpf.Ui.Controls.TextBox>();

	private int _chosen = -1;

	internal static string? SnapshotPath;

	private Prompt(string title, string? message, IReadOnlyList<Field> fields, string ok, bool showCancel, string[]? choices = null)
	{
		Window mainWindow = Application.Current.MainWindow;
		Window owner = ((mainWindow != null && mainWindow.IsLoaded && mainWindow.WindowState != WindowState.Minimized) ? mainWindow : null);
		base.Owner = owner;
		base.WindowStyle = WindowStyle.None;
		base.ResizeMode = ResizeMode.NoResize;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.ShowInTaskbar = false;
		StackPanel stackPanel = new StackPanel();
		System.Windows.Controls.TextBlock element = LauncherPage.Text(title, "PageTitle");
		stackPanel.Children.Add(element);
		if (!string.IsNullOrEmpty(message))
		{
			System.Windows.Controls.TextBlock textBlock = LauncherPage.Text(message, "Muted");
			textBlock.FontSize = 13.0;
			textBlock.Margin = new Thickness(0.0, 8.0, 0.0, 4.0);
			textBlock.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
			stackPanel.Children.Add(new ScrollViewer
			{
				Content = textBlock,
				MaxHeight = 300.0,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
			});
		}
		foreach (Field field in fields)
		{
			System.Windows.Controls.TextBlock textBlock2 = LauncherPage.Text(field.Label, "StatLabel");
			textBlock2.Margin = new Thickness(0.0, 12.0, 0.0, 4.0);
			stackPanel.Children.Add(textBlock2);
			Wpf.Ui.Controls.TextBox textBox = new Wpf.Ui.Controls.TextBox
			{
				Text = field.Value,
				PlaceholderText = field.Placeholder,
				PlaceholderEnabled = true,
				AcceptsReturn = field.Multiline,
				TextWrapping = ((!field.Multiline) ? TextWrapping.NoWrap : TextWrapping.Wrap),
				MinHeight = (field.Multiline ? 70 : 0),
				MaxHeight = (field.Multiline ? 160.0 : double.PositiveInfinity),
				VerticalScrollBarVisibility = (field.Multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden)
			};
			_boxes.Add(textBox);
			stackPanel.Children.Add(textBox);
		}
		UniformGrid uniformGrid = new UniformGrid
		{
			Rows = 1,
			Margin = new Thickness(0.0, 22.0, 0.0, 0.0),
			HorizontalAlignment = HorizontalAlignment.Right
		};
		if (choices != null)
		{
			uniformGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
			for (int i = 0; i < choices.Length; i++)
			{
				int index = i;
				bool flag = i == choices.Length - 1;
				Wpf.Ui.Controls.Button button = new Wpf.Ui.Controls.Button
				{
					Content = choices[i],
					Margin = new Thickness(0.0, 0.0, (!flag) ? 8 : 0, 0.0),
					HorizontalAlignment = HorizontalAlignment.Stretch,
					Appearance = ((!flag) ? ControlAppearance.Secondary : ControlAppearance.Primary),
					IsDefault = flag
				};
				button.Click += delegate
				{
					_chosen = index;
					base.DialogResult = true;
				};
				uniformGrid.Children.Add(button);
			}
		}
		else if (showCancel)
		{
			Wpf.Ui.Controls.Button button2 = new Wpf.Ui.Controls.Button
			{
				Content = "Cancel",
				Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
				IsCancel = true,
				MinWidth = 110.0,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			button2.Click += delegate
			{
				base.DialogResult = false;
			};
			uniformGrid.Children.Add(button2);
		}
		if (choices == null)
		{
			Wpf.Ui.Controls.Button button3 = new Wpf.Ui.Controls.Button
			{
				Content = ok,
				Appearance = ControlAppearance.Primary,
				IsDefault = true,
				MinWidth = 110.0,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			button3.Click += delegate
			{
				base.DialogResult = true;
			};
			uniformGrid.Children.Add(button3);
		}
		stackPanel.Children.Add(uniformGrid);
		Border card = new Border
		{
			Width = 440.0,
			Padding = new Thickness(24.0, 20.0, 24.0, 20.0),
			CornerRadius = new CornerRadius(16.0),
			BorderThickness = new Thickness(1.0),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Child = stackPanel,
			Effect = new DropShadowEffect
			{
				BlurRadius = 40.0,
				ShadowDepth = 8.0,
				Direction = 270.0,
				Opacity = 0.55,
				Color = Colors.Black
			},
			RenderTransformOrigin = new Point(0.5, 0.5),
			RenderTransform = new ScaleTransform(0.96, 0.96)
		};
		card.SetResourceReference(Border.BackgroundProperty, "DialogBackgroundBrush");
		card.SetResourceReference(Border.BorderBrushProperty, "DialogStrokeBrush");
		SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
		if (owner != null)
		{
			base.WindowStartupLocation = WindowStartupLocation.Manual;
			Border border = new Border
			{
				CornerRadius = new CornerRadius(8.0),
				Child = card
			};
			border.SetResourceReference(Border.BackgroundProperty, "DialogScrimBrush");
			base.Content = border;
			CoverOwner(owner);
		}
		else
		{
			base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			base.SizeToContent = SizeToContent.WidthAndHeight;
			card.Margin = new Thickness(24.0);
			card.MouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
			{
				object originalSource = e.OriginalSource;
				if ((originalSource is Border || originalSource is StackPanel) ? true : false)
				{
					DragMove();
				}
			};
			base.Content = card;
		}
		base.Opacity = 0.0;
		if (SnapshotPath != null)
		{
			base.ShowActivated = false;
		}
		base.Loaded += delegate
		{
			if (SnapshotPath != null && owner != null)
			{
				Snapshot(owner, card);
			}
			CubicEase easingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			TimeSpan timeSpan = TimeSpan.FromMilliseconds(140.0);
			BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, timeSpan)
			{
				EasingFunction = easingFunction
			});
			ScaleTransform obj = (ScaleTransform)card.RenderTransform;
			obj.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, timeSpan)
			{
				EasingFunction = easingFunction
			});
			obj.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, timeSpan)
			{
				EasingFunction = easingFunction
			});
			if (_boxes.Count > 0)
			{
				_boxes[0].Focus();
				_boxes[0].SelectAll();
			}
		};
		base.PreviewKeyDown += delegate(object _, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				base.DialogResult = false;
			}
		};
	}

	private void Snapshot(Window owner, Border card)
	{
		DispatcherTimer timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(500.0)
		};
		timer.Tick += delegate
		{
			timer.Stop();
			int num = (int)owner.ActualWidth;
			int num2 = (int)owner.ActualHeight;
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(num, num2, 96.0, 96.0, PixelFormats.Pbgra32);
			renderTargetBitmap.Render((Visual)owner.Content);
			DrawingVisual drawingVisual = new DrawingVisual();
			using (DrawingContext drawingContext = drawingVisual.RenderOpen())
			{
				drawingContext.DrawRectangle(new VisualBrush((Visual)base.Content), null, new Rect(0.0, 0.0, num, num2));
			}
			renderTargetBitmap.Render(drawingVisual);
			PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder
			{
				Frames = { BitmapFrame.Create(renderTargetBitmap) }
			};
			using (FileStream stream = File.Create(SnapshotPath))
			{
				pngBitmapEncoder.Save(stream);
			}
			base.DialogResult = false;
			Application.Current.Shutdown();
		};
		timer.Start();
	}

	private void CoverOwner(Window owner)
	{
		Point point = (PresentationSource.FromVisual(owner)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity).Transform(owner.PointToScreen(new Point(0.0, 0.0)));
		base.Left = point.X;
		base.Top = point.Y;
		base.Width = owner.ActualWidth;
		base.Height = owner.ActualHeight;
	}

	public static string[]? Ask(string title, string? message, params Field[] fields)
	{
		Prompt prompt = new Prompt(title, message, fields, "OK", showCancel: true);
		if (prompt.ShowDialog() != true)
		{
			return null;
		}
		return prompt._boxes.Select((Wpf.Ui.Controls.TextBox b) => b.Text).ToArray();
	}

	public static bool Confirm(string title, string message, string ok)
	{
		return new Prompt(title, message, Array.Empty<Field>(), ok, showCancel: true).ShowDialog() == true;
	}

	public static int Choose(string title, string message, params string[] choices)
	{
		Prompt prompt = new Prompt(title, message, Array.Empty<Field>(), "", showCancel: false, choices);
		prompt.ShowDialog();
		return prompt._chosen;
	}

	public static void Tell(string title, string message)
	{
		new Prompt(title, message, Array.Empty<Field>(), "OK", showCancel: false).ShowDialog();
	}
}
