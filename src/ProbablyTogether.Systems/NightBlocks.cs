using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether.Systems;

public sealed class MsgNightBlock
{
	public int day;

	public string name;

	public Seed seed;

	public Tape tape;

	public int uidAfter;

	/// The fingerprint after the block (NightCheck lines; key 0 is the shop's own values).
	public List<string> items;
}

/// Runs EndNight in blocks: deterministic blocks run under shared dice and an id tape, items in id order.
/// The server sends dice, tape and fingerprint; player copies run the block and compare. Only on game 0.46D.
internal static class NightBlocks
{
	internal const string ForGame = "0.46D";

	private static bool inited;

	private static bool running;

	internal static int Same;

	internal static int Differ;

	internal static int Missing;

	internal static int Extra;

	internal static int Runs;

	internal static string LastSummary = "";

	internal static int S1Bad;

	internal static int S2Runs;

	internal static int S2Bad;

	internal static string S2Summary = "";

	internal static bool On => Net.IsOnline && Net.IsServer && Application.version == ForGame;

	[SystemInit]
	private static void Init()
	{
		if (inited)
		{
			return;
		}
		inited = true;
		Net.On<MsgNightBlock>("night.block", null, ClientBlock);
	}

	/// Calls the game's mod hooks as its own night does.
	private static void Hook(string name)
	{
		try
		{
			AccessTools.Method(typeof(ModHook), name, Type.EmptyTypes, (Type[])null)?.Invoke(null, null);
		}
		catch (Exception ex)
		{
			Log.Warn("night: the game's hook " + name + " threw: " + (ex.InnerException ?? ex).Message);
		}
	}

	private static List<GameItem> Sorted(List<GameItem> items)
	{
		return (from i in items
			where i != null
			orderby i.uniqueId
			select i).ToList();
	}

	/// The night's first block that comes out the same on every copy.
	private static void BlockS1(PlayerStore ps)
	{
		ps.projectorAttractivenessBonus = 0;
		ReactorHelper.OnNight();
		ps.UpdatePowerState();
		CurrentConverterHelper.isNightCycle = true;
		foreach (GameItem item in Sorted(ps.FindAllItem()))
		{
			item.onCycleEndEarlySlotItemFunc?.Invoke(item, item.parentInventory, item.GetIncompleteSlotInInventory());
		}
	}

	/// The night's second shared block: chemistry, fridges, hoppers, every item's end-of-night and late step.
	private static void BlockS2(PlayerStore ps)
	{
		ChemicalHelper.RunNightlyChemicalSimulation();
		MachineFridge.ClearColdMarkers();
		HopperHelper.RunWindow(1);
		foreach (GameItem item in Sorted(ps.FindAllItem()))
		{
			StolenHelper.UpdateStolenItem(item);
			item.onCycleEndSlotItemFunc?.Invoke(item, item.parentInventory, item.GetIncompleteSlotInInventory());
		}
		HopperHelper.RunWindow(2);
		foreach (GameItem item2 in Sorted(ps.FindAllItem()))
		{
			item2.onCycleEndLateSlotItemFunc?.Invoke(item2, item2.parentInventory, item2.GetIncompleteSlotInInventory());
		}
		HopperHelper.RunWindow(3);
		CurrentConverterHelper.isNightCycle = false;
		MachineFridge.HandleColdFeatureExpiry();
	}

	/// The item's parent item, or null if it is in one of the shop's storages.
	private static GameItem ParentItem(GameItem it)
	{
		GameInventory parentInventory = it?.parentInventory;
		if (parentInventory?.parents == null || parentInventory.parents.Count == 0)
		{
			return null;
		}
		return parentInventory.parents[0] as GameItem;
	}

	private static void SendDelta(Dictionary<int, string> before)
	{
		try
		{
			Dictionary<int, string> dictionary = NightCheck.Lines();
			Dictionary<int, GameItem> dictionary2 = new Dictionary<int, GameItem>();
			foreach (GameItem item in Items.All())
			{
				if (item != null && item.uniqueId != 0 && !dictionary2.ContainsKey(item.uniqueId))
				{
					dictionary2[item.uniqueId] = item;
				}
			}
			HashSet<int> hashSet = new HashSet<int>();
			foreach (KeyValuePair<int, string> item2 in dictionary)
			{
				if (!before.TryGetValue(item2.Key, out var value) || value != item2.Value)
				{
					hashSet.Add(item2.Key);
				}
			}
			List<ItemsOp> list = new List<ItemsOp>();
			int num = 0;
			foreach (int item3 in hashSet.OrderBy((int x) => x))
			{
				if (!dictionary2.TryGetValue(item3, out var value2))
				{
					continue;
				}
				// Only the outermost changed item: its rebuilt copy holds what changed inside it.
				bool flag = false;
				for (GameItem gameItem = ParentItem(value2); gameItem != null; gameItem = ParentItem(gameItem))
				{
					if (hashSet.Contains(gameItem.uniqueId))
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					Subtree subtree = Items.Encode(value2);
					if (subtree != null)
					{
						list.Add(new ItemsOp
						{
							t = "sub",
							i = Ids.Of(value2),
							to = Items.RefOf((GraphNodeStorage)(object)value2.parentInventory),
							sub = subtree
						});
					}
				}
			}
			foreach (int key in before.Keys.OrderBy((int x) => x))
			{
				if (key != 0 && !dictionary.ContainsKey(key))
				{
					num++;
					list.Add(new ItemsOp
					{
						t = "destroy",
						i = key
					});
				}
			}
			DeltaItems = list.Count;
			ItemsMove.BroadcastOps("night.delta", list);
			Log.Info("night: the host's own steps changed " + hashSet.Count + " item(s) and removed " + num + "; " + list.Count + " step(s) sent");
		}
		catch (Exception ex)
		{
			Log.Warn("night: sending the night's changes threw: " + ex.Message);
		}
	}

