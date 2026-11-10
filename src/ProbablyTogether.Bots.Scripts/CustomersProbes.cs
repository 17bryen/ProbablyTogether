using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CustomersProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("cust.digest", () => (Game.Store?.storeClientManager != null) ? Customers.Hash().ToString("x16") : "");
		BotProbes.Add("cust.queue", () => BotProbeSet.Str(Game.Store?.storeClientManager?.clientStack?.Count));
		BotProbes.Add("cust.head", delegate
		{
			List<StoreClient> list = Game.Store?.storeClientManager?.clientStack;
			object obj;
			if (list != null)
			{
				if (list.Count == 0)
				{
					return "none";
				}
				obj = list[0]?.identifier;
				if (obj == null)
				{
					return "null";
				}
			}
			else
			{
				obj = "";
			}
			return (string)obj;
		});
		BotProbes.Add("cust.current", delegate
		{
			object obj;
			if (Game.Store != null)
			{
				StoreClientInstance currentClientInstance = Game.Store.currentClientInstance;
				obj = ((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier);
				if (obj == null)
				{
					return "none";
				}
			}
			else
			{
				obj = "";
			}
			return (string)obj;
		});
		BotProbes.Add("cust.sprite", delegate
		{
			PlayerStore store = Game.Store;
			object obj;
			if (store == null)
			{
				obj = null;
			}
			else
			{
				StoreClientInstance currentClientInstance = store.currentClientInstance;
				obj = ((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.spriteName);
			}
			if (obj == null)
			{
				obj = "none";
			}
			return (string)obj;
		});
		BotProbes.Add("cust.visit", () => (Game.Store != null) ? BotProbeSet.Str(Customers.Visit) : "");
		BotProbes.Add("cust.seq", () => (Game.Store != null) ? BotProbeSet.Str(Customers.VisitSeq()) : "");
		BotProbes.Add("cust.holder", () => BotProbeSet.Str(Locks.HeldBy("counter")));
		BotProbes.Add("cust.arrived", () => (Game.Store != null) ? ((!Game.Store.isClientArrived) ? "0" : "1") : "");
		BotProbes.Add("cust.state", () => (Game.Store != null) ? Game.Store.storeState.ToString() : "");
		BotProbes.Add("cust.flag", () => (Game.Store != null) ? BotProbeSet.Str(Game.Store.lockNextBellFlag) : "");
		BotProbes.Add("cust.last", delegate
		{
			object obj;
			if (Game.Store != null)
			{
				StoreClientInstance lastVisitedClient = Game.Store.lastVisitedClient;
				obj = ((lastVisitedClient == null) ? null : lastVisitedClient.GetClientBlueprint()?.identifier);
				if (obj == null)
				{
					return "none";
				}
			}
			else
			{
				obj = "";
			}
			return (string)obj;
		});
		BotProbes.Add("cust.refusals", () => BotProbeSet.Str(Customers.Refusals));
		BotProbes.Add("cust.mismatch", () => BotProbeSet.Str(Customers.Mismatches));
		BotProbes.Add("cust.applied", () => BotProbeSet.Str(Customers.Applied));
		BotProbes.Add("cust.dismissable", () => (Game.Store != null) ? ((!Game.Store.IsClientDimissable()) ? "0" : "1") : "");
		BotProbes.Add("cust.mk", () => string.Join(",", Customers.BotMarkNames));
		BotProbes.Add("cust.botdone", () => BotProbeSet.Str(Customers.BotDone) + "/" + BotProbeSet.Str(Customers.BotFailed));
		BotProbes.Add("cust.ready", () => Net.IsServer ? BotProbeSet.Str(Server.ByPlayer.Values.Count((Server.Peer p) => p.State == Server.PeerState.Ready)) : "");
		BotProbes.Add("cust.action", () => (!Game.ShopLoaded) ? "" : Replay.Digest().ToString("x16"));
		BotProbes.Add("cust.stock", () => (!Game.ShopLoaded) ? "" : CustomersBot.StockText());
		BotProbes.Add("cust.dm", () => (Game.Store != null) ? (BotProbeSet.Str(Game.Store.dealMakerBargainBonus) + (Game.Store.dealMakerActivatedThisCustomer ? "+" : "-")) : "");
		BotProbes.Add("cust.dlgseq", () => BotProbeSet.Str(DialogueSync.Seq("counter")));
		BotProbes.Add("cust.dlgfp", () => DialogueSync.CurrentFp("counter") ?? "-");
		BotProbes.Add("cust.dlgon", () => (DialogueSystem.Counter.Current == null) ? "0" : "1");
		BotProbes.Add("cust.started", delegate
		{
			PlayerStore store = Game.Store;
			if (store != null)
			{
				StoreClientInstance currentClientInstance = store.currentClientInstance;
				if (((currentClientInstance == null) ? ((bool?)null) : currentClientInstance.GetClientBlueprint()?.isMainDialogueStarted) == true)
				{
					return "1";
				}
			}
			return "0";
		});
		BotProbes.Add("cust.dn", () => BotProbeSet.Str(DayNight.Mismatches));
		BotProbes.Add("cust.tool", () => (!Game.ShopLoaded) ? "" : CustomersBot.ToolStateText());
	}
}
