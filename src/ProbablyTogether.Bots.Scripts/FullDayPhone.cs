using System;
using System.Globalization;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public static class FullDayPhone
{
	public static Func<BotStep, StepStatus> Poll(bool toEnd)
	{
		bool called = false;
		bool started = false;
		bool hungUp = false;
		float last = 0f;
		int shown = 0;
		int advanced = 0;
		int chose = 0;
		return delegate(BotStep st)
		{
			PhoneChannel phone = DialogueSystem.Phone;
			if (started && !PhoneChannel.Talking && Locks.HeldBy("phone") < 0 && (toEnd || hungUp))
			{
				st.Detail = (toEnd ? "call ran to its end" : "hung up mid-call") + " (shown " + shown + ", advance " + advanced + ", chose " + chose + "); phone now: line " + DialogueRefs.Fp(phone.Current) + ", caller " + (PhoneChannel.Caller?.dialogFuncId ?? "-") + " after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
				return StepStatus.Pass;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (realtimeSinceStartup - last < 1f)
			{
				return StepStatus.Running;
			}
			last = realtimeSinceStartup;
			if (!called)
			{
				if (Locks.HeldBy("phone") >= 0)
				{
					st.Detail = "the phone is busy (" + Locks.HeldBy("phone") + ")";
					return StepStatus.Running;
				}
				called = DialogueSync.RequestCall(8376L);
				st.Detail = "called Wild: " + called;
				return StepStatus.Running;
			}
			if (!started)
			{
				if (phone.Current == null || !PhoneChannel.Talking)
				{
					st.Detail = "waiting for the call (talking " + PhoneChannel.Talking + ")";
					return StepStatus.Running;
				}
				started = true;
			}
			if (!toEnd)
			{
				if (phone.LineUnfinished)
				{
					if (DialogueSync.RequestShown("phone"))
					{
						shown++;
					}
					return StepStatus.Running;
				}
				if (!hungUp)
				{
					hungUp = DialogueSync.RequestHangup();
					st.Detail = "hang-up sent: " + hungUp;
				}
				return StepStatus.Running;
			}
			if (phone.Current == null)
			{
				st.Detail = "no line (talking " + PhoneChannel.Talking + ")";
				return StepStatus.Running;
			}
			if (phone.LineUnfinished)
			{
				if (DialogueSync.RequestShown("phone"))
				{
					shown++;
				}
			}
			else if (phone.IsChoiceNow)
			{
				if (DialogueSync.RequestChoose("phone", phone.Current.choices.Count - 1))
				{
					chose++;
				}
			}
			else if (DialogueSync.RequestAdvance("phone"))
			{
				advanced++;
			}
			st.Detail = "pacing the call to its end: shown " + shown + ", advance " + advanced + ", chose last " + chose + ", talking " + PhoneChannel.Talking;
			return StepStatus.Running;
		};
	}
}