	internal static int DeltaItems;

	/// The shop's own values a block changes, as a fingerprint line.
	private static string ShopLine(PlayerStore ps)
	{
		return "0|shop|1|-|power=" + (ps.isPowerOn ? "1" : "0") + ",projector=" + ps.projectorAttractivenessBonus.ToString(CultureInfo.InvariantCulture) + ",nightCycle=" + (CurrentConverterHelper.isNightCycle ? "1" : "0") + ",uid=" + ps.currentUniqueId.ToString(CultureInfo.InvariantCulture);
	}

	private static Dictionary<int, string> Fingerprint(PlayerStore ps)
	{
		Dictionary<int, string> dictionary = NightCheck.Lines();
		dictionary[0] = ShopLine(ps);
		return dictionary;
	}

	/// Server: the game's EndNight step for step, block S1 under shared dice.
	internal static void EndNight(PlayerStore ps)
	{
		running = true;
		try
		{
			if (ps.currentClientInstance != null)
			{
				StoreUIManager.Instance.HideClient();
				ps.DismissCurrentClient(callNextWhenPossible: false);
			}
			StoreUIManager.Instance.ClearArrestState();
			RunStats.OnDayEnded(StoreStation.Instance.dayCounter, ps.GetTotalEstimatedValue(), ps.playerCash);
			Hook("FireOnGoingSleepEarly");
			// Push containers now: they cooled during the fade, and nothing ticks until the block ends.
			Machines.PushNow();
			Seed seed;
			Tape tape;
			using (Replay.Server(out seed, -1))
			{
				using (Items.Record(out tape))
				{
					BlockS1(ps);
				}
			}
			Send("s1", seed, tape, ps);
			Dictionary<int, string> afterS1 = NightCheck.Lines();
			List<string> list = new List<string> { "INSURANCE_SERVICE", "BUY_REFERAL", "SELL_REFERAL", "WILD_FIXER", "CRIMINEL_NETWORK" };
			foreach (StoreService item in ps.storeService)
			{
				if (item.unlocked && item.isUsing)
				{
					if (list.Contains(item.id))
					{
						ps.wildFavor += (int)((float)item.cost * Difficulty.Rules.wildServiceFavorRefund);
					}
					ps.playerCash -= item.cost;
					RunStats.OnServicePaid(item.id, item.cost);
					item.currentCooldown = item.totalCooldown;
				}
			}
			Hook("FireOnHandlingNightlyServicesEarly");
			ps.HandleOtherServices();
			ps.HandleJanitorial();
			ps.HandlePower();
			ps.HandleInsurance();
			ps.HandleBuyerReferal();
			ps.HandleSellerReferal();
			ps.HandleCriminelNetwork();
			ps.HandleVandalism();
			ps.HandleButcherHunting();
			LouBombHelper.OnNight();
			LouOperativeHelper.OnNight();
			UseCountHelper.OnNight();
			ps.HandleInspectionEarlyWarning();
			ps.HandleWantedPersonVistedCheck();
			ps.HandleUpgradeTip();
			ps.HandleSecurityWarning();
			ps.HandleAugWarning();
			ps.HandleOperation();
			ps.HandleAfterhourDebuff();
			StartingPerkContent.HandleAgoraphobiaReady();
			Hook("FireOnHandlingNightlyServicesLate");
			ps.expeditionData.OnDayEnd();
			if (StoreStation.GetDayCounter() % 7 == 0)
			{
				ps.exchangeManager.ResetPromotional();
			}
			ps.healthData.OnSleep();
			StorePhoneClient.OnEndDay();
			EmporiumEntry.Instance.ClearFrontInv();
			DebrisHelper.OnNight();
			// Send the host's decisions (services, burglary, Lou...) to every copy, then run block S2 under shared dice.
			SendDelta(afterS1);
			Seed seed2;
			Tape tape2;
			using (Replay.Server(out seed2, -1))
			{
				using (Items.Record(out tape2))
				{
					BlockS2(ps);
				}
			}
			Send("s2", seed2, tape2, ps);
			ps.storeState = PlayerStore.StoreState.MORNING;
			ps.lockShutterButtonFlag = 0;
			if (EmporiumEntry.Instance.vendingFountainElement.childItem != null)
			{
				EmporiumEntry.Instance.vendingFountainElement.childItem.contentWindow.Hide();
			}
			if (EmporiumEntry.Instance.vendingMachineElement.childItem != null)
			{
				EmporiumEntry.Instance.vendingMachineElement.childItem.contentWindow.Hide();
			}
			Hook("FireOnGoingSleepLate");
		}
		finally
		{
			running = false;
		}
	}

