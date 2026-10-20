using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BloodborneLauncher.Views;

namespace BloodborneLauncher.Core;

public sealed class Workspace
{
	public const int MaxOutputLines = 5000;

	private DispatcherTimer? _trophyTimer;

	private DispatcherTimer? _outputTimer;

	private readonly ConcurrentQueue<string> _pendingOutput = new ConcurrentQueue<string>();

	private DispatcherTimer? _saveTimer;

	private bool _iniWarned;

	private bool _stopping;

	private List<WindowsHdr.Display> _hdrBefore = new List<WindowsHdr.Display>();

	public static Workspace Current { get; } = new Workspace();

	public LauncherSettings Settings { get; } = new LauncherSettings();

	public IniFile Ini { get; } = new IniFile();

	public GameRunner Game { get; } = new GameRunner();

	public TrophyTracker Trophies { get; } = new TrophyTracker();

	public List<string> OutputLines { get; } = new List<string>();

	public SessionStats? Session { get; private set; }

	public Updater.Release? Update { get; private set; }

	public Updater.Release? Latest { get; private set; }

	public DateTime? CheckedAt { get; private set; }

	public event Action? TrophiesChanged;

	public event Action<string, LogLevel>? Logged;

	public event Action? Saved;

	public event Action? GameStateChanged;

	public event Action<IReadOnlyList<string>>? GameOutput;

	public event Action? OutputCleared;

	public event Action? PortChanged;

	public event Action<int, SessionStats>? GameCrashed;

	public event Action? UpdateStateChanged;

