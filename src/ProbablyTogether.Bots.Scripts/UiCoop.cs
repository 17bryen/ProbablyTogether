using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class UiCoop : UiCoopBase
{
	private const long Wild = 8376L;

	private int refusedBefore;

	private int noticesBefore;

	private string path = "";

	private const string LeaderVotedLine = "lab: leader voted for a new run";

	public override string Name => "ui-coop";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		Do("wire", delegate
		{
			DialogueBotNet.Ensure();
			return "dialogue lab channel wired";
		});
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("shop-live", 180f, () => Ui.ShopOnline && DialogueSystem.Phone.Ready, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene + ", phone ready " + DialogueSystem.Phone.Ready);
		WaitFor("two-players", 400f, () => Net.Players.Count == 2 && Presence.RosterTexts.Count == 2, () => Net.Players.Count + " player(s)");
		Delay("settle", 3f);
		Check("player-live", () => Net.Players.Count == 2 && Net.IsPlayerLive(Net.LocalPlayer) && Net.IsPlayerLive(UiCoopBase.Other()), () => Net.Players.Count + " player(s), me " + Net.IsPlayerLive(Net.LocalPlayer) + ", other " + Net.IsPlayerLive(UiCoopBase.Other()));
		Check("describe-order", delegate
		{
			Presence.DescribeLock("outing.lab-exact", (string _) => "is at the lab stall");
			return Presence.ActivityFor("outing.lab-exact") == "is at the lab stall" && Presence.ActivityFor("outing.other") == "is busy out there" && Presence.ActivityFor("machine:12") != null && Presence.ActivityFor("nobody.described") == null;
		}, () => "outing.lab-exact '" + Presence.ActivityFor("outing.lab-exact") + "', outing.other '" + Presence.ActivityFor("outing.other") + "', machine:12 '" + Presence.ActivityFor("machine:12") + "', undescribed '" + Presence.ActivityFor("nobody.described") + "'");
		Do("roles", delegate
		{
			UiCoopBase.leaderId = Net.Players.Keys.Min();
			return (UiCoopBase.IAmLeader ? "leader" : "watcher") + "; leader " + Net.PlayerName(UiCoopBase.Leader()) + " (" + UiCoopBase.Leader() + ")";
		});
		Phone();
		Aim();
		Cjk();
		NewRunVote();
	}

	private void Phone()
	{
		Step("phone", 0f, delegate(BotStep st)
		{
			st.Detail = (UiCoopBase.IAmLeader ? "I call Wild" : "I watch the leader's call");
			return StepStatus.Pass;
		});
		WaitFor("p-line-free", 60f, () => !UiCoopBase.IAmLeader || (Locks.HeldBy("phone") < 0 && !PhoneChannel.Talking), () => "phone held by " + Locks.HeldBy("phone"));
		WaitMarkIf("p-watcher-ready", "ui3-phone-watch", 120f, leader: true);
		CommandIf("p-open-line", "phone-open:" + 8376L, (string r) => r.StartsWith("ok", StringComparison.Ordinal), leader: true);
		S("p-call", 20f, delegate(BotStep st)
		{
			if (!UiCoopBase.IAmLeader)
			{
				st.Detail = "skipped (watcher)";
				return StepStatus.Pass;
			}
			if (st.Elapsed < 0.05f && !DialogueSync.RequestCall(8376L))
			{
				st.Detail = "the call was not sent (local gate)";
				return StepStatus.Fail;
			}
			bool flag = Locks.HeldBy("phone") == Net.LocalPlayer && PhoneChannel.Talking;
			st.Detail = "phone lock " + Locks.HeldBy("phone") + ", talking " + PhoneChannel.Talking + ", my roster row '" + UiCoopBase.Row(Net.LocalPlayer) + "'";
			return flag ? StepStatus.Pass : StepStatus.Running;
		});
		WaitMarkIf("p-watcher-saw", "ui3-phone-seen", 90f, leader: true);
		S("p-hangup", 30f, delegate(BotStep st)
		{
			if (!UiCoopBase.IAmLeader)
			{
				st.Detail = "skipped (watcher)";
				return StepStatus.Pass;
			}
			if (st.Elapsed < 0.05f && !DialogueSync.RequestHangup())
			{
				st.Detail = "hang-up not sent";
				return StepStatus.Fail;
			}
			st.Detail = "phone lock " + Locks.HeldBy("phone") + ", talking " + PhoneChannel.Talking;
			return (Locks.HeldBy("phone") < 0) ? StepStatus.Pass : StepStatus.Running;
		});
		MarkIf("p-ready", "ui3-phone-watch", leader: false);
		S("p-seen", 90f, delegate(BotStep st)
		{
			if (UiCoopBase.IAmLeader)
			{
				st.Detail = "skipped (leader)";
				return StepStatus.Pass;
			}
			int num = Locks.HeldBy("phone");
			bool flag = UiCoopBase.Row(UiCoopBase.Leader()).Contains("| on the phone");
			st.Detail = "phone lock " + num + ", leader row '" + UiCoopBase.Row(UiCoopBase.Leader()) + "', labels " + UiCoopBase.Labels();
			return (num == UiCoopBase.Leader() && flag) ? StepStatus.Pass : StepStatus.Running;
		});
		Step("p-label", 5f, delegate(BotStep st)
		{
			if (UiCoopBase.IAmLeader)
			{
				st.Detail = "skipped (leader)";
				return StepStatus.Pass;
			}
			string text = Net.PlayerName(UiCoopBase.Leader()) + " is on the phone";
			string value;
			bool flag = Presence.LockLabelTexts.TryGetValue("phone", out value) && value == text;
			st.Detail = (flag ? ("label '" + value + "' at the phone") : ("no label at the phone (phone object not placed on screen: roster only): " + UiCoopBase.Labels()));
			return flag ? StepStatus.Pass : StepStatus.Running;
		}).Soft();
		Do("p-note", delegate
		{
			if (UiCoopBase.IAmLeader)
			{
				return "skipped (leader)";
			}
			refusedBefore = DialogueSync.RefusedCount;
			noticesBefore = Notices.Shown;
			Net.ToServer("phone.call", new MsgPhoneCall
			{
				number = 8376L
			});
			return "my own call sent around the local gate (refused so far " + refusedBefore + ", notices " + noticesBefore + ")";
		});
		S("p-refused-notice", 15f, delegate(BotStep st)
		{
			if (UiCoopBase.IAmLeader)
			{
				st.Detail = "skipped (leader)";
				return StepStatus.Pass;
			}
			string text = DialogueSync.LastRefused ?? "";
			int num = text.IndexOf(": ", StringComparison.Ordinal);
			string text2 = ((num >= 0) ? text.Substring(num + 2) : text);
			st.Detail = "dialogue refused " + DialogueSync.RefusedCount + " ('" + text + "'), notice '" + Notices.Last + "' from " + Notices.LastSource + " (shown " + Notices.Shown + ")";
			return (DialogueSync.RefusedCount > refusedBefore && Notices.Shown > noticesBefore && text2.Length > 0 && Notices.Last == text2 && Notices.LastSource.StartsWith("dialogue", StringComparison.Ordinal)) ? StepStatus.Pass : StepStatus.Running;
		});
		MarkIf("p-saw", "ui3-phone-seen", leader: false);
		WaitFor("p-gone", 60f, () => Locks.HeldBy("phone") < 0 && !Presence.LockLabelTexts.ContainsKey("phone") && !UiCoopBase.Row(UiCoopBase.Leader()).Contains("on the phone"), () => "phone lock " + Locks.HeldBy("phone") + ", leader row '" + UiCoopBase.Row(UiCoopBase.Leader()) + "', labels " + UiCoopBase.Labels());
	}

	private void Aim()
	{
		MarkIf("a-ready", "ui3-aim-watch", leader: false);
		WaitMarkIf("a-watcher-ready", "ui3-aim-watch", 120f, leader: true);
		Do("a-raise", delegate
		{
			if (!UiCoopBase.IAmLeader)
			{
				return "skipped (watcher)";
			}
			Net.ToServer("sec.aim", new MsgSecAim
			{
				on = true
			});
			return "sec.aim on sent (what entering the shooting view sends)";
		});
		S("a-seen", 30f, delegate(BotStep st)
		{
			if (UiCoopBase.IAmLeader)
			{
				st.Detail = "skipped (leader)";
				return StepStatus.Pass;
			}
			bool flag = UiCoopBase.Row(UiCoopBase.Leader()).Contains("| aiming the shotgun");
			st.Detail = "Security.AimingPlayer " + Security.AimingPlayer + ", leader row '" + UiCoopBase.Row(UiCoopBase.Leader()) + "', labels " + UiCoopBase.Labels();
			return (Presence.Aiming == UiCoopBase.Leader() && flag) ? StepStatus.Pass : StepStatus.Running;
		});
		Step("a-label", 5f, delegate(BotStep st)
		{
			if (UiCoopBase.IAmLeader)
			{
				st.Detail = "skipped (leader)";
				return StepStatus.Pass;
			}
			string want = Net.PlayerName(UiCoopBase.Leader()) + " is aiming the shotgun";
			bool flag = Presence.LockLabelTexts.Values.Any((string t) => t == want);
			st.Detail = (flag ? ("label: " + UiCoopBase.Labels()) : ("no label at the counter (counter not placed on screen: roster only): " + UiCoopBase.Labels()));
			return flag ? StepStatus.Pass : StepStatus.Running;
		}).Soft();
		MarkIf("a-saw", "ui3-aim-seen", leader: false);
		WaitMarkIf("a-watcher-saw", "ui3-aim-seen", 60f, leader: true);
		Check("a-own-none", () => !UiCoopBase.IAmLeader || !Presence.LockLabelTexts.Values.Any((string t) => t.Contains("aiming")), () => "my own aim draws no label here: " + UiCoopBase.Labels());
		Do("a-lower", delegate
		{
			if (!UiCoopBase.IAmLeader)
			{
				return "skipped (watcher)";
			}
			Net.ToServer("sec.aim", new MsgSecAim
			{
				on = false
			});
			return "sec.aim off sent";
		});
		WaitFor("a-gone", 30f, () => Presence.Aiming != UiCoopBase.Leader() && !UiCoopBase.Row(UiCoopBase.Leader()).Contains("aiming") && !Presence.LockLabelTexts.Values.Any((string t) => t.Contains("aiming")), () => "Security.AimingPlayer " + Security.AimingPlayer + ", leader row '" + UiCoopBase.Row(UiCoopBase.Leader()) + "', labels " + UiCoopBase.Labels());
	}

	private void Cjk()
	{
		string text = "王小明 さくら 김민수 from " + Net.PlayerName(Net.LocalPlayer);
		Do("c-fonts", delegate
		{
			TMP_FontAsset font = Kit.Font;
			TMP_FontAsset val = Kit.FontFor("王小明");
			TMP_FontAsset val2 = Kit.FontFor("さくら");
			TMP_FontAsset val3 = Kit.FontFor("김민수");
			return "game font " + (((Object)(object)font != (Object)null) ? ((Object)font).name : "none") + "; Chinese name -> " + (((Object)(object)val != (Object)null) ? ((Object)val).name : "none") + ", Japanese -> " + (((Object)(object)val2 != (Object)null) ? ((Object)val2).name : "none") + ", Korean -> " + (((Object)(object)val3 != (Object)null) ? ((Object)val3).name : "none") + "; CJK switches " + Kit.CjkSwitches;
		});
		Do("c-send", delegate
		{
			if (!Chat.Send(text))
			{
				throw new Exception("Send returned false");
			}
			return "sent a chat line with Chinese, Japanese and Korean names";
		});
		S("c-line", 20f, delegate(BotStep st)
		{
			Chat.Line line = Chat.Lines.LastOrDefault((Chat.Line x) => x.Player == Net.LocalPlayer && x.Text == text);
			if (line == null || (Object)(object)line.Label == (Object)null)
			{
				st.Detail = "line not back yet";
				return StepStatus.Running;
			}
			TMP_FontAsset font = ((TMP_Text)line.Label).font;
			List<char> list = (from c in Kit.Plain(((TMP_Text)line.Label).text)
				where c > ' ' && !char.IsSurrogate(c) && ((Object)(object)font == (Object)null || !font.HasCharacter(c, true, false))
				select c).Distinct().ToList();
			st.Detail = "line font " + (((Object)(object)font != (Object)null) ? ((Object)font).name : "none") + ", missing " + ((list.Count == 0) ? "none" : new string(list.ToArray()));
			return (list.Count == 0) ? StepStatus.Pass : StepStatus.Fail;
		});
	}

	private void NewRunVote()
	{
		Check("n-hidden", () => !NewRun.Visible, () => "plate visible " + NewRun.Visible + " while the run goes on");
		MarkIf("n-ready", "ui3-newrun-ready", leader: false);
		WaitMarkIf("n-watcher-ready", "ui3-newrun-ready", 120f, leader: true);
		Do("n-ending", delegate
		{
			if (!UiCoopBase.IAmLeader)
			{
				return "skipped (watcher)";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "gameover:rent"
			});
			return "asked day-night for a lab losing ending (gameover:rent)";
		});
		WaitFor("n-plate", 60f, () => DayNightRun.RunOver && NewRun.Visible && (Object)(object)Overlay.NewRunArea != (Object)null && ((Component)Overlay.NewRunArea).gameObject.activeInHierarchy, () => "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), plate " + NewRun.Visible + ", ending panel " + ((Object)(object)GameOverUIManager.Instance != (Object)null && GameOverUIManager.Instance.IsShowing));
		Delay("n-draw", 1f);
		Check("n-button", delegate
		{
			GameObject val = ButtonGo();
			Button val2 = (((Object)(object)val != (Object)null) ? val.GetComponent<Button>() : null);
			return (Object)(object)val2 != (Object)null && val.activeInHierarchy && ((Selectable)val2).interactable && NewRun.Text.EndsWith("| Start a new run", StringComparison.Ordinal);
		}, () => "button " + ((Object)(object)ButtonGo() != (Object)null) + ", plate '" + NewRun.Text + "', votes from day-night " + NewRun.VotesKnown + ", ready bar " + ReadyBar.Visible);
		Check("n-ready-hidden", () => !ReadyBar.Visible, () => "ready bar visible " + ReadyBar.Visible);
		Do("n-mode", () => (!NewRun.VotesKnown) ? "no vote published: expecting day-night's refusal" : ("day-night publishes the vote (take back " + NewRun.CanTakeBack + ")"));
		S("n-press-1", 20f, delegate(BotStep st)
		{
			if (NewRun.VotesKnown && !UiCoopBase.IAmLeader)
			{
				st.Detail = "skipped (watcher votes after the leader)";
				return StepStatus.Pass;
			}
			if (st.Elapsed < 0.05f)
			{
				Click();
			}
			if (!NewRun.VotesKnown)
			{
				st.Detail = "pressed (asked " + NewRun.LocalAsked + ")";
				return StepStatus.Pass;
			}
			List<int> list = NewRun.Votes();
			st.Detail = "votes [" + string.Join(",", list) + "], plate '" + NewRun.Text + "'";
			return (list.Contains(Net.LocalPlayer) && NewRun.Text.StartsWith("Start a new run: 1/2 voted", StringComparison.Ordinal) && NewRun.Text.EndsWith(NewRun.CanTakeBack ? "| Take my vote back" : "| Voted", StringComparison.Ordinal)) ? StepStatus.Pass : StepStatus.Running;
		});
		S("n-take-back", 20f, delegate(BotStep st)
		{
			if (!NewRun.VotesKnown || !UiCoopBase.IAmLeader || !NewRun.CanTakeBack)
			{
				st.Detail = "skipped";
				return StepStatus.Pass;
			}
			if (st.Elapsed < 0.05f)
			{
				Click();
			}
			List<int> list = NewRun.Votes();
			st.Detail = "votes [" + string.Join(",", list) + "], plate '" + NewRun.Text + "'";
			return (!list.Contains(Net.LocalPlayer) && NewRun.Text.EndsWith("| Start a new run", StringComparison.Ordinal)) ? StepStatus.Pass : StepStatus.Running;
		});
		S("n-press-again", 20f, delegate(BotStep st)
		{
			if (!NewRun.VotesKnown || !UiCoopBase.IAmLeader || !NewRun.CanTakeBack)
			{
				st.Detail = "skipped";
				return StepStatus.Pass;
			}
			if (st.Elapsed < 0.05f)
			{
				Click();
			}
			List<int> list = NewRun.Votes();
			st.Detail = "votes [" + string.Join(",", list) + "], plate '" + NewRun.Text + "'";
			return list.Contains(Net.LocalPlayer) ? StepStatus.Pass : StepStatus.Running;
		});
		Step("n-leader-voted", 15f, delegate(BotStep st)
		{
			if (!NewRun.VotesKnown || !UiCoopBase.IAmLeader)
			{
				st.Detail = "skipped";
				return StepStatus.Pass;
			}
			if (st.Elapsed < 0.05f)
			{
				Chat.Send("lab: leader voted for a new run");
			}
			bool flag = Chat.Lines.Any((Chat.Line l) => l.Player == Net.LocalPlayer && l.Text == "lab: leader voted for a new run");
			st.Detail = "chat line 'lab: leader voted for a new run' " + (flag ? "posted" : "sent");
			return flag ? StepStatus.Pass : StepStatus.Running;
		});
		WaitFor("n-wait-leader", 60f, () => !NewRun.VotesKnown || UiCoopBase.IAmLeader || Chat.Lines.Any((Chat.Line l) => l.Player == UiCoopBase.Leader() && l.Text == "lab: leader voted for a new run"), () => "waiting for the leader's chat line; plate '" + NewRun.Text + "'");
		S("n-watcher-sees", 20f, delegate(BotStep st)
		{
			if (!NewRun.VotesKnown || UiCoopBase.IAmLeader)
			{
				st.Detail = "skipped";
				return StepStatus.Pass;
			}
			string value = Net.PlayerName(UiCoopBase.Leader()) + " voted";
			st.Detail = "plate '" + NewRun.Text + "'";
			return (NewRun.Text.StartsWith("Start a new run: 1/2 voted", StringComparison.Ordinal) && NewRun.Text.Contains(value) && NewRun.Text.EndsWith("| Start a new run", StringComparison.Ordinal)) ? StepStatus.Pass : StepStatus.Running;
		});
		S("n-press-2", 20f, delegate(BotStep st)
		{
			if (!NewRun.VotesKnown || UiCoopBase.IAmLeader)
			{
				st.Detail = "skipped";
				return StepStatus.Pass;
			}
			if (st.Elapsed < 0.05f)
			{
				Click();
			}
			st.Detail = "votes [" + string.Join(",", NewRun.Votes()) + "], starting " + NewRun.Starting + ", run over " + DayNightRun.RunOver + ", plate '" + NewRun.Text + "'";
			return (NewRun.Votes().Contains(Net.LocalPlayer) || NewRun.Starting || !DayNightRun.RunOver) ? StepStatus.Pass : StepStatus.Running;
		});
		S("n-outcome", 60f, delegate(BotStep st)
		{
			if (NewRun.VotesKnown)
			{
				path = "votes";
				st.Detail = "votes path";
				return StepStatus.Pass;
			}
			if (NewRun.LastRefusal.Length > 0)
			{
				path = "refused";
				st.Detail = "day-night refused: '" + NewRun.LastRefusal + "'; plate '" + NewRun.Text + "'";
				if (!NewRun.Text.Contains(NewRun.LastRefusal) || !NewRun.Text.EndsWith("| Start a new run", StringComparison.Ordinal))
				{
					return StepStatus.Running;
				}
				return StepStatus.Pass;
			}
			st.Detail = "waiting for day-night's answer; plate '" + NewRun.Text + "'";
			return StepStatus.Running;
		});
		S("n-after", 150f, delegate(BotStep st)
		{
			if (path == "refused")
			{
				st.Detail = "run still over, plate still up: " + NewRun.Visible + " (no vote counting in this day-night)";
				if (!NewRun.Visible)
				{
					return StepStatus.Fail;
				}
				return StepStatus.Pass;
			}
			bool flag = !DayNightRun.RunOver && Ui.ShopOnline && !NewRun.Visible;
			st.Detail = "run over " + DayNightRun.RunOver + ", starting " + NewRun.Starting + ", plate " + NewRun.Visible + ", state " + Net.ClientState.ToString() + ", day " + Game.Day + ", cash " + ((Game.Store != null) ? Game.Store.playerCash : (-1));
			return flag ? StepStatus.Pass : StepStatus.Running;
		});
		Check("n-reset", () => (!NewRun.LocalAsked && NewRun.LastRefusal.Length == 0) || path == "refused", () => "asked " + NewRun.LocalAsked + ", refusal '" + NewRun.LastRefusal + "'");
	}

	private static void Click()
	{
		((UnityEvent)ButtonGo().GetComponent<Button>().onClick).Invoke();
	}

	private static GameObject ButtonGo()
	{
		if ((Object)(object)Overlay.NewRunArea == (Object)null)
		{
			return null;
		}
		Button val = ((IEnumerable<Button>)((Component)Overlay.NewRunArea).GetComponentsInChildren<Button>(true)).FirstOrDefault((Func<Button, bool>)((Button b) => ((Object)((Component)b).gameObject).name == "humangenome_accomplice_newrun_button"));
		if (!((Object)(object)val != (Object)null))
		{
			return null;
		}
		return ((Component)val).gameObject;
	}

	private void MarkIf(string name, string mark, bool leader)
	{
		int id = 0;
		bool skip = false;
		Step(name, 15f, delegate(BotStep st)
		{
			if (skip)
			{
				st.Detail = "skipped (" + (UiCoopBase.IAmLeader ? "leader" : "watcher") + ")";
				return StepStatus.Pass;
			}
			string text = DialogueBotNet.Take(id);
			if (text == null)
			{
				return StepStatus.Running;
			}
			st.Detail = "mark " + mark + " -> " + text;
			return (text == "ok") ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			skip = UiCoopBase.IAmLeader != leader;
			if (!skip)
			{
				id = DialogueBotNet.Send("mark:" + mark);
			}
		});
	}

	private void WaitMarkIf(string name, string mark, float timeout, bool leader)
	{
		int id = -1;
		float last = -10f;
		Step(name, timeout, delegate(BotStep st)
		{
			if (UiCoopBase.IAmLeader != leader)
			{
				st.Detail = "skipped (" + (UiCoopBase.IAmLeader ? "leader" : "watcher") + ")";
				return StepStatus.Pass;
			}
			if (id >= 0)
			{
				string text = DialogueBotNet.Take(id);
				if (text == null)
				{
					return StepStatus.Running;
				}
				id = -1;
				if (text == "yes")
				{
					st.Detail = "mark " + mark + " set (" + UiCoopBase.F(st.Elapsed) + " s)";
					return StepStatus.Pass;
				}
			}
			if (st.Elapsed - last >= 1f)
			{
				last = st.Elapsed;
				id = DialogueBotNet.Send("has:" + mark);
			}
			return StepStatus.Running;
		}).TimeoutDetail = () => "mark " + mark + " never set";
	}

	private void CommandIf(string name, string cmd, Func<string, bool> ok, bool leader)
	{
		int id = 0;
		bool skip = false;
		Step(name, 15f, delegate(BotStep st)
		{
			if (skip)
			{
				st.Detail = "skipped (" + (UiCoopBase.IAmLeader ? "leader" : "watcher") + ")";
				return StepStatus.Pass;
			}
			string text = DialogueBotNet.Take(id);
			if (text == null)
			{
				return StepStatus.Running;
			}
			st.Detail = cmd + " -> " + text;
			return ok(text) ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			skip = UiCoopBase.IAmLeader != leader;
			if (!skip)
			{
				id = DialogueBotNet.Send(cmd);
			}
		});
	}
}
