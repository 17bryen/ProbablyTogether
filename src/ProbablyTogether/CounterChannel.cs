using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

internal sealed class CounterChannel : DialogueChannel
{
	private static readonly FieldInfo fUncompleted = AccessTools.Field(typeof(DialogUIManager), "displayingUncompletedDialog");

	private static readonly FieldInfo fScheduled = AccessTools.Field(typeof(DialogUIManager), "scheduledEndActionLines");

	private static readonly FieldInfo fEpoch = AccessTools.Field(typeof(DialogUIManager), "conversationEpoch");

	private static readonly FieldInfo fEndPending = AccessTools.Field(typeof(DialogUIManager), "isEndPending");

	private static readonly FieldInfo fAutoPending = AccessTools.Field(typeof(DialogUIManager), "isAutoAdvancePending");

	private static readonly MethodInfo mFinish = AccessTools.Method(typeof(DialogUIManager), "FinishConversationNow", (Type[])null, (Type[])null);

	private static readonly MethodInfo mScheduleFlow = AccessTools.Method(typeof(DialogUIManager), "ScheduleFlow", new Type[2]
	{
		typeof(float),
		typeof(Action)
	}, (Type[])null);

	private static readonly MethodInfo mScheduleAuto = AccessTools.Method(typeof(DialogUIManager), "ScheduleAutoAdvance", new Type[1] { typeof(float) }, (Type[])null);

	private static DialogUIManager M => DialogUIManager.Instance;

	internal override MonoBehaviour Manager => (MonoBehaviour)(object)M;

	internal override Dialogue Current
	{
		get
		{
			if (!((Object)(object)M != (Object)null))
			{
				return null;
			}
			return M.currentDialog;
		}
	}

	internal override Dialogue Uncompleted
	{
		get
		{
			return DialogueChannel.Get<Dialogue>(fUncompleted, M);
		}
		set
		{
			DialogueChannel.Set(fUncompleted, M, value);
		}
	}

	internal override int Epoch => DialogueChannel.Get<int>(fEpoch, M);

	internal override bool EndPending
	{
		get
		{
			return DialogueChannel.Get<bool>(fEndPending, M);
		}
		set
		{
			DialogueChannel.Set(fEndPending, M, value);
		}
	}

	internal override bool AutoPending => DialogueChannel.Get<bool>(fAutoPending, M);

	internal override bool WaitingManual
	{
		get
		{
			if ((Object)(object)M != (Object)null)
			{
				return M.isWaitingForManualAdvance;
			}
			return false;
		}
		set
		{
			if ((Object)(object)M != (Object)null)
			{
				M.isWaitingForManualAdvance = value;
			}
		}
	}

	internal override bool SkipCurrent
	{
		get
		{
			if ((Object)(object)M != (Object)null)
			{
				return M.isSkipCurrentDialog;
			}
			return false;
		}
		set
		{
			if ((Object)(object)M != (Object)null)
			{
				M.isSkipCurrentDialog = value;
			}
		}
	}

	internal override List<Coroutine> Coroutines => M?.activeCoroutines;

	internal List<Dialogue> Scheduled => DialogueChannel.Get<List<Dialogue>>(fScheduled, M);

	internal CounterChannel()
		: base("counter", "counter")
	{
	}

	internal override bool Start(Dialogue d, bool forced)
	{
		if ((Object)(object)M != (Object)null)
		{
			return M.StartDialogue(d, forced);
		}
		return false;
	}

	internal override void Continue()
	{
		DialogUIManager m = M;
		if (m != null)
		{
			m.ContinueConversation();
		}
	}

	internal override void Finish()
	{
		DialogueChannel.Call(mFinish, M);
	}

	internal override void Select(int index)
	{
		DialogUIManager m = M;
		if (m != null)
		{
			m.SelectChoice(index);
		}
	}

	internal override void End()
	{
		DialogUIManager m = M;
		if (m != null)
		{
			m.EndConversation();
		}
	}

	internal override void Interrupt()
	{
		DialogUIManager m = M;
		if (m != null)
		{
			m.InterruptDialog();
		}
	}

