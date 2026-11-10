using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class FullDayProbes : BotProbeSet
{
	public override void Register()
	{
		FullDayLab.Wire();
		BotProbes.Add("fd.core", FullDayLab.Core);
		BotProbes.Add("fd.parts", FullDayLab.Parts);
		BotProbes.Add("fd.cust", () => (!Game.ShopLoaded) ? "" : Customers.Hash().ToString("x16"));
		BotProbes.Add("fd.items", () => (!Game.ShopLoaded) ? "" : FullDayLab.Fnv(ItemsMove.Digest()).ToString("x16"));
		BotProbes.Add("fd.econ", () => (!Game.ShopLoaded) ? "" : Economy.Hash());
		BotProbes.Add("fd.dlg", () => (!Game.ShopLoaded) ? "" : FullDayLab.Fnv(FullDayLab.Dlg()).ToString("x16"));
		BotProbes.Add("fd.phone", () => (!Game.ShopLoaded) ? "" : ("line=" + DialogueRefs.Fp(DialogueSystem.Phone.Current) + "|caller=" + (PhoneChannel.Caller?.dialogFuncId ?? "-") + "|talking=" + (PhoneChannel.Talking ? 1 : 0)));
		BotProbes.Add("fd.sec", () => (!Game.ShopLoaded) ? "" : SecurityState.Fingerprint(SecurityState.Capture()));
		BotProbes.Add("fd.mach", delegate
		{
			GameItem val = FullDayLab.Machine();
			if (val != null)
			{
				return Machines.Digest(val).ToString("x16") + ":" + FullDayLab.Box();
			}
			return (!Game.ShopLoaded) ? "" : "none";
		});
		BotProbes.Add("fd.replay", () => (!Game.ShopLoaded) ? "" : Replay.Digest().ToString("x16"));
		BotProbes.Add("fd.counter", FullDayLab.Counter);
		BotProbes.Add("fd.holder", () => BotProbeSet.Str(FullDayLab.Holder()));
		BotProbes.Add("fd.customer", () => (!Game.ShopLoaded) ? "" : FullDayLab.CurrentId());
		BotProbes.Add("fd.arrived", delegate
		{
			if (!Game.ShopLoaded)
			{
				return "";
			}
			return (!Game.Store.isClientArrived) ? "0" : "1";
		});
		BotProbes.Add("fd.queue", () => (!Game.ShopLoaded) ? "" : BotProbeSet.Str(Game.Store.storeClientManager?.clientStack?.Count ?? (-1)));
		BotProbes.Add("fd.head", delegate
		{
			List<StoreClient> list = ((!Game.ShopLoaded) ? null : Game.Store.storeClientManager?.clientStack);
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
		BotProbes.Add("fd.visit", () => (!Game.ShopLoaded) ? "" : BotProbeSet.Str(Customers.Visit));
		BotProbes.Add("fd.state", () => (!Game.ShopLoaded) ? "" : Game.Store.storeState.ToString());
		BotProbes.Add("fd.fx", () => BotProbeSet.Str(FullDayLab.Fx));
		BotProbes.Add("fd.marks", () => string.Join(",", FullDayLab.Marks));
		BotProbes.Add("fd.live", () => (!Net.IsServer) ? "" : BotProbeSet.Str(Server.ByPlayer.Values.Count((Server.Peer p) => p.State == Server.PeerState.Ready)));
		BotProbes.Add("fd.ready", () => (DayNight.ReadyPlayers.Count != 0) ? string.Join(",", DayNight.ReadyPlayers.OrderBy((int x) => x)) : "none");
		BotProbes.Add("fd.night", () => (!DayNight.NightRunning) ? "0" : "1");
		BotProbes.Add("fd.events", FullDayLab.EventRows);
		BotProbes.Add("fd.services", () => (!Game.ShopLoaded || Game.Store.storeService == null) ? "" : string.Join(",", from x in Game.Store.storeService
			where x?.unlocked ?? false
			select x.id + (x.isUsing ? "+" : "-")));
		BotProbes.Add("fd.upgrades", delegate
		{
			Dictionary<string, NetworkUpgrade> dictionary = (Game.ShopLoaded ? Game.Store.networkUpgrade : null);
			if (dictionary == null)
			{
				return "";
			}
			List<string> list = (from kv in dictionary
				where kv.Value != null && kv.Value.IsUnlocked()
				select kv.Key).OrderBy<string, string>((string k) => k, StringComparer.Ordinal).ToList();
			return (list.Count != 0) ? string.Join(",", list) : "none";
		});
	}
}
