using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Bots.Scripts;

public static class MachinesLab
{
	public static readonly string[] Sets = new string[3] { "s", "0", "1" };

	public static readonly Dictionary<string, long> Fx = new Dictionary<string, long>(StringComparer.Ordinal);

	public static bool Ready;

	public static string ServerPress = "";

	private static readonly MethodInfo onSelect = AccessTools.Method(typeof(MachineryButton), "OnSelect", (Type[])null, (Type[])null);

	private static readonly string[] SetMachines = new string[4] { "box", "plate", "feed", "chem" };

	private static readonly string[] Kinds = new string[4] { "bottle_printer", "heating_plate", "livestock_feed_dispenser", "chem_finisher" };

	public static GameItem Item(string name)
	{
		if (!Fx.TryGetValue(name, out var value))
		{
			return null;
		}
		return Ids.Find(value) ?? Items.Find(value);
	}

	public static bool HasMark(string marks, string name)
	{
		return ("," + marks).Contains("," + name + "=");
	}

	public static string FxText()
	{
		return string.Join(";", from kv in Fx.OrderBy<KeyValuePair<string, long>, string>((KeyValuePair<string, long> kv) => kv.Key, StringComparer.Ordinal)
			select kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture));
	}

	public static int ParseFx(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0;
		}
		string[] array = text.Split(';');
		foreach (string text2 in array)
		{
			int num = text2.IndexOf('=');
			if (num > 0 && long.TryParse(text2.Substring(num + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				Fx[text2.Substring(0, num)] = result;
			}
		}
		return Fx.Count;
	}

	public static string MySet()
	{
		List<int> list = Net.Players.Keys.OrderBy((int i) => i).ToList();
		return (Math.Max(0, list.IndexOf(Net.LocalPlayer)) % 2).ToString(CultureInfo.InvariantCulture);
	}

	private static string S(object o)
	{
		return Convert.ToString(o, CultureInfo.InvariantCulture);
	}

	public static GameInventory Inner(GameItem bag)
	{
		object obj;
		if (bag == null)
		{
			obj = null;
		}
		else
		{
			PixelWindow contentWindow = bag.contentWindow;
			obj = ((contentWindow != null) ? contentWindow.child : null);
		}
		return (GameInventory)((obj is GameInventory) ? obj : null);
	}

	public static string Box(string k)
	{
		GameItem val = Item(k + ".box");
		if (val == null)
		{
			return "gone";
		}
		GameSlotInventory batterySlot = MachineHelper.GetBatterySlot(val);
		GameItem val2 = ((batterySlot != null) ? batterySlot.childItem : null);
		int num = ((val2 != null) ? val2.GetTagReadonly("power_source_item_energy").GetInt() : (-1));
		GameInventory slot = MachineHelper.GetSlot(val, 1);
		List<string> values = ((slot != null) ? slot.childItems.Select((GameItem i) => i.identifier + "#" + S(i.uniqueId)).OrderBy<string, string>((string x) => x, StringComparer.Ordinal).ToList() : new List<string>());
		return num + "|" + string.Join(",", values);
	}

	public static int OutCount(string k)
	{
		GameItem val = Item(k + ".box");
		GameInventory val2 = ((val != null) ? MachineHelper.GetSlot(val, 1) : null);
		if (val2 == null)
		{
			return -1;
		}
		return val2.childItems.Count;
	}

	public static int Energy(string k)
	{
		GameItem val = Item(k + ".box");
		object obj;
		if (val == null)
		{
			obj = null;
		}
		else
		{
			GameSlotInventory batterySlot = MachineHelper.GetBatterySlot(val);
			obj = ((batterySlot != null) ? batterySlot.childItem : null);
		}
		GameItem val2 = (GameItem)obj;
		if (val2 == null)
		{
			return -1;
		}
		return val2.GetTagReadonly("power_source_item_energy").GetInt();
	}

	public static int Tag(string name, string tag)
	{
		GameItem val = Item(name);
		if (val != null)
		{
			return val.GetTagReadonly(tag).GetInt();
		}
		return -999;
	}

	public static string TagS(string name, string tag)
	{
		GameItem val = Item(name);
		object obj;
		if (val != null)
		{
			obj = val.GetTagReadonly(tag).GetString();
			if (obj == null)
			{
				return "";
			}
		}
		else
		{
			obj = "gone";
		}
		return (string)obj;
	}

	public static string Where(string k, int j)
	{
		GameItem val = Item(k + ".j" + j);
		if (val == null)
		{
			return "gone";
		}
		GameInventory parentInventory = val.parentInventory;
		if (parentInventory != null && parentInventory == Inner(Item(k + ".bagA")))
		{
			return "A";
		}
		if (parentInventory != null && parentInventory == Inner(Item(k + ".bagB")))
		{
			return "B";
		}
		return "?";
	}

	public static string Hop(string k)
	{
		string[] obj = new string[9]
		{
			Where(k, 1),
			Where(k, 2),
			Where(k, 3),
			"|",
			TagS(k + ".in", "HOPPER_FILTER_STRING"),
			"|",
			Tag(k + ".in", "HOPPER_FILTER_MODE_INT").ToString(),
			"|",
			null
		};
		GameItem obj2 = Item(k + ".in");
		obj[8] = ((obj2 != null && obj2.GetTagReadonly("HOPPER_ON_ADD_BOOL").GetBool()) ? "add" : "-");
		return string.Concat(obj);
	}

	public static string Diag(string k)
	{
		GameItem val = Item(k + ".box");
		if (val == null)
		{
			return "no box";
		}
		GameSlotInventory batterySlot = MachineHelper.GetBatterySlot(val);
		GameItem val2 = ((batterySlot != null) ? batterySlot.childItem : null);
		GameInventory slot = MachineHelper.GetSlot(val, 1);
		string text = "?";
		using (Net.Apply())
		{
			GameItem val3 = DirectoryMaster.Item("small_bottled_water", true);
			text = ((val3 == null) ? "no small_bottled_water" : GraphUtils.CanAccept((GraphNodeStorage)(object)slot, val3).ToString());
			if (val3 != null)
			{
				val3.Destroy();
			}
		}
		Action action = Machines.CallbackOf(Machines.WidgetAt(val, 0));
		return "box in " + (Items.RootOf((GraphNodeStorage)(object)val.parentInventory) ?? "?") + ", battery slot " + (batterySlot != null) + " holding " + (((val2 != null) ? val2.identifier : null) ?? "nothing") + " (power tag " + (val2 != null && val2.IsTag("power_source_item")) + ", energy " + ((val2 != null) ? val2.GetTagReadonly("power_source_item_energy").GetInt() : (-1)) + ", can draw " + (val2 != null && PowerHelper.CanDrawPowerSource(val2, 1)) + "), output " + ((slot != null) ? slot.childItems.Count : (-1)) + " item(s), accepts a bottle " + text + ", widget 0 " + Machines.WidgetAt(val, 0)?.GetType().Name + " callback " + (action?.Method.Name ?? "none") + ", " + Machines.Widgets(val).Count + " widgets";
	}

	public static string Farm(string k)
	{
		GameItem val = Item(k + ".farm");
		if (val == null)
		{
			return "gone";
		}
		GameSlotInventory batterySlot = MachineHelper.GetBatterySlot(val);
		GameItem val2 = ((batterySlot != null) ? batterySlot.childItem : null);
		GameItem val3 = Item(k + ".bottle");
		return ((val2 != null) ? val2.GetTagReadonly("power_source_item_energy").GetInt() : (-1)) + "|" + ((val3 != null) ? WaterHelper.GetTotalVolume(val3) : (-1)) + "|turbo " + (Items.IsGone(Item(k + ".turbo")) ? "used" : "here");
	}

	public static int Temp(string k)
	{
		GameItem val = Item(k + ".water");
		if (val != null)
		{
			return (int)val.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
		}
		return -999;
	}

	public static string Dg(string name)
	{
		GameItem val = Item(name);
		if (val != null)
		{
			return S(Machines.Digest(val));
		}
		return "gone";
	}

	public static object Widget<T>(GameItem m, int nth, Func<T, bool> pick = null) where T : class
	{
		int num = 0;
		foreach (object item in Machines.Widgets(m))
		{
			if (item is T arg && (pick == null || pick(arg)))
			{
				if (num == nth)
				{
					return item;
				}
				num++;
			}
		}
		return null;
	}

	public static void Press(MachineryButton b)
	{
		onSelect.Invoke(b, null);
	}

	public static string SpawnAll()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		GameGridInventory[] array = (GameGridInventory[])(object)new GameGridInventory[3] { instance.invElement, instance.hirelingInv, instance.backInvinvElement };
		List<string> list = new List<string>();
		using (Net.Apply())
		{
			string[] sets = Sets;
			foreach (string text in sets)
			{
				if (!(text == "s"))
				{
					Spawn(text + ".bagA", "backpack_large", (GameGridInventory[])(object)new GameGridInventory[1] { instance.invElement }, list);
					Spawn(text + ".bagB", "backpack_large", (GameGridInventory[])(object)new GameGridInventory[1] { instance.invElement }, list);
				}
			}
			sets = Sets;
			foreach (string text2 in sets)
			{
				if (text2 != "s")
				{
					Spawn(text2 + ".plate", "heating_plate", (GameGridInventory[])(object)new GameGridInventory[1] { instance.invElement }, list);
				}
			}
			sets = Sets;
			foreach (string text3 in sets)
			{
				GameGridInventory[] array2 = (GameGridInventory[])(object)new GameGridInventory[3] { instance.hirelingInv, instance.invElement, instance.backInvinvElement };
				if (text3 == "s")
				{
					Spawn(text3 + ".box", "bottle_printer", array2, list);
					Battery(text3);
					continue;
				}
				for (int j = 0; j < SetMachines.Length; j++)
				{
					if (SetMachines[j] != "plate")
					{
						Spawn(text3 + "." + SetMachines[j], Kinds[j], array2, list);
					}
				}
				Battery(text3);
				GameItem val = DirectoryMaster.Item("small_bottled_water", true);
				WaterHelper.AddWater(val, 5, 300, false, 0, 1, true);
				GameItem val2 = Item(text3 + ".plate");
				for (int k = 0; k <= 4; k++)
				{
					if (val.parentInventory != null)
					{
						break;
					}
					GraphNodeStorage bodyStorage = MachineHelper.GetBodyStorage(val2, k);
					GameSlotInventory val3 = (GameSlotInventory)(object)((bodyStorage is GameSlotInventory) ? bodyStorage : null);
					if (val3 != null)
					{
						GraphUtils.TryAcceptAll((GraphNodeStorage)(object)val3, val, -1);
					}
				}
				if (val.parentInventory != null)
				{
					Fx[text3 + ".water"] = Ids.Of(val);
				}
				else
				{
					list.Add("NO-WATER-SLOT");
				}
				Spawn(text3 + ".butch1", "auto_butcher", array2, list);
				Spawn(text3 + ".butch2", "auto_butcher", array2, list);
				Spawn(text3 + ".pellet", "gutterflow_pellet", array2, list);
				if (text3 == "0")
				{
					Spawn(text3 + ".deal", "deal_maker", array2, list);
				}
				GameItem val4 = Spawn(text3 + ".farm", "moisture_farm", array, list);
				GameItem val5 = PreBuiltItemHelper.EnergyCredit(50);
				GraphUtils.TryAcceptAll((GraphNodeStorage)(object)MachineHelper.GetBatterySlot(val4), val5, -1);
				GeneralHelper.SetItemOwned(val5, true);
				GameItem val6 = DirectoryMaster.Item("small_bottled_water", true);
				for (int l = 1; l <= 4; l++)
				{
					if (val6.parentInventory != null)
					{
						break;
					}
					GraphNodeStorage bodyStorage2 = MachineHelper.GetBodyStorage(val4, l);
					GameSlotInventory val7 = (GameSlotInventory)(object)((bodyStorage2 is GameSlotInventory) ? bodyStorage2 : null);
					if (val7 != null)
					{
						GraphUtils.TryAcceptAll((GraphNodeStorage)(object)val7, val6, -1);
					}
				}
				if (val6.parentInventory == null || val5.parentInventory == null)
				{
					throw new InvalidOperationException("the moisture farm took no " + ((val5.parentInventory == null) ? "battery" : "bottle"));
				}
				Fx[text3 + ".bottle"] = Ids.Of(val6);
				Spawn(text3 + ".turbo", "turbo_booster", array, list);
				GameGridInventory[] array3 = array;
				GameGridInventory[] array4 = array;
				GameItem val8 = Spawn(text3 + ".in", "intake_hopper", array3, list);
				GameItem val9 = Spawn(text3 + ".out", "outlet_hopper", array4, list);
				for (int m = 1; m <= 3; m++)
				{
					Spawn(text3 + ".j" + m, "scrap_metal", array3, list);
				}
				HopperHelper.Link(val8, val9);
			}
		}
		try
		{
			using (Net.Apply())
			{
				if (!PlayerStore.Instance.isVendingMachineUnlocked)
				{
					VendingMachineHelper.UnlockVendingMachine(0);
				}
				GameItem val10 = ((IEnumerable<GameItem>)GraphUtils.FindChildrenType<GameItem>((GraphNodeStorage)(object)instance.vendingMachineElement, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null)).FirstOrDefault((Func<GameItem, bool>)((GameItem val11) => val11.identifier == "vending_machine"));
				if (val10 != null)
				{
					val10.ModifyTag("VENDING_CASH", (Action<TagState>)delegate(TagState st)
					{
						st.Enable().SetInt(100);
					}, false);
					Fx["v.vend"] = Ids.Of(val10);
				}
				else
				{
					list.Add("NO-VENDING-MACHINE");
				}
			}
		}
		catch (Exception ex)
		{
			list.Add("vending unlock threw " + ex.GetType().Name);
		}
		return Fx.Count + " fixtures (" + string.Join(", ", from n in list
			group n by n into g
			select g.Key + " x" + g.Count()) + ")";
	}

	private static void Battery(string k)
	{
		GameItem obj = Item(k + ".box");
		GameItem val = PreBuiltItemHelper.EnergyCredit(50);
		GraphUtils.TryAcceptAll((GraphNodeStorage)(object)MachineHelper.GetBatterySlot(obj), val, -1);
		if (val.parentInventory == null)
		{
			throw new InvalidOperationException("the battery did not go into " + k + ".box");
		}
		GeneralHelper.SetItemOwned(val, true);
	}

	private static GameItem Spawn(string name, string kind, GameGridInventory[] into, List<string> notes)
	{
		GameItem val = DirectoryMaster.Item(kind, true);
		if (val == null)
		{
			throw new InvalidOperationException("the game has no item '" + kind + "'");
		}
		foreach (GameGridInventory val2 in into)
		{
			if (val2 == null || !Place(val, val2))
			{
				continue;
			}
			GeneralHelper.SetItemOwned(val, true);
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				GeneralHelper.SetItemOwned(item, true);
			}
			Fx[name] = Ids.Of(val);
			notes.Add(kind + "@" + ((val2 == EmporiumEntry.Instance.invElement) ? "inv" : ((val2 == EmporiumEntry.Instance.hirelingInv) ? "hireling" : ((val2 == EmporiumEntry.Instance.backInvinvElement) ? "back" : "bag"))));
			return val;
		}
		val.Destroy();
		throw new InvalidOperationException("no free cell for " + kind + " (" + name + ")");
	}

	private static bool Place(GameItem it, GameGridInventory inv)
	{
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

	public static IEnumerable<string> StateKeys(string k)
	{
		yield return "mach.box." + k;
		if (k == "s")
		{
			yield return "mach.dg." + k + ".box";
			yield break;
		}
		string[] array = new string[5] { "plate", "feed", "hop", "chem", "farm" };
		foreach (string text in array)
		{
			yield return "mach." + text + "." + k;
		}
		array = new string[6] { "box", "plate", "feed", "chem", "in", "farm" };
		foreach (string text2 in array)
		{
			yield return "mach.dg." + k + "." + text2;
		}
	}
}
