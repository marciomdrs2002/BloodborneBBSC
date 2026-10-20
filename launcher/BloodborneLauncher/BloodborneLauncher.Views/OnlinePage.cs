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
		string[] array = new string[5] { "online_host", "online_port", "online_webapi", "online_game_api", "online_thd_account" };
		foreach (string key in array)
		{
			if (LauncherPage.Ini.Get(key).Length > 0)
			{
				LauncherPage.Ini.Set(key, "");
			}
		}
		LauncherPage.Ini.Set("online_pool", (LauncherPage.S.HuntersDreamToken.Length > 0 && LauncherPage.S.Str("hd_name").Length > 0) ? "huntersdream" : "");
		if (HuntersDream.OnlinePaused)
		{
			base.Body.Children.Add(LauncherPage.Text("Online play is paused in this version. The game starts offline whatever is set here; your settings are kept for when it returns.", "Faint"));
		}
		Group("Play");
		_online = ToggleRow(SymbolRegular.Globe24, "Play online", "Off: the game stays offline", () => LauncherPage.Ini.Get("online_enabled") == "1", delegate(bool v)
		{
			LauncherPage.Ini.Set("online_enabled", v ? "1" : "0");
		});
		Group("Server");
		ComboRow(SymbolRegular.Server24, "Server", "Messages, bloodstains, ghosts, co-op and invasions", Choices.OnlineServers, () => "huntersdream", delegate
		{
		});
		Add(LauncherPage.Card(SymbolRegular.Globe24, "The Hunter's Dream", "Players online, the map and your hunter's profile", LauncherPage.SmallButton("Open website", SymbolRegular.Open24, delegate
		{
			Process.Start(new ProcessStartInfo("https://thehuntersdream.com")
			{
				UseShellExecute = true
			});
		}), out System.Windows.Controls.TextBlock hintBlock));
		Group("The Hunter's Dream account");
		_hdButton = LauncherPage.SmallButton("Connect", SymbolRegular.Person24, async delegate
		{
			await LinkAsync();
		});
		Add(LauncherPage.Card(SymbolRegular.PersonAccounts24, "Hunter's Dream account", null, _hdButton, out _hdHint));
		ShowLink();
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
	}

	private async Task TestAsync()
	{
		_results.Children.Clear();
		_test.IsEnabled = false;
		_test.Content = "Testing…";
		try
		{
			foreach (Online.Result item in await Online.CheckAsync(Online.GameServer(LauncherPage.Ini.Get("online_game_api"))))
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
