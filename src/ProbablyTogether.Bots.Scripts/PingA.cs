using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PingA : PingBotBase
{
	private const float LagSpike = 1f;

	private long item;

	private long hidden;

	private long covered;

	private string itemName = "";

	private string how = "";

	private string hiddenName = "";

	private string coveredName = "";

	private bool onScreen;

	private Vector2 at;

	private Vector2 coveredAt;

	private int ok0;

	private int no0;

	private int gone0;

	private int own0;

	private int nos0;

	private int sent0;

	private int lr0;

	private int rec0;

	private int lines0;

	private int loads0;

	private int chat0;

	private int shownExpected;

	private float t0;

	public override string Name => "ping-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 840f;

	private int Me => Net.LocalPlayer;

	private void NoMarkPing(string tag, Func<long> id, Func<string> name, Func<Vector2> point, string wantWhy)
	{
		WaitFor("token-" + tag, 5f, () => Pointing.LocalTokens >= 1.0, () => "tokens " + Pointing.LocalTokens.ToString("0.00", CultureInfo.InvariantCulture));
		Step("point-" + tag, 0f, delegate(BotStep st)
		{
			if (id() == 0L)
			{
				st.Detail = "no " + tag + " item in this copy: nothing to point at";
				return StepStatus.Pass;
			}
			lines0 = PingBotBase.PointLines();
			string text = Pointing.PointAt(id(), point());
			if (text == null)
			{
				shownExpected++;
			}
			st.Detail = ((text == null) ? ("sent: item " + id() + " '" + name() + "' at " + PingBotBase.F(point().x) + "," + PingBotBase.F(point().y)) : ("refused: " + text));
			return (text == null) ? StepStatus.Pass : StepStatus.Fail;
		});
		S("own-" + tag, 10f, delegate(BotStep st)
		{
			if (id() == 0L)
			{
				st.Detail = "skipped (no " + tag + " item)";
				return StepStatus.Pass;
			}
			if (!PointMarks.Marks.TryGetValue(Me, out var value) || value.ItemId != id())
			{
				st.Detail = "no marker for item " + id() + " yet";
				return StepStatus.Running;
			}
			string text = "You point at " + name() + ".";
			st.Detail = value.Describe() + "; pointing lines +" + (PingBotBase.PointLines() - lines0) + ", '" + text + "' x" + PingBotBase.Lines(text);
			return (!value.Visible && value.Why.Contains(wantWhy) && PingBotBase.PointLines() - lines0 == 1 && PingBotBase.Lines(text) >= 1) ? StepStatus.Pass : StepStatus.Running;
		});
	}

	protected override void Build()
	{
		Do("wire", delegate
		{
			PingLab.Ensure();
			return "ping lab channel wired";
		});
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("shop-live", 240f, () => Ui.ShopOnline, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		WaitKey("b-ready", "b-ready", 400f);
		WaitFor("other-live", 60f, () => PingBotBase.Other() > 0, () => Net.Players.Count + " player(s)");
		KeysStep("keys-free");
		Do("raycast-survey", () => PingBotBase.RaycastSurvey(out var num, out var _) + "; screen " + Screen.width + "x" + Screen.height + ", game area " + LetterboxManager.GameAreaWidth + "x" + LetterboxManager.GameAreaHeight);
		Do("pick", delegate
		{
			item = PingBotBase.PickItem(out how);
			GameItem val = Ids.Find(item);
			at = PingBotBase.CenterOf(item);
			onScreen = val != null && PointMarks.OnThisScreen(val, out var _, at);
			itemName = Pointing.ItemName(item);
			hidden = PingBotBase.PickHidden(out var text);
			hiddenName = ((hidden != 0L) ? Pointing.ItemName(hidden) : "");
			covered = PingBotBase.PickCovered(out var text2);
			coveredName = ((covered != 0L) ? Pointing.ItemName(covered) : "");
			coveredAt = (Vector2)((covered != 0L) ? PingBotBase.CenterOf(covered) : new Vector2(0.5f, 0.5f));
			return "item " + item + " '" + itemName + "' (" + (((val != null) ? val.identifier : null) ?? "?") + "), " + how + "; point at " + PingBotBase.F(at.x) + "," + PingBotBase.F(at.y) + "; undrawn item " + hidden + " '" + hiddenName + "' (" + text + "); covered item " + covered + " '" + coveredName + "' (" + text2 + ")";
		});
		SetKey("tell-item", "a-item", () => item.ToString(CultureInfo.InvariantCulture));
		Delay("settle", 1f);
		Step("point-item", 0f, delegate(BotStep st)
		{
			string text = Pointing.PointAt(item, at);
			if (text == null)
			{
				shownExpected++;
			}
			st.Detail = ((text == null) ? "sent (the point key G does the same)" : ("refused: " + text));
			return (text == null) ? StepStatus.Pass : StepStatus.Fail;
		});
		string value;
		S("own-mark", 10f, delegate(BotStep st)
		{
			if (!PointMarks.Marks.TryGetValue(Me, out var mark))
			{
				st.Detail = "no marker yet";
				return StepStatus.Running;
			}
			st.Detail = "my marker: item " + mark.ItemId + ", " + mark.Describe() + " (on this screen " + onScreen + ")";
			return (mark.ItemId == item && mark.OnItem == onScreen && mark.Visible == onScreen) ? StepStatus.Pass : StepStatus.Running;
		});
		S("own-chat", 5f, delegate(BotStep st)
		{
			string text = "You point at " + itemName + ".";
			st.Detail = "line '" + text + "' x" + PingBotBase.Lines(text);
			return (PingBotBase.Lines(text) == 1) ? StepStatus.Pass : StepStatus.Running;
		});
		RaycastCheck("on-item", () => Me, () => item);
		WaitKey("b-saw", "b-saw", 60f);
		WaitFor("token", 5f, () => Pointing.LocalTokens >= 1.0, () => "tokens " + Pointing.LocalTokens.ToString("0.00", CultureInfo.InvariantCulture));
		Step("point-spot", 0f, delegate(BotStep st)
		{
			chat0 = PingBotBase.PointLines();
			string text = Pointing.PointAt(0L, PingBotBase.SpotAt);
			if (text == null)
			{
				shownExpected++;
			}
			st.Detail = ((text == null) ? ("sent: spot " + PingBotBase.F(PingBotBase.SpotAt.x) + "," + PingBotBase.F(PingBotBase.SpotAt.y) + " in view " + Presence.LocalView) : ("refused: " + text));
			return (text == null) ? StepStatus.Pass : StepStatus.Fail;
		});
		S("own-spot", 10f, delegate(BotStep st)
		{
			if (!PointMarks.Marks.TryGetValue(Me, out var mark2))
			{
				st.Detail = "no marker";
				return StepStatus.Running;
			}
			st.Detail = mark2.Describe();
			Vector2 val = mark2.Anchor - PingBotBase.SpotAt;
			bool flag = val.sqrMagnitude < 4.0000004E-06f;
			return (mark2.ItemId == 0L && mark2.Visible && !mark2.OnItem && flag && mark2.Text == PingBotBase.Who(Me) + " points here") ? StepStatus.Pass : StepStatus.Running;
		});
		Check("spot-no-chat", () => PingBotBase.PointLines() == chat0, () => "pointing lines before " + chat0 + ", now " + PingBotBase.PointLines());
		WaitKey("b-saw-spot", "b-saw-spot", 60f);
		S("expired", 8f, delegate(BotStep st)
		{
			st.Detail = "markers " + PointMarks.Marks.Count + ", last expired: player " + PointMarks.LastExpiredPlayer + " at " + PingBotBase.F(PointMarks.LastExpiredAge) + " s";
			return (!PointMarks.Marks.ContainsKey(Me)) ? StepStatus.Pass : StepStatus.Running;
		});
		Check("about-4s", () => PointMarks.LastExpiredPlayer == Me && PointMarks.LastExpiredAge >= 3.9f && PointMarks.LastExpiredAge <= 4.8f, () => "my spot marker lived " + PingBotBase.F(PointMarks.LastExpiredAge) + " s");
		WaitKey("b-expired", "b-expired", 60f);
		NoMarkPing("hidden", () => hidden, () => hiddenName, () => new Vector2(0.5f, 0.5f), "not drawn");
		SetKey("tell-hidden", "a-hidden", () => hidden.ToString(CultureInfo.InvariantCulture));
		WaitKey("b-saw-hidden", "b-saw-hidden", 60f);
		NoMarkPing("covered", () => covered, () => coveredName, () => coveredAt, "covered");
		SetKey("tell-covered", "a-covered", () => covered.ToString(CultureInfo.InvariantCulture));
		WaitKey("b-saw-covered", "b-saw-covered", 60f);
		Delay("refill", 6.5f);
		AskServer("ask-before");
		Do("burst", delegate
		{
			ok0 = PingBotBase.I(base.Server, "ping.ok");
			no0 = PingBotBase.I(base.Server, "ping.no");
			own0 = Pointing.ReceivedFrom(Me);
			nos0 = Pointing.RefusedByServer;
			for (int i = 0; i < 8; i++)
			{
				Net.ToServer("ping.at", new MsgPingAt
				{
					id = item,
					x = at.x,
					y = at.y,
					v = Presence.LocalView
				});
			}
			shownExpected += 4;
			return "8 ping.at in one frame (raw, past the client's limiter); server shown " + ok0 + ", refused " + no0 + " before";
		});
		Delay("burst-settle", 3f);
		AskServer("ask-after");
		Check("server-limit", () => PingBotBase.I(base.Server, "ping.ok") - ok0 == 4 && PingBotBase.I(base.Server, "ping.no") - no0 == 4, () => "server shown +" + (PingBotBase.I(base.Server, "ping.ok") - ok0) + " (want " + 4 + "), refused +" + (PingBotBase.I(base.Server, "ping.no") - no0) + " (want " + 4 + ")");
		Check("burst-shown", () => Pointing.ReceivedFrom(Me) - own0 == 4, () => "my own pings shown here +" + (Pointing.ReceivedFrom(Me) - own0));
		Check("burst-answer", () => Pointing.RefusedByServer - nos0 == 1 && Pointing.LastNo == "You are pointing too fast. Wait a moment.", () => "ping.no received +" + (Pointing.RefusedByServer - nos0) + " (one per 2 s), last '" + Pointing.LastNo + "'");
		Delay("refill-2", 5f);
		AskServer("ask-before-2");
		Step("local-burst", 0f, delegate(BotStep st)
		{
			ok0 = PingBotBase.I(base.Server, "ping.ok");
			no0 = PingBotBase.I(base.Server, "ping.no");
			sent0 = Pointing.Sent;
			lr0 = Pointing.LocalRefused;
			List<string> list = new List<string>();
			for (int i = 0; i < 4; i++)
			{
				list.Add(Pointing.PointAt(item, at) ?? "sent");
			}
			shownExpected += list.Count((string x) => x == "sent");
			st.Detail = "4 PointAt in one frame: " + string.Join(", ", list);
			return (list[0] == "sent" && list[1] == "sent" && list[2] == "sent" && list[3] == "too fast") ? StepStatus.Pass : StepStatus.Fail;
		});
		Delay("local-settle", 3f);
		AskServer("ask-after-2");
		Check("local-limit", () => Pointing.Sent - sent0 == 3 && Pointing.LocalRefused - lr0 == 1 && PingBotBase.I(base.Server, "ping.ok") - ok0 == 3 && PingBotBase.I(base.Server, "ping.no") - no0 == 0 && PingBotBase.Lines("Wait a moment before pointing again.") >= 1, () => "sent +" + (Pointing.Sent - sent0) + ", refused here +" + (Pointing.LocalRefused - lr0) + ", server shown +" + (PingBotBase.I(base.Server, "ping.ok") - ok0) + " refused +" + (PingBotBase.I(base.Server, "ping.no") - no0) + ", line 'Wait a moment before pointing again.' x" + PingBotBase.Lines("Wait a moment before pointing again."));
		WaitFor("client-full", 10f, () => Pointing.LocalTokens >= 2.999, () => "tokens " + Pointing.LocalTokens.ToString("0.00", CultureInfo.InvariantCulture));
		Delay("server-full", 3f);
		AskServer("ask-before-4");
		Step("honest-burst", 0f, delegate(BotStep st)
		{
			ok0 = PingBotBase.I(base.Server, "ping.ok");
			no0 = PingBotBase.I(base.Server, "ping.no");
			sent0 = Pointing.Sent;
			List<string> list = new List<string>();
			for (int i = 0; i < 3; i++)
			{
				list.Add(Pointing.PointAt(item, at) ?? "sent");
			}
			t0 = Time.realtimeSinceStartup;
			shownExpected += list.Count((string x) => x == "sent");
			st.Detail = "3 PointAt in one frame: " + string.Join(", ", list) + "; tokens left " + Pointing.LocalTokens.ToString("0.00", CultureInfo.InvariantCulture);
			return list.All((string x) => x == "sent") ? StepStatus.Pass : StepStatus.Fail;
		});
		WaitFor("client-refill", 5f, () => Pointing.LocalTokens >= 1.0, () => "tokens " + Pointing.LocalTokens.ToString("0.00", CultureInfo.InvariantCulture));
		Step("honest-4th", 0f, delegate(BotStep st)
		{
			float s = Time.realtimeSinceStartup - t0;
			string text = Pointing.PointAt(item, at);
			if (text == null)
			{
				shownExpected++;
			}
			st.Detail = ((text == null) ? "sent" : ("refused: " + text)) + " " + PingBotBase.F(s) + " s after the burst, the moment this client's limiter refilled (1 per " + PingBotBase.F(1.6f) + " s)";
			return (text == null) ? StepStatus.Pass : StepStatus.Fail;
		});
		Delay("honest-settle", 3f);
		AskServer("ask-after-4");
		Check("honest-none-refused", () => Pointing.Sent - sent0 == 4 && PingBotBase.I(base.Server, "ping.ok") - ok0 == 4 && PingBotBase.I(base.Server, "ping.no") - no0 == 0, () => "sent +" + (Pointing.Sent - sent0) + ", server shown +" + (PingBotBase.I(base.Server, "ping.ok") - ok0) + " (want 4), refused +" + (PingBotBase.I(base.Server, "ping.no") - no0) + " (want 0)");
		Delay("lag-refill", 6.5f);
		AskServer("ask-before-5");
		Do("lag-burst", delegate
		{
			ok0 = PingBotBase.I(base.Server, "ping.ok");
			no0 = PingBotBase.I(base.Server, "ping.no");
			for (int i = 0; i < 3; i++)
			{
				Net.ToServer("ping.at", new MsgPingAt
				{
					id = item,
					x = at.x,
					y = at.y,
					v = Presence.LocalView
				});
			}
			t0 = Time.realtimeSinceStartup;
			shownExpected += 3;
			return "3 ping.at as raw frames in one frame: a burst the client allowed, arriving " + PingBotBase.F(1f) + " s late";
		});
		Step("lag-4th", 5f, delegate(BotStep st)
		{
			float num = Time.realtimeSinceStartup - t0;
			if (num < 0.6f)
			{
				return StepStatus.Running;
			}
			Net.ToServer("ping.at", new MsgPingAt
			{
				id = item,
				x = at.x,
				y = at.y,
				v = Presence.LocalView
			});
			shownExpected++;
			st.Detail = "the 4th (sent when the client's limiter refilled, arriving on time) " + PingBotBase.F(num) + " s after the late burst; equal buckets of 3 would hold " + (num / 1.5f).ToString("0.00", CultureInfo.InvariantCulture) + " token(s) then (refused), this server holds " + (1f + num / 1.5f).ToString("0.00", CultureInfo.InvariantCulture);
			return StepStatus.Pass;
		});
		Delay("lag-settle", 3f);
		AskServer("ask-after-5");
		Check("lag-none-refused", () => PingBotBase.I(base.Server, "ping.ok") - ok0 == 4 && PingBotBase.I(base.Server, "ping.no") - no0 == 0, () => "server shown +" + (PingBotBase.I(base.Server, "ping.ok") - ok0) + " (want 4), refused +" + (PingBotBase.I(base.Server, "ping.no") - no0) + " (want 0)");
		Delay("refill-3", 2.5f);
		AskServer("ask-before-3");
		Do("bad-id", delegate
		{
			ok0 = PingBotBase.I(base.Server, "ping.ok");
			gone0 = PingBotBase.I(base.Server, "ping.nogone");
			nos0 = Pointing.RefusedByServer;
			Net.ToServer("ping.at", new MsgPingAt
			{
				id = 987654321L,
				x = 0.5f,
				y = 0.5f,
				v = "store"
			});
			return "ping.at for item 987654321 (no such item)";
		});
		S("bad-answer", 6f, delegate(BotStep st)
		{
			st.Detail = "ping.no +" + (Pointing.RefusedByServer - nos0) + ", last '" + Pointing.LastNo + "'";
			return (Pointing.RefusedByServer > nos0 && Pointing.LastNo == "That item is not in the shop any more.") ? StepStatus.Pass : StepStatus.Running;
		});
		AskServer("ask-after-3");
		Check("bad-refused", () => PingBotBase.I(base.Server, "ping.ok") == ok0 && PingBotBase.I(base.Server, "ping.nogone") - gone0 == 1, () => "server shown +" + (PingBotBase.I(base.Server, "ping.ok") - ok0) + " (want 0), refused gone +" + (PingBotBase.I(base.Server, "ping.nogone") - gone0) + " (want 1)");
		SetKey("tell-spam-done", "a-spam-done", () => "1");
		WaitKey("b-resyncing", "b-resyncing", 120f);
		int askId = 0;
		float askAt = -10f;
		S("b-loading", 40f, delegate(BotStep st)
		{
			if (askId > 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(askId);
				if (dictionary != null)
				{
					askId = 0;
					string text = (dictionary.TryGetValue("ping.loading", out value) ? value : "");
					st.Detail = "server: loading players '" + text + "'";
					if (text.Split(',').Contains(PingBotBase.Other().ToString(CultureInfo.InvariantCulture)))
					{
						return StepStatus.Pass;
					}
				}
			}
			if (askId == 0 && st.Elapsed - askAt >= 0.3f)
			{
				askAt = st.Elapsed;
				askId = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		WaitFor("token-2", 5f, () => Pointing.LocalTokens >= 1.0, () => "tokens " + Pointing.LocalTokens.ToString("0.00", CultureInfo.InvariantCulture));
		Step("ping-during", 0f, delegate(BotStep st)
		{
			string text = Pointing.PointAt(item, at);
			if (text == null)
			{
				shownExpected++;
			}
			st.Detail = ((text == null) ? ("sent while player " + PingBotBase.Other() + " loads") : ("refused: " + text));
			return (text == null) ? StepStatus.Pass : StepStatus.Fail;
		});
		Delay("during-settle", 1f);
		AskServer("ask-during");
		Check("sent-to-live-only", () => base.Server.TryGetValue("ping.lastto", out value) && value == Me.ToString(CultureInfo.InvariantCulture), () => "the server sent the last ping to players '" + (base.Server.TryGetValue("ping.lastto", out value) ? value : "?") + "' (me " + Me + ", loading " + PingBotBase.Other() + ")");
		SetKey("tell-during", "a-during", () => "1");
		WaitKey("b-resynced", "b-resynced", 200f);
		Do("resync", delegate
		{
			rec0 = Pointing.Received;
			lines0 = PingBotBase.PointLines();
			loads0 = PingLab.Loads;
			Snapshot.RequestResync();
			return "resync asked; " + rec0 + " ping(s) taken so far, " + lines0 + " pointing line(s), " + PointMarks.Marks.Count + " marker(s)";
		});
		WaitFor("reloaded", 150f, () => PingLab.Loads > loads0 && Net.IsLive && !Snapshot.IsLoading && Ui.ShopOnline, () => "loads " + PingLab.Loads + ", state " + Net.ClientState);
		Delay("reload-settle", 2f);
		Check("no-replay", () => Pointing.Received == rec0 && PointMarks.Marks.Count == 0 && PingBotBase.PointLines() == lines0, () => "pings taken " + rec0 + " -> " + Pointing.Received + ", markers " + PointMarks.Marks.Count + ", pointing lines " + lines0 + " -> " + PingBotBase.PointLines() + ", ignored while loading " + Pointing.IgnoredLoading + "; replay " + DayLog.Last);
		AskServer("ask-final");
		Check("daylog", () => base.Server.TryGetValue("ping.daylog", out value) && value == "0", () => "ping.* messages in the server's day log: " + (base.Server.TryGetValue("ping.daylog", out value) ? value : "?"));
		SetKey("tell-expect", "a-expect", () => shownExpected.ToString(CultureInfo.InvariantCulture));
		SetKey("tell-done", "a-done", () => "1");
	}
}
