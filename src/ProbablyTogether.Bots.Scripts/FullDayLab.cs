using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ProbablyTogether.Systems;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public static class FullDayLab
{
	public static long Fx;

	public static readonly List<string> Marks = new List<string>();

	public static int Loads;

	public static int DaysEnded;

	public static readonly List<string> Notes = new List<string>();

	private static bool wired;

	private static readonly MethodInfo onSelect = AccessTools.Method(typeof(MachineryButton), "OnSelect", (Type[])null, (Type[])null);

	private static readonly MethodInfo useClicked = AccessTools.Method(typeof(WildNetworkUI), "OnUseClicked", Type.EmptyTypes, (Type[])null);

	internal static void Wire()
	{
		if (wired)
		{
			return;
		}
		wired = true;
		Net.On<MsgFullDayLab>("fullday.lab", OnServerLab, null);
		Snapshot.Loaded += delegate
		{
			Loads++;
		};
		DayNight.DayEnded += delegate
		{
			DaysEnded++;
		};
		Locks.Changed += delegate(string name, int holder)
		{
			if (name == "counter")
			{
				BotLog.Info(Net.IsServer ? "fullday-server" : "fullday", "lock counter -> " + holder + " (" + ((holder > 0) ? Net.PlayerName(holder) : "free") + ") at " + Time.realtimeSinceStartup.ToString("F1", CultureInfo.InvariantCulture) + " s, customer " + CurrentId());
			}
		};
		Negotiation.Refused += delegate(string kind, string why)
		{
			Note("negotiation refused " + kind + ": " + why);
		};
		Customers.Refused += delegate(string what, string why)
		{
			Note("customers refused " + what + ": " + why);
		};
		DayNight.Refused += delegate(string what, string why)
		{
			Note("day-night refused " + what + ": " + why);
		};
		Security.Refused += delegate(string what, string why)
		{
			Note("security refused " + what + ": " + why);
		};
	}

	internal static void Note(string s)
	{
		Notes.Add(s);
		if (Notes.Count > 40)
		{
			Notes.RemoveAt(0);
		}
		BotLog.Info("fullday", s);
	}

	private static void OnServerLab(MsgFullDayLab m, int from)
	{
		if (m == null || m.op == null || !Net.IsServer)
		{
			return;
		}
		string text = Net.PlayerName(from);
		switch (m.op)
		{
		case "mark":
			if (m.a != null && m.a.Length < 64 && !Marks.Contains(m.a))
			{
				Marks.Add(m.a);
			}
			BotLog.Info("fullday-server", "mark " + m.a + " from " + text);
			break;
		case "stock":
			BotLog.Info("fullday-server", "stand-in for " + text + ": customer stock on the front counter -> " + Customers.BotDo("stock", "2"));
			break;
		case "add":
			BotLog.Info("fullday-server", "stand-in for " + text + ": queue " + m.a + " -> " + Customers.BotDo("add", m.a ?? "scavGeneral"));
			break;
		default:
			BotLog.Info("fullday-server", "unknown lab op '" + m.op + "' from " + text);
			break;
		}
	}

	public static GameItem Machine()
	{
		if (Fx == 0L)
		{
			return null;
		}
		return Items.Find(Fx);
	}

	internal static string SpawnFixture()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		string text = null;
		using (Net.Apply())
		{
			GameItem val = DirectoryMaster.Item("bottle_printer", true);
			if (val == null)
			{
				throw new InvalidOperationException("the game has no bottle_printer");
			}
			GameGridInventory[] array = (GameGridInventory[])(object)new GameGridInventory[3] { instance.invElement, instance.hirelingInv, instance.soldElement };
			foreach (GameGridInventory val2 in array)
			{
				if (val2 != null && Place(val, val2))
				{
					text = ((val2 == instance.invElement) ? "inv" : ((val2 == instance.hirelingInv) ? "hireling" : "sold"));
					break;
				}
			}
			if (text == null)
			{
				val.Destroy();
				throw new InvalidOperationException("no free cell for the bottle printer");
			}
			GeneralHelper.SetItemOwned(val, true);
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				GeneralHelper.SetItemOwned(item, true);
			}
			GameItem val3 = PreBuiltItemHelper.EnergyCredit(50);
			GraphUtils.TryAcceptAll((GraphNodeStorage)(object)MachineHelper.GetBatterySlot(val), val3, -1);
			if (val3.parentInventory == null)
			{
				throw new InvalidOperationException("the battery did not go into the bottle printer");
			}
			GeneralHelper.SetItemOwned(val3, true);
			Fx = Ids.Of(val);
		}
		return "bottle printer #" + Fx + " in " + text + ", " + Box();
	}

	private static bool Place(GameItem it, GameGridInventory inv)
	{
		if (((inv != null) ? inv.inventoryShape : null) == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			for (int num = 4; num >= 1; num--)
			{
				for (int j = 0; j < inv.inventoryShape.height; j++)
				{
					for (int k = 0; k < inv.inventoryShape.width; k++)
					{
						GridShapeBuilder val = new GridShapeBuilder(it.modifiedShape).SetTransform(k, j, i == 1, num);
						SlotMarker val2 = ((GameInventory)inv).TryInventorySlot(it, it.unitCount, (GridShape)(object)val, (TagSystem)null);
						if (val2 != null && val2.targetItem == null && (object)val2.inventory == inv && val2.numTransfer >= it.unitCount)
						{
							val2.TryAcceptOnce(-1);
							return (object)it.parentInventory == inv;
						}
					}
				}
			}
		}
		return false;
	}

	public static string Box()
	{
		GameItem val = Machine();
		if (val == null)
		{
			return "gone";
		}
		GameSlotInventory batterySlot = MachineHelper.GetBatterySlot(val);
		GameItem val2 = ((batterySlot != null) ? batterySlot.childItem : null);
		GameInventory slot = MachineHelper.GetSlot(val, 1);
		return ((val2 != null) ? val2.GetTagReadonly("power_source_item_energy").GetInt() : (-1)) + "|" + ((slot != null) ? slot.childItems.Count : (-1));
	}

	public static int Out()
	{
		GameItem val = Machine();
		GameInventory val2 = ((val != null) ? MachineHelper.GetSlot(val, 1) : null);
		if (val2 == null)
		{
			return -1;
		}
		return val2.childItems.Count;
	}

	public static void Press(MachineryButton b)
	{
		onSelect.Invoke(b, null);
	}

	public static void ClickBuy(string id)
	{
		Economy.SelectUpgrade(id);
		useClicked.Invoke(null, null);
	}

	public static ulong Fnv(string s)
	{
		ulong num = 14695981039346656037uL;
		if (s != null)
		{
			foreach (char c in s)
			{
				num ^= c;
				num *= 1099511628211L;
			}
		}
		return num;
	}

	public static string Dlg()
	{
		return Part(DialogueSystem.Counter) + " " + Part(DialogueSystem.Phone) + "/" + (PhoneChannel.Caller?.dialogFuncId ?? "-") + (PhoneChannel.Talking ? "/talking" : "");
		static string Part(DialogueChannel ch)
		{
			return ch.Name + ":" + DialogueRefs.Fp(ch.Current) + (ch.LineUnfinished ? "/typing" : "");
		}
	}

	public static string EventRows()
	{
		StoreEventManager val = ((!Game.ShopLoaded) ? null : Game.Station?.storeEventManager);
		if (val == null)
		{
			return "";
		}
		List<string> list = new List<string>();
		foreach (StoreEvent activeEvent in val.GetActiveEvents())
		{
			list.Add("a:" + activeEvent.identifier + ":" + activeEvent.duration + ":" + activeEvent.startDay + ":" + (activeEvent.IsKnown ? 1 : 0));
		}
		if (val.futurEvents != null)
		{
			for (int i = 0; i < val.futurEvents.Count; i++)
			{
				list.Add("f:" + val.futurEvents[i]?.identifier + ":" + ((val.futurEventsDay != null && i < val.futurEventsDay.Count) ? val.futurEventsDay[i] : (-1)) + ":" + ((val.futurEvents[i]?.IsKnown ?? false) ? 1 : 0));
			}
		}
		if (val.eventTracker != null)
		{
			foreach (KeyValuePair<string, int> item in val.eventTracker)
			{
				list.Add("t:" + item.Key + ":" + item.Value);
			}
		}
		list.Sort(StringComparer.Ordinal);
		if (list.Count != 0)
		{
			return string.Join("|", list.Select((string r, int num) => "e" + num + "=" + r));
		}
		return "none";
	}

	public static string Core()
	{
		if (!Game.ShopLoaded)
		{
			return "";
		}
		MsgDigest msgDigest = Digest.Compute();
		return "day=" + msgDigest.day + "|cash=" + msgDigest.cash + "|favor=" + msgDigest.favor + "|items=" + msgDigest.items + "|hash=" + msgDigest.hash + "|state=" + msgDigest.state + "|client=" + msgDigest.client + "|queue=" + msgDigest.queue;
	}

	public static string Parts()
	{
		if (!Game.ShopLoaded)
		{
			return "";
		}
		Dictionary<string, string> dictionary = Digest.Parts();
		if (dictionary == null || dictionary.Count == 0)
		{
			return "none";
		}
		return string.Join("|", from kv in dictionary.OrderBy<KeyValuePair<string, string>, string>((KeyValuePair<string, string> kv) => kv.Key, StringComparer.Ordinal)
			select kv.Key + "=" + kv.Value);
	}

	public static string Counter()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null || !Game.ShopLoaded)
		{
			return "";
		}
		return "front " + Rows((GameInventory)(object)instance.frontInvinvElement) + " / back " + Rows((GameInventory)(object)instance.backInvinvElement);
		static string Rows(GameInventory inv)
		{
			if (((inv != null) ? inv.childItems : null) == null)
			{
				return "-";
			}
			List<string> list = (from i in inv.childItems
				where i != null
				select Ids.Of(i) + ":" + i.identifier + ":" + (GeneralHelper.IsItemOwned(i) ? "own" : "cust")).OrderBy<string, string>((string x) => x, StringComparer.Ordinal).ToList();
			if (list.Count != 0)
			{
				return string.Join(",", list);
			}
			return "empty";
		}
	}

	public static int CustItems(GameInventory inv)
	{
		if (((inv != null) ? inv.childItems : null) != null)
		{
			return inv.childItems.Count((GameItem i) => i != null && !GeneralHelper.IsItemOwned(i));
		}
		return -1;
	}

	public static string CurrentId()
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
			obj = ((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier);
		}
		if (obj == null)
		{
			obj = "none";
		}
		return (string)obj;
	}

	public static int Holder()
	{
		if (!Net.IsServer)
		{
			return Locks.HeldBy("counter");
		}
		return Locks.Holder("counter");
	}

	public static string Counters()
	{
		return "replay ok/bad " + Replay.ChecksOk + "/" + Replay.ChecksBad + ", customers mismatch " + Customers.Mismatches + ", dialogue mismatch " + DialogueSync.MismatchCount + ", items apply mismatch " + ItemsMove.ApplyMismatches + ", machines mismatch " + Machines.MismatchCount + ", economy mismatch " + Economy.Mismatches + ", day-night mismatch " + DayNight.Mismatches + ", loads " + Loads;
	}
}
