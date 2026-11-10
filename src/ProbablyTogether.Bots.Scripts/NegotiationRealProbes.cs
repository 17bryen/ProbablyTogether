using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class NegotiationRealProbes : BotProbeSet
{
	private static bool wired;

	internal static int OpSerial;

	internal static readonly Dictionary<string, long[]> Made = new Dictionary<string, long[]>();

	internal static string LastResult = "";

	private static readonly FieldInfo tabletFrame = AccessTools.Field(typeof(NegociationUIManager), "frame");

	public override void Register()
	{
		BotProbes.Add("nego.cust", () => Negotiation.CurrentCustomer()?.identifier ?? "none");
		BotProbes.Add("nego.arrived", () => (!PlayerStore.HasInstance || !PlayerStore.Instance.isClientArrived) ? "0" : "1");
		BotProbes.Add("nego.hash", delegate
		{
			try
			{
				return Negotiation.Hash().ToString("x16");
			}
			catch
			{
				return "!";
			}
		});
		BotProbes.Add("nego.tablet", () => (!TabletOpen()) ? "0" : "1");
		BotProbes.Add("nego.tskip", () => BotProbeSet.Str(Negotiation.TabletSkipped));
		BotProbes.Add("nego.gate", () => Negotiation.LastTabletGate);
		BotProbes.Add("nego.confirms", () => BotProbeSet.Str(Negotiation.ConfirmsAsked));
		BotProbes.Add("nego.clicks", () => BotProbeSet.Str(Negotiation.LabelerClicks));
		BotProbes.Add("nego.ops", () => BotProbeSet.Str(OpSerial) + " " + LastResult);
		BotProbes.Add("nego.counter", delegate
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			return ((Object)(object)instance == (Object)null) ? "" : (Rows((GraphNodeStorage)(object)instance.backInvinvElement) + " | " + Rows((GraphNodeStorage)(object)instance.frontInvinvElement));
		});
		BotProbes.Add("nego.set", () => BotProbeSet.Str(Negotiation.CurrentCustomer()?.barterOffer?.currentSetIndex ?? (-1)));
		BotProbes.Add("nego.patience", () => BotProbeSet.Str(Negotiation.CurrentCustomer()?.clientImpatience ?? (-1)));
		BotProbes.Add("nego.state", () => (!PlayerStore.HasInstance) ? "" : PlayerStore.Instance.storeState.ToString());
		BotProbes.Add("nego.queue", delegate
		{
			List<StoreClient> list = Game.Store?.storeClientManager?.clientStack;
			return (list != null) ? (list.Count + ":" + ((list.Count <= 0) ? "none" : list[0]?.identifier)) : "";
		});
		BotProbes.Add("nego.dlg", () => (DialogueSystem.Counter?.Current != null) ? DialogueSync.CurrentFp("counter") : "none");
		if (!wired)
		{
			wired = true;
			Net.On<BotNegoOp>("bot.nego.op", ServerOp, ClientOp);
		}
	}

	internal static bool TabletOpen()
	{
		NegociationUIManager instance = NegociationUIManager.Instance;
		if ((Object)(object)instance != (Object)null && (Object)(object)instance.uiObject != (Object)null)
		{
			return instance.uiObject.activeSelf;
		}
		return false;
	}

	internal static bool ConfirmOpen()
	{
		NegociationUIManager instance = NegociationUIManager.Instance;
		NegotiationTabletFrame val = ((Object)(object)instance != (Object)null && tabletFrame != null) ? (tabletFrame.GetValue(instance) as NegotiationTabletFrame) : null;
		if ((Object)(object)val != (Object)null && (Object)(object)val.confirmRoot != (Object)null)
		{
			return val.confirmRoot.activeSelf;
		}
		return false;
	}

	internal static bool HasFrame()
	{
		NegociationUIManager instance = NegociationUIManager.Instance;
		if ((Object)(object)instance != (Object)null && tabletFrame != null)
		{
			return tabletFrame.GetValue(instance) != null;
		}
		return false;
	}

	private static string Rows(GraphNodeStorage root)
	{
		if (root == null)
		{
			return "";
		}
		List<string> list = new List<string>();
		foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(root, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
		{
			if ((object)item.parentInventory == root)
			{
				list.Add(item.uniqueId + ":" + item.identifier + ":" + (GeneralHelper.IsItemOwned(item) ? "own" : "cust") + ":" + BotProbeSet.Str((int)item.GetNegociatedValue()));
			}
		}
		list.Sort(StringComparer.Ordinal);
		if (list.Count != 0)
		{
			return string.Join(" ", list);
		}
		return "empty";
	}

	private static void ServerOp(BotNegoOp m, int from)
	{
		if (!Net.IsServer || m == null || m.op == null || !Negotiation.ShopUp)
		{
			return;
		}
		if (m.op == "queue")
		{
			string text = (LastResult = Customers.BotDo("add", m.arg));
			BotLog.Info("nego3", "queue " + m.arg + " for " + Net.PlayerName(from) + ": " + text);
			return;
		}
		List<GameItem> list = new List<GameItem>();
		Seed seed;
		string text2;
		using (Replay.Server(out seed))
		{
			text2 = Run(m.op, m.arg, m.n, list);
		}
		m.seed = seed;
		m.created = ((IEnumerable<GameItem>)list).Select((Func<GameItem, long>)((GameItem x) => x.uniqueId)).ToArray();
		m.result = text2;
		m.serial = ++OpSerial;
		LastResult = text2;
		Made[m.op + ":" + m.arg] = m.created;
		if (m.op == "label" && m.created.Length != 0)
		{
			NegotiationProbes.LabelItem = m.created[0];
		}
		Net.ToAll("bot.nego.op", m);
		BotLog.Info("nego3", "op " + m.op + " " + m.arg + " x" + m.n + " for " + Net.PlayerName(from) + ": " + text2 + "; ids " + string.Join(",", m.created));
	}

	private static void ClientOp(BotNegoOp m)
	{
		if (!Net.IsClient || m == null || m.op == null || !Negotiation.ShopUp)
		{
			return;
		}
		List<GameItem> list = new List<GameItem>();
		string text;
		using (Replay.Client(m.seed))
		{
			text = Run(m.op, m.arg, m.n, list);
		}
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			if (m.created == null)
			{
				break;
			}
			if (i >= m.created.Length)
			{
				break;
			}
			if (list[i].uniqueId != m.created[i])
			{
				Ids.Bind(list[i], m.created[i]);
				num++;
			}
		}
		OpSerial = m.serial;
		LastResult = text;
		Made[m.op + ":" + m.arg] = m.created ?? new long[0];
		if (m.op == "label" && m.created != null && m.created.Length != 0)
		{
			NegotiationProbes.LabelItem = m.created[0];
		}
		BotLog.Info("nego3", "op " + m.op + " " + m.arg + ": " + text + " here, server " + m.result + "; " + list.Count + " item(s), " + num + " bound (0 = the seeded replay made the same ids)");
	}

	private static string Run(string op, string arg, int n, List<GameItem> made)
	{
		PlayerStore instance = PlayerStore.Instance;
		EmporiumEntry instance2 = EmporiumEntry.Instance;
		switch (op)
		{
		case "unlock":
			instance.isBargainIntroduced = true;
			return "bargain introduced";
		case "augintro":
			instance.isAugIntroduced = true;
			return "augments introduced";
		case "stock":
		{
			for (int num = 0; num < Math.Max(1, Math.Min(n, 6)); num++)
			{
				GameItem val7 = ItemSpawner.Spawn(arg);
				if (val7 == null)
				{
					return "no item " + arg;
				}
				GeneralHelper.SetItemOwned(val7, true);
				GraphUtils.TryAcceptAll((GraphNodeStorage)(object)instance2.invElement, val7, -1);
				made.Add(val7);
			}
			return "stocked " + made.Count + " " + arg;
		}
		case "label":
		{
			GameItem val3 = ItemSpawner.Spawn("flashlight");
			GeneralHelper.SetItemOwned(val3, true);
			ItemCondition val4 = ItemConditionList.conditionRusted[1];
			ItemFeature val5 = new ItemFeature("CATEGORY_RUSTED")
			{
				identifier = "br_bot_rust"
			};
			val5.SetConditions(val4, val4);
			val3.AddItemFeature(val5);
			GraphUtils.TryAcceptAll((GraphNodeStorage)(object)instance2.invElement, val3, -1);
			made.Add(val3);
			StoreEvent val6 = EventListNormal.CreateSeedHeist();
			NegotiationProbes.EventGuids = string.Join(",", val6.negociationDatas.Select((NegociationData d) => d.identifier));
			return "rusted flashlight, event guids " + NegotiationProbes.EventGuids;
		}
		case "rank":
		{
			AppraisalItemType lowestRankType = AppraisalItemRegistry.GetLowestRankType();
			for (int num2 = 0; num2 <= 10; num2++)
			{
				if (lowestRankType == null)
				{
					break;
				}
				if (AppraisalItemRegistry.CanBlock(lowestRankType, num2))
				{
					instance.appraiserLevel = num2;
					break;
				}
			}
			instance.appraisalBlacklist = new List<string>();
			return "appraiser level " + instance.appraiserLevel + " (the lowest type can be turned away)";
		}
		case "gun":
		{
			GunOrderData val = Negotiation.CurrentCustomer()?.gunOrderData;
			if (val == null)
			{
				return "no gun order";
			}
			GameItem val2;
			if (val.isServiceOrder)
			{
				val2 = val.GetPlacedServiceGun();
			}
			else
			{
				val2 = PreBuiltItemHelper.CreateHandmadeGun((ItemFeatureList.EquipmentCondition)2);
				GeneralHelper.SetItemOwned(val2, true);
				GraphUtils.TryAcceptAll((GraphNodeStorage)(object)instance2.invElement, val2, -1);
				made.Add(val2);
			}
			if (val2 == null)
			{
				return "no service gun";
			}
			List<string> list = new List<string>();
			foreach (GunOrderRequirement mandatoryRequirement in val.mandatoryRequirements)
			{
				int v = (mandatoryRequirement.isCaliber ? mandatoryRequirement.value : (mandatoryRequirement.isMustBeLargerThan ? (mandatoryRequirement.value + 1) : (mandatoryRequirement.value - 1)));
				val2.ModifyTag(mandatoryRequirement.id, (Action<TagState>)delegate(TagState t)
				{
					t.SetInt(v).SetEnabled(true);
				}, true);
				list.Add(mandatoryRequirement.id.Replace("GUN_CURRENT_", "").Replace("_TAG", "") + "=" + v);
			}
			return (val.isServiceOrder ? "service gun " : "gun ") + val2.uniqueId + " " + string.Join(",", list) + " accepted " + val.IsAccepted();
		}
		default:
			return "unknown op " + op;
		}
	}
}
