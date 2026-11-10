using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.UI;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class EscOffline : EscBase
{
	public override string Name => "esc-offline";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 300f;

	private static CustomUIWindow Win(string id)
	{
		CustomUIManager instance = CustomUIManager.Instance;
		if (instance == null)
		{
			return null;
		}
		return instance.GetWindow(id);
	}

	private static string WinText(string id, string el)
	{
		CustomUIWindow obj = Win(id);
		object obj2;
		if (obj == null)
		{
			obj2 = null;
		}
		else
		{
			CustomUIElement obj3 = obj.Get(el);
			if (obj3 == null)
			{
				obj2 = null;
			}
			else
			{
				TextMeshProUGUI text = obj3.text;
				obj2 = ((text != null) ? ((TMP_Text)text).text : null);
			}
		}
		if (obj2 == null)
		{
			obj2 = "";
		}
		return (string)obj2;
	}

	private static string JoinLabel()
	{
		if (!((Object)(object)JoinMenu.Button != (Object)null))
		{
			return "";
		}
		TextMeshProUGUI componentInChildren = JoinMenu.Button.GetComponentInChildren<TextMeshProUGUI>(true);
		return ((componentInChildren != null) ? ((TMP_Text)componentInChildren).text : null) ?? "";
	}

	protected override void Build()
	{
		Check("offline", () => !Net.IsOnline && Net.Role == ProbablyTogether.Role.Off, () => "role " + Net.Role);
		WaitFor("menu", 180f, () => BotProbes.Scene == "EmporiumMenu" && (Object)(object)NewGameData.Instance != (Object)null, () => "scene " + BotProbes.Scene);
		Delay("menu-settle", 3f);
		string saved = "";
		Do("game-zh", delegate
		{
			Locale selectedLocale = LocalizationSettings.SelectedLocale;
			object obj;
			LocaleIdentifier identifier;
			if (!((Object)(object)selectedLocale != (Object)null))
			{
				obj = "en";
			}
			else
			{
				identifier = selectedLocale.Identifier;
				obj = identifier.Code;
			}
			saved = (string)obj;
			List<Locale> locales = LocalizationSettings.AvailableLocales.Locales;
			Locale val = ((IEnumerable<Locale>)locales).FirstOrDefault((Func<Locale, bool>)delegate(Locale l)
			{
				LocaleIdentifier identifier2 = l.Identifier;
				return identifier2.Code.StartsWith("zh");
			});
			if ((Object)(object)val == (Object)null)
			{
				throw new Exception("no zh locale (codes " + string.Join(",", locales.Select(delegate(Locale l)
				{
					LocaleIdentifier identifier2 = l.Identifier;
					return identifier2.Code;
				})) + ")");
			}
			LocalizationSettings.SelectedLocale = val;
			string[] obj2 = new string[7] { "game locale ", saved, " -> ", null, null, null, null };
			identifier = val.Identifier;
			obj2[3] = identifier.Code;
			obj2[4] = " (all: ";
			obj2[5] = string.Join(",", locales.Select(delegate(Locale l)
			{
				LocaleIdentifier identifier2 = l.Identifier;
				return identifier2.Code;
			}));
			obj2[6] = ")";
			return string.Concat(obj2);
		});
		Delay("game-zh-settle", 3f);
		Do("game-zh-read", delegate
		{
			Lang.ForceRefresh();
			return "our texts follow '" + Lang.Code + "'";
		});
		Check("zh-follows-game", () => Lang.Code == "zh", () => "code '" + Lang.Code + "'");
		WaitFor("zh-join-button", 5f, () => JoinLabel() == "加入服务器", () => "Join server button '" + JoinLabel() + "'");
		Do("zh-open-join", delegate
		{
			((UnityEvent)JoinMenu.Button.GetComponentInChildren<Button>(true).onClick).Invoke();
			return "clicked the Join button";
		});
		WaitFor("zh-join-window", 10f, () => JoinMenu.DialogOpen, () => "open " + JoinMenu.DialogOpen);
		Delay("pic-zh-join", ((Args.Get("--br-esc-hold") ?? "") == "1") ? 6f : 0.1f);
		Check("zh-join-texts", () => WinText("humangenome_accomplice.join", "find") == "服务器列表" && WinText("humangenome_accomplice.join", "join") == "加入", () => "Server list '" + WinText("humangenome_accomplice.join", "find") + "', Join '" + WinText("humangenome_accomplice.join", "join") + "'");
		Check("zh-join-font", delegate
		{
			CustomUIWindow obj = Win("humangenome_accomplice.join");
			TextMeshProUGUI t = ((obj == null) ? null : obj.Get("find")?.text);
			return (Object)(object)t != (Object)null && (Object)(object)((TMP_Text)t).font != (Object)null && ((TMP_Text)t).text.All((char ch) => ch < '\u0080' || ((TMP_Text)t).font.HasCharacter(ch, true, true));
		}, delegate
		{
			CustomUIWindow obj = Win("humangenome_accomplice.join");
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				CustomUIElement obj3 = obj.Get("find");
				if (obj3 == null)
				{
					obj2 = null;
				}
				else
				{
					TextMeshProUGUI text = obj3.text;
					if (text == null)
					{
						obj2 = null;
					}
					else
					{
						TMP_FontAsset font = ((TMP_Text)text).font;
						obj2 = ((font != null) ? ((Object)font).name : null);
					}
				}
			}
			if (obj2 == null)
			{
				obj2 = "-";
			}
			return "font " + (string?)obj2;
		});
		Do("zh-close-join", delegate
		{
			JoinMenu.CloseDialog();
			return "closed";
		});
		Do("game-en", delegate
		{
			Locale val = ((IEnumerable<Locale>)LocalizationSettings.AvailableLocales.Locales).FirstOrDefault((Func<Locale, bool>)delegate(Locale l)
			{
				LocaleIdentifier identifier2 = l.Identifier;
				return identifier2.Code == (string.IsNullOrEmpty(saved) ? "en" : saved);
			});
			if ((Object)(object)val != (Object)null)
			{
				LocalizationSettings.SelectedLocale = val;
			}
			object obj;
			if (!((Object)(object)val != (Object)null))
			{
				obj = "?";
			}
			else
			{
				LocaleIdentifier identifier = val.Identifier;
				obj = identifier.Code;
			}
			return "game locale back to " + (string?)obj;
		});
		Delay("game-en-settle", 3f);
		Do("game-en-read", delegate
		{
			Lang.ForceRefresh();
			return "our texts follow '" + Lang.Code + "'";
		});
		WaitFor("en-join-button", 5f, () => Lang.Code == "en" && JoinLabel() == "Join server", () => "code '" + Lang.Code + "', button '" + JoinLabel() + "'");
		Do("new-run", delegate
		{
			NewGameData instance = NewGameData.Instance;
			instance.saveFile = null;
			instance.isLoadFromMainMenu = false;
			instance.saveSlot = -1;
			instance.startType = (NewGameData.StartType)1;
			instance.startScenarioId = "";
			instance.skipIntro = true;
			instance.disableTutorialHint = true;
			instance.difficultyId = "normal";
			EmporiumEntry.sceneLoadStartTime = Time.realtimeSinceStartup;
			SceneManager.LoadScene("InventoryScene", (LoadSceneMode)0);
			return "new WATER_MERCHANT run, loading InventoryScene";
		});
		WaitFor("shop", 240f, () => Items.Ready && (Object)(object)EscBase.Esc != (Object)null, () => "scene " + BotProbes.Scene);
		Delay("shop-settle", 3f);
		Open("open");
		Do("dump", (Func<string>)EscBase.Dump);
		Check("no-invite", () => !EscapeMenu.InviteShown, () => "Copy invite shown offline");
		Check("main-menu-game-text", () => EscapeMenu.MainMenuLabel.Length > 0 && EscapeMenu.MainMenuLabel != "Leave server", () => "main menu label '" + EscapeMenu.MainMenuLabel + "'");
		Check("footer-game-text", () => EscapeMenu.FooterLabel.IndexOf("AUTOSAVE", StringComparison.OrdinalIgnoreCase) >= 0, () => "footer '" + EscapeMenu.FooterLabel + "'");
		Check("footer-is-the-line", () => EscBase.FooterOk(), () => EscBase.FooterWhy());
		Check("box-not-grown", () => EscapeMenu.Grown == 0f && EscBase.Outside().Length == 0, () => "grown " + EscapeMenu.Grown.ToString("F0") + ", outside: " + EscBase.Outside());
		Close("close");
	}
}
