using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public abstract class CounterBot : CustomersBot
{
	protected int v1;

	protected static readonly string[] Same = new string[12]
	{
		"cust.current", "cust.visit", "cust.holder", "ctr.lock", "ctr.jlock", "cash", "ctr.nego", "ctr.feat", "ctr.rep", "cust.stock",
		"cust.digest", "cust.action"
	};

	public override float TimeoutSeconds => 2400f;

	protected static EmporiumEntry E => EmporiumEntry.Instance;

	protected static int Me => Net.LocalPlayer;

	internal static string FeatureText()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return "";
		}
		List<string> list = new List<string>();
		GraphNodeStorage[] array = (GraphNodeStorage[])(object)new GraphNodeStorage[2]
		{
			(GraphNodeStorage)instance.backInvinvElement,
			(GraphNodeStorage)instance.frontInvinvElement
		};
		foreach (GraphNodeStorage val in array)
		{
			if (val == null)
			{
				continue;
			}
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (item?.itemFeatures == null)
				{
					continue;
				}
				foreach (ItemFeature itemFeature in item.itemFeatures)
				{
					if (itemFeature != null && itemFeature.identifier != null && (itemFeature.identifier.StartsWith("bargain") || itemFeature.identifier.StartsWith("blackmail") || itemFeature.identifier.StartsWith("threaten")))
					{
						list.Add(item.uniqueId + ":" + itemFeature.identifier);
					}
				}
			}
		}
		list.Sort(StringComparer.Ordinal);
		if (list.Count != 0)
		{
			return string.Join("+", list);
		}
		return "none";
	}

	internal static string RepText()
	{
		PlayerStore store = Game.Store;
		if (store?.storeReputations == null)
		{
			return "";
		}
		List<string> list = new List<string>();
		foreach (StoreReputation storeReputation in store.storeReputations)
		{
			if (storeReputation != null && storeReputation.factionId != null)
			{
				list.Add(storeReputation.factionId + "=" + storeReputation.GetReputationExact().ToString("0.###", CultureInfo.InvariantCulture));
			}
		}
		list.Sort(StringComparer.Ordinal);
		return string.Join(",", list);
	}

	protected static string Holder()
	{
		return CustomersBot.Mine("ctr.lock");
	}

	protected static GameItem OwnedFitting(GameInventory to)
	{
		foreach (GameItem item in ((GameInventory)E.invElement).childItems.ToList())
		{
			if (item != null && GeneralHelper.IsItemOwned(item) && item.MayRemove() && Ids.Of(item) != 0L && to.TryFindOneValidInventorySlot(item) != null)
			{
				return item;
			}
		}
		return null;
	}

	protected static GameItem CheapestGoods()
	{
		GameItem result = null;
		long num = long.MaxValue;
		foreach (GameItem childItem in ((GameInventory)E.frontInvinvElement).childItems)
		{
			if (childItem != null && !GeneralHelper.IsItemOwned(childItem))
			{
				long negociatedValue = childItem.GetNegociatedValue();
				if (negociatedValue < num)
				{
					num = negociatedValue;
					result = childItem;
				}
			}
		}
		return result;
	}

	protected BotStep PaceTalk(string name, float timeout, bool needStart)
	{
		float lastNudge = 0f;
		int seq0 = -1;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			CounterChannel counter = DialogueSystem.Counter;
			if (seq0 < 0)
			{
				seq0 = DialogueSync.Seq("counter");
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (counter.Current != null)
			{
				if (realtimeSinceStartup - lastNudge > 1.5f)
				{
					lastNudge = realtimeSinceStartup;
					if (counter.IsChoiceNow)
					{
						DialogueSync.RequestChoose("counter", 0);
					}
					else if (counter.LineUnfinished)
					{
						DialogueSync.RequestShown("counter");
					}
					else
					{
						DialogueSync.RequestAdvance("counter");
					}
				}
				return StepStatus.Running;
			}
			if (needStart && (DialogueSync.Seq("counter") <= seq0 + 1 || CustomersBot.Mine("cust.started") != "1"))
			{
				return StepStatus.Running;
			}
			if (st.Elapsed < 2f)
			{
				return StepStatus.Running;
			}
			st.Detail = "no line up: dialogue steps " + seq0 + " -> " + DialogueSync.Seq("counter") + ", goods " + CustomersBot.Mine("cust.stock") + " after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
			return StepStatus.Pass;
		});
		botStep.TimeoutDetail = () => "a line is still up: " + DialogueSync.CurrentFp("counter") + ", holder " + Holder() + "; " + CustomersBot.ShowMine();
		return botStep;
	}

	protected BotStep MoveRefusedHere(string name, Func<GameItem> pick, Func<GameInventory> where, Func<string> want)
	{
		return Step(name, 5f, delegate(BotStep st)
		{
			GameInventory val = where();
			GameItem val2 = pick();
			if (val2 == null)
			{
				st.Detail = "no item to try";
				return StepStatus.Fail;
			}
			int sentCount = ItemsMove.SentCount;
			int num = ItemsMove.QuickMove(val2, (GraphNodeStorage)(object)val, all: false, -1, "bot");
			st.Detail = val2.identifier + "#" + Ids.Of(val2) + " -> " + Items.RefOf((GraphNodeStorage)(object)val)?.ToString() + ": request " + num + ", sent " + (ItemsMove.SentCount - sentCount) + ", notice '" + ItemsMove.LastNotice + "' (want '" + want() + "')";
			return (num == 0 && ItemsMove.SentCount == sentCount && ItemsMove.LastNotice == want()) ? StepStatus.Pass : StepStatus.Fail;
		});
	}

	protected unsafe BotStep HaggleRefusedHere(string name, BargainType type, int percent, Func<string> holderName)
	{
		return Step(name, 5f, delegate(BotStep st)
		{
			bool flag = Negotiation.RequestBargain(type, percent);
			st.Detail = "bargain " + type.ToString() + " " + percent + ": " + (flag ? "SENT" : "not sent") + " (" + holderName() + " holds the counter)";
			return (!flag) ? StepStatus.Pass : StepStatus.Fail;
		});
	}

	protected BotStep NegoOp(string name, string op)
	{
		return Do(name, delegate
		{
			Net.ToServer("bot.nego.op", new BotNegoOp
			{
				op = op
			});
			return "bot.nego.op " + op + " sent";
		});
	}

	protected static GameItem Cig()
	{
		GraphNodeStorage[] array = (GraphNodeStorage[])(object)new GraphNodeStorage[2]
		{
			(GraphNodeStorage)E.frontInvinvElement,
			(GraphNodeStorage)E.backInvinvElement
		};
		for (int i = 0; i < array.Length; i++)
		{
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(array[i], (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (item != null && item.IsTag("CIGARETTE_TAG") && !GeneralHelper.IsItemOwned(item))
				{
					return item;
				}
			}
		}
		return null;
	}
}
