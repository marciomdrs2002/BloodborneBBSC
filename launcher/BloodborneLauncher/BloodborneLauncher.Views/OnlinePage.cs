using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BloodborneLauncher.Core;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public sealed class OnlinePage : LauncherPage
{
	private readonly ToggleSwitch _online;

	private readonly StackPanel _results = new StackPanel();

	private readonly Wpf.Ui.Controls.Button _test;

	private readonly Wpf.Ui.Controls.Button _hdButton;

	private readonly System.Windows.Controls.TextBlock _hdHint;

	private CancellationTokenSource? _linking;

	public OnlinePage()
		: base("Online", "1. Turn on Play online. 2. Connect your Hunter's Dream account below (with Discord, or without it). 3. In the game, choose online at the title screen: The Hunter's Dream's welcome message means you are online. Everyone needs game version 1.09.")
	{
		// The BBSC local server keeps its own address in bbport.ini (set again at every Play).
		if (!Bbsc.Selected(LauncherPage.S))
		{
			string[] array = new string[5] { "online_host", "online_port", "online_webapi", "online_game_api", "online_thd_account" };
			foreach (string key in array)
			{
				if (LauncherPage.Ini.Get(key).Length > 0)
				{
					LauncherPage.Ini.Set(key, "");
				}
			}
			LauncherPage.Ini.Set("online_pool", (LauncherPage.S.HuntersDreamToken.Length > 0 && LauncherPage.S.Str("hd_name").Length > 0) ? "huntersdream" : "");
		}
		if (HuntersDream.OnlinePaused)
		{
			base.Body.Children.Add(LauncherPage.Text("Online play is paused in this version. The game starts offline whatever is set here; your settings are kept for when it returns.", "Faint"));
		}
		Group("Play");
		_online = ToggleRow(SymbolRegular.Globe24, "Play online", "Off: the game stays offline", () => LauncherPage.Ini.Get("online_enabled") == "1", delegate(bool v)
		{
			LauncherPage.Ini.Set("online_enabled", v ? "1" : "0");
		}, null, ShowStatus);
		Group("Server");
		ComboRow(SymbolRegular.Server24, "Server", "Messages, bloodstains, ghosts, co-op and invasions", Choices.OnlineServers, () => Bbsc.Selected(LauncherPage.S) ? Bbsc.ServerKey : "huntersdream", delegate(string v)
		{
			LauncherPage.S.Set("bbsc_online_server", v);
		}, null, 240.0, ShowServer, null, dropdown: true);
		base.Body.Children.Add(_hdPanel);
		Add(LauncherPage.Card(SymbolRegular.Globe24, "The Hunter's Dream", "Players online, the map and your hunter's profile", LauncherPage.SmallButton("Open website", SymbolRegular.Open24, delegate
		{
			Process.Start(new ProcessStartInfo("https://thehuntersdream.com")
			{
				UseShellExecute = true
			});
		}), out System.Windows.Controls.TextBlock hintBlock), _hdPanel);
		Group("The Hunter's Dream account", _hdPanel);
		_hdButton = LauncherPage.SmallButton("Connect", SymbolRegular.Person24, async delegate
		{
			await LinkAsync();
		});
		Add(LauncherPage.Card(SymbolRegular.PersonAccounts24, "Hunter's Dream account", null, _hdButton, out _hdHint), _hdPanel);
		ShowLink();
		base.Body.Children.Add(_bbscPanel);
		BuildBbsc();
		Group("Connection");
		IniText(SymbolRegular.Router24, "Local P2P port", "Empty: 3658, the PS4's", "3658", "online_p2p_port", 120.0);
		ComboRow(SymbolRegular.Timer24, "Summon sign timeout", "How long a sign you touched waits for its host before giving up", Choices.SignTimeout, () => LauncherPage.Ini.Get("online_sign_timeout", "30"), delegate(string v)
		{
			LauncherPage.Ini.Set("online_sign_timeout", v);
		});
		_test = LauncherPage.SmallButton("Test", SymbolRegular.PlayCircle24, async delegate
		{
			await TestAsync();
		});
		Add(LauncherPage.Card(SymbolRegular.Pulse24, "Test the connection", "Checks The Hunter's Dream: game server, co-op sessions and the relay summons use", _test, out hintBlock));
		base.Body.Children.Add(_results);
		ShowServer();
	}

	// ---------------------------------------------------------------- BBSC local server
	// Steps in order; each one says whether it is done and opens the next:
	//   host: 1. Tailscale  2. start the servers  3. invite code  4. account
	//   join: 1. Tailscale  2. the host's invite code  3. account

	private readonly StackPanel _hdPanel = new StackPanel();

	private readonly StackPanel _bbscPanel = new StackPanel();

	private readonly StackPanel _hostRows = new StackPanel();

	private readonly StackPanel _joinRows = new StackPanel();

	private System.Windows.Controls.TextBlock _tailscaleHint;

	private System.Windows.Controls.TextBlock _serversHint;

	private System.Windows.Controls.TextBlock _inviteHint;

	private System.Windows.Controls.TextBlock _joinHint;

	private System.Windows.Controls.TextBlock _accountGroup;

	private System.Windows.Controls.TextBlock _accountHint;

	private Wpf.Ui.Controls.Button _hostButton;

	private Wpf.Ui.Controls.Button _copyButton;

	private Wpf.Ui.Controls.Button _createButton;

	private SettingRow _inviteRow;

	private bool _busy;

	private bool _refreshing;

	private System.Windows.Controls.TextBlock _offWarning;

	/// <summary>The steps follow what is running (servers started or stopped elsewhere, a friend's host going online).</summary>
	private readonly System.Windows.Threading.DispatcherTimer _statusTimer = new System.Windows.Threading.DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(3.0)
	};

	private void BuildBbsc()
	{
		Group("BBSC local server", _bbscPanel);
		_bbscPanel.Children.Add(LauncherPage.Text("Co-op with your friends without a public server, over Tailscale. The host's PC runs the servers; friends need nothing else installed. Messages, bloodstains and ghosts stay within the group. Follow the steps, then press Play.", "Faint"));
		_offWarning = LauncherPage.Text("⚠ Play online is off (top of this page): the game will start offline.", "Faint");
		_offWarning.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "DangerTextBrush");
		_offWarning.Margin = new Thickness(0.0, 4.0, 0.0, 4.0);
		_bbscPanel.Children.Add(_offWarning);
		_statusTimer.Tick += delegate
		{
			if (base.IsVisible && Bbsc.Selected(LauncherPage.S) && !_busy)
			{
				ShowStatus();
			}
		};
		_statusTimer.Start();
		ComboRow(SymbolRegular.PeopleTeam24, "This PC", null, Choices.BbscRoles, () => Bbsc.Hosting(LauncherPage.S) ? "host" : "join", delegate(string v)
		{
			LauncherPage.S.Set("bbsc_role", v);
		}, _bbscPanel, 240.0, ShowServer);
		ToggleRow(SymbolRegular.PeopleSync24, "Seamless co-op (BBSC mod)", System.IO.File.Exists(Bbsc.ModDll) ? "One session through deaths, lamps, bosses and the Hunter's Dream. The host rings the Hunt Bell, guests the Kin Bell (free at the Bath Messengers). Off: the game's own co-op with its bells." : "bbsc\\bbsc.dll is missing from this folder: the game's own co-op is used", () => Bbsc.ModEnabled(LauncherPage.S), delegate(bool v)
		{
			LauncherPage.S.Set("bbsc_mod", v ? "1" : "0");
		}, _bbscPanel);

		Group("1. Tailscale", _bbscPanel);
		Add(LauncherPage.Card(SymbolRegular.Globe24, "Tailscale", "Everyone signs in to the same Tailscale network (tailscale.com)", LauncherPage.IconButton(SymbolRegular.ArrowClockwise24, "Check again", delegate
		{
			ShowServer();
		}), out _tailscaleHint), _bbscPanel);

		// Host
		_bbscPanel.Children.Add(_hostRows);
		Group("2. Start hosting", _hostRows);
		_hostButton = LauncherPage.SmallButton("Start hosting", SymbolRegular.Play24, async delegate
		{
			await ToggleHostingAsync();
		}, ControlAppearance.Primary);
		Add(LauncherPage.Card(SymbolRegular.Server24, "Co-op server", "Runs on this PC while the launcher is open; closing the launcher stops it", _hostButton, out _serversHint), _hostRows);
		Group("3. Invite your friends", _hostRows);
		_copyButton = LauncherPage.SmallButton("Copy", SymbolRegular.Copy24, delegate
		{
			CopyInvite();
		});
		_inviteRow = Add(LauncherPage.Card(SymbolRegular.Link24, "Invite code", "Your friends paste it under Join a host", _copyButton, out _inviteHint), _hostRows);

		// Join
		_bbscPanel.Children.Add(_joinRows);
		Group("2. The host's invite code", _joinRows);
		TextRow(SymbolRegular.Link24, "Invite code", "The BBSC-... code your host sent (a BBport BBCOOP-... code or an address works too)", "BBSC-...", () => LauncherPage.S.Str("bbsc_invite"), delegate(string v)
		{
			LauncherPage.S.Set("bbsc_invite", v.Trim());
			LauncherPage.S.Set("bbsc_host", Bbsc.ParseInvite(v) ?? "");
			ShowStatus();
		}, _joinRows);
		_joinHint = LauncherPage.Text("", "Faint");
		_joinHint.Margin = new Thickness(24.0, 0.0, 0.0, 8.0);
		_joinRows.Children.Add(_joinHint);

		// Account (step 4 hosting, 3 joining)
		_accountGroup = Group("4. Your account", _bbscPanel);
		_bbscPanel.Children.Add(LauncherPage.Text("Your name on the co-op server (up to 16 characters; capitals count). The password is kept encrypted for your Windows user.", "Faint"));
		TextRow(SymbolRegular.Person24, "Account name", null, "Account name", () => LauncherPage.Ini.Get("online_username"), delegate(string v)
		{
			LauncherPage.Ini.Set("online_username", v.Trim());
		}, _bbscPanel);
		Wpf.Ui.Controls.PasswordBox password = new Wpf.Ui.Controls.PasswordBox
		{
			Width = 300.0,
			PlaceholderText = "Password",
			PlaceholderEnabled = true,
			Password = LauncherPage.S.OnlinePassword,
			VerticalAlignment = VerticalAlignment.Center
		};
		password.PasswordChanged += delegate
		{
			LauncherPage.S.OnlinePassword = password.Password;
			LauncherPage.Ws.SaveSoon();
		};
		Add(LauncherPage.Card(SymbolRegular.Password24, "Password", null, password, out System.Windows.Controls.TextBlock _), _bbscPanel);
		_createButton = LauncherPage.SmallButton("Create account", SymbolRegular.PersonAdd24, async delegate
		{
			await CreateAccountAsync();
		});
		Add(LauncherPage.Card(SymbolRegular.PersonAdd24, "New here?", "Creates the name and password above on the co-op server, once", _createButton, out _accountHint), _bbscPanel);
	}

	private void ShowServer()
	{
		bool bbsc = Bbsc.Selected(LauncherPage.S);
		_hdPanel.Visibility = bbsc ? Visibility.Collapsed : Visibility.Visible;
		_bbscPanel.Visibility = bbsc ? Visibility.Visible : Visibility.Collapsed;
		ShowStatus();
	}

	/// <summary>Every step's state, from what is running now. The network checks run off the UI
	/// thread (each can wait up to half a second), then the rows are set.</summary>
	private async void ShowStatus()
	{
		if (_accountGroup == null || !Bbsc.Selected(LauncherPage.S) || _refreshing)
		{
			return;
		}
		_refreshing = true;
		try
		{
			bool hosting = Bbsc.Hosting(LauncherPage.S);
			_hostRows.Visibility = hosting ? Visibility.Visible : Visibility.Collapsed;
			_joinRows.Visibility = hosting ? Visibility.Collapsed : Visibility.Visible;
			_accountGroup.Text = hosting ? "4. Your account" : "3. Your account";
			_offWarning.Visibility = (LauncherPage.Ini.Get("online_enabled") == "1") ? Visibility.Collapsed : Visibility.Visible;

			string address = await Task.Run(() => Bbsc.TailscaleAddress(refresh: true));
			string host = Bbsc.HostAddress(LauncherPage.S);
			(bool serverUp, bool gameUp) = await Task.Run(() => (host.Length > 0 && Bbsc.Listening(host, Bbsc.ShadnetPort), host.Length > 0 && Bbsc.Listening(host, Bbsc.GameServerPort)));
			bool up = serverUp && gameUp;

			LauncherPage.SetHint(_tailscaleHint, (address != null) ? ("✓ Connected as " + address) : "Not found: install Tailscale, sign in, then press ↻");
			if (hosting)
			{
				_hostButton.IsEnabled = address != null && !_busy;
				_hostButton.Content = serverUp ? "Stop hosting" : "Start hosting";
				_hostButton.Icon = LauncherPage.Icon(serverUp ? SymbolRegular.Stop24 : SymbolRegular.Play24);
				LauncherPage.SetHint(_serversHint, (address == null) ? "Waiting for step 1"
					: up ? ("✓ Running on " + host + " (co-op " + Bbsc.ShadnetPort + ", game " + Bbsc.GameServerPort + ")")
					: serverUp ? "Co-op server running, game server not: press Stop hosting, then Start hosting"
					: (System.IO.File.Exists(Bbsc.PythonExe) ? "Not running" : "Not running · game-server\\python is missing from this folder"));
				_copyButton.IsEnabled = up;
				_inviteRow.Opacity = up ? 1.0 : 0.5;
				LauncherPage.SetHint(_inviteHint, up ? ShortInvite(Bbsc.MakeInvite(host)) : "Start hosting first");
			}
			else
			{
				string invite = LauncherPage.S.Str("bbsc_invite");
				_joinHint.Text = (host.Length == 0)
					? ((invite.Length > 0) ? "Not an invite code" : "Paste the code your host sent")
					: (up ? ("✓ Host " + host + " is online") : ("Host " + host + " is not hosting right now: is their launcher hosting, and are you both on Tailscale?"));
			}
			_createButton.IsEnabled = up && !_busy;
			if (!up)
			{
				LauncherPage.SetHint(_accountHint, hosting ? "Start hosting first" : "The host must be online first");
			}
			else if (_accountHint.Text.StartsWith("Start hosting") || _accountHint.Text.StartsWith("The host must"))
			{
				LauncherPage.SetHint(_accountHint, null);
			}
		}
		finally
		{
			_refreshing = false;
		}
	}

	/// <summary>The invite code shortened for the row; Copy copies all of it.</summary>
	private static string ShortInvite(string code)
	{
		return (code.Length > 24) ? (code.Substring(0, 12) + "…" + code.Substring(code.Length - 6)) : code;
	}

	private async Task ToggleHostingAsync()
	{
		_busy = true;
		ShowStatus();
		try
		{
			string host = Bbsc.HostAddress(LauncherPage.S);
			if (host.Length > 0 && Bbsc.Listening(host, Bbsc.ShadnetPort))
			{
				Bbsc.StopAll();
				LauncherPage.Ws.Log("BBSC: hosting stopped", LogLevel.Info);
				await Task.Delay(500);
			}
			else
			{
				string problem = await Bbsc.PrepareAsync(LauncherPage.S, delegate(string line)
				{
					LauncherPage.Ws.Log(line, LogLevel.Step);
				});
				if (problem != null)
				{
					Prompt.Tell("BBSC local server", problem);
				}
			}
		}
		finally
		{
			_busy = false;
			ShowStatus();
		}
	}

	private void CopyInvite()
	{
		string host = Bbsc.HostAddress(LauncherPage.S);
		if (host.Length == 0)
		{
			return;
		}
		try
		{
			Clipboard.SetText(Bbsc.MakeInvite(host));
		}
		catch (Exception)
		{
		}
		LauncherPage.SetHint(_inviteHint, ShortInvite(Bbsc.MakeInvite(host)) + "   ✓ copied");
		LauncherPage.Ws.Log("BBSC: invite code copied to the clipboard", LogLevel.Good);
	}

	private async Task CreateAccountAsync()
	{
		string name = LauncherPage.Ini.Get("online_username").Trim();
		string password = LauncherPage.S.OnlinePassword;
		if (name.Length == 0 || password.Length == 0)
		{
			Prompt.Tell("Create account", "Type the account name and password first.");
			return;
		}
		_busy = true;
		ShowStatus();
		try
		{
			string host = Bbsc.HostAddress(LauncherPage.S);
			string problem = await Bbsc.RegisterAsync(host, name, password);
			if (problem == null)
			{
				LauncherPage.Ws.Log("BBSC: account " + name + " created on " + host, LogLevel.Good);
				LauncherPage.SetHint(_accountHint, "✓ " + name + " created. Press Play.");
				return;
			}
			Prompt.Tell("Create account", "Could not create " + name + ". " + problem + "\n\nIf the account already exists, just press Play.");
		}
		finally
		{
			_busy = false;
			_createButton.IsEnabled = true;
		}
	}

	private void ShowLink()
	{
		string text = LauncherPage.S.Str("hd_name");
		bool flag = text.Length > 0 && LauncherPage.S.HuntersDreamToken.Length > 0;
		LauncherPage.SetHint(_hdHint, flag ? ("Connected as " + text) : "Not connected. Connect with Discord, or create an account here without it");
		if (_linking == null)
		{
			_hdButton.Content = (flag ? "Disconnect" : "Connect");
		}
	}

	private async Task AccountWithoutDiscordAsync(bool recover)
	{
		Prompt.Field[] fields = ((!recover) ? new Prompt.Field[1]
		{
			new Prompt.Field("Account name", "", "Your online name (letters, digits, - and _)")
		} : new Prompt.Field[2]
		{
			new Prompt.Field("Account name"),
			new Prompt.Field("Recovery code")
		});
		string[] array = Prompt.Ask(recover ? "Recover the account" : "Create an account", recover ? "The name and the recovery code you wrote down. Other PCs signed in to this account are signed out." : "An account held by this PC. You get a recovery code: write it down, it is the only way back in on another PC or after signing out.", fields);
		if (array == null || array[0].Trim().Length == 0)
		{
			return;
		}
		try
		{
			string text;
			string huntersDreamToken;
			string text2;
			if (!recover)
			{
				(text, huntersDreamToken, text2) = await HuntersDream.CreateAccountAsync(array[0].Trim(), CancellationToken.None);
			}
			else
			{
				(text, huntersDreamToken, text2) = await HuntersDream.RecoverAsync(array[0].Trim(), array[1].Trim(), CancellationToken.None);
			}
			LauncherPage.S.HuntersDreamToken = huntersDreamToken;
			LauncherPage.S.Set("hd_name", text);
			LauncherPage.Ini.Set("online_pool", "huntersdream");
			LauncherPage.Ws.SaveNow();
			LauncherPage.Ws.Log("The Hunter's Dream: signed in as " + text, LogLevel.Good);
			if (text2.Length > 0)
			{
				try
				{
					Clipboard.SetText(text2);
				}
				catch (Exception)
				{
				}
				Prompt.Tell("Your recovery code", $"Signed in as {text}.\n\nRecovery code (copied to the clipboard):\n\n{text2}\n\n" + "Write it down now: it is shown only once, and it is the only way back into this account.");
			}
		}
		catch (Exception ex2)
		{
			Prompt.Tell(recover ? "Recovering failed" : "Creating the account failed", ex2.Message);
		}
		finally
		{
			ShowLink();
		}
	}

	private async Task LinkAsync()
	{
		CancellationTokenSource linking = _linking;
		if (linking != null)
		{
			linking.Cancel();
			return;
		}
		string huntersDreamToken = LauncherPage.S.HuntersDreamToken;
		if (huntersDreamToken != null && huntersDreamToken.Length > 0)
		{
			await HuntersDream.LogoutAsync(huntersDreamToken);
			LauncherPage.S.HuntersDreamToken = "";
			LauncherPage.S.Set("hd_name", "");
			LauncherPage.Ini.Set("online_pool", "");
			LauncherPage.Ws.SaveNow();
			LauncherPage.Ws.Log("The Hunter's Dream: disconnected");
			ShowLink();
			return;
		}
		int num = Prompt.Choose("The Hunter's Dream account", "Sign in with Discord, or use an account held by this PC (a name and a recovery code, no Discord needed).", "Cancel", "Discord", "Without Discord…", "Recover with a code…");
		if (num <= 0)
		{
			return;
		}
		if (num < 2)
		{
			_linking = new CancellationTokenSource();
			_hdButton.Content = "Cancel";
			try
			{
				HuntersDream.Link link = await HuntersDream.StartLinkAsync(_linking.Token);
				try
				{
					Clipboard.SetText(link.UserCode);
				}
				catch (Exception)
				{
				}
				Process.Start(new ProcessStartInfo(link.Page)
				{
					UseShellExecute = true
				});
				LauncherPage.SetHint(_hdHint, "Enter the code " + link.UserCode + " on the page that opened (it is on the clipboard), then approve");
				var (text, huntersDreamToken2) = await HuntersDream.WaitLinkAsync(link, _linking.Token);
				LauncherPage.S.HuntersDreamToken = huntersDreamToken2;
				LauncherPage.S.Set("hd_name", text);
				LauncherPage.Ini.Set("online_pool", "huntersdream");
				LauncherPage.Ws.SaveNow();
				LauncherPage.Ws.Log("The Hunter's Dream: connected as " + text, LogLevel.Good);
				return;
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception ex3)
			{
				Prompt.Tell("Connecting failed", ex3.Message);
				return;
			}
			finally
			{
				_linking.Dispose();
				_linking = null;
				ShowLink();
			}
		}
		await AccountWithoutDiscordAsync(num == 3);
	}

	private void IniText(SymbolRegular icon, string title, string? hint, string placeholder, string key, double width = 300.0)
	{
		TextRow(icon, title, hint, placeholder, () => LauncherPage.Ini.Get(key), delegate(string v)
		{
			LauncherPage.Ini.Set(key, v.Trim());
		}, null, width);
	}

	public override void OnShown()
	{
		_online.IsChecked = LauncherPage.Ini.Get("online_enabled") == "1";
		ShowStatus();
	}

	/// <summary>The BBSC test: Tailscale, the host's two servers, and signing in with this account.</summary>
	private static async Task<System.Collections.Generic.List<Online.Result>> BbscChecksAsync()
	{
		System.Collections.Generic.List<Online.Result> results = new System.Collections.Generic.List<Online.Result>();
		string address = await Task.Run(() => Bbsc.TailscaleAddress(refresh: true));
		results.Add(new Online.Result("Tailscale", address ?? "this PC", address != null, (address != null) ? "" : "not running or not signed in"));
		string host = Bbsc.HostAddress(LauncherPage.S);
		if (host.Length == 0)
		{
			results.Add(new Online.Result("Host", "-", Ok: false, Bbsc.Hosting(LauncherPage.S) ? "no Tailscale address" : "no invite code"));
			return results;
		}
		bool coop = await Task.Run(() => Bbsc.Listening(host, Bbsc.ShadnetPort));
		results.Add(new Online.Result("Co-op server", host + ":" + Bbsc.ShadnetPort, coop, coop ? "" : "not hosting"));
		string ssInfo = "";
		bool game = false;
		try
		{
			using System.Net.Http.HttpClient http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(4.0) };
			ssInfo = await http.GetStringAsync("http://" + host + ":" + Bbsc.GameServerPort + "/bb-eu/ss.info");
			game = ssInfo.Length > 100;
		}
		catch (Exception ex)
		{
			ssInfo = ex.GetBaseException().Message;
		}
		results.Add(new Online.Result("Game server", host + ":" + Bbsc.GameServerPort, game, game ? "answers the game's ss.info" : ssInfo));
		string name = LauncherPage.Ini.Get("online_username").Trim();
		if (!coop || name.Length == 0 || LauncherPage.S.OnlinePassword.Length == 0)
		{
			results.Add(new Online.Result("Account", (name.Length > 0) ? name : "-", Ok: false, (name.Length == 0 || LauncherPage.S.OnlinePassword.Length == 0) ? "enter the name and password" : "needs the co-op server"));
			return results;
		}
		string login = await Bbsc.LoginAsync(host, name, LauncherPage.S.OnlinePassword);
		results.Add(new Online.Result("Account", name, login == null, (login == null) ? "signs in" : login));
		return results;
	}

	private async Task TestAsync()
	{
		_results.Children.Clear();
		_test.IsEnabled = false;
		_test.Content = "Testing…";
		try
		{
			System.Collections.Generic.List<Online.Result> checks = Bbsc.Selected(LauncherPage.S)
				? await BbscChecksAsync()
				: await Online.CheckAsync(Online.GameServer(LauncherPage.Ini.Get("online_game_api")));
			foreach (Online.Result item in checks)
			{
				string text = (item.Ok ? ((item.Detail.Length > 0) ? ("Reachable: " + item.Detail) : "Reachable") : ("Not reachable (" + item.Detail + ")"));
				System.Windows.Controls.TextBlock hintBlock;
				SettingRow settingRow = LauncherPage.Card(item.Ok ? SymbolRegular.CheckmarkCircle24 : SymbolRegular.ErrorCircle24, item.Name + ": " + item.Address, text, null, out hintBlock);
				settingRow.ShowIcon = true;
				settingRow.Icon.SetResourceReference(Page.ForegroundProperty, item.Ok ? "GoodTextBrush" : "DangerTextBrush");
				LauncherPage.SetHint(settingRow.Hint, text);
				settingRow.Margin = new Thickness(24.0, 0.0, 0.0, 4.0);
				_results.Children.Add(settingRow);
				LauncherPage.Ws.Log($"{item.Name} {item.Address}: {(item.Ok ? "reachable" : ("not reachable " + item.Detail))}", item.Ok ? LogLevel.Good : LogLevel.Warn);
			}
		}
		finally
		{
			_test.IsEnabled = true;
			_test.Content = "Test";
		}
	}
}
