using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProbablyTogether.Bots.Scripts;

internal static class ProductionLab
{
	public sealed class Mark
	{
		public string name;
	}

	public sealed class Unlock
	{
		public string what;
	}

	public sealed class Cash
	{
		public string what;

		public int cash;
	}

	public static readonly string[] Sets = new string[2] { "1", "2" };

	public static readonly string[] Roles = new string[9] { "feeder", "cage", "butcher", "butcher2", "water", "water2", "chem", "ras", "rasbutcher" };

	public static readonly string[] Extra = new string[1] { "food" };

	public const string HirelingWater = "hw";

	public static readonly Dictionary<string, long> Fx = new Dictionary<string, long>(StringComparer.Ordinal);

	public static bool Ready;

	public static readonly Dictionary<string, HashSet<int>> Marks = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

	public static int Loads;

	public static int Cashed;

	private static bool wired;

	public static IEnumerable<string> Keys()
	{
		string[] sets = Sets;
		foreach (string s in sets)
		{
			foreach (string item in Roles.Concat(Extra))
			{
				yield return s + "." + item;
			}
		}
		yield return "hw";
	}

	public static IEnumerable<string> HashKeys()
	{
		string[] sets = Sets;
		foreach (string s in sets)
		{
			string[] roles = Roles;
			foreach (string text in roles)
			{
				yield return s + "." + text;
			}
		}
		yield return "vend";
		yield return "fountain";
	}

