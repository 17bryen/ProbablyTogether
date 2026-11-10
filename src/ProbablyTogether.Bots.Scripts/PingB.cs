using System.Globalization;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PingB : PingBotBase
{
	private long item;

	private string name = "";

	private int chat0;

	private int recv0;

	private int recA;

	private int lines0;

	private int loads0;

	public override string Name => "ping-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 840f;

	private int A => PingBotBase.Other();

	private void NoMarkSeen(string tag, string key)
	{
		S("saw-" + tag, 15f, delegate(BotStep st)
		{
			long num = long.Parse(Vars[key], CultureInfo.InvariantCulture);
			if (num == 0L)
			{
				st.Detail = "A had no " + tag + " item to point at";
				return StepStatus.Pass;
			}
			if (!PointMarks.Marks.TryGetValue(A, out var value) || value.ItemId != num)
			{
				st.Detail = "no marker from A for item " + num + " yet";
				return StepStatus.Running;
			}
			GameItem val = Ids.Find(num);
			Rect area;
			bool flag = val != null && PointMarks.ItemArea(val, out area);
			bool flag2 = val != null && PointMarks.OnThisScreen(val, out area, value.Spot);
			string text = PingBotBase.Who(A) + " points at " + Pointing.ItemName(num) + ".";
			string text2 = (flag2 ? "on this screen" : (flag ? "drawn here but covered" : "not drawn here"));
			st.Detail = "A's marker for item " + num + " (" + text2 + "): " + value.Describe() + "; pointing lines +" + (PingBotBase.PointLines() - chat0) + ", '" + text + "' x" + PingBotBase.Lines(text);
			return (val != null && value.Visible == flag2 && PingBotBase.PointLines() - chat0 == 1 && PingBotBase.Lines(text) >= 1) ? StepStatus.Pass : StepStatus.Running;
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
		Delay("settle", 2f);
		SetKey("tell-ready", "b-ready", () => "1");
		WaitKey("a-item", "a-item", 400f);
		Do("item", delegate
		{
			item = long.Parse(Vars["a-item"], CultureInfo.InvariantCulture);
			GameItem val = Ids.Find(item);
			name = Pointing.ItemName(item);
			return "A points at item " + item + ": here " + ((val == null) ? "NOT FOUND" : ("'" + name + "' (" + val.identifier + ")"));
		});
		S("saw-item", 30f, delegate(BotStep st)
		{
			if (!PointMarks.Marks.TryGetValue(A, out var value))
			{
				st.Detail = "no marker from player " + A;
				return StepStatus.Running;
			}
			GameItem val = Ids.Find(item);
			Rect area;
			bool flag = val != null && PointMarks.OnThisScreen(val, out area, value.Spot);
			st.Detail = "A's marker: item " + value.ItemId + ", " + value.Describe() + " (item on this screen " + flag + ")";
			return (value.ItemId == item && val != null && value.OnItem == flag && value.Visible == flag) ? StepStatus.Pass : StepStatus.Running;
		});
		S("chat-line", 5f, delegate(BotStep st)
		{
			string text = PingBotBase.Who(A) + " points at " + name + ".";
			st.Detail = "line '" + text + "' x" + PingBotBase.Lines(text);
			return (PingBotBase.Lines(text) == 1) ? StepStatus.Pass : StepStatus.Running;
		});
		RaycastCheck("on-item", () => A, () => item);
		Do("raycast-survey", () => PingBotBase.RaycastSurvey(out var _, out var _));
		Do("count", delegate
		{
			chat0 = PingBotBase.PointLines();
			return chat0 + " pointing line(s)";
		});
		SetKey("tell-saw", "b-saw", () => "1");
		S("saw-spot", 30f, delegate(BotStep st)
		{
			if (!PointMarks.Marks.TryGetValue(A, out var value))
			{
				st.Detail = "no marker";
				return StepStatus.Running;
			}
			st.Detail = value.Describe() + ", my view " + Presence.LocalView;
			Vector2 val = value.Anchor - PingBotBase.SpotAt;
			bool flag = val.sqrMagnitude < 4.0000004E-06f;
			return (value.ItemId == 0L && value.Visible && !value.OnItem && flag && value.Text == PingBotBase.Who(A) + " points here") ? StepStatus.Pass : StepStatus.Running;
		});
		Check("spot-no-chat", () => PingBotBase.PointLines() == chat0, () => "pointing lines before " + chat0 + ", now " + PingBotBase.PointLines());
		SetKey("tell-saw-spot", "b-saw-spot", () => "1");
		S("expired", 8f, delegate(BotStep st)
		{
			st.Detail = "markers " + PointMarks.Marks.Count + ", last expired: player " + PointMarks.LastExpiredPlayer + " at " + PingBotBase.F(PointMarks.LastExpiredAge) + " s";
			return (!PointMarks.Marks.ContainsKey(A)) ? StepStatus.Pass : StepStatus.Running;
		});
		Check("about-4s", () => PointMarks.LastExpiredPlayer == A && PointMarks.LastExpiredAge >= 3.9f && PointMarks.LastExpiredAge <= 4.8f, () => "A's spot marker lived " + PingBotBase.F(PointMarks.LastExpiredAge) + " s here");
		Do("count-hidden", delegate
		{
			chat0 = PingBotBase.PointLines();
			return chat0 + " pointing line(s)";
		});
		SetKey("tell-expired", "b-expired", () => "1");
		WaitKey("a-hidden", "a-hidden", 90f);
		NoMarkSeen("hidden", "a-hidden");
		Do("count-covered", delegate
		{
			chat0 = PingBotBase.PointLines();
			return chat0 + " pointing line(s)";
		});
		SetKey("tell-saw-hidden", "b-saw-hidden", () => "1");
		WaitKey("a-covered", "a-covered", 90f);
		NoMarkSeen("covered", "a-covered");
		Do("recv", delegate
		{
			recv0 = Pointing.ReceivedFrom(A);
			return recv0 + " ping(s) from A so far";
		});
		SetKey("tell-saw-covered", "b-saw-covered", () => "1");
		WaitKey("a-spam-done", "a-spam-done", 200f);
		Delay("spam-settle", 1f);
		Check("spam-seen", () => Pointing.ReceivedFrom(A) - recv0 == 15, () => "pings from A shown here since the covered item: +" + (Pointing.ReceivedFrom(A) - recv0) + " (want " + 15 + ": " + 4 + " of the raw burst of 8, 3 of A's own burst of 4, 4 honest, 4 behind the lag spike; the made-up id never)");
		Do("before-reload", delegate
		{
			recA = Pointing.ReceivedFrom(A);
			lines0 = PingBotBase.PointLines();
			loads0 = PingLab.Loads;
			return recA + " from A, " + lines0 + " pointing line(s)";
		});
		SetKey("tell-resyncing", "b-resyncing", () => "1");
		Do("resync", delegate
		{
			Snapshot.RequestResync();
			return "resync asked (the server sends the morning snapshot and today's log, like a mid-day join)";
		});
		WaitFor("reloaded", 150f, () => PingLab.Loads > loads0 && Net.IsLive && !Snapshot.IsLoading && Ui.ShopOnline, () => "loads " + PingLab.Loads + ", state " + Net.ClientState);
		WaitKey("a-during", "a-during", 90f);
		Delay("during-settle", 2f);
		Check("none-from-before", () => Pointing.ReceivedFrom(A) == recA && !PointMarks.Marks.ContainsKey(A) && PingBotBase.PointLines() == lines0, () => "from A " + recA + " -> " + Pointing.ReceivedFrom(A) + ", A's marker " + PointMarks.Marks.ContainsKey(A) + ", pointing lines " + lines0 + " -> " + PingBotBase.PointLines() + ", ignored while loading " + Pointing.IgnoredLoading + "; replay " + DayLog.Last);
		SetKey("tell-resynced", "b-resynced", () => "1");
		WaitKey("a-done", "a-done", 300f);
		SetKey("tell-done", "b-done", () => "1");
	}
}
