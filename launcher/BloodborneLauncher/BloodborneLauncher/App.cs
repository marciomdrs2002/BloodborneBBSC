using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BloodborneLauncher.Core;

namespace BloodborneLauncher;

public class App : Application
{
	private static readonly string CrashLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "bbport-launcher", "crash.log");

	private bool _contentLoaded;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		base.DispatcherUnhandledException += OnDispatcherException;
		AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs args)
		{
			Record("AppDomain", args.ExceptionObject as Exception);
		};
		TaskScheduler.UnobservedTaskException += delegate(object? _, UnobservedTaskExceptionEventArgs args)
		{
			Record("UnobservedTask", args.Exception);
			args.SetObserved();
		};
		string text = Paths.DetectPortDir();
		if (text != null)
		{
			List<string> list = LayoutCleanup.Run(text);
			if (list.Count > 0)
			{
				MessageBox.Show("These old files could not be removed (in use?) and can be deleted by hand:\n\n" + string.Join("\n", list), "Bloodborne Launcher", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
		}
	}

	private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs args)
	{
		Exception exception = args.Exception;
		if (exception is COMException && exception.HResult == -2144980991)
		{
			args.Handled = true;
			return;
		}
		Record("Dispatcher", args.Exception);
		MessageBox.Show(args.Exception.Message + "\n\nLogged to:\n" + CrashLog, "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Hand);
		args.Handled = true;
	}

	private static void Record(string source, Exception? exception)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(CrashLog));
			StringBuilder stringBuilder = new StringBuilder().AppendLine(new string('=', 70));
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(4, 2, stringBuilder);
			handler.AppendFormatted(DateTime.Now, "yyyy-MM-dd HH:mm:ss");
			handler.AppendLiteral("  [");
			handler.AppendFormatted(source);
			handler.AppendLiteral("]");
			StringBuilder stringBuilder2 = stringBuilder.AppendLine(ref handler).AppendLine(exception?.ToString() ?? "(no exception object)").AppendLine();
			File.AppendAllText(CrashLog, stringBuilder2.ToString());
		}
		catch
		{
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.30.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			base.StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
			Uri resourceLocator = new Uri("/Bloodborne Launcher;component/app.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.30.0")]
	public static void Main()
	{
		App app = new App();
		app.InitializeComponent();
		app.Run();
	}
}
