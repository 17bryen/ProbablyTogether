using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public static class MachinesX
{
	public sealed class MarkMsg
	{
		public string name;
	}

	public static readonly Dictionary<string, long> Fx = new Dictionary<string, long>(StringComparer.Ordinal);

	public static bool Ready;

	public static readonly string[] DgNames = new string[7] { "box", "deal", "cage", "butch", "coord", "fridge", "cp" };

	private static readonly SortedDictionary<string, int> marks = new SortedDictionary<string, int>(StringComparer.Ordinal);

	private static bool wired;

	private static readonly MethodInfo onSelect = AccessTools.Method(typeof(MachineryButton), "OnSelect", (Type[])null, (Type[])null);

	public static string PureId => ItemConditionList.conditionWaterPurity[0]?.identifier ?? "";

	public static GameItem Item(string name)
	{
		if (!Fx.TryGetValue(name, out var value))
		{
			return null;
		}
		return Ids.Find(value) ?? Items.Find(value);
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

	private static string S(object o)
	{
		return Convert.ToString(o, CultureInfo.InvariantCulture);
	}

	public static GameItem Cassette()
	{
		if (!((Object)(object)EmporiumEntry.Instance != (Object)null))
		{
			return null;
		}
		return EmporiumEntry.Instance.cassettePlayer;
	}

	public static string Cp()
	{
		GameItem val = Cassette();
		if (val == null)
		{
			return "none";
		}
		return S(val.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt()) + "|" + S(val.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt()) + "|" + val.GetTagReadonly("CURRENT_PLAYING_TRACK_ID_TAG").GetString();
	}

	public static int Tracks()
	{
		return MachineCassettePlayer.GetTrackListFromCassette(MachineCassettePlayer.GetCassetteInventory()).Count;
	}

	public static void Wire()
	{
		if (wired)
		{
			return;
		}
		wired = true;
		Net.On("machines.bot.mark", delegate(MarkMsg m, int from)
		{
			if (m != null && m.name != null && m.name.Length < 40)
			{
				marks.TryGetValue(m.name, out var value);
				marks[m.name] = value + 1;
			}
		}, null);
	}

	public static void Mark(string name)
	{
		if (Net.IsClient)
		{
			Net.ToServer("machines.bot.mark", new MarkMsg
			{
				name = name
			});
		}
	}

	public static string Marks()
	{
		return string.Join(",", marks.Select((KeyValuePair<string, int> kv) => kv.Key + "=" + kv.Value));
	}

	public static int CpState()
	{
		GameItem val = Cassette();
		if (val != null)
		{
			return val.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt();
		}
		return -1;
	}

	public static int CpIndex()
	{
		GameItem val = Cassette();
		if (val != null)
		{
			return val.GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt();
		}
		return -1;
	}

	private static int Count(GameInventory inv, Func<GameItem, bool> pick)
	{
		if (inv == null)
		{
			return -1;
		}
		int num = 0;
		foreach (GameItem childItem in inv.childItems)
		{
			if (childItem != null && pick(childItem))
			{
				num++;
			}
		}
		return num;
	}

	public static int LiveRats()
	{
		GameItem val = Item("x.cage");
		if (val != null)
		{
			return Count((GameInventory)(object)AnimalCage.GetCageInventory(val), (GameItem it) => it.IsTag("ANIMAL_ITEM_TAG") && !HusbandryHelper.IsAnimalDead(it));
		}
		return -1;
	}

	public static int AllRats()
	{
		GameItem val = Item("x.cage");
		if (val != null)
		{
			return Count((GameInventory)(object)AnimalCage.GetCageInventory(val), (GameItem it) => it.IsTag("ANIMAL_ITEM_TAG"));
		}
		return -1;
	}

	public static int DeadInTray()
	{
		GameItem val = Item("x.butch");
		if (val != null)
		{
			return Count((GameInventory)(object)MachineAutoButcher.GetOutputGrid(val), (GameItem it) => it.IsTag("ANIMAL_ITEM_TAG") && HusbandryHelper.IsAnimalDead(it));
		}
		return -1;
	}

	public static int TrayItems()
	{
		GameItem val = Item("x.butch");
		if (val != null)
		{
			return Count((GameInventory)(object)MachineAutoButcher.GetOutputGrid(val), (GameItem it) => true);
		}
		return -1;
	}

	public static string Rats()
	{
		return LiveRats() + " live, " + AllRats() + " in the cage, tray " + DeadInTray() + " dead / " + TrayItems() + " items";
	}

	public static string Presets()
	{
		GameItem val = Item("x.coord");
		if (val == null)
		{
			return "gone";
		}
		List<MachineStorageCoordinator.Preset> list = MachineStorageCoordinator.LoadPresets(val);
		return list.Count + ":" + string.Join(",", list.Select((MachineStorageCoordinator.Preset p) => p.name));
	}

	public static int Temp()
	{
		GameItem val = Item("x.water");
		if (val != null)
		{
			return (int)Math.Round(val.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble());
		}
		return -999;
	}

	public static int Temp2()
	{
		GameItem val = Item("x.water2");
		if (val != null)
		{
			return (int)Math.Round(val.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble());
		}
		return -999;
	}

	public static string FridgeDiag()
	{
		GameItem f = Item("x.fridge");
		if (f == null)
		{
			return "no fridge";
		}
		GameSlotInventory batterySlot = MachineHelper.GetBatterySlot(f);
		GameItem val = ((batterySlot != null) ? batterySlot.childItem : null);
		bool flag = (Object)(object)EmporiumEntry.Instance != (Object)null && EmporiumEntry.Instance.GetAllItems().Contains(f);
		return "battery " + (((val != null) ? val.identifier : null) ?? "none") + " " + ((val != null) ? val.GetTagReadonly("power_source_item_energy").GetInt() : (-1)) + ", can draw 1 " + (val != null && PowerHelper.CanDrawPowerSource(val, 1)) + ", 1 s func " + (((GameItemFunc)f).onOneSecondItemFunc != null) + ", in GetAllItems " + flag + "; " + W("x.water") + "; " + W("x.water2");
		string W(string n)
		{
			GameItem val2 = Item(n);
			if (val2 == null)
			{
				return n + " gone";
			}
			GameInventory parentInventory = val2.parentInventory;
			string text = (((object)parentInventory == MachineFridge.GetFreezeZone(f)) ? "freeze" : (((object)parentInventory == MachineFridge.GetChillZone(f)) ? "chill" : "elsewhere"));
			return n + " " + text + " " + val2.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble().ToString("F2", CultureInfo.InvariantCulture) + " C, heating " + val2.IsTag("HEATING_TAG") + ", liquid " + val2.IsTag("LIQUID_CONTAINER_TAG") + ", " + WaterHelper.GetTotalVolume(val2) + " ml, empty " + WaterHelper.IsEmpty(val2);
		}
	}

	public static string WaterLabel(GameItem w)
	{
		if (w == null)
		{
			return "gone";
		}
		ItemFeature val = w.FindItemFeatureByCategory("CATEGORY_WATER_PURITY");
		if (val == null)
		{
			return "no label";
		}
		return (val.IsExposed() ? "x" : "h") + ":" + (val.fakeCondition?.identifier ?? "-") + ":" + (val.realCondition?.identifier ?? "-") + ":" + S(val.customIntValue1) + ":" + (val.IsFeatureMatch() ? "match" : "differs") + ":cl" + S(w.GetTagReadonly(Liquid.GetCurrentPartFromId("chlorine")).GetInt());
	}

	public static int BoxOut()
	{
		GameItem val = Item("x.box");
		GameInventory val2 = ((val != null) ? MachineHelper.GetSlot(val, 1) : null);
		if (val2 == null)
		{
			return -1;
		}
		return val2.childItems.Count;
	}

	public static int DealEnergy()
	{
		GameItem val = Item("x.deal");
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

	public static string DealOn()
	{
		if (PlayerStore.Instance == null || !PlayerStore.Instance.dealMakerActivatedThisCustomer)
		{
			return "0";
		}
		return "1";
	}

	public static string Dg(string n)
	{
		GameItem val = ((n == "cp") ? Cassette() : Item("x." + n));
		if (val != null)
		{
			return S(Machines.Digest(val));
		}
		return "gone";
	}

	public static MachineryButton ButtonCalling(GameItem m, MethodInfo target)
	{
		if (m == null || target == null)
		{
			return null;
		}
		int metadataToken = target.MetadataToken;
		foreach (object item in Machines.Widgets(m))
		{
			MachineryButton val = (MachineryButton)((item is MachineryButton) ? item : null);
			if (val == null)
			{
				continue;
			}
			byte[] array = Machines.CallbackOf(val)?.Method?.GetMethodBody()?.GetILAsByteArray();
			if (array == null)
			{
				continue;
			}
			for (int i = 0; i + 4 < array.Length; i++)
			{
				if ((array[i] == 40 || array[i] == 111) && (array[i + 1] | (array[i + 2] << 8) | (array[i + 3] << 16) | (array[i + 4] << 24)) == metadataToken)
				{
					return val;
				}
			}
		}
		return null;
	}

	public static MachineryButton Widget0(GameItem m)
	{
		return Machines.Widgets(m).OfType<MachineryButton>().FirstOrDefault() ?? throw new InvalidOperationException("no button on " + Machines.Describe(m));
	}

	public static MachineryButton ButtonText(GameItem m, string text)
	{
		return Machines.Widgets(m).OfType<MachineryButton>().FirstOrDefault((Func<MachineryButton, bool>)((MachineryButton b) => string.Equals(b.text, text, StringComparison.OrdinalIgnoreCase)));
	}

	public static void Press(MachineryButton b)
	{
		onSelect.Invoke(b, null);
	}

	public static string SpawnAll()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		List<string> list = new List<string>();
		GameGridInventory[] array = (GameGridInventory[])(object)new GameGridInventory[3] { instance.invElement, instance.hirelingInv, instance.backInvinvElement };
		using (Net.Apply())
		{
			GameItem obj = Spawn("x.fridge", "fridge", (GameGridInventory[])(object)new GameGridInventory[1] { instance.invElement }, list);
			Battery(obj, "fridge");
			GameItem val = DirectoryMaster.Item("small_bottled_water", true);
			WaterHelper.AddWater(val, 5, 300, false, 0, 1, true);
			val.ModifyTag("CONTAINER_TEMP_TAG", (Action<TagState>)delegate(TagState st)
			{
				st.Enable().SetDouble(25.0);
			}, false);
			Put((GameInventory)(object)MachineFridge.GetFreezeZone(obj), val, "bottle in the freeze zone");
			Fx["x.water"] = Ids.Of(val);
			GameItem val2 = DirectoryMaster.Item("small_bottled_water", true);
			WaterHelper.AddWater(val2, 5, 300, false, 0, 1, true);
			val2.ModifyTag("CONTAINER_TEMP_TAG", (Action<TagState>)delegate(TagState st)
			{
				st.Enable().SetDouble(25.0);
			}, false);
			Put((GameInventory)(object)MachineFridge.GetChillZone(obj), val2, "bottle in the chill zone");
			Fx["x.water2"] = Ids.Of(val2);
			GameItem val3 = DirectoryMaster.Item("small_bottled_water", true);
			WaterHelper.AddWater(val3, 2, 250, false, 0, 1, true);
			val3.ModifyTag(Liquid.GetCurrentPartFromId("chlorine"), (Action<TagState>)delegate(TagState st)
			{
				st.Enable().SetInt(40000);
			}, false);
			val3.ModifyTag("CONTAINER_TEMP_TAG", (Action<TagState>)delegate(TagState st)
			{
				st.Enable().SetDouble(26.0);
			}, false);
			ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(val3);
			waterFeature.customIntValue1 = 9990;
			waterFeature.fakeCondition = ItemConditionList.conditionWaterPurity[2];
			if (!Place(val3, instance.invElement))
			{
				throw new InvalidOperationException("no free cell for the chlorine bottle");
			}
			GeneralHelper.SetItemOwned(val3, true);
			Fx["x.chlor"] = Ids.Of(val3);
			list.Add("chlorine bottle " + WaterLabel(val3));
			Battery(Spawn("x.box", "bottle_printer", array, list), "printer");
			Battery(Spawn("x.deal", "deal_maker", array, list), "deal maker");
			Spawn("x.coord", "storage_coordinator", array, list);
			GameItem obj2 = Spawn("x.cage", "animal_cage", array, list);
			GameGridInventory cageInventory = AnimalCage.GetCageInventory(obj2);
			Put((GameInventory)(object)cageInventory, PreBuiltItemHelper.CreateRat("female", 8, false), "cage rat");
			Put((GameInventory)(object)cageInventory, PreBuiltItemHelper.CreateRat("male", 8, false), "cage rat");
			Put((GameInventory)(object)cageInventory, PreBuiltItemHelper.CreateRat("female", 12, false), "cage rat");
			GameItem val4 = DirectoryMaster.Item("auto_butcher", true);
			Put((GameInventory)(object)AnimalCage.GetButcherSlot(obj2), val4, "butcher in the cage");
			val4.ModifyTag("AUTO_BUTCHER_CAGE_HARVEST_INT", (Action<TagState>)delegate(TagState st)
			{
				st.Enable().SetInt(3);
			}, false);
			val4.ModifyTag("AUTO_BUTCHER_CAGE_AGE_MIN_INT", (Action<TagState>)delegate(TagState st)
			{
				st.Enable().SetInt(1);
			}, false);
			val4.ModifyTag("AUTO_BUTCHER_CAGE_AGE_MAX_INT", (Action<TagState>)delegate(TagState st)
			{
				st.Enable().SetInt(30);
			}, false);
			val4.ModifyTag("AUTO_BUTCHER_CAGE_KEEP_FEMALES_INT", (Action<TagState>)delegate(TagState st)
			{
				st.Enable().SetInt(0);
			}, false);
			val4.ModifyTag("AUTO_BUTCHER_CAGE_KEEP_MALES_INT", (Action<TagState>)delegate(TagState st)
			{
				st.Enable().SetInt(0);
			}, false);
			val4.ModifyTag("AUTO_BUTCHER_CAGE_COUNT_PROTECTED_BOOL", (Action<TagState>)delegate(TagState st)
			{
				st.Enable().SetBool(false);
			}, false);
			GameItem val5 = PreBuiltItemHelper.CreateRat("male", 9, false);
			HusbandryHelper.OnAnimalDead(val5, false);
			Put((GameInventory)(object)MachineAutoButcher.GetOutputGrid(val4), val5, "dead rat in the tray");
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)obj2, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				GeneralHelper.SetItemOwned(item, true);
			}
			Fx["x.butch"] = Ids.Of(val4);
			GameItem obj3 = Spawn("x.butch2", "auto_butcher", array, list);
			obj3.ModifyTag("AUTO_BUTCHER_DROP_TO_BUTCHER_BOOL", (Action<TagState>)delegate(TagState st)
			{
				st.Enable().SetBool(true);
			}, false);
			obj3.ModifyTag("AUTO_BUTCHER_KILL_STORE_ONLY_BOOL", (Action<TagState>)delegate(TagState st)
			{
				st.Enable().SetBool(true);
			}, false);
			GameGridInventory outputGrid = MachineAutoButcher.GetOutputGrid(obj3);
			int num = 0;
			for (int num2 = 0; num2 < 80; num2++)
			{
				GameItem val6 = PreBuiltItemHelper.CreateRat("male", 9, false);
				HusbandryHelper.OnAnimalDead(val6, false);
				if (!GraphUtils.CanAccept((GraphNodeStorage)(object)outputGrid, val6))
				{
					val6.Destroy();
					break;
				}
				GraphUtils.TryAcceptAll((GraphNodeStorage)(object)outputGrid, val6, -1);
				if ((object)val6.parentInventory != outputGrid)
				{
					val6.Destroy();
					break;
				}
				GeneralHelper.SetItemOwned(val6, true);
				num++;
			}
			list.Add("butcher 2 tray holds " + num + " dead rats");
			GameItem val7 = PreBuiltItemHelper.CreateRat("female", 10, false);
			GameGridInventory[] array2 = array;
			foreach (GameGridInventory val8 in array2)
			{
				if (val8 != null && Place(val7, val8))
				{
					break;
				}
			}
			if (val7.parentInventory == null)
			{
				throw new InvalidOperationException("no free cell for the live rat");
			}
			GeneralHelper.SetItemOwned(val7, true);
			Fx["x.rat"] = Ids.Of(val7);
			GameGridInventory cassetteInventory = MachineCassettePlayer.GetCassetteInventory();
			string[] array3 = new string[3] { "cassette1", "cassette2", "cassette3" };
			foreach (string text in array3)
			{
				GameItem val9 = DirectoryMaster.Item(text, true);
				GeneralHelper.SetItemOwned(val9, true);
				Put((GameInventory)(object)cassetteInventory, val9, text);
			}
			Fx["x.cp"] = -103L;
		}
		return Fx.Count + " fixtures (" + string.Join(", ", list) + ")";
	}

	private static void Battery(GameItem m, string what)
	{
		GameItem val = PreBuiltItemHelper.EnergyCredit(50);
		GraphUtils.TryAcceptAll((GraphNodeStorage)(object)MachineHelper.GetBatterySlot(m), val, -1);
		if (val.parentInventory == null)
		{
			throw new InvalidOperationException("the battery did not go into the " + what);
		}
		GeneralHelper.SetItemOwned(val, true);
	}

	private static void Put(GameInventory inv, GameItem it, string what)
	{
		if (inv == null)
		{
			throw new InvalidOperationException("no inventory for the " + what);
		}
		GraphUtils.TryAcceptAll((GraphNodeStorage)(object)inv, it, -1);
		if (it.parentInventory != inv)
		{
			throw new InvalidOperationException("the " + what + " (" + it.identifier + ") did not fit");
		}
		GeneralHelper.SetItemOwned(it, true);
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
			notes.Add(kind + "@" + ((val2 == EmporiumEntry.Instance.invElement) ? "inv" : ((val2 == EmporiumEntry.Instance.hirelingInv) ? "hireling" : "back")));
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
}
