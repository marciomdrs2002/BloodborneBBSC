using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using BloodborneLauncher.Core;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace BloodborneLauncher.Views;

public sealed class ControlsPage : LauncherPage
{
	private readonly StackPanel _table = new StackPanel();

	private readonly InfoBar _conflicts;

	private readonly Wpf.Ui.Controls.TextBox _search;

	private readonly System.Windows.Controls.TextBlock _padStatus;

	private readonly Grid _overlay;

	private readonly System.Windows.Controls.TextBlock _captureTitle;

	private readonly System.Windows.Controls.TextBlock _captureFeedback;

	private readonly System.Windows.Controls.TextBlock _captureHelp;

	private readonly System.Windows.Controls.TextBlock _captureBoxText;

	private readonly WrapPanel _padChoices;

	private readonly DispatcherTimer _padTimer;

	private string? _padInput;

	private ControlDef? _padControl;

	private HashSet<string> _padHeld = new HashSet<string>();

	private string? _padState;

	private DateTime? _padSeen;

	private ControlDef? _target;

	private readonly ComboBox _profileBox;

	private readonly System.Windows.Controls.TextBlock _profileHint;

	private readonly Wpf.Ui.Controls.Button _deleteProfile;

	private List<ControlProfiles.Profile> _profiles = new List<ControlProfiles.Profile>();

	private bool _fillingProfiles;