	public void Initialise()
	{
		Settings.Load();
		ResolvePort();
		Trophies.SetUserDir(GameEnvironment.UserDir(Settings));
		_trophyTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(2.0)
		};
		_trophyTimer.Tick += delegate
		{
			string text = GameEnvironment.UserDir(Settings);
			if (!string.Equals(Trophies.LogPath, Path.Combine(text, "trophies.log"), StringComparison.OrdinalIgnoreCase))
			{
				Trophies.SetUserDir(text);
				this.TrophiesChanged?.Invoke();
			}
			Announce(Trophies.Poll(!Game.Running));
		};
		_trophyTimer.Start();
		Game.Output += delegate(string line)
		{
			_pendingOutput.Enqueue(line);
		};
		_outputTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(100.0)
		};
		_outputTimer.Tick += delegate
		{
			FlushOutput();
		};
		_outputTimer.Start();
		Game.Exited += delegate(int code)
		{
			Dispatch(delegate
			{
				Log((code == 0) ? "The game exited normally." : $"The game exited with code {code}.", (code == 0) ? LogLevel.Good : LogLevel.Warn);
				_pendingOutput.Enqueue($"— the game exited (code {code}) —");
				FlushOutput();
				if (_hdrBefore.Count > 0)
				{
					int num = WindowsHdr.Restore(_hdrBefore);
					if (num > 0)
					{
						Log((num == 1) ? "Windows HDR was switched off during the game; turned it back on." : $"Windows HDR was switched off on {num} displays during the game; turned it back on.");
					}
				}
				_hdrBefore = new List<WindowsHdr.Display>();
				bool stopping = _stopping;
				_stopping = false;
				SessionStats session = Session;
				if (session != null)
				{
					session.Finish();
					if (session.Length > TimeSpan.FromSeconds(10.0))
					{
						SessionStats.AddPlaytime(Settings, PlayerProfiles.Current(Settings), session.Length);
						SaveSoon();
						Log("Session: " + session.Summary);
					}
					if (code != 0 && !stopping)
					{
						this.GameCrashed?.Invoke(code, session);
					}
				}
				this.GameStateChanged?.Invoke();
			});
		};
	}

	public void ResolvePort()
	{
		string text = Settings.Str("port_dir");
		string gameDir = Paths.Expand(Settings.Str("game_dir"));
		string text2 = Paths.DetectPortDir();
		if (text2 != null)
		{
			Paths.PortDir = text2;
		}
		else if (Paths.IsPortDir(text))
		{
			Paths.PortDir = text;
		}
		else
		{
			string text3 = Paths.PortBesideGame(gameDir) ?? Paths.PortNearGame(gameDir) ?? Paths.DetectPortDir();
			if (text3 != null)
			{
				Paths.PortDir = text3;
				Settings.Set("port_dir", text3);
			}
			else
			{
				Paths.PortDir = text;
			}
		}
		if (Settings.Str("game_dir").Length == 0 && Paths.IsPortDir(Paths.PortDir))
		{
			string value = Path.Combine(Directory.GetParent(Paths.PortDir)?.FullName ?? Paths.PortDir, "CUSA03173");
			Settings.Set("game_dir", value);
		}
		Ini.Load();
		_iniWarned = false;
		this.PortChanged?.Invoke();
	}

	public async Task CheckForUpdatesAsync(bool quiet)
	{
		try
		{
			bool beta = Updater.BetaChannel(Settings);
			Latest = await Updater.LatestAsync(beta);
			CheckedAt = DateTime.Now;
			Update = ((Latest != null && (Latest.IsNewerThan(Updater.Current, Updater.CurrentBeta) || (!beta && Updater.CurrentBeta > 0 && Latest.Beta == 0))) ? Latest : null);
			if (Update != null)
			{
				Log($"Launcher update available: {Update.Tag} (you have {Updater.CurrentLabel}).", LogLevel.Good);
			}
			else if (!quiet)
			{
				Log((Latest == null) ? "No launcher release is published yet." : ("The launcher is up to date (" + Updater.CurrentLabel + ")."), LogLevel.Good);
			}
		}
		catch (Exception ex)
		{
			if (!quiet)
			{
				Log("Could not check for updates: " + ex.GetBaseException().Message, LogLevel.Warn);
			}
		}
		this.UpdateStateChanged?.Invoke();
	}

	private void AnnounceFromSave(List<int> ids)
	{
		if (ids.Count != 0)
		{
			List<string> list = (from id in ids
				where id != 0 && TrophyTracker.ById.ContainsKey(id)
				select TrophyTracker.ById[id].Name).ToList();
			if (list.Count > 0)
			{
				Log("Trophies already earned in this save: " + string.Join(", ", list), LogLevel.Good);
			}
			Announce(ids.Where((int id) => id == 0).ToList());
			this.TrophiesChanged?.Invoke();
		}
	}

	private void Announce(List<int> ids)
	{
		if (ids.Count == 0)
		{
			return;
		}
		foreach (int id in ids)
		{
			if (TrophyTracker.ById.TryGetValue(id, out Trophy value))
			{
				Log($"Trophy unlocked: {value.Name} ({value.Grade})", LogLevel.Good);
				if (Settings.Bool("trophy_notifications"))
				{
					TrophyToast.Announce(value);
				}
			}
		}
		this.TrophiesChanged?.Invoke();
	}

	public void SaveSoon()
	{
		if (_saveTimer == null)
		{
			_saveTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(350.0)
			};
			_saveTimer.Tick += delegate
			{
				_saveTimer.Stop();
				SaveNow();
			};
		}
		_saveTimer.Stop();
		_saveTimer.Start();
	}

	public void SaveNow()
	{
		_saveTimer?.Stop();
		try
		{
			Settings.Save();
			if (!Paths.IsPortDir(Paths.PortDir))
			{
				string environmentVariable = Environment.GetEnvironmentVariable("BB_CONFIG");
				if (environmentVariable == null || environmentVariable.Length <= 0)
				{
					if (!_iniWarned)
					{
						_iniWarned = true;
						Log("bbport.ini not written: the engine folder is not set (Settings › Bloodborne PC).", LogLevel.Warn);
					}
					goto IL_0068;
				}
			}
			Ini.Save();
			goto IL_0068;
			IL_0068:
			this.Saved?.Invoke();
		}
		catch (Exception ex)
		{
			Log("Could not save settings: " + ex.Message, LogLevel.Bad);
		}
	}

	public string? StartGame()
	{
		SaveNow();
		OutputLines.Clear();
		this.OutputCleared?.Invoke();
		if (Settings.Bool("save_backups"))
		{
			try
			{
				if (SaveBackups.Create(Settings) != null)
				{
					Log($"Saves backed up before playing (the newest {10} are kept).", LogLevel.Step);
				}
			}
			catch (Exception ex)
			{
				Log("Could not back up the saves: " + ex.Message, LogLevel.Warn);
			}
		}
		bool flag = Settings.Bool("safe_mode_next");
		Session = new SessionStats();
		_hdrBefore = WindowsHdr.Displays();
		string text = Game.Start(Settings);
		if (text != null)
		{
			Log(text, LogLevel.Bad);
			return text;
		}
		Log(GameRunner.Custom(Settings) ? "Started the game with the custom command." : "Started Bloodborne.", LogLevel.Step);
		if (Settings.Bool("section_stats"))
		{
			Settings.Set("section_stats", value: false);
			SaveSoon();
		}
		if (flag)
		{
			Settings.Set("safe_mode_next", value: false);
			SaveSoon();
			Log("Safe mode for this run: experimental engine options and mods are off.", LogLevel.Step);
		}
		this.GameStateChanged?.Invoke();
		return null;
	}

	public async Task StopGameAsync()
	{
		Log("Stopping the game…", LogLevel.Step);
		_stopping = true;
		await Game.StopAsync();
		this.GameStateChanged?.Invoke();
	}

	public void ClearOutput()
	{
		OutputLines.Clear();
		this.OutputCleared?.Invoke();
	}

	public void Log(string message, LogLevel level = LogLevel.Info)
	{
		Dispatch(delegate
		{
			this.Logged?.Invoke(message, level);
		});
	}

	private void FlushOutput()
	{
		if (_pendingOutput.IsEmpty)
		{
			return;
		}
		List<string> list = new List<string>();
		string result;
		while (_pendingOutput.TryDequeue(out result))
		{
			list.Add(result);
		}
		foreach (string item in list)
		{
			Announce(Trophies.Observe(item));
			AnnounceFromSave(Trophies.ObserveSave(item));
			Session?.Observe(item);
		}
		OutputLines.AddRange(list);
		if (OutputLines.Count > 5000)
		{
			OutputLines.RemoveRange(0, OutputLines.Count - 5000);
		}
		this.GameOutput?.Invoke(list);
	}

	private static void Dispatch(Action action)
	{
		Dispatcher dispatcher = Application.Current?.Dispatcher;
		if (dispatcher == null || dispatcher.CheckAccess())
		{
			action();
		}
		else
		{
			dispatcher.BeginInvoke(action);
		}
	}
}
