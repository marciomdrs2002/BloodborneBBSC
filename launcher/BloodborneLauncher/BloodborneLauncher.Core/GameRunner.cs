using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace BloodborneLauncher.Core;

public sealed class GameRunner
{
	private Process? _process;

	private StreamWriter? _log;

	public static string SessionLog => Path.Combine(Paths.IsPortDir(Paths.PortDir) ? Paths.PortDir : Paths.ConfigDir, "logs", "last_session.log");

	public bool Running => _process != null;

	public DateTime StartedAt { get; private set; }

	public event Action<string>? Output;

	public event Action<int>? Exited;

	public static bool Custom(LauncherSettings s)
	{
		return s.Str("launch_method") == "custom";
	}

	private static bool Writable(string dir)
	{
		try
		{
			Directory.CreateDirectory(dir);
			string path = Path.Combine(dir, ".write-test");
			File.WriteAllText(path, "");
			File.Delete(path);
			return true;
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return false;
		}
	}

	public string? Start(LauncherSettings s)
	{
		if (_process != null)
		{
			return "The game is already running.";
		}
		SdlPad.Stop();
		ProcessStartInfo processStartInfo;
		if (Custom(s))
		{
			string text = Paths.Expand(s.Str("custom_command").Trim());
			if (text.Length == 0)
			{
				return "No custom command is set. Choose one in Settings › Launch.";
			}
			processStartInfo = new ProcessStartInfo(text)
			{
				Arguments = s.Str("custom_args")
			};
		}
		else
		{
			if (!Paths.IsPortDir(Paths.PortDir))
			{
				return "The engine folder is not set. Choose it in Settings › Bloodborne PC.";
			}
			if (!Writable(Path.Combine(Paths.EngineDir, "out")))
			{
				return "Windows does not let the game write into its own folder (" + Paths.PortDir + "), which it needs for its prepared files, logs and saves. This happens under Program Files: move the Bloodborne PC folder somewhere else, such as C:\\Games or your Documents.";
			}
			if (File.Exists(Paths.Start))
			{
				processStartInfo = new ProcessStartInfo(Paths.Start);
			}
			else
			{
				string obj = (File.Exists(Paths.Python) ? Paths.Python : "py");
				processStartInfo = new ProcessStartInfo(obj);
				if (obj == "py")
				{
					processStartInfo.ArgumentList.Add("-3");
				}
				processStartInfo.ArgumentList.Add(Path.Combine(Paths.EngineDir, "scripts", "run_windows.py"));
			}
			processStartInfo.ArgumentList.Add("--game-dir");
			processStartInfo.ArgumentList.Add(GameEnvironment.GameDir(s));
		}
		processStartInfo.WorkingDirectory = (Paths.IsPortDir(Paths.PortDir) ? Paths.EngineDir : Environment.CurrentDirectory);
		foreach (var (key, value) in GameEnvironment.Build(s))
		{
			processStartInfo.Environment[key] = value;
		}
		processStartInfo.Environment["PYTHONUNBUFFERED"] = "1";
		processStartInfo.Environment["PYTHONUTF8"] = "1";
		processStartInfo.Environment["PYTHONIOENCODING"] = "utf-8";
		processStartInfo.UseShellExecute = false;
		processStartInfo.CreateNoWindow = true;
		processStartInfo.RedirectStandardOutput = true;
		processStartInfo.RedirectStandardError = true;
		processStartInfo.StandardOutputEncoding = Encoding.UTF8;
		processStartInfo.StandardErrorEncoding = Encoding.UTF8;
		try
		{
			Process process = new Process
			{
				StartInfo = processStartInfo,
				EnableRaisingEvents = true
			};
			OpenLog();
			Emit($"Bloodborne Launcher {Updater.CurrentLabel}, engine {Updater.LocalEngine() ?? "unknown"}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			process.OutputDataReceived += delegate(object _, DataReceivedEventArgs e)
			{
				if (e.Data != null)
				{
					Emit(e.Data);
				}
			};
			process.ErrorDataReceived += delegate(object _, DataReceivedEventArgs e)
			{
				if (e.Data != null)
				{
					Emit(e.Data);
				}
			};
			process.Exited += delegate
			{
				int obj2 = -1;
				try
				{
					process.WaitForExit();
					obj2 = process.ExitCode;
				}
				catch
				{
				}
				_process = null;
				CloseLog();
				this.Exited?.Invoke(obj2);
			};
			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			_process = process;
			StartedAt = DateTime.Now;
			return null;
		}
		catch (Exception ex)
		{
			_process = null;
			CloseLog();
			return "Could not start: " + ex.Message;
		}
	}

	private void OpenLog()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(SessionLog));
			try
			{
				if (File.Exists(SessionLog))
				{
					File.Copy(SessionLog, Path.Combine(Path.GetDirectoryName(SessionLog), "previous_session.log"), overwrite: true);
				}
			}
			catch (IOException)
			{
			}
			FileStream stream = new FileStream(SessionLog, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
			_log = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
			{
				AutoFlush = true
			};
		}
		catch
		{
			_log = null;
		}
	}

	private void Emit(string line)
	{
		lock (this)
		{
			try
			{
				_log?.WriteLine(line);
			}
			catch
			{
			}
		}
		this.Output?.Invoke(line);
	}

	private void CloseLog()
	{
		lock (this)
		{
			_log?.Dispose();
			_log = null;
		}
	}

	public async Task StopAsync()
	{
		Process process = _process;
		if (process == null)
		{
			return;
		}
		Process[] processesByName = Process.GetProcessesByName("bb-probe");
		foreach (Process process2 in processesByName)
		{
			try
			{
				process2.CloseMainWindow();
			}
			catch
			{
			}
		}
		try
		{
			process.CloseMainWindow();
		}
		catch
		{
		}
		await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(3000));
		if (process.HasExited)
		{
			return;
		}
		try
		{
			process.Kill(entireProcessTree: true);
		}
		catch
		{
		}
	}
}
