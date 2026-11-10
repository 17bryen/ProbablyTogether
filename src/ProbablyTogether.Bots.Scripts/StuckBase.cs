using ProbablyTogether.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public abstract class StuckBase : FullDayBot
{
	protected int day0;

	protected int loads0;

	protected int ended0;

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 1500f;

	protected static string Mode => (Args.Get("--br-stuck") ?? "leave").Trim();

	protected static string Cust => (Args.Get("--br-stuck-cust") ?? "").Trim();

	protected static string Why()
	{
		return FullDayBot.DlgText() + "; holder " + Locks.HeldBy("counter") + " (me " + Net.LocalPlayer + "), waiting " + DialogueSystem.Counter.WaitingManual + ", end pending " + DialogueSystem.Counter.EndPending + ", unfinished " + DialogueSystem.Counter.LineUnfinished + ", bell flag " + (Game.ShopLoaded ? FullDayBot.Ps.lockNextBellFlag : (-1)) + ", dismissable " + (Game.ShopLoaded && FullDayBot.Ps.currentClientInstance != null && FullDayBot.Ps.IsClientDimissable()) + ", can end day " + (Game.ShopLoaded && FullDayBot.Ps.CanEndDay()) + ", last day-night refusal '" + DayNight.LastRefusedWhy + "'";
	}

	protected BotStep FinishVisit(string name, float timeout)
	{
		float last = 0f;
		float lastDismiss = -100f;
		int presses = 0;
		int dismisses = 0;
		int seq0 = -1;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (seq0 < 0)
			{
				seq0 = DialogueSync.Seq("counter");
			}
			if (FullDayLab.CurrentId() == "none" && FullDayBot.NoDialogue)
			{
				st.Detail = "conversation over and the counter empty: steps " + seq0 + " -> " + DialogueSync.Seq("counter") + ", " + presses + " press(es), " + dismisses + " dismiss(es) after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			if (FullDayBot.Now - last < 1.2f)
			{
				return StepStatus.Running;
			}
			last = FullDayBot.Now;
			CounterChannel counter = DialogueSystem.Counter;
			DialogUIManager instance = DialogUIManager.Instance;
			if (counter.Current != null && (Object)(object)instance != (Object)null)
			{
				presses++;
				if (counter.LineUnfinished)
				{
					instance.OnCurrentTextDisplayed();
				}
				else if (counter.IsChoiceNow)
				{
					instance.SelectChoice(0);
				}
				else
				{
					instance.OnAdvancePressed();
				}
				return StepStatus.Running;
			}
			if (FullDayBot.Ps.isClientArrived && FullDayBot.Ps.IsClientDimissable() && FullDayBot.Now - lastDismiss > 4f)
			{
				lastDismiss = FullDayBot.Now;
				dismisses++;
				FullDayBot.Ps.TryPlayerDismissCurrentClient(false);
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "STUCK: " + presses + " press(es), " + dismisses + " dismiss(es), steps " + seq0 + " -> " + DialogueSync.Seq("counter") + "; " + Why() + "; " + FullDayBot.Shop();
		return botStep;
	}

	protected BotStep Close(string name)
	{
		float pressed = -100f;
		int presses = 0;
		BotStep botStep = Step(name, 60f, delegate(BotStep st)
		{
			if (FullDayBot.State == "CLOSED")
			{
				st.Detail = presses + " shutter press(es); closed after " + FullDayBot.F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			if (FullDayBot.Now - pressed >= 5f)
			{
				pressed = FullDayBot.Now;
				presses++;
				StoreShutterButton.Instance.EndAndClose();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "could not close: " + presses + " press(es); " + Why() + "; " + FullDayBot.Shop();
		return botStep;
	}

	protected void Night(string tag)
	{
		Do("note-day-" + tag, delegate
		{
			day0 = FullDayBot.Day;
			loads0 = FullDayLab.Loads;
			ended0 = FullDayLab.DaysEnded;
			return "day " + day0 + ", loads " + loads0;
		});
		Delay("after-close-" + tag, 6f);
		ReadyStep("ready-" + tag, () => ended0);
		WaitFor("morning-" + tag, 300f, () => FullDayBot.Live && FullDayLab.Loads > loads0 && FullDayBot.Day == day0 + 1 && FullDayBot.State == "MORNING", () => FullDayBot.Shop() + ", loads " + FullDayLab.Loads + ", night " + BotProbes.Read("fd.night"));
		Same("morning-" + tag, 30f, FullDayBot.All);
	}
}
