using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BloodborneLauncher.Core;

public static class LayoutCleanup
{
	private static readonly string[] OldEngine = new string[7] { "out", "scripts", "tools", "fsr4_shaders", "fsr4vk", "licenses", "gamecontrollerdb.txt" };

	private static readonly string[] OldEnginePatches = new string[3] { "Bloodborne.xml", "Bloodborne-1.09-sites.json", "redzone.json" };

	public static List<string> Leftovers(string? package)
	{
		List<string> list = new List<string>();
		if (string.IsNullOrEmpty(package) || Paths.EngineDirOf(package) == package || !File.Exists(Path.Combine(package, "package.json")) || !File.Exists(Path.Combine(package, "engine", "out", "bb-probe.exe")))
		{
			return list;
		}
		string[] oldEngine = OldEngine;
		foreach (string path in oldEngine)
		{
			string text = Path.Combine(package, path);
			if (Directory.Exists(text) || File.Exists(text))
			{
				list.Add(text);
			}
		}
		oldEngine = OldEnginePatches;
		foreach (string path2 in oldEngine)
		{
			string text2 = Path.Combine(package, "patches", path2);
			if (File.Exists(text2))
			{
				list.Add(text2);
			}
		}
		if (File.Exists(Path.Combine(package, "engine", "out", "bb-start.exe")))
		{
			oldEngine = new string[2] { "scripts", "tools" };
			foreach (string path3 in oldEngine)
			{
				string text3 = Path.Combine(package, "engine", path3);
				if (Directory.Exists(text3))
				{
					list.Add(text3);
				}
			}
		}
		return list;
	}

	private static void Remove(string path)
	{
		if (Path.GetFileName(path) == "Bloodborne.xml" && File.Exists(path))
		{
			File.Move(path, path + ".bak", overwrite: true);
			return;
		}
		if (File.Exists(path))
		{
			File.SetAttributes(path, FileAttributes.Normal);
			File.Delete(path);
			return;
		}
		DirectoryInfo directoryInfo = new DirectoryInfo(path);
		if (!directoryInfo.Exists)
		{
			return;
		}
		directoryInfo.Attributes &= ~FileAttributes.ReadOnly;
		if (directoryInfo.LinkTarget == null)
		{
			foreach (FileSystemInfo item in directoryInfo.EnumerateFileSystemInfos())
			{
				Remove(item.FullName);
			}
		}
		directoryInfo.Delete(recursive: false);
	}

	public static List<string> Run(string package)
	{
		List<string> leftovers = Leftovers(package);
		List<string> failed = new List<string>();
		if (leftovers.Count == 0)
		{
			return failed;
		}
		ProgressBar bar = new ProgressBar
		{
			Height = 6.0,
			Minimum = 0.0,
			Maximum = leftovers.Count,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
		};
		Window window = new Window
		{
			Title = "Bloodborne Launcher",
			Width = 420.0,
			SizeToContent = SizeToContent.Height,
			ResizeMode = ResizeMode.NoResize,
			WindowStartupLocation = WindowStartupLocation.CenterScreen,
			WindowStyle = WindowStyle.ToolWindow,
			Topmost = true,
			Content = new StackPanel
			{
				Margin = new Thickness(22.0),
				Children = 
				{
					(UIElement)new TextBlock
					{
						Text = "New version: cleaning up",
						FontSize = 16.0,
						FontWeight = FontWeights.SemiBold
					},
					(UIElement)new TextBlock
					{
						Text = "Removing the old engine files from the Bloodborne PC folder. Your saves, settings and mods stay. Please wait…",
						TextWrapping = TextWrapping.Wrap,
						Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
					},
					(UIElement)bar
				}
			}
		};
		window.Loaded += async delegate
		{
			int i = 0;
			while (i < leftovers.Count)
			{
				string path = leftovers[i];
				try
				{
					await Task.Run(delegate
					{
						Remove(path);
					});
				}
				catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
				{
					failed.Add(path);
				}
				bar.Value = i + 1;
				int num = i + 1;
				i = num;
			}
			window.Close();
		};
		ShutdownMode shutdownMode = Application.Current.ShutdownMode;
		Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		window.ShowDialog();
		Application.Current.ShutdownMode = shutdownMode;
		return failed;
	}
}
