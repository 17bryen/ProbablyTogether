using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PicsLead : PicsBase
{
	private const long Wild = 8376L;

	public override string Name => "pics-lead";

	protected override void Build()
	{
		Join();
		WaitMark("w-shop", "pics-shop-seen", 120f);
		Command("phone-open", "phone-open:" + 8376L, (string r) => r.StartsWith("ok", StringComparison.Ordinal));
		S("call", 20f, delegate(BotStep st)
		{
			if (st.Elapsed < 0.05f && !DialogueSync.RequestCall(8376L))
			{
				st.Detail = "the call was not sent (local gate)";
				return StepStatus.Fail;
			}
			st.Detail = "phone lock " + Locks.HeldBy("phone") + ", talking " + PhoneChannel.Talking;
			return (Locks.HeldBy("phone") == Net.LocalPlayer && PhoneChannel.Talking) ? StepStatus.Pass : StepStatus.Running;
		});
		WaitMark("w-phone", "pics-phone-seen", 90f);
		S("hangup", 30f, delegate(BotStep st)
		{
			if (st.Elapsed < 0.05f && !DialogueSync.RequestHangup())
			{
				st.Detail = "hang-up not sent";
				return StepStatus.Fail;
			}
			st.Detail = "phone lock " + Locks.HeldBy("phone");
			return (Locks.HeldBy("phone") < 0) ? StepStatus.Pass : StepStatus.Running;
		});
		Delay("between-1", 2f);
		Do("aim", delegate
		{
			Net.ToServer("sec.aim", new MsgSecAim
			{
				on = true
			});
			return "sec.aim on";
		});
		WaitMark("w-aim", "pics-aim-seen", 60f);
		Do("lower", delegate
		{
			Net.ToServer("sec.aim", new MsgSecAim
			{
				on = false
			});
			return "sec.aim off";
		});
		Delay("between-2", 2f);
		Do("chat", () => (!Chat.Send("Shutter goes up soon. I've got the phone.")) ? "not sent" : "sent 'Shutter goes up soon. I've got the phone.'");
		WaitMark("w-chat", "pics-chat-seen", 60f);
		Do("open", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "dayNight.open sent";
		});
		WaitFor("opened", 30f, () => PicsBase.State == "OPEN", () => "state " + PicsBase.State);
		WaitMark("w-open", "pics-open-seen", 90f);
		WaitFor("counter-free", 60f, () => Locks.HeldBy("counter") < 0 || Locks.HeldBy("counter") == Net.LocalPlayer, () => "counter " + Locks.HeldBy("counter"));
		Do("queue", delegate
		{
			if (PicsBase.Ps.CanEndDay())
			{
				return "every waiting customer can be sent away";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "a story customer is waiting: lab op clearqueue";
		});
		WaitFor("closable", 20f, () => PicsBase.Ps.CanEndDay(), () => "queue " + PicsBase.Ps.storeClientManager.clientStack.Count);
		float pressed = -100f;
		S("close", 40f, delegate(BotStep st)
		{
			if (PicsBase.State == "CLOSED")
			{
				st.Detail = "closed";
				return StepStatus.Pass;
			}
			if (Time.realtimeSinceStartup - pressed >= 5f)
			{
				pressed = Time.realtimeSinceStartup;
				StoreShutterButton.Instance.EndAndClose();
			}
			st.Detail = "state " + PicsBase.State + ", last refusal " + DayNight.LastRefusedWhy;
			return StepStatus.Running;
		});
		Delay("past-close", 3f);
		Do("ready-reset", delegate
		{
			pressed = -100f;
			return "pressing End Day";
		});
		S("ready", 40f, delegate(BotStep st)
		{
			if (DayNight.IsReady(Net.LocalPlayer))
			{
				st.Detail = "ready (barrier " + string.Join(",", DayNight.ReadyPlayers) + ")";
				return StepStatus.Pass;
			}
			if (Time.realtimeSinceStartup - pressed >= 4f)
			{
				pressed = Time.realtimeSinceStartup;
				EndOfDayUIManager.Instance.OpenUI();
				EndOfDayUIManager.Instance.OnEndDayButtonClick();
			}
			st.Detail = "last refusal '" + DayNight.LastRefusedWhy + "'";
			return StepStatus.Running;
		});
		WaitMark("w-ready", "pics-ready-seen", 90f);
		Do("ending", delegate
		{
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "gameover:rent"
			});
			return "asked day-night for a lab losing ending (gameover:rent)";
		});
		WaitFor("plate", 60f, () => DayNightRun.RunOver && NewRun.Visible, () => "run over " + DayNightRun.RunOver + ", plate " + NewRun.Visible);
		Delay("watcher-holds-plate", 15f);
		Do("vote", delegate
		{
			Click();
			return "pressed Start a new run";
		});
		WaitFor("voted", 20f, () => NewRun.Votes().Contains(Net.LocalPlayer), () => "plate '" + NewRun.Text + "'");
		Delay("watcher-holds-vote", 15f);
	}

	private static void Click()
	{
		RectTransform newRunArea = Overlay.NewRunArea;
		Button? obj = ((newRunArea != null) ? ((IEnumerable<Button>)((Component)newRunArea).GetComponentsInChildren<Button>(true)).FirstOrDefault((Func<Button, bool>)((Button x) => ((Object)((Component)x).gameObject).name == "humangenome_accomplice_newrun_button")) : null);
		if ((Object)(object)obj == (Object)null)
		{
			throw new Exception("no plate button");
		}
		((UnityEvent)obj.onClick).Invoke();
	}
}
