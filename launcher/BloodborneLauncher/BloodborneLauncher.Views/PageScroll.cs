using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace BloodborneLauncher.Views;

public static class PageScroll
{
	public static void Attach(ScrollViewer page)
	{
		page.PreviewMouseWheel += delegate(object _, MouseWheelEventArgs e)
		{
			if (!e.Handled && !DropDownOpen(e.OriginalSource, page) && !Owned(e.OriginalSource as DependencyObject, page, e.Delta))
			{
				page.ScrollToVerticalOffset(page.VerticalOffset - (double)e.Delta);
				e.Handled = true;
			}
		};
	}

	private static bool DropDownOpen(object source, ScrollViewer page)
	{
		if (!(Mouse.Captured is ComboBox { IsDropDownOpen: not false }))
		{
			if (source is Visual visual)
			{
				PresentationSource presentationSource = PresentationSource.FromVisual(visual);
				if (presentationSource != null)
				{
					return presentationSource != PresentationSource.FromVisual(page);
				}
			}
			return false;
		}
		return true;
	}

	private static bool Owned(DependencyObject? source, ScrollViewer page, int delta)
	{
		while (source != null && source != page)
		{
			if (source is ScrollViewer { ScrollableHeight: >0.0 } scrollViewer && ((delta > 0) ? (scrollViewer.VerticalOffset > 0.0) : (scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight)))
			{
				return true;
			}
			if (source is ComboBox || source is Slider)
			{
				return true;
			}
			if (source is FrameworkElement { Tag: string tag } && tag == "capture-wheel")
			{
				return true;
			}
			bool flag = ((source is Visual || source is Visual3D) ? true : false);
			source = (flag ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source));
		}
		return false;
	}
}
