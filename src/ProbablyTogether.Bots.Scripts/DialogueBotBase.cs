using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public abstract class DialogueBotBase : BotScript
{
	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 420f;

	protected static int RefusedEventCount
	{
		get
		{
			lock (DialogueBotNet.RefusedEvents)
			{
				return DialogueBotNet.RefusedEvents.Count;
			}
		}
	}

	protected static string LastRefusedEvent
	{
		get
		{
			lock (DialogueBotNet.RefusedEvents)
			{
				return (DialogueBotNet.RefusedEvents.Count > 0) ? DialogueBotNet.RefusedEvents[DialogueBotNet.RefusedEvents.Count - 1] : "";
			}
		}
	}

	protected static string Cur()
	{
		if (!Game.ShopLoaded)
		{
			return "";
		}
		PlayerStore store = Game.Store;
		object obj;
		if (store == null)
		{
			obj = null;
		}
		else
		{
			StoreClientInstance currentClientInstance = store.currentClientInstance;
			obj = ((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier);
		}
		if (obj == null)
		{
			obj = "none";
		}
		return (string)obj;
	}

	protected static bool BfScheduled()
	{
		return DialogueSystem.Counter.Scheduled?.Exists((Dialogue d) => d != null && d.plainText == "flush me") ?? false;
	}

	protected static int ChoiceBubbles()
	{
		List<DialogChoiceBubble> list = DialogUIManager.Instance?.dialogChoiceBubbles;
		if (list == null)
		{
			return -1;
		}
		int num = 0;
		foreach (DialogChoiceBubble item in list)
		{
			if ((Object)(object)item != (Object)null && ((Component)item).gameObject.activeSelf)
			{
				num++;
			}
		}
		return num;
	}

	protected static string Live()
	{
		return DialogueSystem.DigestPart() + " at " + DialogueSync.At("counter") + " customer " + Cur() + " holder " + Locks.HeldBy("counter") + " main " + BotProbes.Read("dlg.main") + " cash " + (Game.ShopLoaded ? Game.Store.playerCash : (-1)) + " refused " + DialogueSync.RefusedCount + " (" + DialogueSync.LastRefused + ")";
	}

	protected void QuietOptions()
	{
		Do("quiet-options", delegate
		{
			EmporiumOption instance = EmporiumOption.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				return "no options object";
			}
			instance.autoNextDialog = false;
			return "autoNextDialog off on this copy (the bot paces by hand)";
		});
	}

	protected BotStep Drive(string name, float timeout, Func<int, bool> done, Func<Dialogue, int> choose = null)
	{
		int sentSeq = int.MinValue;
		int advances = 0;
		float sentAt = -10f;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (done(advances))
			{
				st.Detail = advances + " advance(s); " + Live();
				return StepStatus.Pass;
			}
			CounterChannel counter = DialogueSystem.Counter;
			if (counter.Current == null)
			{
				return StepStatus.Running;
			}
			if (counter.Seq == sentSeq && st.Elapsed - sentAt < 3f)
			{
				return StepStatus.Running;
			}
			if (counter.LineUnfinished)
			{
				if (DialogueSync.IsLocalPacer("counter") && DialogueSync.RequestShown("counter"))
				{
					sentSeq = counter.Seq;
					sentAt = st.Elapsed;
				}
				return StepStatus.Running;
			}
			if (counter.IsChoiceNow)
			{
				if (choose == null)
				{
					return StepStatus.Running;
				}
				int num = choose(counter.Current);
				if (num < 0 || num >= counter.Current.choices.Count || !counter.Current.IsChoiceEnabledAt(num))
				{
					num = 0;
				}
				if (DialogueSync.RequestChoose("counter", num))
				{
					sentSeq = counter.Seq;
					sentAt = st.Elapsed;
				}
				return StepStatus.Running;
			}
			if ((counter.WaitingManual || counter.EndPending) && DialogueSync.RequestAdvance("counter"))
			{
				sentSeq = counter.Seq;
				sentAt = st.Elapsed;
				advances++;
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => advances + " advance(s); " + Live();
		return botStep;
	}

	protected void Join()
	{
		Do("wire", delegate
		{
			DialogueBotNet.Ensure();
			return "test graph registered, lab commands wired";
		});
		WaitFor("joined", 120f, () => Net.IsClient && Net.LocalPlayer > 0, () => "player " + Net.LocalPlayer);
		WaitFor("live", 120f, () => Net.IsLive && BotProbes.InShop && DialogueSystem.Counter.Ready && DialogueSystem.Phone.Ready, () => "scene " + BotProbes.Scene + " state " + Net.ClientState);
		Delay("settle", 2f);
	}

	protected void Command(string name, string cmd, Func<string, bool> ok, float timeout = 15f)
	{
		int id = 0;
		Step(name, timeout, delegate(BotStep st)
		{
			string text = DialogueBotNet.Take(id);
			if (text == null)
			{
				return StepStatus.Running;
			}
			st.Detail = cmd + " -> " + text;
			return ok(text) ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			id = DialogueBotNet.Send(cmd);
		}).TimeoutDetail = () => "no answer to '" + cmd + "'";
	}

	protected void WaitMark(string name, string mark, float timeout)
	{
		int id = -1;
		float last = -10f;
		Step(name, timeout, delegate(BotStep st)
		{
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
					st.Detail = "mark " + mark + " set after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
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

	protected void RequestThen(string name, float timeout, Func<bool> send, Func<bool> until, Func<string> detail)
	{
		bool sent = false;
		Step(name, timeout, delegate(BotStep st)
		{
			if (!sent)
			{
				st.Detail = "request not sent (gate refused it locally); " + detail();
				return StepStatus.Fail;
			}
			if (!until())
			{
				return StepStatus.Running;
			}
			st.Detail = detail() + " after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
			return StepStatus.Pass;
		}, delegate
		{
			sent = send();
		}).TimeoutDetail = () => "timed out; " + detail();
	}

	protected BotStep SameAsServerSoon(string name, float timeout, params string[] keys)
	{
		int id = -1;
		BotStep step = Step(name, timeout, delegate(BotStep st)
		{
			if (id < 0)
			{
				id = BotProbes.AskServer();
				return StepStatus.Running;
			}
			Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
			if (dictionary == null)
			{
				return StepStatus.Running;
			}
			id = -1;
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			string[] array = keys;
			foreach (string text in array)
			{
				string text2 = BotProbes.Read(text);
				list2.Add(text + "=" + text2);
				if (text.EndsWith(".at", StringComparison.Ordinal))
				{
					dictionary.TryGetValue(text.Substring(0, text.Length - 3) + ".hist", out var value);
					if (!DialogueSync.AtMatches(text2, value, out var pending, out var detail))
					{
						list.Add(text + (pending ? " pending: " : " differs: ") + detail);
					}
				}
				else
				{
					dictionary.TryGetValue(text, out var value2);
					if (text2 != value2)
					{
						list.Add(text + " here " + text2 + " server " + value2);
					}
				}
			}
			if (list.Count == 0)
			{
				st.Detail = "same as server: " + string.Join(" ", list2);
				return StepStatus.Pass;
			}
			st.Detail = string.Join("; ", list);
			return StepStatus.Running;
		});
		step.TimeoutDetail = () => "still different after " + timeout.ToString("F0", CultureInfo.InvariantCulture) + " s: " + (step.Detail ?? "no reply");
		return step;
	}

	protected static string Ph()
	{
		return DialogueSync.Digest() + " lock " + Locks.HeldBy("phone") + " refused " + DialogueSync.RefusedCount + " (" + DialogueSync.LastRefused + ")";
	}

	protected static string Co()
	{
		return DialogueSync.Digest() + " lock " + Locks.HeldBy("counter") + " bt " + DialogueBotNet.BtValue + "/" + DialogueBotNet.BtEnds + " cash " + (PlayerStore.HasInstance ? PlayerStore.Instance.playerCash : (-1)) + " refused " + DialogueSync.RefusedCount;
	}
}
