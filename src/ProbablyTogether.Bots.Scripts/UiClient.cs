using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class UiClient : BotScript
{
	private bool fakeReady;

	private bool sawLoading;

	private bool consumedAtClose;

	private bool menuBefore;

	private KeyValuePair<Func<ReadyBar.View>, Action<bool>> realBinding;

	private int spamSent;

	private int resyncFrom;

	private long dragId;

	private string dragName = "";

	public override string Name => "ui-client";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 400f;

	protected override void Build()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("shop-live", 180f, () => Ui.ShopOnline, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		Do("describe-lab-lock", delegate
		{
			Presence.DescribeLock("bot.lock", (string _) => "is testing the lock label", (string _) => new Vector2(0.5f, 0.55f));
			return "lab lock label registered";
		});
		Delay("settle", 2f);
		Check("overlay", () => (Object)(object)Overlay.Root != (Object)null && ((Component)Overlay.Root).gameObject.activeInHierarchy, delegate
		{
			object obj;
			if (!((Object)(object)Overlay.Root != (Object)null))
			{
				obj = "missing";
			}
			else
			{
				string name = ((Object)Overlay.Root).name;
				Transform parent = ((Transform)Overlay.Root).parent;
				obj = name + " under " + ((parent != null) ? ((Object)parent).name : null);
			}
			return "root " + (string?)obj + ", loading screen " + LoadingScreen.Visible;
		});
		Check("click-through", () => ((Component)Overlay.Root).GetComponentsInChildren<Graphic>(true).All((Graphic g) => !g.raycastTarget), () => ((Component)Overlay.Root).GetComponentsInChildren<Graphic>(true).Length + " graphics, raycast targets: " + string.Join(",", from g in ((Component)Overlay.Root).GetComponentsInChildren<Graphic>(true)
			where g.raycastTarget
			select ((Object)g).name));
		Check("loading-hidden", () => !LoadingScreen.Visible, () => "loading screen '" + LoadingScreen.Text + "'");
		Check("patches", () => UiBot.Patched(AccessTools.Method(typeof(KeyBindHelper), "GetKeyDown", (Type[])null, (Type[])null), "UiKeyDownPatch") && UiBot.Patched(AccessTools.Method(typeof(KeyBindHelper), "GetKey", (Type[])null, (Type[])null), "UiKeyPatch") && UiBot.Patched(AccessTools.Method(typeof(GameOptionUIManager), "ConsumedEscapeThisFrame", (Type[])null, (Type[])null), "UiConsumedEscapePatch") && UiBot.Patched(AccessTools.Method(typeof(StoreUIManager), "ResolveEscape", (Type[])null, (Type[])null), "UiResolveEscapePatch") && UiBot.Patched(AccessTools.Method(typeof(EscapeUIManager), "OnMainMenu", (Type[])null, (Type[])null), "UiLeavePatch"), () => "key binds, escape claim, ResolveEscape, leave");
		WaitFor("two-players", 150f, () => Net.Players.Count >= 2, () => Net.Players.Count + " player(s)");
		WaitFor("roster", 15f, () => Presence.RosterTexts.Count == Net.Players.Count && Net.Players.Keys.All((int id) => Presence.RosterTexts.ContainsKey(id)), () => "header '" + Presence.RosterHeader + "' rows: " + string.Join(" / ", Presence.RosterTexts.Values));
		string value;
		Step("lock-shown", 60f, delegate(BotStep st)
		{
			int num = Locks.HeldBy("bot.lock");
			if (num <= 0)
			{
				st.Detail = "lock not taken yet";
				return StepStatus.Running;
			}
			string text = Net.PlayerName(num);
			if (num == Net.LocalPlayer)
			{
				bool num2 = Presence.RosterTexts.TryGetValue(num, out value) && value.Contains("testing the lock label");
				bool flag = !Presence.LockLabelTexts.ContainsKey("bot.lock");
				st.Detail = "I hold it; my roster row '" + value + "', own label drawn " + !flag;
				if (!(num2 && flag))
				{
					return StepStatus.Running;
				}
				return StepStatus.Pass;
			}
			string value2;
			bool flag2 = Presence.LockLabelTexts.TryGetValue("bot.lock", out value2) && value2 == text + " is testing the lock label";
			string value3;
			bool flag3 = Presence.RosterTexts.TryGetValue(num, out value3) && value3.Contains("testing the lock label");
			st.Detail = "label '" + value2 + "', holder row '" + value3 + "'";
			return (flag2 && flag3) ? StepStatus.Pass : StepStatus.Running;
		});
		Do("cursor-set", delegate
		{
			Presence.FakePointer = new Vector2(UiBot.XFor(Net.LocalPlayer), 0.6f);
			return "my pointer at game-area (" + UiBot.XFor(Net.LocalPlayer).ToString("F2", CultureInfo.InvariantCulture) + ", 0.60)";
		});
		WaitFor("cursor-seen", 40f, delegate
		{
			Presence.Remote remote = UiBot.OtherRemote();
			return remote != null && remote.Visible && Mathf.Abs(remote.Anchor.x - UiBot.XFor(remote.Id)) <= 0.01f && Mathf.Abs(remote.Anchor.y - 0.6f) <= 0.01f && (Object)(object)((Transform)Overlay.Cursors).Find("cursor_" + remote.Id) != (Object)null && ((Component)((Transform)Overlay.Cursors).Find("cursor_" + remote.Id)).gameObject.activeSelf;
		}, () => UiBot.Describe(UiBot.OtherRemote()) + "; game area " + LetterboxManager.GameAreaWidth + "x" + LetterboxManager.GameAreaHeight);
		Do("view-away", delegate
		{
			Presence.FakeView = "bot-away-" + Net.LocalPlayer;
			return "my view " + Presence.LocalView;
		});
		WaitFor("cursor-hidden", 20f, delegate
		{
			Presence.Remote remote = UiBot.OtherRemote();
			return remote != null && !remote.Visible;
		}, () => UiBot.Describe(UiBot.OtherRemote()));
		Delay("away", 3f);
		Do("view-back", delegate
		{
			Presence.FakeView = null;
			return "my view " + Presence.LocalView;
		});
		WaitFor("cursor-back", 30f, () => UiBot.OtherRemote()?.Visible ?? false, () => UiBot.Describe(UiBot.OtherRemote()));
		Do("drag-set", delegate
		{
			GameItem val = UiBot.AnyItem();
			if (val == null)
			{
				throw new Exception("no item with an id in the shop");
			}
			dragId = Ids.Of(val);
			dragName = val.GetDisplayName(false);
			Presence.FakeDrag = dragId;
			return "dragging #" + dragId + " '" + dragName + "'";
		});
		WaitFor("drag-label", 30f, delegate
		{
			Presence.Remote remote = UiBot.OtherRemote();
			return remote != null && remote.Drag > 0 && remote.LabelText.Contains("holding ") && !remote.LabelText.Contains("an item");
		}, () => UiBot.Describe(UiBot.OtherRemote()));
		Do("drag-clear", delegate
		{
			Presence.FakeDrag = 0L;
			return "not dragging";
		});
		WaitFor("drag-cleared", 30f, delegate
		{
			Presence.Remote remote = UiBot.OtherRemote();
			return remote != null && remote.Drag == 0L && !remote.LabelText.Contains("holding");
		}, () => UiBot.Describe(UiBot.OtherRemote()));
		WaitFor("chat-history", 30f, () => Chat.Lines.Any((Chat.Line l) => l.System && l.N > 0 && l.Text.Contains("morning line")), UiBot.Lines);
		Check("chat-greeting", () => Chat.Lines.Any((Chat.Line l) => l.N == 0 && l.Text.StartsWith("You joined")), UiBot.Lines);
		Do("chat-send", delegate
		{
			if (!Chat.Send("hello from " + Net.PlayerName(Net.LocalPlayer) + " <color=red>x"))
			{
				throw new Exception("Send returned false");
			}
			return "sent";
		});
		WaitFor("chat-own", 20f, () => Chat.Lines.Any((Chat.Line l) => l.Player == Net.LocalPlayer && l.Text == "hello from " + Net.PlayerName(Net.LocalPlayer) + " ‹color=red>x"), UiBot.Lines);
		WaitFor("chat-other", 40f, () => Chat.Lines.Any((Chat.Line l) => l.Player == UiBot.Other() && l.Text.StartsWith("hello from " + Net.PlayerName(UiBot.Other()))), UiBot.Lines);
		Check("chat-escaped-once", () => !Chat.Lines.Any((Chat.Line l) => l.Text.Contains("<color")) && Chat.Lines.Count((Chat.Line l) => l.Player == Net.LocalPlayer && l.Text.StartsWith("hello from")) == 1, UiBot.Lines);
		WaitFor("chat-system", 60f, () => Chat.Lines.Any((Chat.Line l) => l.System && l.N > 0 && l.Text.Contains("server says hello")), UiBot.Lines);
		Check("chat-empty-local", () => !Chat.Send("    "), () => "blank line not sent");
		Do("chat-empty-server", delegate
		{
			Net.ToServer("chat.say", new MsgChatSay
			{
				text = "\a\t "
			});
			return "sent a line of control characters";
		});
		WaitFor("chat-empty-refused", 15f, () => Chat.LastNotice.Contains("Empty"), () => "notice '" + Chat.LastNotice + "'");
		Delay("rate-window", 5.5f);
		Step("chat-rate", 20f, delegate(BotStep st)
		{
			if (Net.LocalPlayer != Net.Players.Keys.Min())
			{
				st.Detail = "skipped: only the first player floods";
				return StepStatus.Pass;
			}
			if (spamSent == 0)
			{
				for (int i = 1; i <= 6; i++)
				{
					Chat.Send("burst " + i);
				}
				spamSent = 6;
			}
			int num = Chat.Lines.Count((Chat.Line l) => l.Player == Net.LocalPlayer && l.Text.StartsWith("burst "));
			st.Detail = num + " of 6 posted, notice '" + Chat.LastNotice + "'";
			return (num == 5 && Chat.LastNotice.Contains("too fast")) ? StepStatus.Pass : StepStatus.Running;
		});
		Do("ready-bind", delegate
		{
			realBinding = ReadyBar.Saved();
			string boundBy = ReadyBar.BoundBy;
			ReadyBar.Bind(() => new ReadyBar.View
			{
				Active = true,
				Ready = (fakeReady ? new List<int> { Net.LocalPlayer } : new List<int>()),
				Needed = Net.Players.Count,
				SecondsLeft = (fakeReady ? 90f : (-1f)),
				LocalReady = fakeReady
			}, delegate(bool r)
			{
				fakeReady = r;
			});
			return "lab binding (bound before: " + ((boundBy.Length > 0) ? boundBy : "nothing") + ")";
		});
		WaitFor("ready-shown", 5f, () => ReadyBar.Visible && ReadyBar.Text.Contains("0/" + Net.Players.Count), () => "visible " + ReadyBar.Visible + ", '" + ReadyBar.Text + "'");
		Do("ready-press", delegate
		{
			ReadyBar.Press(ready: true);
			return "pressed";
		});
		WaitFor("ready-counted", 5f, () => ReadyBar.Text.Contains("1/" + Net.Players.Count) && ReadyBar.Text.Contains("left") && Presence.RosterTexts.TryGetValue(Net.LocalPlayer, out value) && value.EndsWith("| ready"), () => "'" + ReadyBar.Text + "' / my row '" + (Presence.RosterTexts.TryGetValue(Net.LocalPlayer, out value) ? value : "") + "'");
		Do("ready-restore", delegate
		{
			ReadyBar.Restore(realBinding);
			return "binding before the stand-in put back: " + ((ReadyBar.BoundBy.Length > 0) ? ReadyBar.BoundBy : "nothing");
		});
		Do("chat-open", delegate
		{
			menuBefore = StoreUIManager.Instance.escapeUIManager.menuPanel.activeSelf;
			Chat.Open();
			return "opened (escape menu was " + (menuBefore ? "open" : "closed") + ")";
		});
		Step("typing-on", 5f, delegate(BotStep st)
		{
			if (st.Elapsed < 0.3f)
			{
				return StepStatus.Running;
			}
			bool flag = Chat.IsOpen && StoreUIManager.IsAnyTyping() && !KeyBindHelper.GetKeyDown("advance_dialogue") && GameOptionUIManager.ConsumedEscapeThisFrame();
			st.Detail = "open " + Chat.IsOpen + ", typing " + StoreUIManager.IsAnyTyping() + ", escape claimed " + GameOptionUIManager.ConsumedEscapeThisFrame() + ", input root " + ((Object)(object)Overlay.InputRoot != (Object)null && ((Component)Overlay.InputRoot).gameObject.activeSelf);
			return flag ? StepStatus.Pass : StepStatus.Fail;
		});
		Do("chat-escape", delegate
		{
			Chat.CloseByEscape();
			consumedAtClose = GameOptionUIManager.ConsumedEscapeThisFrame();
			return "closed by Escape; claimed on that frame " + consumedAtClose;
		});
		Step("typing-off", 5f, delegate(BotStep st)
		{
			if (st.Elapsed < 0.5f)
			{
				return StepStatus.Running;
			}
			bool activeSelf = StoreUIManager.Instance.escapeUIManager.menuPanel.activeSelf;
			bool flag = consumedAtClose && !Chat.IsOpen && !StoreUIManager.IsAnyTyping() && !GameOptionUIManager.ConsumedEscapeThisFrame() && activeSelf == menuBefore;
			st.Detail = "claimed at close " + consumedAtClose + ", open " + Chat.IsOpen + ", typing " + StoreUIManager.IsAnyTyping() + ", claimed now " + GameOptionUIManager.ConsumedEscapeThisFrame() + ", escape menu " + (activeSelf ? "open" : "closed");
			return flag ? StepStatus.Pass : StepStatus.Fail;
		});
		WaitFor("lock-released", 90f, () => Locks.HeldBy("bot.lock") < 0 && !Presence.LockLabelTexts.ContainsKey("bot.lock") && Presence.RosterTexts.Values.All((string t) => !t.Contains("testing the lock label")), () => "holder " + Locks.HeldBy("bot.lock"));
		Do("resync", delegate
		{
			Interactable.isPaused = true;
			TooltipContainer.isPaused = true;
			resyncFrom = Chat.Lines.Count;
			sawLoading = false;
			Snapshot.RequestResync();
			return "asked; " + resyncFrom + " chat line(s) before";
		});
		Step("resync-loading", 30f, delegate(BotStep st)
		{
			if (LoadingScreen.Visible)
			{
				sawLoading = true;
				st.Detail = "'" + LoadingScreen.Text + "'";
			}
			return (sawLoading && Ui.ShopOnline && !LoadingScreen.Visible) ? StepStatus.Pass : StepStatus.Running;
		});
		Check("pauses-cleared", () => !Interactable.isPaused && !TooltipContainer.isPaused, () => "Interactable.isPaused " + Interactable.isPaused + ", TooltipContainer.isPaused " + TooltipContainer.isPaused);
		Check("chat-no-dupes", delegate
		{
			List<int> list = (from l in Chat.Lines
				where l.N > 0
				select l.N).ToList();
			return list.Count == list.Distinct().Count() && Chat.Lines.Count == resyncFrom;
		}, () => Chat.Lines.Count + " line(s) after (was " + resyncFrom + ")");
		WaitFor("overlay-back", 10f, () => (Object)(object)Overlay.Root != (Object)null && ((Component)Overlay.Root).gameObject.activeInHierarchy && Presence.RosterTexts.Count == Net.Players.Count, () => "roster " + Presence.RosterTexts.Count);
		int ask = 0;
		float askedAt = 0f;
		Step("chat-caught-up", 40f, delegate(BotStep st)
		{
			if (ask == 0 || Time.realtimeSinceStartup - askedAt > 2f)
			{
				ask = BotProbes.AskServer();
				askedAt = Time.realtimeSinceStartup;
			}
			Dictionary<string, string> dictionary = BotProbes.TakeReply(ask);
			if (dictionary == null)
			{
				return StepStatus.Running;
			}
			ask = 0;
			dictionary.TryGetValue("ui.chat.last", out value);
			string text = Chat.LastSeen.ToString(CultureInfo.InvariantCulture);
			st.Detail = "last line here #" + text + ", server #" + value;
			return (text == value) ? StepStatus.Pass : StepStatus.Running;
		});
	}
}
