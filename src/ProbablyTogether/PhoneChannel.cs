using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

internal sealed class PhoneChannel : DialogueChannel
{
	private static readonly FieldInfo fUncompleted = AccessTools.Field(typeof(DialoguePhoneUIManager), "displayingUncompletedDialog");

	private static readonly FieldInfo fEpoch = AccessTools.Field(typeof(DialoguePhoneUIManager), "conversationEpoch");

	private static readonly FieldInfo fEndPending = AccessTools.Field(typeof(DialoguePhoneUIManager), "isEndPending");

	private static readonly FieldInfo fAutoPending = AccessTools.Field(typeof(DialoguePhoneUIManager), "isAutoAdvancePending");

	private static readonly FieldInfo fCoroutines = AccessTools.Field(typeof(DialoguePhoneUIManager), "activeCoroutines");

	private static readonly MethodInfo mFinish = AccessTools.Method(typeof(DialoguePhoneUIManager), "FinishConversationNow", (Type[])null, (Type[])null);

	private static readonly MethodInfo mScheduleFlow = AccessTools.Method(typeof(DialoguePhoneUIManager), "ScheduleFlow", new Type[2]
	{
		typeof(float),
		typeof(Action)
	}, (Type[])null);

	private static readonly MethodInfo mScheduleAuto = AccessTools.Method(typeof(DialoguePhoneUIManager), "ScheduleAutoAdvance", new Type[1] { typeof(float) }, (Type[])null);

	private static readonly FieldInfo fTalking = AccessTools.Field(typeof(PhoneUIManager), "isTalking");

	private static readonly MethodInfo mQuickSet = AccessTools.PropertySetter(typeof(PhoneUIManager), "isQuickDialCall");

	private static DialoguePhoneUIManager M => DialoguePhoneUIManager.Instance;

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

	internal override List<Coroutine> Coroutines => DialogueChannel.Get<List<Coroutine>>(fCoroutines, M);

	internal static bool Talking => DialogueChannel.Get<bool>(fTalking, PhoneUIManager.Instance);

	internal static StorePhoneClient Caller
	{
		get
		{
			if (!((Object)(object)PhoneUIManager.Instance != (Object)null))
			{
				return null;
			}
			return PhoneUIManager.Instance.currentPhoneClient;
		}
	}

	internal PhoneChannel()
		: base("phone", "phone")
	{
	}

	internal override bool Start(Dialogue d, bool forced)
	{
		if ((Object)(object)M == (Object)null || d == null)
		{
			return false;
		}
		M.StartDialogue(d);
		return true;
	}

	internal override void Continue()
	{
		DialoguePhoneUIManager m = M;
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
		DialoguePhoneUIManager m = M;
		if (m != null)
		{
			m.SelectChoice(index);
		}
	}

	internal override void End()
	{
		DialoguePhoneUIManager m = M;
		if (m != null)
		{
			m.EndConversation();
		}
	}

	internal override void Interrupt()
	{
		DialoguePhoneUIManager m = M;
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
		DialoguePhoneUIManager m = M;
		if (m != null)
		{
			m.DisplayChoice();
		}
	}

	internal static void SetQuick(bool v)
	{
		if ((Object)(object)PhoneUIManager.Instance != (Object)null && mQuickSet != null)
		{
			mQuickSet.Invoke(PhoneUIManager.Instance, new object[1] { v });
		}
	}

	internal override void Shown(bool server, bool pacer)
	{
		DialoguePhoneUIManager m = M;
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
		if (currentDialog.endAction != null && server)
		{
			Dialogue node = currentDialog;
			ModHelper.Delay(Mod.Id, 1.1f, (Action)delegate
			{
				if ((UnityEngine.Object)(object)m != (UnityEngine.Object)null)
				{
					DialogueSystem.ServerEndActionTimer(this, node);
				}
			});
		}
		if (currentDialog.choices != null && currentDialog.choices.Count > 0)
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
		if (node != null)
		{
			node.ExecuteEndAction();
		}
	}
}
