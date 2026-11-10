using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.UI;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class UiJoin : BotScript
{
	private string before = "";

	private string address = "";

	private string pw = "";

	private string bad;

	public override string Name => "ui-join";

	public override float TimeoutSeconds => 400f;

	protected override void Build()
	{
		address = Args.Get("--br-ui-join") ?? "";
		pw = Args.Get("--br-ui-pw") ?? "";
		bad = Args.Get("--br-ui-badpw");
		Check("switches", () => JoinMenu.ValidAddress(address) && Args.Join == null, () => "--br-ui-join=" + address + ((bad != null) ? ", wrong password first" : "") + ", --br-join " + (Args.Join ?? "absent"));
		WaitFor("menu", 90f, () => Ui.AtMenu && (Object)(object)MainMenuUIController.Instance != (Object)null, () => "scene " + BotProbes.Scene);
		WaitFor("button", 30f, () => (Object)(object)JoinMenu.Button != (Object)null, () => "fallback " + JoinMenu.UsingFallbackButton);
		Do("files-before", () => before = UiBot.SaveHash());
		Do("click", delegate
		{
			((UnityEvent)JoinMenu.Button.GetComponentInChildren<Button>(true).onClick).Invoke();
			return "clicked '" + ((Object)JoinMenu.Button).name + "' (" + (JoinMenu.UsingFallbackButton ? "menu-layer panel" : "copy of the Mods button") + ")";
		});
		WaitFor("window", 10f, () => JoinMenu.DialogOpen, () => "open " + JoinMenu.DialogOpen);
		Check("mods-closed", () => (Object)(object)ModUIManager.Instance == (Object)null || (Object)(object)ModUIManager.Instance.panel == (Object)null || !ModUIManager.Instance.panel.activeSelf, () => "Mods panel " + (((Object)(object)ModUIManager.Instance?.panel != (Object)null && ModUIManager.Instance.panel.activeSelf) ? "OPEN" : "closed"));
		if (bad != null)
		{
			Do("fill-bad", () => Fill(bad));
			Do("join-bad", delegate
			{
				JoinMenu.Join();
				return "joining with a wrong password";
			});
			WaitFor("refused", 40f, () => Net.ClientState == ClientState.Idle && JoinMenu.Status.Contains("Wrong password") && JoinMenu.DialogOpen, () => "state " + Net.ClientState.ToString() + ", window " + JoinMenu.DialogOpen + ", status '" + JoinMenu.Status + "'");
		}
		Do("fill", () => Fill(pw));
		Do("join", delegate
		{
			JoinMenu.Join();
			return "joining " + address;
		});
		WaitFor("live", 180f, () => Ui.ShopOnline, () => "state " + Net.ClientState.ToString() + ", status '" + JoinMenu.Status + "', loading '" + LoadingScreen.Text + "'");
		Check("reserved-slot", () => NewGameData.Instance.saveSlot == 990077 && Game.Store.saveSlotId == 990077, () => "slot " + NewGameData.Instance.saveSlot);
		Check("own-saves-untouched", () => UiBot.SaveHash() == before, () => "before " + before + " / after " + UiBot.SaveHash());
		Check("remembered", () => JoinMenu.LoadServers().Any((JoinMenu.SavedServer s) => s.address == address && s.password == pw && s.name == Net.ServerName), () => string.Join(", ", from s in JoinMenu.LoadServers()
			select s.address + " '" + s.name + "' pw " + (s.password.Length > 0)));
		Do("leave", delegate
		{
			StoreUIManager.Instance.escapeUIManager.OnMainMenu();
			StoreUIManager.Instance.confirmPanelUIManager.OnYesClick();
			return "left via the escape menu";
		});
		WaitFor("at-menu", 60f, () => Net.Role == ProbablyTogether.Role.Off && Ui.AtMenu && (Object)(object)MainMenuUIController.Instance != (Object)null && (Object)(object)JoinMenu.Button != (Object)null, () => "scene " + BotProbes.Scene);
		Do("reopen", delegate
		{
			JoinMenu.OpenDialog();
			return "window " + JoinMenu.DialogOpen;
		});
		Check("address-kept", delegate
		{
			CustomUIWindow window = CustomUIManager.Instance.GetWindow("humangenome_accomplice.join");
			object obj;
			if (window == null)
			{
				obj = null;
			}
			else
			{
				CustomUIElement obj2 = window.Get("address");
				obj = ((obj2 != null) ? obj2.GetString() : null);
			}
			return (string?)obj == address;
		}, delegate
		{
			CustomUIWindow window = CustomUIManager.Instance.GetWindow("humangenome_accomplice.join");
			object obj;
			if (window == null)
			{
				obj = null;
			}
			else
			{
				CustomUIElement obj2 = window.Get("address");
				obj = ((obj2 != null) ? obj2.GetString() : null);
			}
			return "address field '" + (string?)obj + "'";
		});
		Do("open-list", delegate
		{
			((UnityEvent)CustomUIManager.Instance.GetWindow("humangenome_accomplice.join").Get("find").button.onClick).Invoke();
			return "clicked Server list";
		});
		WaitFor("list-open", 10f, () => ServerList.IsOpen && !JoinMenu.IsVisible, () => "list " + ServerList.IsOpen + ", join visible " + JoinMenu.IsVisible);
		Check("recent-listed", delegate
		{
			CustomUIWindow window = CustomUIManager.Instance.GetWindow("humangenome_accomplice.servers");
			return ((window != null) ? window.Get("rec_0") : null) != null && JoinMenu.LoadServers().FirstOrDefault()?.address == address;
		}, () => "rows " + ServerList.Shown);
		WaitFor("list-answer", 20f, () => ServerList.State != "loading", () => "state " + ServerList.State);
		Do("version-column", delegate
		{
			CustomUIWindow window = CustomUIManager.Instance.GetWindow("humangenome_accomplice.servers");
			List<ServerList.Entry> list = ServerList.Rows.Where((ServerList.Entry r) => r.Mod.Length > 0).ToList();
			if (list.Count == 0)
			{
				return "skipped: no public server says its version (state " + ServerList.State + ")";
			}
			if (window.Get("h_ver") == null)
			{
				throw new Exception("no Version column name although " + list.Count + " server(s) say their version");
			}
			CustomUIElement val = window.Get("ver_0");
			string text = (((Object)(object)val?.text != (Object)null) ? ((TMP_Text)val.text).text : null);
			if (val == null || text == null || (text.Length > 0 && !text.StartsWith("v", StringComparison.Ordinal)))
			{
				throw new Exception("first public row's version cell '" + text + "'");
			}
			Rect rect = val.root.GetComponent<RectTransform>().rect;
			float width = rect.width;
			string[] array = new string[7] { "h_name", "h_city", "h_ver", "h_mods", "h_pl", "h_day", "ver_0" };
			foreach (string text2 in array)
			{
				TextMeshProUGUI val2 = window.Get(text2)?.text;
				if (!((Object)(object)val2 == (Object)null))
				{
					((TMP_Text)val2).ForceMeshUpdate(false, false);
					if (((TMP_Text)val2).isTextTruncated)
					{
						string[] obj = new string[6]
						{
							text2,
							" '",
							((TMP_Text)val2).text,
							"' is cut off at ",
							null,
							null
						};
						rect = ((TMP_Text)val2).rectTransform.rect;
						obj[4] = rect.width.ToString("F0", CultureInfo.InvariantCulture);
						obj[5] = " wide";
						throw new Exception(string.Concat(obj));
					}
				}
			}
			return "Version column shown; first public row '" + text + "' (" + width.ToString("F0", CultureInfo.InvariantCulture) + " wide); " + list.Count + " of " + ServerList.Rows.Count + " server(s) say their version: " + string.Join(", ", from r in list.Take(4)
				select r.Name + " v" + r.Mod);
		});
		Do("list-back", delegate
		{
			((UnityEvent)CustomUIManager.Instance.GetWindow("humangenome_accomplice.servers").Get("back").button.onClick).Invoke();
			return "clicked Back";
		});
		WaitFor("join-back", 10f, () => !ServerList.IsOpen && JoinMenu.IsVisible, () => "list " + ServerList.IsOpen + ", join visible " + JoinMenu.IsVisible);
		Do("close", delegate
		{
			JoinMenu.CloseDialog();
			return "closed";
		});
	}

	private string Fill(string password)
	{
		CustomUIWindow window = CustomUIManager.Instance.GetWindow("humangenome_accomplice.join");
		window.Get("address").SetString(address, true);
		window.Get("name").SetString("botj", true);
		window.Get("password").SetString(password, true);
		window.Get("remember").SetBool(true, true);
		return "address " + address + ", name botj, password " + ((password.Length > 0) ? ("(" + password.Length + " chars)") : "none");
	}
}
