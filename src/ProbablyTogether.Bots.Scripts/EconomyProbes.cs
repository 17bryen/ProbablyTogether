using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class EconomyProbes : BotProbeSet
{
	public override void Register()
	{
		EconomyLab.Wire();
		BotProbes.Add("econ.hash", () => (!Game.ShopLoaded) ? "" : Economy.Hash());
		BotProbes.Add("econ.parts", delegate
		{
			if (!Game.ShopLoaded)
			{
				return "";
			}
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, string> item in Economy.Parts())
			{
				list.Add(item.Key + ":" + item.Value.Substring(0, 6));
			}
			return string.Join(",", list);
		});
		BotProbes.Add("econ.digest", () => (!Game.ShopLoaded) ? "" : Replay.Digest().ToString("x16"));
		BotProbes.Add("econ.describe", () => (!Game.ShopLoaded) ? "" : Economy.Describe());
		string[] array = new string[5] { "BREWER", "SHOWCASE_I", "SEC_GRACE", "MARKETPLACE_REQUESTS", "DOSSIER_UPGRADE" };
		foreach (string text in array)
		{
			string k = text;
			BotProbes.Add("econ.up." + k, delegate
			{
				NetworkUpgrade val = EconomyLab.Up(k);
				return (val != null) ? (val.state + "/" + val.cost + "/" + val.cooldownCurrent) : "";
			});
		}
		BotProbes.Add("econ.race", delegate
		{
			NetworkUpgrade val = ((EconomyLab.RaceId != null) ? EconomyLab.Up(EconomyLab.RaceId) : null);
			return (val != null) ? (EconomyLab.RaceId + ":" + val.state) : "";
		});
		BotProbes.Add("econ.services", delegate
		{
			if (!Game.ShopLoaded)
			{
				return "";
			}
			List<string> list = new List<string>();
			string[] array2 = new string[3] { "JANITORIAL_SERVICE", "CRIMINEL_NETWORK", "CARTEL_PROTECTION" };
			foreach (string text2 in array2)
			{
				StoreService val = EconomyLab.Svc(text2);
				if (val != null)
				{
					list.Add(text2 + ":" + (val.unlocked ? 1 : 0) + (val.isUsing ? 1 : 0) + val.currentCooldown);
				}
			}
			return string.Join(",", list);
		});
		BotProbes.Add("econ.tokens", () => (!Game.ShopLoaded) ? "" : (Game.Store.commissaryData.redeemedCredit + "/" + Game.Store.commissaryData.GetStock("nutrifruit")));
		BotProbes.Add("econ.afterhour", delegate
		{
			GameGridInventory val = EmporiumEntry.Instance?.afterhourInventory;
			if (val == null)
			{
				return "";
			}
			List<string> list = new List<string>();
			foreach (GameItem item2 in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				list.Add(item2.identifier + "#" + item2.uniqueId);
			}
			return string.Join(",", list);
		});
		BotProbes.Add("econ.requests", delegate
		{
			List<StoreRequest> list = Game.Store?.storeRequests;
			if (list == null)
			{
				return "";
			}
			List<string> list2 = new List<string>();
			foreach (StoreRequest item3 in list)
			{
				if (item3 != null)
				{
					list2.Add(item3.id + ":" + item3.requiredItem + "x" + item3.requiredItemCount);
				}
			}
			return string.Join(",", list2);
		});
		BotProbes.Add("econ.days", () => (!Game.ShopLoaded) ? "" : Game.Store.daySinceLastBrowseUpgrade.ToString(CultureInfo.InvariantCulture));
		BotProbes.Add("econ.marks", () => string.Join(",", EconomyLab.Marks));
		BotProbes.Add("econ.refusals", () => Economy.Refusals.ToString(CultureInfo.InvariantCulture));
		BotProbes.Add("econ.counts", () => "applied " + Economy.Applied + " refused " + Economy.Refusals + " mismatch " + Economy.Mismatches + " blocked " + Economy.Blocked + " windows skipped " + Economy.WindowsSkipped + " game errors " + Economy.GameErrors);
		BotProbes.Add("econ.dpart", delegate
		{
			if (!Game.ShopLoaded)
			{
				return "";
			}
			Dictionary<string, string> dictionary = Digest.Parts();
			string value;
			return (dictionary == null || !dictionary.TryGetValue("economy", out value)) ? "missing" : value;
		});
		BotProbes.Add("core.digest", delegate
		{
			if (!Game.ShopLoaded)
			{
				return "";
			}
			MsgDigest msgDigest = Digest.Compute();
			string text2 = ((msgDigest.parts == null) ? "" : string.Join(",", from kv in msgDigest.parts.OrderBy<KeyValuePair<string, string>, string>((KeyValuePair<string, string> kv) => kv.Key, StringComparer.Ordinal)
				select kv.Key + "=" + ((kv.Value.Length > 8) ? kv.Value.Substring(0, 8) : kv.Value)));
			return "d" + msgDigest.day + " c" + msgDigest.cash + " f" + msgDigest.favor + " i" + msgDigest.items + " h" + msgDigest.hash.Substring(0, Math.Min(8, msgDigest.hash.Length)) + " " + msgDigest.state + " q" + msgDigest.queue + " [" + text2 + "]";
		});
		BotProbes.Add("econ.counter", () => (!Game.ShopLoaded) ? "" : EconomyLab.CounterId());
		BotProbes.Add("econ.head", () => (!Game.ShopLoaded) ? "" : EconomyLab.QueueHead());
		BotProbes.Add("econ.rent", delegate
		{
			PlayerStore store = Game.Store;
			return (store != null && Game.ShopLoaded) ? (store.rentValue + "+" + store.temporaryRent + "/" + store.dayUntilRent) : "";
		});
		BotProbes.Add("econ.georgio", delegate
		{
			PlayerStore store = Game.Store;
			return (store != null && Game.ShopLoaded) ? (store.loanOfferedToGeorgio + "@" + store.dayWhenLoanOfferedGeorgio) : "";
		});
		BotProbes.Add("econ.permit", delegate
		{
			PlayerStore store = Game.Store;
			return (store != null && Game.ShopLoaded) ? ((store.hasBusinessPermit ? "1" : "0") + "/" + store.storeClientData?.businessPermitOfficialState) : "";
		});
		BotProbes.Add("econ.intro", delegate
		{
			PlayerStore store = Game.Store;
			return (store == null || !Game.ShopLoaded) ? "" : ((store.isNetworkIntroduced ? "1" : "0") + "/" + store.networkIntroducedDay + "/" + (Economy.IntroBrowsing ? "browsing" : "-") + "/" + (StorePhoneClient.GetWildPhoneClient()?.phoneState).ToString());
		});
		BotProbes.Add("econ.owned", () => (!Game.ShopLoaded) ? "" : EconomyLab.OwnedList("joe_card", "scav_token", "handnote", "business_permit"));
		BotProbes.Add("econ.lock", () => (!Game.ShopLoaded) ? "" : Locks.HeldBy("outing.commissary").ToString(CultureInfo.InvariantCulture));
		BotProbes.Add("econ.loc", delegate
		{
			if (!Game.ShopLoaded)
			{
				return "";
			}
			return (DayNightOuting.LocationId.Length <= 0) ? "home" : DayNightOuting.LocationId;
		});
		BotProbes.Add("econ.runover", () => (!DayNightRun.RunOver) ? "running" : ("ended:" + DayNightRun.Ending));
		BotProbes.Add("econ.windows", () => (Economy.NetworkWindowOpen ? "1" : "0") + "|" + (Economy.CommissaryOpenedByGame ? "1" : "0"));
		BotProbes.Add("econ.closedrefusals", () => Economy.ClosedWindowRefusals.ToString(CultureInfo.InvariantCulture));
	}
}
