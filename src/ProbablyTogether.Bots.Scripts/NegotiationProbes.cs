using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class NegotiationProbes : BotProbeSet
{
	private static bool wired;

	internal static long LabelItem;

	internal static string EventGuids = "";

	internal static int LoadedCount;

	internal static readonly List<string> Marks = new List<string>();

	internal static readonly List<string> RefusedKinds = new List<string>();

	public override void Register()
	{
		BotProbes.Add("nego.who", () => Negotiation.CurrentCustomer()?.identifier ?? "none");
		BotProbes.Add("nego.intent", () => Negotiation.CurrentCustomer()?.clientIntent.ToString() ?? "none");
		BotProbes.Add("nego.budget", delegate
		{
			StoreClient val = Negotiation.CurrentCustomer();
			return (val != null) ? ((!val.useClientBudget) ? "no-budget" : BotProbeSet.Str(val.GetBudget())) : "none";
		});
		BotProbes.Add("nego.complete", delegate
		{
			StoreClient val = Negotiation.CurrentCustomer();
			return (val != null) ? (val.isBusinessComplete + " wholesale " + val.isOfferWholesale) : "none";
		});
		BotProbes.Add("nego.guids", () => EventGuids);
		BotProbes.Add("nego.holder", () => BotProbeSet.Str(Locks.HeldBy("counter")));
		BotProbes.Add("nego.back", delegate
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			return Counter((GraphNodeStorage)(object)((instance != null) ? instance.backInvinvElement : null));
		});
		BotProbes.Add("nego.front", delegate
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			return Counter((GraphNodeStorage)(object)((instance != null) ? instance.frontInvinvElement : null));
		});
		BotProbes.Add("nego.sold", delegate
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			return BotProbeSet.Str((((instance != null) ? instance.soldElement : null) != null) ? GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)EmporiumEntry.Instance.soldElement, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Count : (-1));
		});
		BotProbes.Add("nego.rep", Rep);
		BotProbes.Add("nego.bargain", Bargain);
		BotProbes.Add("nego.label", Label);
		BotProbes.Add("nego.name", delegate
		{
			GameItem val = ((LabelItem != 0L) ? Negotiation.Find(LabelItem) : null);
			return (val != null) ? ((!GeneralHelper.HasCustomName(val)) ? "-" : GeneralHelper.GetCustomName(val)) : "none";
		});
		BotProbes.Add("nego.names", delegate
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			GameGridInventory val = ((instance != null) ? instance.invElement : null);
			if (val == null)
			{
				return "";
			}
			List<string> list = (from x in ((IEnumerable<GameItem>)GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null)).Where((Func<GameItem, bool>)GeneralHelper.HasCustomName)
				select Ids.Of(x) + "=" + GeneralHelper.GetCustomName(x)).ToList();
			list.Sort(StringComparer.Ordinal);
			return (list.Count != 0) ? string.Join(" ", list) : "none";
		});
		BotProbes.Add("nego.career", () => (!PlayerStore.HasInstance) ? "" : Career());
		BotProbes.Add("nego.digest", delegate
		{
			try
			{
				return Replay.Digest().ToString("x");
			}
			catch
			{
				return "!";
			}
		});
		BotProbes.Add("nego.marks", () => string.Join(",", Marks));
		if (wired)
		{
			return;
		}
		wired = true;
		Net.On("bot.nego.mark", delegate(BotNegoMark m, int from)
		{
			if (m != null && m.mark != null && m.mark.Length < 64)
			{
				Marks.Add(m.mark);
				BotLog.Info("nego", "mark " + m.mark + " from " + Net.PlayerName(from));
			}
		}, null);
		Negotiation.Refused += delegate(string kind, string reason)
		{
			RefusedKinds.Add(kind);
			BotLog.Info("nego", "refused " + kind + ": " + reason);
		};
		Snapshot.Loaded += delegate
		{
			LoadedCount++;
		};
	}

	private static string Counter(GraphNodeStorage root)
	{
		if (root == null)
		{
			return "";
		}
		List<string> list = new List<string>();
		foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(root, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
		{
			list.Add(Ids.Of(item) + ":" + item.identifier + ":" + (GeneralHelper.IsItemOwned(item) ? "own" : "cust") + ":" + BotProbeSet.Str((int)item.GetNegociatedValue()));
		}
		list.Sort(StringComparer.Ordinal);
		if (list.Count != 0)
		{
			return string.Join(" ", list);
		}
		return "empty";
	}

	private static string Rep()
	{
		if (!PlayerStore.HasInstance || PlayerStore.Instance.storeReputations == null)
		{
			return "";
		}
		return string.Join(" ", from r in PlayerStore.Instance.storeReputations
			where r?.factionId != null
			select r.factionId.Replace("FACTION_", "") + "=" + r.GetReputationExact().ToString("0.0000", CultureInfo.InvariantCulture));
	}

	private static string Bargain()
	{
		BargainUIManager instance = BargainUIManager.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return "none";
		}
		return "disc " + instance.DiscountOffered + "/" + instance.DiscountPercent + " mark " + instance.MarkupOffered + "/" + instance.MarkupPercent + " bmark " + instance.BuyingMarkupOffered + "/" + instance.BuyingMarkupPercent + " bdisc " + instance.BuyingDiscountOffered + " black " + instance.BlackmailOffered + " feat " + Features();
	}

	private static string Features()
	{
		string[] source = new string[8] { "bargainDiscount", "bargainMarkup", "bargainBuyingMarkup", "bargainBuyingDiscount", "blackmailDiscount", "blackmailMarkup", "threatenDiscount", "threatenMarkup" };
		return string.Join("", source.Select((string i) => (!Negotiation.HasFeatureOnCounter(i)) ? "" : (i.Substring(0, 1) + i.Length)));
	}

	private static string Label()
	{
		GameItem val = ((LabelItem != 0L) ? Negotiation.Find(LabelItem) : null);
		if (val?.itemFeatures == null)
		{
			return "none";
		}
		ItemFeature val2 = ((IEnumerable<ItemFeature>)val.itemFeatures).FirstOrDefault((Func<ItemFeature, bool>)((ItemFeature x) => x != null && x.category == "CATEGORY_RUSTED"));
		if (val2 == null)
		{
			return "no-label";
		}
		return "fake " + val2.fakeCondition?.identifier + " real " + val2.realCondition?.identifier + " exposed " + val2.IsExposed() + " water " + val2.customIntValue1 + " value " + BotProbeSet.Str((int)val.GetNegociatedValue());
	}

	internal static string Career()
	{
		PlayerStore instance = PlayerStore.Instance;
		return "lvl " + instance.appraiserLevel + " prog " + instance.appraisalProgress.ToString("0.####", CultureInfo.InvariantCulture) + " day " + instance.appraisalPromotionDay + " decl " + instance.appraisalPromotionDeclined + " intro " + instance.isAppraisalIntroduced + " block " + string.Join("+", instance.appraisalBlacklist ?? new List<string>());
	}
}
