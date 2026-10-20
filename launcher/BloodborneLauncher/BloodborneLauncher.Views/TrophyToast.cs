using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using BloodborneLauncher.Core;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public sealed class TrophyToast : Window
{
	private struct Point32
	{
		public int X;

		public int Y;
	}

	private struct Rect32
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private struct MonitorInfo
	{
		public int Size;

		public Rect32 Monitor;

		public Rect32 Work;

		public uint Flags;
	}

	private static readonly Queue<Trophy> Pending = new Queue<Trophy>();

	private static bool _showing;

	private readonly string _corner;

	private readonly TranslateTransform _slide = new TranslateTransform();

	private const int GWL_EXSTYLE = -20;

	private const int WS_EX_TRANSPARENT = 32;

	private const int WS_EX_TOOLWINDOW = 128;

	private const int WS_EX_LAYERED = 524288;

	private const int WS_EX_NOACTIVATE = 134217728;

	private const uint MONITOR_DEFAULTTOPRIMARY = 1u;

	private const uint MONITOR_DEFAULTTONEAREST = 2u;

	private static readonly nint HWND_TOPMOST = new IntPtr(-1);

	private const uint SWP_NOSIZE = 1u;

	private const uint SWP_NOMOVE = 2u;

	private const uint SWP_NOACTIVATE = 16u;

	private const uint SWP_SHOWWINDOW = 64u;

	public static Color GradeColor(Grade grade)
	{
		return grade switch
		{
			Grade.Platinum => Color.FromRgb(185, 211, 238), 
			Grade.Gold => Color.FromRgb(227, 179, 65), 
			Grade.Silver => Color.FromRgb(196, 200, 208), 
			_ => Color.FromRgb(200, 132, 75), 
		};
	}

	public static void Announce(Trophy trophy)
	{
		Pending.Enqueue(trophy);
		if (!_showing)
		{
			Next();
		}
	}

	private static void Next()
	{
		if (Pending.Count == 0)
		{
			_showing = false;
			return;
		}
		_showing = true;
		Trophy trophy = Pending.Dequeue();
		LauncherSettings settings = Workspace.Current.Settings;
		if (settings.Bool("trophy_sound"))
		{
			Chime.Play(settings.Int("trophy_volume"), settings.Str("trophy_sound_file"));
		}
		new TrophyToast(trophy, settings.Str("trophy_position")).Show();
	}

	private TrophyToast(Trophy trophy, string corner)
	{
		_corner = corner;
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.Topmost = true;
		base.ShowActivated = false;
		base.ShowInTaskbar = false;
		base.ResizeMode = ResizeMode.NoResize;
		base.SizeToContent = SizeToContent.WidthAndHeight;
		base.Focusable = false;
		base.IsHitTestVisible = false;
		Color color = GradeColor(trophy.Grade);
		FontFamily fontFamily = new FontFamily("Segoe UI");
		Border element = new Border
		{
			Width = 46.0,
			Height = 46.0,
			CornerRadius = new CornerRadius(2.0),
			Background = new LinearGradientBrush(Color.FromRgb(90, 93, 98), Color.FromRgb(51, 53, 57), 90.0),
			BorderBrush = new SolidColorBrush(Color.FromArgb(80, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderThickness = new Thickness(1.0),
			Child = new SymbolIcon
			{
				Symbol = SymbolRegular.Trophy24,
				Filled = true,
				FontSize = 24.0,
				Foreground = new SolidColorBrush(Color.FromRgb(242, 242, 242))
			},
			VerticalAlignment = VerticalAlignment.Center
		};
		SymbolIcon element2 = new SymbolIcon
		{
			Symbol = SymbolRegular.Trophy24,
			Filled = true,
			FontSize = 15.0,
			Foreground = new SolidColorBrush(color),
			Margin = new Thickness(0.0, 1.0, 7.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		System.Windows.Controls.TextBlock element3 = new System.Windows.Controls.TextBlock
		{
			Text = trophy.Name,
			FontFamily = fontFamily,
			FontSize = 17.0,
			Foreground = new SolidColorBrush(Color.FromRgb(244, 244, 244)),
			TextTrimming = TextTrimming.CharacterEllipsis,
			MaxWidth = 330.0,
			VerticalAlignment = VerticalAlignment.Center
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		stackPanel.Children.Add(element2);
		stackPanel.Children.Add(element3);
		System.Windows.Controls.TextBlock element4 = new System.Windows.Controls.TextBlock
		{
			Text = "You have earned a trophy!",
			FontFamily = fontFamily,
			FontSize = 13.5,
			Foreground = new SolidColorBrush(Color.FromRgb(201, 203, 207)),
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
		};
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(14.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel2.Children.Add(stackPanel);
		stackPanel2.Children.Add(element4);
		StackPanel stackPanel3 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		stackPanel3.Children.Add(element);
		stackPanel3.Children.Add(stackPanel2);
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(0.0, 1.0)
		};
		linearGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(238, 74, 77, 82), 0.0));
		linearGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(238, 44, 46, 50), 0.45));
		linearGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(242, 28, 29, 32), 1.0));
		base.Content = new Border
		{
			Margin = new Thickness(18.0),
			Padding = new Thickness(9.0, 9.0, 26.0, 9.0),
			CornerRadius = new CornerRadius(3.0),
			Background = linearGradientBrush,
			BorderBrush = new SolidColorBrush(Color.FromArgb(102, 200, 204, 210)),
			BorderThickness = new Thickness(1.0),
			Effect = new DropShadowEffect
			{
				BlurRadius = 14.0,
				ShadowDepth = 2.0,
				Opacity = 0.6
			},
			Child = stackPanel3,
			MinWidth = 380.0,
			RenderTransform = _slide
		};
		base.Opacity = 0.0;
		base.SourceInitialized += delegate
		{
			nint handle = new WindowInteropHelper(this).Handle;
			SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x20 | 0x8000000 | 0x80 | 0x80000);
			DispatcherTimer raise = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(200.0)
			};
			raise.Tick += delegate
			{
				SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, 83u);
			};
			raise.Start();
			base.Closed += delegate
			{
				raise.Stop();
			};
		};
		base.Loaded += delegate
		{
			Place();
			Animate();
		};
	}

	private Rect Area()
	{
		nint num = IntPtr.Zero;
		Process[] processesByName = Process.GetProcessesByName("bb-probe");
		foreach (Process process in processesByName)
		{
			if (process.MainWindowHandle != IntPtr.Zero)
			{
				num = process.MainWindowHandle;
			}
			process.Dispose();
		}
		nint monitor = ((num != IntPtr.Zero) ? MonitorFromWindow(num, 2u) : MonitorFromPoint(default(Point32), 1u));
		MonitorInfo info = new MonitorInfo
		{
			Size = Marshal.SizeOf<MonitorInfo>()
		};
		if (!GetMonitorInfo(monitor, ref info))
		{
			return SystemParameters.WorkArea;
		}
		PresentationSource presentationSource = PresentationSource.FromVisual(this);
		double num2 = presentationSource?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
		double num3 = presentationSource?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;
		Rect32 monitor2 = info.Monitor;
		return new Rect((double)monitor2.Left * num2, (double)monitor2.Top * num3, (double)(monitor2.Right - monitor2.Left) * num2, (double)(monitor2.Bottom - monitor2.Top) * num3);
	}

	private void Place()
	{
		Rect rect = Area();
		bool flag = _corner.EndsWith("right");
		bool flag2 = _corner.StartsWith("bottom");
		base.Left = (flag ? (rect.Right - base.ActualWidth - 12.0) : (rect.Left + 12.0));
		base.Top = (flag2 ? (rect.Bottom - base.ActualHeight - 12.0) : (rect.Top + 12.0));
	}

	private void Animate()
	{
		int num = (_corner.EndsWith("right") ? 60 : (-60));
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(260.0)));
		_slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(num, 0.0, TimeSpan.FromMilliseconds(320.0))
		{
			EasingFunction = easingFunction
		});
		DispatcherTimer hold = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(5.5)
		};
		hold.Tick += delegate
		{
			hold.Stop();
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(380.0));
			doubleAnimation.Completed += delegate
			{
				Close();
				Next();
			};
			BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		};
		hold.Start();
	}

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(nint window, nint after, int x, int y, int cx, int cy, uint flags);

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(nint window, int index);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(nint window, int index, int value);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromWindow(nint window, uint flags);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromPoint(Point32 point, uint flags);

	[DllImport("user32.dll")]
	private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
