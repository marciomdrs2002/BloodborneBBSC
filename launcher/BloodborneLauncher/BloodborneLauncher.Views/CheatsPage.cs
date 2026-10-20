using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BloodborneLauncher.Core;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public sealed class CheatsPage : LauncherPage
{
	private static readonly Choice[] Modes = new Choice[3]
	{
		new Choice("Off", "off"),
		new Choice("Infinite", "infinite", "Kept full at the normal carry limit, even if you have none. Items put into storage while it is on come back"),
		new Choice("Custom amount", "custom", "Refilled to the amount below whenever it drops")
	};

	private static readonly string[] EnginesWithoutCheats = new string[4] { "bbhost-0.4.0-fps1", "bbhost-0.4.0-fps2", "bbhost-0.4.0-fps3", "bbhost-0.4.0-fps4" };

	private readonly InfoBar _engine;

	private readonly InfoBar _online;

	public static bool Supported
	{
		get
		{
			string text = Updater.LocalEngine();
			if (text != null)
			{
				return !EnginesWithoutCheats.Contains(text);
			}
			return false;
		}
	}

	public CheatsPage()
		: base("Cheats", "Set here for the next start. While playing, change them in the in-game menu (Insert or L3 + R3): they apply live. Turning a cheat off puts the game's values back; items you already have stay.")
	{
		_engine = new InfoBar
		{
			IsClosable = false,
			Severity = InfoBarSeverity.Warning,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0),
			Title = "Not in this engine yet",
			Message = "Your engine doesn't apply cheats yet: they arrive with the next engine update. What you pick here is saved and starts working then."
		};
		base.Body.Children.Add(_engine);
		_online = new InfoBar
		{
			IsClosable = false,
			Severity = InfoBarSeverity.Informational,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0),
			Title = "Off while playing online",
			Message = "Play online is on: every cheat stays off in online sessions, so other players never meet them. Turn Play online off and they work again; what you pick here is kept."
		};
		base.Body.Children.Add(_online);
		Group("Items");
		Cheat(SymbolRegular.Drop24, "Blood Vials", "cheat_vials", "cheat_vials_amount");
		Cheat(SymbolRegular.Flash24, "Quicksilver Bullets", "cheat_bullets", "cheat_bullets_amount");
		IniToggle(SymbolRegular.Beaker24, "Infinite consumables", "Molotovs, throwing knives, pebbles, antidotes, papers and the like are not used up (not Blood Vials or Bullets, which are above, and not insight items)", "cheat_consumables");
		Group("Character");
		IniToggle(SymbolRegular.Run24, "Infinite stamina", "Your stamina stays full", "cheat_stamina");
		IniToggle(SymbolRegular.EyeOff24, "Stealth", "Enemies don't notice you and your steps make no noise", "cheat_stealth");
		IniToggle(SymbolRegular.Shield24, "No weapon wear", "Equipped weapons are kept at full durability, so no repairs", "cheat_no_durability");
		ComboRow(SymbolRegular.Target24, "Firearm range", "How far firearm bullets travel (pistols, rifles, blunderbuss, cannons)", new Choice[4]
		{
			new Choice("Normal", "off"),
			new Choice("150%", "150"),
			new Choice("200%", "200"),
			new Choice("300%", "300")
		}, () => LauncherPage.Ini.Get("cheat_gun_range", "off"), delegate(string v)
		{
			LauncherPage.Ini.Set("cheat_gun_range", v);
		});
		Group("Blood Echoes");
		IniToggle(SymbolRegular.Heart24, "Keep echoes on death", "Dying never drops your Blood Echoes, so there is nothing to pick back up", "cheat_keep_echoes");
		ComboRow(SymbolRegular.ArrowTrendingLines24, "Echoes from kills", "Enemies already loaded give the new amount after you rest at a lamp or reload", new Choice[4]
		{
			new Choice("Normal", "1"),
			new Choice("x2", "2"),
			new Choice("x5", "5"),
			new Choice("x10", "10")
		}, () => LauncherPage.Ini.Get("cheat_echo_multiplier", "1"), delegate(string v)
		{
			LauncherPage.Ini.Set("cheat_echo_multiplier", v);
		});
		Add(LauncherPage.Card(SymbolRegular.Add24, "Add echoes or insight", "In the in-game menu (Insert or L3 + R3): +1,000, +10,000, +100,000 echoes and +1 insight", null, out System.Windows.Controls.TextBlock _));
	}

	private void Cheat(SymbolRegular icon, string title, string key, string amountKey)
	{
		Slider amount = null;
		ComboRow(icon, title, null, Modes, Mode, delegate(string v)
		{
			LauncherPage.Ini.Set(key, (v == "custom") ? Amount().ToString(CultureInfo.InvariantCulture) : v);
			ShowAmount(amount, v == "custom");
			LauncherPage.Ws.Log(title + ": " + ((v == "custom") ? $"kept at {Amount()}" : v) + ".", LogLevel.Step);
		});
		amount = SliderRow(SymbolRegular.NumberSymbol24, "Amount", "How many " + title + " to keep", 1.0, 99.0, 1.0, () => Amount(), delegate(double v)
		{
			LauncherPage.S.Set(amountKey, (int)v);
			if (Mode() == "custom")
			{
				LauncherPage.Ini.Set(key, ((int)v).ToString(CultureInfo.InvariantCulture));
			}
		}, "0");
		ShowAmount(amount, Mode() == "custom");
		int Amount()
		{
			int result;
			return Math.Clamp((int.TryParse(LauncherPage.Ini.Get(key), out result) && result > 0) ? result : LauncherPage.S.Int(amountKey), 1, 99);
		}
		string Mode()
		{
			string text = LauncherPage.Ini.Get(key, "off");
			if (text == "infinite")
			{
				return "infinite";
			}
			if (int.TryParse(text, out var result) && result > 0)
			{
				return "custom";
			}
			return "off";
		}
	}

	private static void ShowAmount(Slider amount, bool show)
	{
		DependencyObject dependencyObject = amount;
		while (dependencyObject != null && !(dependencyObject is SettingRow))
		{
			dependencyObject = LogicalTreeHelper.GetParent(dependencyObject);
		}
		(((UIElement)dependencyObject) ?? amount).Visibility = ((!show) ? Visibility.Collapsed : Visibility.Visible);
	}

	public override void OnShown()
	{
		_engine.IsOpen = !Supported;
		_online.IsOpen = LauncherPage.Ini.Get("online_enabled") == "1" && !HuntersDream.OnlinePaused;
	}
}
