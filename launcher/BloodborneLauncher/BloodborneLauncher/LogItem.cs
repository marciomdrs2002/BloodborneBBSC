using System.Windows.Media;

namespace BloodborneLauncher;

public class LogItem
{
	public string Time { get; set; } = "";

	public string Message { get; set; } = "";

	public Brush Color { get; set; } = Brushes.White;
}