	private static void Send(string name, Seed seed, Tape tape, PlayerStore ps)
	{
		try
		{
			int uidAfter = seed.UidAfter;
			// A player's copy must not ask for a resync over a block it only measures: no digest to check there.
			Seed seed2 = new Seed
			{
				RandomState = seed.RandomState,
				SysSeed = seed.SysSeed,
				UidBase = seed.UidBase,
				Actor = seed.Actor,
				P = seed.P
			};
			Net.ToAll("night.block", new MsgNightBlock
			{
				day = Game.Day,
				name = name,
				seed = seed2,
				tape = tape,
				uidAfter = uidAfter,
				items = Fingerprint(ps).Values.ToList()
			});
			Log.Info("night: block " + name + " ran under shared dice (" + (tape?.uids?.Count ?? 0) + " new id(s)); its fingerprint sent");
		}
		catch (Exception ex)
		{
			Log.Warn("night: sending block " + name + " threw: " + ex.Message);
		}
	}

	private static void ClientBlock(MsgNightBlock m)
	{
		if (m == null || m.items == null || DayLog.Replaying || !Game.ShopLoaded || Net.IsServer || (m.name != "s1" && m.name != "s2"))
		{
			return;
		}
		PlayerStore store = Game.Store;
		try
		{
			using (Replay.Client(m.seed ?? new Seed()))
			{
				using (Items.Play(m.tape ?? new Tape()))
				{
					if (m.name == "s1")
					{
						BlockS1(store);
					}
					else
					{
						BlockS2(store);
					}
				}
			}
			Compare(m, Fingerprint(store));
		}
		catch (Exception ex)
		{
			Log.Warn("night: running block " + m.name + " here threw: " + ex);
		}
	}

	private static void Compare(MsgNightBlock m, Dictionary<int, string> here)
	{
		Dictionary<int, string> dictionary = new Dictionary<int, string>();
		foreach (string item in m.items)
		{
			int num = item.IndexOf('|');
			if (num > 0 && int.TryParse(item.Substring(0, num), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				dictionary[result] = item;
			}
		}
		Same = (Differ = (Missing = (Extra = 0)));
		List<string> list = new List<string>();
		foreach (KeyValuePair<int, string> item2 in dictionary.OrderBy((KeyValuePair<int, string> p) => p.Key))
		{
			if (!here.TryGetValue(item2.Key, out var value))
			{
				Missing++;
				if (list.Count < 12)
				{
					list.Add("not here: " + NightCheck.Head(item2.Value));
				}
			}
			else if (value == item2.Value)
			{
				Same++;
			}
			else
			{
				Differ++;
				if (list.Count < 12)
				{
					list.Add((item2.Key == 0) ? ("the shop: server " + item2.Value.Substring(10) + " / here " + value.Substring(10)) : NightCheck.Explain(item2.Value, value));
				}
			}
		}
		foreach (int key in here.Keys)
		{
			if (!dictionary.ContainsKey(key))
			{
				Extra++;
				if (list.Count < 12)
				{
					list.Add("only here: " + NightCheck.Head(here[key]));
				}
			}
		}
		Runs++;
		if (m.name == "s2")
		{
			S2Runs++;
			S2Bad = Differ + Missing + Extra;
			S2Summary = "block s2 of day " + m.day + ": " + Same + " same, " + Differ + " different, " + Missing + " only on the server, " + Extra + " only here";
		}
		else
		{
			S1Bad = Differ + Missing + Extra;
		}
		LastSummary = "block " + m.name + " of day " + m.day + ": " + Same + " same, " + Differ + " different, " + Missing + " only on the server, " + Extra + " only here";
		Log.Info("night: " + LastSummary + ((list.Count > 0) ? (": " + string.Join(" | ", list)) : ""));
	}

	/// Only on the game version EndNight was read from.
	[HarmonyPatch(typeof(PlayerStore), "EndNight")]
	private static class EndNightPatch
	{
		private static bool Prefix(PlayerStore __instance)
		{
			if (running || !On || DayNightGate.LocalCommit)
			{
				return true;
			}
			try
			{
				EndNight(__instance);
				return false;
			}
			catch (Exception ex)
			{
				Log.Error("night: the night in blocks threw part way (the rest of the game's night is skipped; the morning reload sets every player's copy to the server's): " + ex);
				return false;
			}
		}
	}
}
