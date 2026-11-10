using System.Collections.Generic;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DialogueProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("dlg.counter.fp", () => DialogueSync.CurrentFp("counter"));
		BotProbes.Add("dlg.counter.seq", () => BotProbeSet.Str(DialogueSync.Seq("counter")));
		BotProbes.Add("dlg.phone.fp", () => DialogueSync.CurrentFp("phone"));
		BotProbes.Add("dlg.phone.seq", () => BotProbeSet.Str(DialogueSync.Seq("phone")));
		BotProbes.Add("dlg.phone.caller", () => PhoneChannel.Caller?.dialogFuncId ?? "-");
		BotProbes.Add("dlg.phone.talking", () => (!PhoneChannel.Talking) ? "0" : "1");
		BotProbes.Add("dlg.counter.at", () => DialogueSync.At("counter"));
		BotProbes.Add("dlg.counter.hist", () => DialogueSync.AtHistory("counter"));
		BotProbes.Add("dlg.phone.at", () => DialogueSync.At("phone"));
		BotProbes.Add("dlg.phone.hist", () => DialogueSync.AtHistory("phone"));
		BotProbes.Add("dlg.bt.value", () => BotProbeSet.Str(DialogueBotNet.BtValue));
		BotProbes.Add("dlg.bt.ends", () => BotProbeSet.Str(DialogueBotNet.BtEnds));
		BotProbes.Add("dlg.bf.ends", () => BotProbeSet.Str(DialogueBotNet.BfEnds));
		BotProbes.Add("dlg.timeouts", () => BotProbeSet.Str(DialogueSystem.ShownTimeouts));
		BotProbes.Add("lock.counter", () => BotProbeSet.Str(Locks.HeldBy("counter")));
		BotProbes.Add("lock.phone", () => BotProbeSet.Str(Locks.HeldBy("phone")));
		BotProbes.Add("story.hash", DialogueEvents.StoryHash);
		BotProbes.Add("events.hash", DialogueEvents.EventsHash);
		BotProbes.Add("requests.hash", DialogueEvents.RequestsHash);
		BotProbes.Add("tutorial", delegate
		{
			if (!PlayerStore.HasInstance)
			{
				return "";
			}
			return (!PlayerStore.Instance.tutorialEnable) ? "off" : "on";
		});
		BotProbes.Add("devtools", () => (!DevTools.IsEnabled) ? "off" : "on");
		BotProbes.Add("dlg.digest", DialogueSystem.DigestPart);
		BotProbes.Add("dlg.future", delegate
		{
			PlayerStore val = (Game.ShopLoaded ? Game.Store : null);
			if (val?.futurStoreClientIdQueue == null)
			{
				return "";
			}
			List<string> list = new List<string>();
			for (int i = 0; i < val.futurStoreClientIdQueue.Count; i++)
			{
				list.Add(val.futurStoreClientIdQueue[i] + "/" + ((val.futurStoreClientDayQueue != null && i < val.futurStoreClientDayQueue.Count) ? BotProbeSet.Str(val.futurStoreClientDayQueue[i]) : "?"));
			}
			return string.Join(",", list);
		});
		BotProbes.Add("dlg.main", delegate
		{
			object obj;
			if (!Game.ShopLoaded)
			{
				obj = null;
			}
			else
			{
				PlayerStore store = Game.Store;
				if (store == null)
				{
					obj = null;
				}
				else
				{
					StoreClientInstance currentClientInstance = store.currentClientInstance;
					obj = ((currentClientInstance != null) ? currentClientInstance.GetClientBlueprint() : null);
				}
			}
			StoreClient val = (StoreClient)obj;
			return (val != null) ? (val.identifier + "/" + (val.isMainDialogueStarted ? "1" : "0")) : "-";
		});
	}
}