	public ControlsPage()
		: base("Controls", "Keyboard and mouse for every action, the mouse camera, and shareable profiles. Gamepads work as on PS4.")
	{
		Group("Keyboard & mouse");
		ToggleSwitch pc = ToggleRow(SymbolRegular.Keyboard24, "PC controls", "The keyboard and mouse layout below; off keeps the gamepad-style keys", () => LauncherPage.Ini.Get("pc_controls", "1") != "0", delegate(bool v)
		{
			LauncherPage.Ini.Set("pc_controls", v ? "1" : "0");
		});
		StackPanel mouse = new StackPanel();
		ToggleRow(SymbolRegular.CursorClick24, "Mouse camera", "The mouse turns the game's own camera by raw counts, at any frame rate", () => LauncherPage.Ini.Get("mouse_camera", "1") != "0", delegate(bool v)
		{
			LauncherPage.Ini.Set("mouse_camera", v ? "1" : "0");
		}, mouse);
		ToggleRow(SymbolRegular.CursorHover24, "Mouse in menus", "The mouse becomes a pointer in the game's menus: hover and click items", () => LauncherPage.Ini.Get("mouse_menu", "1") != "0", delegate(bool v)
		{
			LauncherPage.Ini.Set("mouse_menu", v ? "1" : "0");
		}, mouse);
		SliderRow(SymbolRegular.Gauge24, "Mouse sensitivity", "5 is the default", 0.0, 10.0, 1.0, () => LauncherPage.ParseDouble(LauncherPage.Ini.Get("mouse_sensitivity", "5"), 5.0), delegate(double v)
		{
			LauncherPage.Ini.Set("mouse_sensitivity", ((int)v).ToString());
		}, "0", mouse);
		IniToggle(SymbolRegular.ArrowSwap24, "Invert horizontal", null, "mouse_invert_x", mouse);
		IniToggle(SymbolRegular.ArrowSortDown24, "Invert vertical", null, "mouse_invert_y", mouse);
		IniToggle(SymbolRegular.ArrowRotateClockwise24, "Camera auto-rotation", "Let the camera turn by itself with movement, as with a gamepad", "mouse_auto_rotation", mouse);
		ComboRow(SymbolRegular.KeyboardShift24, "Button prompts", "The button icons in the game's menus and prompts. Automatic shows your keys while the keyboard or mouse is in use, and the controller's buttons with a controller; also in-game under System › PC Controls", Choices.ButtonPrompts, () => LauncherPage.Ini.Get("button_prompts", "auto"), delegate(string v)
		{
			LauncherPage.Ini.Set("button_prompts", v);
		}, mouse);
		base.Body.Children.Add(mouse);
		ToggleRow(SymbolRegular.CursorHoverOff24, "Hide mouse cursor", "Hidden 2 seconds after the mouse stops moving; moving it brings it back", () => LauncherPage.Ini.Get("hide_cursor", "1") != "0", delegate(bool v)
		{
			LauncherPage.Ini.Set("hide_cursor", v ? "1" : "0");
		});
		pc.Click += delegate
		{
			Enable();
			Rebuild();
		};
		Enable();
		Group("Profiles");
		_profileBox = new ComboBox
		{
			Width = 260.0,
			VerticalAlignment = VerticalAlignment.Center
		};
		_profileBox.SelectionChanged += delegate
		{
			ProfileChosen();
		};
		_deleteProfile = LauncherPage.IconButton(SymbolRegular.Delete24, "Delete this profile", delegate
		{
			DeleteProfile();
		});
		Add(LauncherPage.Card(SymbolRegular.PeopleCommunity24, "Profile", "", LauncherPage.Row(_profileBox, _deleteProfile), out _profileHint));
		Add(LauncherPage.Card(SymbolRegular.Share24, "Save and share", "Profiles are small JSON files. Copy one and paste it on any paste site (Pastebin, GitHub Gist, Rentry, hastebin…) to share it; others import it from the link.", null, out System.Windows.Controls.TextBlock _));
		WrapPanel wrapPanel = new WrapPanel
		{
			Margin = new Thickness(46.0, -4.0, 0.0, 12.0)
		};
		wrapPanel.Children.Add(LauncherPage.SmallButton("Save as…", SymbolRegular.Save24, delegate
		{
			SaveProfile();
		}));
		wrapPanel.Children.Add(LauncherPage.SmallButton("Copy JSON", SymbolRegular.Copy24, delegate
		{
			CopyProfile();
		}));
		wrapPanel.Children.Add(LauncherPage.SmallButton("Export file…", SymbolRegular.ArrowExportUp24, delegate
		{
			ExportProfile();
		}));
		wrapPanel.Children.Add(LauncherPage.SmallButton("Import file…", SymbolRegular.DocumentArrowDown20, delegate
		{
			ImportFile();
		}));
		wrapPanel.Children.Add(LauncherPage.SmallButton("Import from link…", SymbolRegular.Link24, async delegate
		{
			await ImportLinkAsync();
		}));
		foreach (FrameworkElement child2 in wrapPanel.Children)
		{
			child2.Margin = new Thickness(0.0, 4.0, 6.0, 0.0);
		}
		base.Body.Children.Add(wrapPanel);
		Group("Gamepad");
		Add(LauncherPage.Card(SymbolRegular.XboxController24, "Controller", "", null, out _padStatus));
		ComboRow(SymbolRegular.Games24, "Controller icons", "The controller's buttons in the game's menus and prompts. Automatic shows PlayStation buttons with a PlayStation controller and Xbox buttons (A, B, X, Y, LB, RT…) with any other; rebound buttons show as bound. Also in-game under System › PC Controls", Choices.PadIcons, () => LauncherPage.Ini.Get("pad_icons", "auto"), delegate(string v)
		{
			LauncherPage.Ini.Set("pad_icons", v);
		});
		IniToggle(SymbolRegular.ArrowSwap24, "Swap sticks", "Move with the right stick and look with the left", "pad_swap_sticks", null, delegate
		{
			Rebuild();
		});
		IniToggle(SymbolRegular.ArrowBidirectionalLeftRight24, "Invert camera left / right", "The camera stick's horizontal direction", "pad_invert_look_x");
		IniToggle(SymbolRegular.ArrowBidirectionalUpDown24, "Invert camera up / down", "The camera stick's vertical direction", "pad_invert_look_y");
		SliderRow(SymbolRegular.CircleSmall24, "Stick dead zone", "Ignores small stick movement, for sticks that drift. 0 = off (the game's own)", 0.0, 30.0, 1.0, () => (!double.TryParse(LauncherPage.Ini.Get("pad_deadzone", "0"), out var result)) ? 0.0 : result, delegate(double v)
		{
			LauncherPage.Ini.Set("pad_deadzone", ((int)Math.Round(v)).ToString());
		}, "0'%'");
		Grid grid = new Grid
		{
			Margin = new Thickness(2.0, 24.0, 0.0, 8.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		System.Windows.Controls.TextBlock textBlock = LauncherPage.Text("Actions", "GroupHeader");
		textBlock.Margin = new Thickness(0.0);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		grid.Children.Add(textBlock);
		_search = new Wpf.Ui.Controls.TextBox
		{
			PlaceholderText = "Filter actions or keys",
			PlaceholderEnabled = true,
			Width = 210.0,
			Icon = LauncherPage.Icon(SymbolRegular.Search24)
		};
		_search.TextChanged += delegate
		{
			Rebuild();
		};
		StackPanel element = LauncherPage.Row(_search, LauncherPage.SmallButton("Reset all", SymbolRegular.ArrowUndo24, delegate
		{
			Bindings.ResetAll(LauncherPage.Ini);
			PadMap.ResetAll(LauncherPage.Ini);
			LauncherPage.Ws.Log("Key and controller bindings reset to the defaults.", LogLevel.Step);
			Changed();
		}));
		Grid.SetColumn(element, 1);
		grid.Children.Add(element);
		base.Body.Children.Add(grid);
		System.Windows.Controls.TextBlock textBlock2 = LauncherPage.Text("Click an action and press a key, click a mouse button or turn the wheel. Each action takes one key and one mouse input. × removes one, right-click unbinds the action. In the Controller column, pick the button for an action: one already in use swaps over.", "Faint");
		textBlock2.Margin = new Thickness(2.0, 0.0, 0.0, 8.0);
		base.Body.Children.Add(textBlock2);
		_conflicts = new InfoBar
		{
			IsClosable = false,
			IsOpen = false,
			Severity = InfoBarSeverity.Warning,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		base.Body.Children.Add(_conflicts);
		base.Body.Children.Add(new Card
		{
			Content = _table,
			Padding = new Thickness(12.0, 8.0, 12.0, 10.0)
		});
		_captureTitle = LauncherPage.Text("", "PageTitle");
		_captureHelp = LauncherPage.Text("", "Muted");
		_captureHelp.Margin = new Thickness(0.0, 4.0, 0.0, 12.0);
		_captureFeedback = LauncherPage.Text("", "Muted");
		_captureFeedback.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "WarningTextBrush");
		_captureBoxText = LauncherPage.Text("", "Faint");
		_captureBoxText.HorizontalAlignment = HorizontalAlignment.Center;
		_captureBoxText.VerticalAlignment = VerticalAlignment.Center;
		_captureBoxText.TextAlignment = TextAlignment.Center;
		Border border = new Border
		{
			Height = 110.0,
			CornerRadius = new CornerRadius(6.0),
			BorderThickness = new Thickness(1.0),
			Child = _captureBoxText,
			Tag = "capture-wheel",
			Cursor = Cursors.Hand,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		border.SetResourceReference(Border.BackgroundProperty, "PanelDeepBrush");
		border.SetResourceReference(Border.BorderBrushProperty, "AccentTextFillColorPrimaryBrush");
		border.PreviewMouseDown += delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (_padInput == null)
			{
				string text = InputMap.MouseToken(e.ChangedButton);
				if (text != null)
				{
					Commit(text);
				}
			}
		};
		border.PreviewMouseWheel += delegate(object _, MouseWheelEventArgs e)
		{
			e.Handled = true;
			if (_padInput == null)
			{
				Commit((e.Delta > 0) ? "WheelUp" : "WheelDown");
			}
		};
		_padChoices = new WrapPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
		};
		foreach (PadMap.Button item in PadMap.Buttons.Where((PadMap.Button b) => b.Sdl.Length > 0))
		{
			string sdl = item.Sdl;
			Wpf.Ui.Controls.Button button = new Wpf.Ui.Controls.Button
			{
				Content = item.Label,
				Padding = new Thickness(8.0, 3.0, 8.0, 3.0),
				Margin = new Thickness(0.0, 0.0, 4.0, 4.0),
				FontSize = 12.0
			};
			button.Click += delegate
			{
				CommitPad(sdl);
			};
			_padChoices.Children.Add(button);
		}
		_padTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(16.0)
		};
		_padTimer.Tick += delegate
		{
			PollPad();
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(LauncherPage.SmallButton("Unbind", SymbolRegular.Dismiss24, delegate
		{
			if (_padInput != null)
			{
				CommitPad("");
			}
			else
			{
				Commit(null);
			}
		}));
		stackPanel.Children.Add(LauncherPage.SmallButton("Cancel", SymbolRegular.ArrowExit20, delegate
		{
			EndCapture();
		}));
		StackPanel child = new StackPanel
		{
			Children = 
			{
				(UIElement)_captureTitle,
				(UIElement)_captureHelp,
				(UIElement)border,
				(UIElement)_padChoices,
				(UIElement)_captureFeedback,
				(UIElement)stackPanel
			}
		};
		Border border2 = new Border
		{
			Width = 500.0,
			Padding = new Thickness(22.0, 18.0, 22.0, 16.0),
			CornerRadius = new CornerRadius(8.0),
			BorderThickness = new Thickness(1.0),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Child = child
		};
		border2.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
		border2.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
		_overlay = new Grid
		{
			Visibility = Visibility.Collapsed,
			Background = new SolidColorBrush(Color.FromArgb(176, 16, 16, 18)),
			Focusable = true
		};
		_overlay.Children.Add(border2);
		_overlay.MouseDown += delegate(object _, MouseButtonEventArgs e)
		{
			if (e.OriginalSource == _overlay)
			{
				EndCapture();
			}
		};
		Grid grid2 = new Grid();
		base.Content = null;
		grid2.Children.Add(base.Scroller);
		grid2.Children.Add(_overlay);
		base.Content = grid2;
		base.PreviewKeyDown += Page_PreviewKeyDown;
		DispatcherTimer dispatcherTimer = new DispatcherTimer();
		dispatcherTimer.Interval = TimeSpan.FromSeconds(1.5);
		dispatcherTimer.Tick += delegate
		{
			if (base.IsVisible)
			{
				UpdatePadStatus();
			}
		};
		dispatcherTimer.Start();
		DispatcherTimer dispatcherTimer2 = new DispatcherTimer();
		dispatcherTimer2.Interval = TimeSpan.FromMilliseconds(100.0);
		dispatcherTimer2.Tick += delegate
		{
			if (base.IsVisible && !LauncherPage.Ws.Game.Running && !(_target != null) && _padInput == null && SdlPad.Available)
			{
				string text = SdlPad.State();
				if (_padState != null && text != _padState)
				{
					int num;
					if (_padSeen.HasValue)
					{
						DateTime now = DateTime.Now;
						DateTime? padSeen = _padSeen;
						num = ((now - padSeen > TimeSpan.FromMinutes(2.0)) ? 1 : 0);
					}
					else
					{
						num = 1;
					}
					_padSeen = DateTime.Now;
					if (num != 0)
					{
						UpdatePadStatus();
					}
				}
				_padState = text;
			}
		};
		dispatcherTimer2.Start();
		LauncherPage.Ws.PortChanged += delegate
		{
			Rebuild();
			FillProfiles();
		};
		Rebuild();
		FillProfiles();
		void Enable()
		{
			mouse.IsEnabled = pc.IsChecked == true;
		}
	}

	public override void OnShown()
	{
		UpdatePadStatus();
		Rebuild();
		FillProfiles();
	}

	private void UpdatePadStatus()
	{
		List<SdlPad.Device> list = (LauncherPage.Ws.Game.Running ? null : SdlPad.Devices());
		if (list != null)
		{
			if (list.Count == 0)
			{
				LauncherPage.SetHint(_padStatus, "No controller connected. Plug one in or pair it over Bluetooth: it shows here right away. DualShock 4, DualSense, Xbox, Switch Pro and many generic pads work without extra tools.");
				return;
			}
			list = (from d in list
				group d by (Name: d.Name, Supported: d.Supported) into g
				select g.First()).ToList();
			IEnumerable<string> values = list.Select((SdlPad.Device d) => (!d.Supported) ? ("● " + d.Name + ": connected, but the game has no button layout for it. Switch the pad to its XInput / PC mode if it has one, or use Steam Input") : ("● " + d.Name));
			int num;
			if (_padSeen.HasValue)
			{
				DateTime now = DateTime.Now;
				DateTime? padSeen = _padSeen;
				num = ((now - padSeen < TimeSpan.FromMinutes(2.0)) ? 1 : 0);
			}
			else
			{
				num = 0;
			}
			bool flag = (byte)num != 0;
			LauncherPage.SetHint(_padStatus, string.Join("\n", values) + ((!list.Any((SdlPad.Device d) => d.Supported)) ? "" : (flag ? "\nResponding: ready to play. The game uses the pad you press." : "\nDetected. Wireless pads show here while only the receiver is plugged in: press any button to check the pad is on.")));
		}
		else
		{
			List<Gamepads.Pad> list2 = Gamepads.Detect();
			LauncherPage.SetHint(_padStatus, (list2.Count == 0) ? "No controller connected. Plug one in or pair it over Bluetooth: it shows here right away." : ("● " + string.Join("   ● ", list2.Select((Gamepads.Pad p) => p.Name + (p.Bluetooth ? " (Bluetooth)" : ""))) + (LauncherPage.Ws.Game.Running ? "\nIn use by the game." : "\nConnected.")));
		}
	}

	private Dictionary<string, List<ControlDef>> Usage()
	{
		Dictionary<string, List<ControlDef>> dictionary = new Dictionary<string, List<ControlDef>>(StringComparer.OrdinalIgnoreCase);
		ControlDef[] controls = InputMap.Controls;
		foreach (ControlDef controlDef in controls)
		{
			foreach (string item in Bindings.Get(LauncherPage.Ini, controlDef))
			{
				if (!dictionary.TryGetValue(item, out var value))
				{
					value = (dictionary[item] = new List<ControlDef>());
				}
				value.Add(controlDef);
			}
		}
		return dictionary;
	}

	private void Rebuild()
	{
		_table.Children.Clear();
		bool isEnabled = LauncherPage.Ini.Get("pc_controls", "1") != "0";
		Dictionary<string, List<ControlDef>> dictionary = Usage();
		string filter = _search.Text.Trim();
		_table.Children.Add(TableRow(Header("Action"), Header("Keyboard & mouse"), Header("Controller"), new FrameworkElement(), header: true));
		foreach (IGrouping<string, ControlDef> item in from controlDef in InputMap.Controls
			group controlDef by controlDef.Group)
		{
			List<ControlDef> list = item.Where((ControlDef control) => Matches(control, filter)).ToList();
			if (list.Count == 0)
			{
				continue;
			}
			System.Windows.Controls.TextBlock textBlock = LauncherPage.Text(item.Key.ToUpperInvariant(), "StatLabel");
			textBlock.FontWeight = FontWeights.SemiBold;
			textBlock.Margin = new Thickness(4.0, 14.0, 0.0, 4.0);
			_table.Children.Add(textBlock);
			using List<ControlDef>.Enumerator enumerator2 = list.GetEnumerator();
			ControlDef current2;
			StackPanel stackPanel;
			ControlDef c;
			Wpf.Ui.Controls.Button obj;
			int visibility;
			Wpf.Ui.Controls.Button button;
			FrameworkElement frameworkElement;
			for (; enumerator2.MoveNext(); obj.Visibility = (Visibility)visibility, button = obj, button.Click += delegate
			{
				string text2 = PadMap.InputFor(c);
				if (text2 != null)
				{
					PadMap.Reset(LauncherPage.Ini, text2);
				}
				Store(c, InputMap.Split(c.Default));
			}, frameworkElement = Cell(current2, dictionary), frameworkElement.IsEnabled = isEnabled, _table.Children.Add(TableRow(stackPanel, frameworkElement, PadCell(current2), button)))
			{
				current2 = enumerator2.Current;
				stackPanel = new StackPanel
				{
					VerticalAlignment = VerticalAlignment.Center
				};
				stackPanel.Children.Add(new System.Windows.Controls.TextBlock
				{
					Text = current2.Label,
					FontSize = 13.5
				});
				if (current2.Hint.Length > 0)
				{
					stackPanel.Children.Add(LauncherPage.Text(current2.Hint, "Faint"));
				}
				c = current2;
				obj = new Wpf.Ui.Controls.Button
				{
					Icon = LauncherPage.Icon(SymbolRegular.ArrowUndo24),
					Appearance = ControlAppearance.Transparent,
					Padding = new Thickness(6.0),
					ToolTip = "Back to the default (" + Show(current2.Default) + ")",
					VerticalAlignment = VerticalAlignment.Center
				};
				if (Bindings.IsDefault(LauncherPage.Ini, current2))
				{
					string text = PadMap.InputFor(current2);
					if (text == null || PadMap.IsDefault(LauncherPage.Ini, text))
					{
						visibility = 1;
						continue;
					}
				}
				visibility = 0;
			}
		}
		List<string> list2 = (from u in dictionary
			where u.Value.Count > 1
			select InputMap.Display(u.Key) + " (" + string.Join(", ", u.Value.Select((ControlDef controlDef) => controlDef.Label)) + ")").ToList();
		List<string> list3 = (from k in dictionary.Keys.Where(InputMap.Reserved.ContainsKey)
			select InputMap.Reserved[k]).ToList();
		List<string> list4 = new List<string>();
		if (list2.Count > 0)
		{
			list4.Add("Used by more than one action: " + string.Join("; ", list2));
		}
		if (list3.Count > 0)
		{
			list4.Add("Also used by the port: " + string.Join("; ", list3));
		}
		_conflicts.IsOpen = list4.Count > 0;
		_conflicts.Title = ((list2.Count > 0) ? "Shared inputs" : "Reserved keys");
		_conflicts.Message = string.Join("\n", list4);
	}

	private static string Show(string value)
	{
		List<string> list = InputMap.Split(value);
		if (list == null || list.Count <= 0)
		{
			return "not bound";
		}
		return string.Join(", ", list.Select(InputMap.Display));
	}

	private bool Matches(ControlDef control, string filter)
	{
		if (filter.Length == 0)
		{
			return true;
		}
		if (!Has(control.Label) && !Has(control.Hint) && (control.Pad == null || !Has(control.Pad)))
		{
			return Bindings.Get(LauncherPage.Ini, control).Any((string n) => Has(n) || Has(InputMap.Display(n)));
		}
		return true;
		bool Has(string s)
		{
			return s.Contains(filter, StringComparison.OrdinalIgnoreCase);
		}
	}

	private static System.Windows.Controls.TextBlock Header(string text)
	{
		System.Windows.Controls.TextBlock textBlock = LauncherPage.Text(text, "StatLabel");
		textBlock.FontWeight = FontWeights.SemiBold;
		return textBlock;
	}

	private static Grid TableRow(FrameworkElement name, FrameworkElement keys, FrameworkElement pad, FrameworkElement action, bool header = false)
	{
		Grid obj = new Grid
		{
			Margin = new Thickness(0.0, (!header) ? 1 : 4, 0.0, (!header) ? 1 : 2),
			MinHeight = ((!header) ? 40 : 0),
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = new GridLength(240.0)
				},
				new ColumnDefinition
				{
					Width = new GridLength(1.6, GridUnitType.Star)
				},
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				},
				new ColumnDefinition
				{
					Width = new GridLength(40.0)
				}
			}
		};
		name.Margin = new Thickness(4.0, 0.0, 8.0, 0.0);
		Grid.SetColumn(keys, 1);
		Grid.SetColumn(pad, 2);
		Grid.SetColumn(action, 3);
		keys.Margin = new Thickness(header ? 10 : 4, 0.0, 4.0, 0.0);
		pad.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		obj.Children.Add(name);
		obj.Children.Add(keys);
		obj.Children.Add(pad);
		obj.Children.Add(action);
		return obj;
	}

	private FrameworkElement Cell(ControlDef control, Dictionary<string, List<ControlDef>> usage)
	{
		List<string> list = Bindings.Get(LauncherPage.Ini, control);
		WrapPanel wrapPanel = new WrapPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		foreach (string item in list)
		{
			List<ControlDef> value;
			List<ControlDef> list2 = (usage.TryGetValue(item, out value) ? value.Where((ControlDef c) => c != control).ToList() : new List<ControlDef>());
			string value2;
			string text = (InputMap.Reserved.TryGetValue(item, out value2) ? value2 : null);
			System.Windows.Controls.TextBlock textBlock = new System.Windows.Controls.TextBlock
			{
				Text = InputMap.Display(item),
				FontSize = 12.0,
				VerticalAlignment = VerticalAlignment.Center
			};
			textBlock.SetResourceReference(System.Windows.Controls.TextBlock.FontFamilyProperty, "MonoFont");
			SymbolIcon symbolIcon = new SymbolIcon
			{
				Symbol = (InputMap.IsMouse(item) ? SymbolRegular.CursorClick24 : SymbolRegular.Keyboard24),
				FontSize = 11.0,
				Margin = new Thickness(0.0, 0.0, 5.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center
			};
			symbolIcon.SetResourceReference(Page.ForegroundProperty, "TextFillColorTertiaryBrush");
			System.Windows.Controls.Button button = new System.Windows.Controls.Button
			{
				Content = new SymbolIcon
				{
					Symbol = SymbolRegular.Dismiss16,
					FontSize = 10.0
				},
				Margin = new Thickness(6.0, 0.0, -3.0, 0.0),
				Padding = new Thickness(2.0),
				ToolTip = "Remove"
			};
			button.SetResourceReference(FrameworkElement.StyleProperty, "BareButton");
			string n = item;
			button.Click += delegate(object _, RoutedEventArgs e)
			{
				e.Handled = true;
				Store(control, (from b in Bindings.Get(LauncherPage.Ini, control)
					where !b.Equals(n, StringComparison.OrdinalIgnoreCase)
					select b).ToList());
			};
			Border border = new Border();
			border.Child = LauncherPage.Row(symbolIcon, textBlock, button);
			Border border2 = border;
			border2.SetResourceReference(FrameworkElement.StyleProperty, "Chip");
			if (list2.Count > 0 || text != null)
			{
				border2.SetResourceReference(Border.BorderBrushProperty, "WarningTextBrush");
				textBlock.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "WarningTextBrush");
				border2.ToolTip = string.Join("\n", new string[2]
				{
					(list2.Count > 0) ? ("Also bound to: " + string.Join(", ", list2.Select((ControlDef c) => c.Label))) : null,
					text
				}.Where((string t) => t != null));
			}
			wrapPanel.Children.Add(border2);
		}
		if (list.Count == 0)
		{
			System.Windows.Controls.TextBlock textBlock2 = LauncherPage.Text("Not bound", "Faint");
			textBlock2.FontStyle = FontStyles.Italic;
			textBlock2.Margin = new Thickness(2.0, 0.0, 8.0, 0.0);
			textBlock2.VerticalAlignment = VerticalAlignment.Center;
			wrapPanel.Children.Add(textBlock2);
		}
		if (!Bindings.IsDefault(LauncherPage.Ini, control))
		{
			Ellipse ellipse = new Ellipse
			{
				Width = 5.0,
				Height = 5.0,
				Margin = new Thickness(6.0, 0.0, 0.0, 0.0),
				ToolTip = "Changed from the default"
			};
			ellipse.SetResourceReference(Shape.FillProperty, "AccentTextFillColorPrimaryBrush");
			wrapPanel.Children.Add(ellipse);
		}
		System.Windows.Controls.Button button2 = new System.Windows.Controls.Button();
		button2.Content = wrapPanel;
		button2.Padding = new Thickness(6.0, 2.0, 6.0, 2.0);
		button2.HorizontalContentAlignment = HorizontalAlignment.Stretch;
		button2.ToolTip = "Click to set a key or mouse input for " + control.Label + ". Right-click to unbind.";
		button2.SetResourceReference(FrameworkElement.StyleProperty, "BareButton");
		button2.Click += delegate
		{
			BeginCapture(control);
		};
		button2.MouseRightButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			Store(control, new List<string>());
		};
		return button2;
	}

	private FrameworkElement PadCell(ControlDef control)
	{
		string input = PadMap.InputFor(control);
		if (input == null)
		{
			string text = control.Pad ?? "—";
			if (LauncherPage.Ini.Flag("pad_swap_sticks"))
			{
				text = ((text == "Left stick") ? "Right stick" : ((text == "Right stick") ? "Left stick" : text));
			}
			System.Windows.Controls.TextBlock textBlock = LauncherPage.Text(text, "Faint");
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			return textBlock;
		}
		string text2 = PadMap.Main(LauncherPage.Ini, input);
		Wpf.Ui.Controls.Button button = new Wpf.Ui.Controls.Button
		{
			Content = ((text2.Length > 0) ? PadMap.Label(text2) : "Not bound"),
			Icon = LauncherPage.Icon(SymbolRegular.XboxController24),
			Padding = new Thickness(8.0, 3.0, 10.0, 3.0),
			FontSize = 12.5,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Left,
			ToolTip = "The controller button for this action (the game's " + control.Pad + "). Click, then press a button on the pad."
		};
		if (!PadMap.IsDefault(LauncherPage.Ini, input))
		{
			button.FontWeight = FontWeights.SemiBold;
		}
		button.Click += delegate
		{
			BeginPadCapture(control, input);
		};
		return button;
	}

	private void Store(ControlDef control, List<string> names)
	{
		Bindings.Set(LauncherPage.Ini, control, names);
		Changed();
	}

	private void Changed()
	{
		LauncherPage.Ws.SaveSoon();
		Rebuild();
		FillProfiles();
	}

	private void BeginCapture(ControlDef control)
	{
		_target = control;
		_padInput = null;
		_captureTitle.Text = "Bind " + control.Label;
		_captureHelp.Text = "Press a key, or click / scroll inside the box. Esc cancels.";
		_captureBoxText.Text = "Click or scroll here";
		_padChoices.Visibility = Visibility.Collapsed;
		_captureFeedback.Text = "";
		_overlay.Visibility = Visibility.Visible;
		_overlay.Focus();
		Keyboard.Focus(_overlay);
	}

	private void EndCapture()
	{
		_overlay.Visibility = Visibility.Collapsed;
		_target = null;
		_padInput = null;
		_padControl = null;
		_padTimer.Stop();
	}

	private void BeginPadCapture(ControlDef control, string input)
	{
		_target = null;
		_padControl = control;
		_padInput = input;
		_captureTitle.Text = "Controller button for " + control.Label;
		bool flag = !LauncherPage.Ws.Game.Running && SdlPad.Available;
		_captureHelp.Text = (flag ? "Press the button on your controller, or pick one below. Esc cancels. A button another action uses swaps over." : (LauncherPage.Ws.Game.Running ? "Pick a button below (pressing one works while the game is closed)." : "Pick a button below."));
		_captureBoxText.Text = (flag ? "Waiting for a button…" : "Controller input is not available right now");
		_padChoices.Visibility = Visibility.Visible;
		_captureFeedback.Text = "";
		_overlay.Visibility = Visibility.Visible;
		_overlay.Focus();
		Keyboard.Focus(_overlay);
		if (flag)
		{
			_padHeld = SdlPad.Held();
			_padTimer.Start();
		}
	}

	private void PollPad()
	{
		if (_padInput == null || LauncherPage.Ws.Game.Running)
		{
			_padTimer.Stop();
			return;
		}
		HashSet<string> hashSet = SdlPad.Held();
		string text = hashSet.Except(_padHeld).FirstOrDefault();
		_padHeld = hashSet;
		if (text != null)
		{
			CommitPad(text);
		}
	}

	private void CommitPad(string button)
	{
		if (_padInput != null && !(_padControl == null))
		{
			string padInput = _padInput;
			ControlDef padControl = _padControl;
			EndCapture();
			PadMap.Assign(LauncherPage.Ini, padInput, button);
			Changed();
			LauncherPage.Ws.Log($"Controller: {padControl.Label} on {((button.Length > 0) ? PadMap.Label(button) : "nothing")}.", LogLevel.Step);
		}
	}

	private void Commit(string? name)
	{
		if (!(_target == null))
		{
			ControlDef target = _target;
			EndCapture();
			if (name == null)
			{
				Bindings.Set(LauncherPage.Ini, target, new List<string>());
			}
			else
			{
				Bindings.Assign(LauncherPage.Ini, target, name, keepOtherKind: true);
			}
			Changed();
			LauncherPage.Ws.Log(target.Label + ": " + Show(InputMap.Join(Bindings.Get(LauncherPage.Ini, target))), LogLevel.Step);
		}
	}

	private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (_target == null && _padInput == null)
		{
			return;
		}
		e.Handled = true;
		Key key = ((e.Key == Key.System) ? e.SystemKey : e.Key);
		if (_padInput != null)
		{
			if (key == Key.Escape)
			{
				EndCapture();
			}
			return;
		}
		if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
		{
			EndCapture();
			return;
		}
		string text = InputMap.SdlKeyName(key);
		if (text == null)
		{
			_captureFeedback.Text = $"{key} cannot be bound; try another key.";
		}
		else
		{
			Commit(text);
		}
	}

	private static bool SameBindings(Dictionary<string, string> a, Dictionary<string, string> b)
	{
		return InputMap.Controls.All(delegate(ControlDef c)
		{
			string value;
			List<string> list = InputMap.Split(a.TryGetValue(c.Name, out value) ? value : c.Default);
			string value2;
			List<string> list2 = InputMap.Split(b.TryGetValue(c.Name, out value2) ? value2 : c.Default);
			return list.Count == list2.Count && list.Zip(list2).All(((string First, string Second) p) => p.First.Equals(p.Second, StringComparison.OrdinalIgnoreCase));
		});
	}

	private ControlProfiles.Profile? Matching()
	{
		Dictionary<string, string> current = ControlProfiles.Current(LauncherPage.Ini);
		string chosen = LauncherPage.S.Str("controls_profile");
		return _profiles.FirstOrDefault((ControlProfiles.Profile p) => p.Name == chosen && SameBindings(p.Bindings, current)) ?? _profiles.FirstOrDefault((ControlProfiles.Profile p) => SameBindings(p.Bindings, current));
	}

	private void FillProfiles()
	{
		_fillingProfiles = true;
		_profiles = ControlProfiles.All();
		_profileBox.Items.Clear();
		ControlProfiles.Profile match = Matching();
		if (match == null)
		{
			_profileBox.Items.Add(new ComboBoxItem
			{
				Content = "Custom (not saved)",
				Tag = null
			});
		}
		foreach (ControlProfiles.Profile profile in _profiles)
		{
			_profileBox.Items.Add(new ComboBoxItem
			{
				Content = (profile.BuiltIn ? (profile.Name + " (built in)") : profile.Name),
				Tag = profile
			});
		}
		_profileBox.SelectedItem = _profileBox.Items.OfType<ComboBoxItem>().First((ComboBoxItem i) => i.Tag == match);
		LauncherPage.SetHint(_profileHint, (match == null) ? "Your current bindings are not saved in a profile. Use Save as… to keep them" : string.Join("  ·  ", new string[2]
		{
			match.Description,
			(match.Author.Length > 0) ? ("by " + match.Author) : ""
		}.Where((string t) => t.Length > 0)));
		_deleteProfile.IsEnabled = (object)match != null && !match.BuiltIn;
		_fillingProfiles = false;
	}

	private void ProfileChosen()
	{
		if (!_fillingProfiles && _profileBox.SelectedItem is ComboBoxItem { Tag: ControlProfiles.Profile tag })
		{
			if (Matching() == null && !Prompt.Confirm("Replace your bindings?", "Your current bindings are not saved in a profile. Using \"" + tag.Name + "\" replaces them.", "Use profile"))
			{
				FillProfiles();
				return;
			}
			ControlProfiles.Apply(LauncherPage.S, LauncherPage.Ini, tag);
			LauncherPage.Ws.Log("Control profile: " + tag.Name, LogLevel.Step);
			Changed();
		}
	}

	private ControlProfiles.Profile CurrentAsProfile()
	{
		ControlProfiles.Profile profile = Matching();
		object obj = profile?.Name;
		if (obj == null)
		{
			string text = LauncherPage.S.Str("controls_profile");
			obj = ((text != null && text.Length > 0) ? text : "My controls");
		}
		return new ControlProfiles.Profile((string)obj, profile?.Author ?? LauncherPage.S.Str("profile_author"), profile?.Description ?? "", ControlProfiles.Current(LauncherPage.Ini), BuiltIn: false, "", PadMap.Current(LauncherPage.Ini));
	}

	private void SaveProfile()
	{
		ControlProfiles.Profile profile = CurrentAsProfile();
		string[] array = Prompt.Ask("Save profile", "Saves your current bindings so you can switch back to them or share them.", new Prompt.Field("Name", profile.BuiltIn ? "" : profile.Name, "My controls"), new Prompt.Field("Author", LauncherPage.S.Str("profile_author"), "Optional"), new Prompt.Field("Description", profile.Description, "What the layout is good for (optional)", Multiline: true));
		if (array == null)
		{
			return;
		}
		string name = array[0].Trim();
		if (name.Length == 0)
		{
			name = "My controls";
		}
		if (ControlProfiles.BuiltIns.Any((ControlProfiles.Profile b) => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
		{
			Prompt.Tell("Choose another name", "\"" + name + "\" is a built-in profile.");
		}
		else if (!_profiles.Any((ControlProfiles.Profile p) => !p.BuiltIn && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) || Prompt.Confirm("Replace the profile?", "A profile called \"" + name + "\" already exists.", "Replace"))
		{
			LauncherPage.S.Set("profile_author", array[1].Trim());
			ControlProfiles.Profile profile2 = new ControlProfiles.Profile(name, array[1].Trim(), array[2].Trim(), ControlProfiles.Current(LauncherPage.Ini), BuiltIn: false, "", PadMap.Current(LauncherPage.Ini));
			try
			{
				ControlProfiles.Save(profile2);
				LauncherPage.S.Set("controls_profile", name);
				LauncherPage.Ws.SaveSoon();
				LauncherPage.Ws.Log("Saved control profile \"" + name + "\".", LogLevel.Good);
			}
			catch (Exception ex)
			{
				LauncherPage.Ws.Log("Could not save the profile: " + ex.Message, LogLevel.Bad);
			}
			FillProfiles();
		}
	}

	private void DeleteProfile()
	{
		if (_profileBox.SelectedItem is ComboBoxItem { Tag: ControlProfiles.Profile { BuiltIn: false } tag } && Prompt.Confirm("Delete the profile?", "\"" + tag.Name + "\" is removed from this PC. Your current bindings stay as they are.", "Delete"))
		{
			try
			{
				ControlProfiles.Delete(tag);
				LauncherPage.Ws.Log("Deleted control profile \"" + tag.Name + "\".", LogLevel.Step);
			}
			catch (Exception ex)
			{
				LauncherPage.Ws.Log("Could not delete the profile: " + ex.Message, LogLevel.Bad);
			}
			FillProfiles();
		}
	}

	private void CopyProfile()
	{
		Clipboard.SetText(ControlProfiles.ToJson(CurrentAsProfile()));
		LauncherPage.Ws.Log("Profile JSON copied. Paste it on any paste site and share the link.", LogLevel.Good);
	}

	private void ExportProfile()
	{
		ControlProfiles.Profile profile = CurrentAsProfile();
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Title = "Export control profile",
			FileName = profile.Name + ".json",
			Filter = "Control profile|*.json"
		};
		if (saveFileDialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			File.WriteAllText(saveFileDialog.FileName, ControlProfiles.ToJson(profile));
			LauncherPage.Ws.Log("Profile exported: " + saveFileDialog.FileName, LogLevel.Good);
		}
		catch (Exception ex)
		{
			LauncherPage.Ws.Log("Could not export the profile: " + ex.Message, LogLevel.Bad);
		}
	}

	private void ImportFile()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Import control profile",
			Filter = "Control profile|*.json|All files|*.*"
		};
		if (openFileDialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			Offer(ControlProfiles.Parse(File.ReadAllText(openFileDialog.FileName)));
		}
		catch (Exception ex)
		{
			Prompt.Tell("Could not import", ex.Message);
		}
	}

	private async Task ImportLinkAsync()
	{
		string[] array = Prompt.Ask("Import from a link", "Paste a link to a shared profile: a Pastebin, GitHub Gist or file, Rentry, hastebin, paste.rs, pastes.dev or dpaste link, or any link that shows the raw JSON.", new Prompt.Field("Link", "", "https://pastebin.com/…"));
		if (array == null || array[0].Trim().Length == 0)
		{
			return;
		}
		try
		{
			LauncherPage.Ws.Log("Downloading the profile…", LogLevel.Step);
			Offer(await ControlProfiles.DownloadAsync(array[0]));
		}
		catch (Exception ex)
		{
			Prompt.Tell("Could not import", (ex is FormatException) ? ex.Message : ("The link could not be read: " + ex.GetBaseException().Message));
		}
	}

	private void Offer(ControlProfiles.Parsed parsed)
	{
		ControlProfiles.Profile profile = parsed.Profile;
		Dictionary<string, string> current = ControlProfiles.Current(LauncherPage.Ini);
		List<string> list = (from c in InputMap.Controls
			where !SameBindings(new Dictionary<string, string> { [c.Name] = profile.Bindings.GetValueOrDefault(c.Name, c.Default) }, new Dictionary<string, string> { [c.Name] = current[c.Name] })
			select "  " + c.Label + ": " + Show(profile.Bindings.GetValueOrDefault(c.Name, c.Default))).ToList();
		List<string> list2 = new List<string>();
		if (profile.Author.Length > 0)
		{
			list2.Add("by " + profile.Author);
		}
		if (profile.Description.Length > 0)
		{
			list2.Add(profile.Description);
		}
		list2.Add((list.Count == 0) ? "It is the same as your current bindings." : ($"Changes {list.Count} of {InputMap.Controls.Length} actions:\n" + string.Join("\n", list.Take(12)) + ((list.Count > 12) ? "\n  …" : "")));
		if (parsed.Notes.Count > 0)
		{
			list2.Add("Notes:\n  " + string.Join("\n  ", parsed.Notes.Take(6)));
		}
		if (Prompt.Confirm("Import \"" + profile.Name + "\"?", string.Join("\n\n", list2), "Save and use"))
		{
			string name = profile.Name;
			int num = 2;
			while (_profiles.Any((ControlProfiles.Profile p) => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
			{
				name = $"{profile.Name} ({num})";
				num++;
			}
			profile = profile with
			{
				Name = name
			};
			try
			{
				ControlProfiles.Save(profile);
			}
			catch (Exception ex)
			{
				LauncherPage.Ws.Log("Could not save the imported profile: " + ex.Message, LogLevel.Bad);
			}
			ControlProfiles.Apply(LauncherPage.S, LauncherPage.Ini, profile);
			LauncherPage.Ws.Log("Imported control profile \"" + name + "\".", LogLevel.Good);
			Changed();
		}
	}
}