	internal override void ScheduleFlow(float delay, Action action)
	{
		DialogueChannel.Call(mScheduleFlow, M, delay, action);
	}

	internal override void ScheduleAutoAdvance(float delay)
	{
		DialogueChannel.Call(mScheduleAuto, M, delay);
	}

	internal override void DisplayChoice()
	{
		DialogUIManager m = M;
		if (m != null)
		{
			m.DisplayChoice();
		}
	}

	internal static bool WouldRefuse(Dialogue d, bool forced)
	{
		DialogUIManager m = M;
		if (d == null || (Object)(object)m == (Object)null)
		{
			return true;
		}
		Dialogue currentDialog = m.currentDialog;
		if (currentDialog == null || forced)
		{
			return false;
		}
		bool flag = currentDialog.priority >= d.priority;
		if (flag && m.isWaitingForManualAdvance && !HasPendingChainConsequence(currentDialog))
		{
			flag = false;
		}
		return flag;
	}

	private static bool HasPendingChainConsequence(Dialogue d)
	{
		int num = 0;
		Dialogue nextDialogue = d.nextDialogue;
		while (nextDialogue != null && num++ < 512)
		{
			if (nextDialogue.startAction != null)
			{
				return true;
			}
			if (nextDialogue.endAction != null && !nextDialogue.isEndActionExecuted)
			{
				return true;
			}
			if (nextDialogue.choices != null && nextDialogue.choices.Count > 0)
			{
				return true;
			}
			nextDialogue = nextDialogue.nextDialogue;
		}
		return false;
	}

	internal override void Shown(bool server, bool pacer)
	{
		DialogUIManager m = M;
		if ((Object)(object)m == (Object)null)
		{
			return;
		}
		Uncompleted = null;
		Dialogue currentDialog = m.currentDialog;
		if (currentDialog == null)
		{
			return;
		}
		PushShown(currentDialog);
		if (currentDialog.endAction != null)
		{
			Scheduled?.Add(currentDialog);
			if (server)
			{
				Dialogue node = currentDialog;
				// ModHelper.Delay does not end with the object, so check the object first.
				ModHelper.Delay(Mod.Id, node.endActionDelay, (Action)delegate
				{
					if ((UnityEngine.Object)(object)m != (UnityEngine.Object)null)
					{
						DialogueSystem.ServerEndActionTimer(this, node);
					}
				});
			}
		}
		if (m.IsChoice())
		{
			m.isSkipCurrentDialog = false;
			ScheduleFlow(DialogUIManager.ScaleByDialogSpeed(0.85f) + DialogUIManager.GetTypewriterDelay(), (Action)m.DisplayChoice);
		}
		else if (!m.IsCurrentDialogEnd())
		{
			bool flag = pacer && (Object)(object)EmporiumOption.Instance != (Object)null && EmporiumOption.Instance.autoNextDialog;
			if (m.isSkipCurrentDialog)
			{
				m.isSkipCurrentDialog = false;
				currentDialog.ExecuteEndAction();
				if (flag)
				{
					ScheduleAutoAdvance(0.01f);
				}
				else
				{
					m.isWaitingForManualAdvance = true;
				}
			}
			else if (flag)
			{
				ScheduleAutoAdvance(DialogUIManager.ScaleByDialogSpeed(0.99f) + DialogUIManager.GetTypewriterDelay());
			}
			else
			{
				m.isWaitingForManualAdvance = true;
			}
		}
		else
		{
			m.isSkipCurrentDialog = false;
			EndPending = true;
			if (server)
			{
				ScheduleFlow(DialogUIManager.ScaleByDialogSpeed(1f) + DialogUIManager.GetTypewriterDelay(), (Action)m.EndConversation);
			}
		}
	}

	internal override void FireEndAction(Dialogue node)
	{
		DialogUIManager m = M;
		if (!((Object)(object)m == (Object)null) && node != null)
		{
			Scheduled?.Remove(node);
			if (m.currentDialog == node && node.nextDialogue == null && (node.choices == null || node.choices.Count == 0))
			{
				m.EndConversation();
			}
			node.ExecuteEndAction();
			m.CheckOwned();
		}
	}
}
