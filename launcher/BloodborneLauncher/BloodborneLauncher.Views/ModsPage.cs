using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BloodborneLauncher.Core;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public sealed class ModsPage : LauncherPage
{
	private readonly StackPanel _modList = new StackPanel();

	private readonly StackPanel _patchList = new StackPanel();

	private readonly System.Windows.Controls.TextBlock _modsHint;

	private readonly System.Windows.Controls.TextBlock _patchesHint;

	private List<Mods.Entry> _mods = new List<Mods.Entry>();

	private List<(Patches.Patch Patch, bool On)> _patches = new List<(Patches.Patch, bool)>();

	public ModsPage()
		: base("Mods & patches", "Extract each mod into its own folder (with dvdroot_ps4, or straight chr/, parts/ and so on). Applied when the game starts.")
	{
		Group("Mods");
		SettingToggle(SymbolRegular.PuzzlePiece24, "Load mods", "When two mods replace the same file, the one lower in the list wins", "mods_enabled");
		Add(LauncherPage.Card(SymbolRegular.Folder24, "Mods folder", "", LauncherPage.Row(LauncherPage.SmallButton("Browse", SymbolRegular.FolderOpen24, delegate
		{
			string text = LauncherPage.PickFolder("Mods folder", GameEnvironment.ModsDir(LauncherPage.S));
			if (text != null)
			{
				LauncherPage.S.Set("mods_dir", text);
				LauncherPage.Ws.SaveSoon();
				RefreshMods();
			}
		}), LauncherPage.IconButton(SymbolRegular.Open24, "Open in Explorer", delegate
		{
			Paths.OpenFolder(GameEnvironment.ModsDir(LauncherPage.S));
		}), LauncherPage.IconButton(SymbolRegular.ArrowClockwise24, "Refresh the list", delegate
		{
			RefreshMods();
		})), out _modsHint));
		base.Body.Children.Add(_modList);
		Group("Third-party patches");
		base.Body.Children.Add(LauncherPage.Text("shadPS4-format XML patches for version 01.09 from the patches folder, and the port's built-in ones (Bloodborne.xml, off until you switch them on; frame rate, effects and resolution have their own settings). Put your own patches in a file of their own: Bloodborne.xml is replaced on every update.", "Faint"));
		Add(LauncherPage.Card(SymbolRegular.Folder24, "Patches folder", "", LauncherPage.Row(LauncherPage.SmallButton("Browse", SymbolRegular.FolderOpen24, delegate
		{
			string text = LauncherPage.PickFolder("Patches folder", GameEnvironment.PatchesDir(LauncherPage.S));
			if (text != null)
			{
				LauncherPage.S.Set("patches_dir", text);
				LauncherPage.Ws.SaveSoon();
				RefreshPatches();
			}
		}), LauncherPage.IconButton(SymbolRegular.Open24, "Open in Explorer", delegate
		{
			Paths.OpenFolder(GameEnvironment.PatchesDir(LauncherPage.S));
		}), LauncherPage.IconButton(SymbolRegular.ArrowClockwise24, "Refresh the list", delegate
		{
			RefreshPatches();
		})), out _patchesHint));
		base.Body.Children.Add(_patchList);
		LauncherPage.Ws.PortChanged += OnShown;
	}

	public override void OnShown()
	{
		RefreshMods();
		RefreshPatches();
	}

	private void RefreshMods()
	{
		string text = GameEnvironment.ModsDir(LauncherPage.S);
		_mods = Mods.Load(text);
		LauncherPage.SetHint(_modsHint, (_mods.Count == 0) ? (text + " — no mods") : $"{text} — {_mods.Count} mod{((_mods.Count == 1) ? "" : "s")}");
		DrawMods();
	}

	private void DrawMods()
	{
		_modList.Children.Clear();
		for (int i = 0; i < _mods.Count; i++)
		{
			int index = i;
			Mods.Entry mod = _mods[i];
			ToggleSwitch toggle = new ToggleSwitch
			{
				IsChecked = mod.Enabled,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
			};
			toggle.Click += delegate
			{
				_mods[index] = mod with
				{
					Enabled = (toggle.IsChecked == true)
				};
				SaveMods();
			};
			Wpf.Ui.Controls.Button button = LauncherPage.IconButton(SymbolRegular.ArrowUp24, "Load earlier", delegate
			{
				Move(index, -1);
			});
			Wpf.Ui.Controls.Button button2 = LauncherPage.IconButton(SymbolRegular.ArrowDown24, "Load later", delegate
			{
				Move(index, 1);
			});
			button.IsEnabled = i > 0;
			button2.IsEnabled = i < _mods.Count - 1;
			System.Windows.Controls.TextBlock hintBlock;
			SettingRow settingRow = LauncherPage.Card(SymbolRegular.Box24, mod.Name, $"Load order {i + 1}", LauncherPage.Row(button, button2, toggle), out hintBlock);
			settingRow.Margin = new Thickness(24.0, 0.0, 0.0, 4.0);
			_modList.Children.Add(settingRow);
		}
	}

	private void Move(int index, int direction)
	{
		int num = index + direction;
		if (num >= 0 && num < _mods.Count)
		{
			List<Mods.Entry> mods = _mods;
			List<Mods.Entry> mods2 = _mods;
			int index2 = num;
			Mods.Entry value = _mods[num];
			Mods.Entry value2 = _mods[index];
			mods[index] = value;
			mods2[index2] = value2;
			SaveMods();
			DrawMods();
		}
	}

	private void SaveMods()
	{
		try
		{
			Mods.Save(_mods);
		}
		catch (Exception ex)
		{
			LauncherPage.Ws.Log("Could not save mods.json: " + ex.Message, LogLevel.Bad);
		}
	}

	private void RefreshPatches()
	{
		string text = GameEnvironment.PatchesDir(LauncherPage.S);
		(HashSet<string> Enabled, HashSet<string> Disabled) profile = Patches.LoadProfile();
		_patches = (from p in Patches.Discover(text)
			select (p: p, Patches.IsOn(p, profile))).ToList();
		LauncherPage.SetHint(_patchesHint, (_patches.Count == 0) ? (text + " — no patches") : text);
		_patchList.Children.Clear();
		for (int num = 0; num < _patches.Count; num++)
		{
			int index = num;
			(Patches.Patch, bool) tuple = _patches[num];
			Patches.Patch patch = tuple.Item1;
			bool item = tuple.Item2;
			ToggleSwitch toggle = new ToggleSwitch
			{
				IsChecked = item,
				VerticalAlignment = VerticalAlignment.Center
			};
			toggle.Click += delegate
			{
				_patches[index] = (patch, toggle.IsChecked == true);
				try
				{
					Patches.Save(_patches);
				}
				catch (Exception ex)
				{
					LauncherPage.Ws.Log("Could not save patches.json: " + ex.Message, LogLevel.Bad);
				}
			};
			string info = patch.File + ((patch.Author.Length > 0) ? (" · by " + patch.Author) : "");
			System.Windows.Controls.TextBlock hintBlock;
			SettingRow settingRow = LauncherPage.Card(SymbolRegular.DocumentText24, patch.Name, info, toggle, out hintBlock);
			settingRow.Margin = new Thickness(24.0, 0.0, 0.0, 4.0);
			if (patch.Note.Length > 0)
			{
				settingRow.ToolTip = patch.Note;
			}
			_patchList.Children.Add(settingRow);
		}
	}
}