	public static GameItem Item(string key)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if (key == "vend")
		{
			if (instance == null)
			{
				return null;
			}
			GameSlotInventory vendingMachineElement = instance.vendingMachineElement;
			if (vendingMachineElement == null)
			{
				return null;
			}
			return vendingMachineElement.childItem;
		}
		if (key == "fountain")
		{
			if (instance == null)
			{
				return null;
			}
			GameSlotInventory vendingFountainElement = instance.vendingFountainElement;
			if (vendingFountainElement == null)
			{
				return null;
			}
			return vendingFountainElement.childItem;
		}
		if (!Fx.TryGetValue(key, out var value))
		{
			return null;
		}
		return Items.Find(value);
	}

	public static string FxText()
	{
		return string.Join(",", from kv in Fx.OrderBy<KeyValuePair<string, long>, string>((KeyValuePair<string, long> k) => k.Key, StringComparer.Ordinal)
			select kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture));
	}

	public static int ParseFx(string text)
	{
		Fx.Clear();
		string[] array = (text ?? "").Split(',');
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

	public static string SetFor(int player)
	{
		if (player > 0)
		{
			return ((player - 1) % 2 + 1).ToString(CultureInfo.InvariantCulture);
		}
		return "1";
	}

	public static int MarkCount(string name)
	{
		if (!Marks.TryGetValue(name, out var value))
		{
			return 0;
		}
		return value.Count;
	}

	public static string H(string key)
	{
		GameItem val = Item(key);
		if (val != null)
		{
			return Production.Hash(val).ToString("x16");
		}
		return "";
	}

	public static int Tag(string key, string tag)
	{
		GameItem val = Item(key);
		if (val != null)
		{
			return val.GetTagReadonly(tag).GetInt();
		}
		return -1;
	}

	public static bool TagB(string key, string tag)
	{
		GameItem val = Item(key);
		if (val != null)
		{
			return val.GetTagReadonly(tag).GetBool();
		}
		return false;
	}

	public static string Water(string key)
	{
		GameItem val = Item(key);
		ItemFeature val2 = ((val == null) ? null : WaterFeatureHelper.GetWaterFeature(val));
		if (val2 != null)
		{
			return (val2.IsExposed() ? "exposed" : "hidden") + ":" + (val2.IsFeatureMatch() ? "match" : "differs") + ":" + val2.customIntValue1.ToString(CultureInfo.InvariantCulture) + ":" + WaterHelper.GetWaterPurity(val).ToString(CultureInfo.InvariantCulture);
		}
		return "";
	}

	public static string WaterLabel(string key)
	{
		GameItem val = Item(key);
		ItemFeature val2 = ((val == null) ? null : WaterFeatureHelper.GetWaterFeature(val));
		if (val2 != null)
		{
			return (val2.IsExposed() ? "exposed" : "hidden") + ":" + val2.customIntValue1.ToString(CultureInfo.InvariantCulture) + ":" + WaterHelper.GetWaterPurity(val).ToString(CultureInfo.InvariantCulture);
		}
		return "";
	}

	public static string Chem(string key)
	{
		GameItem val = Item(key);
		ItemFeature val2 = ((val == null) ? null : ChemicalProductHelper.GetChemicalFeature(val));
		if (val2 != null)
		{
			return (val2.IsExposed() ? "exposed" : "hidden") + ":" + (val2.realCondition?.identifier ?? "-") + ":" + (val2.fakeCondition?.identifier ?? "-") + ":" + (val2.isFeatureMatch ? "match" : "differs");
		}
		return "";
	}

	public static GameGridInventory FeedGrid(string set)
	{
		GameItem val = Item(set + ".feeder");
		if (val != null)
		{
			return MachineFeedDispenser.GetStorageGrid(val);
		}
		return null;
	}

	public static int Count(GameInventory inv, Func<GameItem, bool> pick)
	{
		if (((inv != null) ? inv.childItems : null) == null)
		{
			return -1;
		}
		int num = 0;
		foreach (GameItem childItem in inv.childItems)
		{
			if (childItem != null && !Production.IsGone(childItem) && pick(childItem))
			{
				num++;
			}
		}
		return num;
	}

	public static int Food(string set)
	{
		return Count((GameInventory)(object)FeedGrid(set), (GameItem it) => it.identifier == "processed_meat");
	}

	public static int FeederAnimals(string set)
	{
		return Count((GameInventory)(object)FeedGrid(set), (GameItem it) => it.IsTag("ANIMAL_ITEM_TAG"));
	}

	public static int LiveRats(string set)
	{
		GameItem val = Item(set + ".cage");
		if (val != null)
		{
			return Count((GameInventory)(object)AnimalCage.GetCageInventory(val), (GameItem it) => it.IsTag("ANIMAL_ITEM_TAG") && !HusbandryHelper.IsAnimalDead(it));
		}
		return -1;
	}

	public static int DeadInTray(string set)
	{
		GameItem val = Item(set + ".butcher");
		if (val != null)
		{
			return Count((GameInventory)(object)MachineAutoButcher.GetOutputGrid(val), (GameItem it) => it.IsTag("ANIMAL_ITEM_TAG") && HusbandryHelper.IsAnimalDead(it));
		}
		return -1;
	}

	public static int TrayItems(string set)
	{
		GameItem val = Item(set + ".butcher");
		if (val != null)
		{
			return Count((GameInventory)(object)MachineAutoButcher.GetOutputGrid(val), (GameItem it) => true);
		}
		return -1;
	}

	public static int LiveFish(string set)
	{
		GameItem val = Item(set + ".ras");
		if (val != null)
		{
			return Count((GameInventory)(object)MachineRAS.GetTankGrid(val), (GameItem it) => AquacultureHelper.IsFish(it) && !AquacultureHelper.IsDead(it));
		}
		return -1;
	}

	public static int DeadFishInTank(string set)
	{
		GameItem val = Item(set + ".ras");
		if (val != null)
		{
			return Count((GameInventory)(object)MachineRAS.GetTankGrid(val), (Func<GameItem, bool>)AquacultureHelper.IsCarcass);
		}
		return -1;
	}

	public static int CarcassInTray(string set)
	{
		GameItem val = Item(set + ".rasbutcher");
		if (val != null)
		{
			return Count((GameInventory)(object)MachineAutoButcher.GetOutputGrid(val), (Func<GameItem, bool>)AquacultureHelper.IsCarcass);
		}
		return -1;
	}

	public static int RasTrayItems(string set)
	{
		GameItem val = Item(set + ".rasbutcher");
		if (val != null)
		{
			return Count((GameInventory)(object)MachineAutoButcher.GetOutputGrid(val), (GameItem it) => true);
		}
		return -1;
	}

	public static int RasFeederKcal(string set)
	{
		GameItem val = Item(set + ".ras");
		object obj;
		if (val != null)
		{
			GameSlotInventory dispenserSlot = MachineRAS.GetDispenserSlot(val);
			obj = ((dispenserSlot != null) ? dispenserSlot.childItem : null);
		}
		else
		{
			obj = null;
		}
		GameItem val2 = (GameItem)obj;
		if (val2 != null)
		{
			return val2.GetTagReadonly("STORED_KCAL_TAG").GetInt();
		}
		return -1;
	}

	public static void Wire()
	{
		if (wired)
		{
			return;
		}
		wired = true;
		Net.On("production.bot.mark", delegate(Mark msg, int from)
		{
			if (msg != null && msg.name != null)
			{
				if (!Marks.TryGetValue(msg.name, out var value))
				{
					value = (Marks[msg.name] = new HashSet<int>());
				}
				value.Add(from);
				Log.Info("production lab: mark '" + msg.name + "' from " + Net.PlayerName(from) + " (" + value.Count + ")");
			}
		}, null);
		Net.On("production.bot.unlock", delegate(Unlock msg, int from)
		{
			Log.Info("production lab: " + Net.PlayerName(from) + " asks the server to unlock the " + msg?.what + " outside any action (Applying " + Net.Applying + ")");
			if (msg?.what == "fountain")
			{
				VendingFountainHelper.UnlockVendingFountain();
			}
			else
			{
				VendingMachineHelper.UnlockVendingMachine(0);
			}
		}, null);
		Net.On("production.bot.cash", delegate(Cash msg, int from)
		{
			GameItem val = Item(msg?.what ?? "");
			if (val == null)
			{
				Log.Warn("production lab: cash for the " + msg?.what + ": no such machine");
			}
			else
			{
				using (Net.Apply())
				{
					SetInt(val, "VENDING_CASH", msg.cash);
				}
				Cashed++;
				Net.ToAll("production.bot.cashed", msg);
			}
		}, null);
		Net.On("production.bot.cashed", null, delegate(Cash msg)
		{
			GameItem val = Item(msg?.what ?? "");
			if (val == null)
			{
				Log.Warn("production lab: replayed cash for the " + msg?.what + ": no such machine here");
			}
			else
			{
				using (Net.Apply())
				{
					SetInt(val, "VENDING_CASH", msg.cash);
				}
				Cashed++;
			}
		});
	}

	public static string SpawnAll()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		List<string> list = new List<string>();
		using (Net.Apply())
		{
			PlayerStore store = Game.Store;
			if (store.playerCash < 6000)
			{
				store.playerCash = 6000;
			}
			if (instance.vendingMachineElement.childItem != null)
			{
				list.Add("vending machine ALREADY unlocked");
			}
			if (instance.vendingFountainElement.childItem != null)
			{
				list.Add("fountain ALREADY unlocked");
			}
			FishSpecies val = FishSpecies.All.Values.First((FishSpecies x) => !string.IsNullOrEmpty(x.fishIdentifier));
			int num = val.stageStartDays[val.stageStartDays.Length - 1];
			string[] sets = Sets;
			foreach (string text in sets)
			{
				GameGridInventory[] array = (GameGridInventory[])(object)((text == "1") ? new GameGridInventory[3] { instance.invElement, instance.hiddenElement, instance.hirelingInv } : new GameGridInventory[3] { instance.hirelingInv, instance.invElement, instance.hiddenElement });
				GameGridInventory[] array2 = array;
				GameGridInventory[] array3 = (GameGridInventory[])(object)new GameGridInventory[2] { instance.invElement, instance.hiddenElement };
				GameItem val2 = Spawn("livestock_feed_dispenser", array, list);
				GameGridInventory storageGrid = MachineFeedDispenser.GetStorageGrid(val2);
				Put((GameInventory)(object)storageGrid, DirectoryMaster.Item("processed_meat", true), "food");
				Put((GameInventory)(object)storageGrid, DirectoryMaster.Item("processed_meat", true), "food");
				Put((GameInventory)(object)storageGrid, PreBuiltItemHelper.CreateRat("male", 6, false), "feeder rat");
				Fx[text + ".feeder"] = Ids.Of(val2);
				GameItem val3 = Spawn("animal_cage", array, list);
				GameGridInventory cageInventory = AnimalCage.GetCageInventory(val3);
				Put((GameInventory)(object)cageInventory, PreBuiltItemHelper.CreateRat("female", 8, false), "cage rat");
				Put((GameInventory)(object)cageInventory, PreBuiltItemHelper.CreateRat("male", 8, false), "cage rat");
				Put((GameInventory)(object)cageInventory, PreBuiltItemHelper.CreateRat("female", 12, false), "cage rat");
				GameItem val4 = DirectoryMaster.Item("auto_butcher", true);
				Put((GameInventory)(object)AnimalCage.GetButcherSlot(val3), val4, "butcher in the cage");
				GameItem val5 = PreBuiltItemHelper.CreateRat("male", 9, false);
				HusbandryHelper.OnAnimalDead(val5, false);
				Put((GameInventory)(object)MachineAutoButcher.GetOutputGrid(val4), val5, "dead rat in the tray");
				Fx[text + ".cage"] = Ids.Of(val3);
				Fx[text + ".butcher"] = Ids.Of(val4);
				Fx[text + ".butcher2"] = Ids.Of(Spawn("auto_butcher", array, list));
				Fx[text + ".water"] = Ids.Of(Spawn(WaterPremadeHelper.BaseWaterAsHQWater("bottled_water"), array2, list));
				Fx[text + ".water2"] = Ids.Of(Spawn(WaterPremadeHelper.BaseWaterAsHQWater("bottled_water"), array2, list));
				GameItem val6 = DirectoryMaster.Item("birth_control_pill", true) ?? throw new InvalidOperationException("the game has no birth_control_pill");
				ItemFeature obj = ChemicalProductHelper.GetChemicalFeature(val6) ?? throw new InvalidOperationException("the pill has no chemical label");
				ItemCondition realCondition = obj.realCondition;
				ItemCondition val7 = ((realCondition != null && realCondition.identifier == "lab_pure") ? ItemConditionList.CreateJunkGrade() : ItemConditionList.CreateLabPure());
				ChemicalProductHelper.SetPurityFeature(val6, realCondition, val7);
				obj.isFeatureMatch = false;
				Fx[text + ".chem"] = Ids.Of(Spawn(val6, array2, list));
				Fx[text + ".food"] = Ids.Of(Spawn("processed_meat", array3, list));
				GameItem val8 = MachineRAS.SpawnRASWithEmptyTank() ?? throw new InvalidOperationException("the game has no RAS");
				Spawn(val8, array, list);
				GameItem val9 = DirectoryMaster.Item("auto_butcher", true);
				Put((GameInventory)(object)MachineRAS.GetButcherSlot(val8), val9, "butcher in the RAS");
				Put((GameInventory)(object)MachineRAS.GetDispenserSlot(val8), DirectoryMaster.Item("livestock_feed_dispenser", true), "dispenser in the RAS");
				GameGridInventory tankGrid = MachineRAS.GetTankGrid(val8);
				Put((GameInventory)(object)tankGrid, AquacultureItems.SpawnFish(val, "female", num), "live fish");
				Put((GameInventory)(object)tankGrid, AquacultureItems.SpawnFish(val, "male", num), "live fish");
				GameItem val10 = AquacultureItems.SpawnFish(val, "male", num);
				AquacultureHelper.OnFishDead(val10);
				Put((GameInventory)(object)tankGrid, val10, "dead fish in the tank");
				GameItem val11 = AquacultureItems.SpawnFish(val, "female", num);
				AquacultureHelper.OnFishDead(val11);
				Put((GameInventory)(object)MachineAutoButcher.GetOutputGrid(val9), val11, "fish carcass in the RAS butcher's tray");
				Fx[text + ".ras"] = Ids.Of(val8);
				Fx[text + ".rasbutcher"] = Ids.Of(val9);
			}
			Fx["hw"] = Ids.Of(Spawn(WaterPremadeHelper.BaseWaterAsHQWater("bottled_water"), (GameGridInventory[])(object)new GameGridInventory[2] { instance.hirelingInv, instance.invElement }, list));
			list.Add("fish " + val.speciesID + " age " + num);
		}
		return Fx.Count + " fixtures (" + string.Join(", ", from p in list
			group p by p into g
			select g.Key + " x" + g.Count()) + ")";
	}

	private static void SetInt(GameItem it, string tag, int v)
	{
		it.ModifyTag(tag, (Action<TagState>)delegate(TagState s)
		{
			s.Enable().SetInt(v);
		}, false);
	}

	private static void Put(GameInventory inv, GameItem it, string what)
	{
		if (inv == null)
		{
			throw new InvalidOperationException("no inventory for the " + what);
		}
		if (it == null)
		{
			throw new InvalidOperationException("no item for the " + what);
		}
		GraphUtils.TryAcceptAll((GraphNodeStorage)(object)inv, it, -1);
		if (it.parentInventory != inv)
		{
			throw new InvalidOperationException("the " + what + " (" + it.identifier + ") did not fit");
		}
	}

	private static GameItem Spawn(string kind, GameGridInventory[] into, List<string> where)
	{
		return Spawn(DirectoryMaster.Item(kind, true) ?? throw new InvalidOperationException("the game has no item '" + kind + "'"), into, where);
	}

	private static GameItem Spawn(GameItem it, GameGridInventory[] into, List<string> where)
	{
		string identifier = it.identifier;
		EmporiumEntry instance = EmporiumEntry.Instance;
		foreach (GameGridInventory val in into)
		{
			if (val != null && Place(it, val))
			{
				where.Add((val == instance.invElement) ? "inv" : ((val == instance.hiddenElement) ? "stash" : "hireling"));
				return it;
			}
		}
		it.Destroy();
		throw new InvalidOperationException("no free cell for " + identifier);
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
