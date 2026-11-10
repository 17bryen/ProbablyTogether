using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DayNightProbes : BotProbeSet
{
	internal static int Loads;

	private static bool hooked;

	private static bool Shop
	{
		get
		{
			if (BotProbes.InShop)
			{
				return Game.ShopLoaded;
			}
			return false;
		}
	}

	public override void Register()
	{
		if (!hooked)
		{
			hooked = true;
			Snapshot.Loaded += delegate
			{
				Loads++;
			};
		}
		BotProbes.Add("dn.state", () => (!Shop) ? "" : PlayerStore.Instance.storeState.ToString());
		BotProbes.Add("dn.visit", () => (!Shop) ? "" : BotProbeSet.Str(PlayerStore.Instance.visitLeftTonight));
		BotProbes.Add("dn.queue", () => (!Shop) ? "" : BotProbeSet.Str(PlayerStore.Instance.storeClientManager?.clientStack?.Count ?? (-1)));
		BotProbes.Add("dn.client", delegate
		{
			if (Shop)
			{
				StoreClientInstance currentClientInstance = PlayerStore.Instance.currentClientInstance;
				return ((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier) ?? "none";
			}
			return "";
		});
		BotProbes.Add("dn.front", delegate
		{
			if (!Shop)
			{
				return "";
			}
			EmporiumEntry instance = EmporiumEntry.Instance;
			return Items((GraphNodeStorage)(object)((instance != null) ? instance.frontInvinvElement : null));
		});
		BotProbes.Add("dn.digest", () => (!Shop) ? "" : Replay.Digest().ToString("x16"));
		BotProbes.Add("dn.outing", () => (!Shop) ? "" : DayNightOuting.OutingHash().ToString("x16"));
		BotProbes.Add("dn.bag", () => (!Shop) ? "" : (Items((GraphNodeStorage)(object)EmporiumEntry.Instance?.afterhourInventory) + "|" + DayNightOuting.BagHash().ToString("x16")));
		BotProbes.Add("dn.loc", delegate
		{
			if (!Shop)
			{
				return "";
			}
			return (DayNightOuting.LocationId.Length <= 0) ? "none" : DayNightOuting.LocationId;
		});
		BotProbes.Add("dn.map", delegate
		{
			if (!Shop)
			{
				return "";
			}
			return (!DayNightOuting.MapOpen) ? "0" : "1";
		});
		BotProbes.Add("dn.scav", () => (!Shop) ? "" : BotProbeSet.Str(PlayerStore.Instance.scavengingAttempts));
		BotProbes.Add("dn.wound", () => (!Shop || PlayerStore.Instance.healthData == null) ? "" : (PlayerStore.Instance.healthData.woundState + "/" + (PlayerStore.Instance.healthData.isWoundedFresh ? 1 : 0)));
		BotProbes.Add("dn.ready", () => (DayNight.ReadyPlayers.Count != 0) ? string.Join(",", DayNight.ReadyPlayers.OrderBy((int x) => x)) : "none");
		BotProbes.Add("dn.runover", () => (!DayNightRun.RunOver) ? "running" : ("ended:" + DayNightRun.Ending));
		BotProbes.Add("dn.refusals", () => BotProbeSet.Str(DayNight.Refusals));
		BotProbes.Add("dn.mismatches", () => BotProbeSet.Str(DayNight.Mismatches));
		BotProbes.Add("dn.night", () => (!DayNight.NightRunning) ? "0" : "1");
		BotProbes.Add("dn.endless", delegate
		{
			if (!Shop)
			{
				return "";
			}
			return (!DayNightRun.Endless) ? "0" : "1";
		});
		BotProbes.Add("dn.live", () => (!Net.IsServer) ? "" : BotProbeSet.Str(Server.ByPlayer.Values.Count((Server.Peer p) => p.State == Server.PeerState.Ready)));
		BotProbes.Add("dn.lastref", delegate
		{
			if (!Net.IsServer)
			{
				return "";
			}
			return (DayNight.ServerLastRefused.Length <= 0) ? "none" : DayNight.ServerLastRefused;
		});
		BotProbes.Add("dn.votes", () => (DayNightRun.NewRunVotes.Count != 0) ? string.Join(",", DayNightRun.NewRunVotes.OrderBy((int x) => x)) : "none");
		BotProbes.Add("dn.stamp", delegate
		{
			if (!Shop)
			{
				return "";
			}
			return (!PlayerStore.Instance.isStampIntroduced) ? "0" : "1";
		});
		BotProbes.Add("dn.counter", () => BotProbeSet.Str(Locks.HeldBy("counter")));
		BotProbes.Add("dn.slot", delegate
		{
			if (!Net.IsServer)
			{
				return "";
			}
			int num = ServerMode.Config?.saveSlot ?? 1;
			int num2 = 0;
			string fileName = Path.GetFileName(SaveFiles.Slot(num));
			try
			{
				num2 = System.IO.Directory.GetFiles(Paths.Root, "ended_*_" + fileName).Length;
			}
			catch
			{
			}
			return (File.Exists(Paths.Under(SaveFiles.Slot(num))) ? "1" : "0") + "/" + num2;
		});
	}

	private static string Items(GraphNodeStorage g)
	{
		if (g == null)
		{
			return "none";
		}
		ulong num = 14695981039346656037uL;
		int num2 = 0;
		foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(g, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
		{
			num2++;
			string text = item.uniqueId + ":" + item.identifier + ";";
			foreach (char c in text)
			{
				num ^= c;
				num *= 1099511628211L;
			}
		}
		return num2 + ":" + (num & 0xFFFFFFFFu).ToString("x8");
	}
}
