using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public static class ItemsUseLab
{
	public static readonly string[] Sets = new string[3] { "s", "0", "1" };

	public const int ScrapPerSet = 3;

	public static readonly Dictionary<string, long> Fx = new Dictionary<string, long>(StringComparer.Ordinal);

	public static bool Ready;

	private static readonly FieldInfo fCur = AccessTools.Field(typeof(ItemMouseDragHandler), "currentItem");

	private static readonly FieldInfo fLastItem = AccessTools.Field(typeof(ItemMouseDragHandler), "lastItem");

	private static readonly FieldInfo fLastInv = AccessTools.Field(typeof(ItemMouseDragHandler), "lastInventory");

	private static readonly FieldInfo fRemove = AccessTools.Field(typeof(ItemMouseDragHandler), "currentRemove");

	private static readonly FieldInfo fDragging = AccessTools.Field(typeof(ItemMouseDragHandler), "isDragging");

	private static readonly MethodInfo mEndDrag = AccessTools.Method(typeof(ItemMouseDragHandler), "EndDrag", (Type[])null, (Type[])null);

	private static readonly string[] Kinds = new string[9] { "toolbox", "storage_bay", "storage_bay_expansion_kit", "gene_scanner", "common_chemical", "scratcher", "flashlight", "mind_reprogrammer", "hypertech_electronic" };

	private static readonly string[] Names = new string[9] { "toolbox", "bay", "kit", "scanner", "chem", "ticket", "tool", "mind", "elec" };

	public const int StartCharge = 5;

	public static GameItem Item(string name)
	{
		if (!Fx.TryGetValue(name, out var value))
		{
			return null;
		}
		return Ids.Find(value);
	}

	public static bool Alive(string name)
	{
		return !ItemsUse.Dead(Item(name));
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

	public static int Slots(string k)
	{
		GameItem val = Item(k + ".toolbox");
		if (!ItemsUse.Dead(val))
		{
			return ToolboxHelper.GetUnlockedSlots(val);
		}
		return -1;
	}

	public static string Tool(string k)
	{
		GameItem val = Item(k + ".tool");
		if (ItemsUse.Dead(val))
		{
			return "gone";
		}
		GameInventory parentInventory = val.parentInventory;
		GameItem val2 = ((parentInventory != null) ? parentInventory.GetParentItem() : null);
		if (val2 != null && val2 == Item(k + ".toolbox"))
		{
			return "box:" + (parentInventory.identifier ?? "?");
		}
		return "out:" + (parentInventory?.identifier ?? "none");
	}

	public static int Charge(string k)
	{
		GameItem val = Item(k + ".mind");
		if (!ItemsUse.Dead(val))
		{
			return MindReprogrammerHelper.GetCharge(val);
		}
		return -1;
	}

	public static int Cooldown(string k)
	{
		GameItem val = Item(k + ".mind");
		if (!ItemsUse.Dead(val))
		{
			return MindReprogrammerHelper.GetCooldown(val);
		}
		return -1;
	}

	public static string Perks()
	{
		PlayerStore val = (PlayerStore.HasInstance ? PlayerStore.Instance : null);
		if (val?.startingPerks == null)
		{
			return "";
		}
		return string.Join(",", (from p in val.startingPerks
			where p != null
			select p.id).OrderBy<string, string>((string x) => x, StringComparer.Ordinal));
	}

	public static bool PerkWindowOpen()
	{
		CustomUIWindow val = (((Object)(object)CustomUIManager.Instance != (Object)null) ? CustomUIManager.Instance.GetWindow("mind_reprogrammer") : null);
		if (val != null)
		{
			return val.IsAlive;
		}
		return false;
	}

	public static int Tabs(string k)
	{
		GameItem val = Item(k + ".bay");
		if (!ItemsUse.Dead(val))
		{
			return 1 + StorageExpansionHelper.GetExtraTabs(val);
		}
		return -1;
	}

	public static string TabName(string k, int i)
	{
		GameItem val = Item(k + ".bay");
		object obj;
		if (!ItemsUse.Dead(val))
		{
			obj = StorageExpansionHelper.GetTabName(val, i);
			if (obj == null)
			{
				return "";
			}
		}
		else
		{
			obj = "";
		}
		return (string)obj;
	}

	public static bool ScannerOn(string k)
	{
		GameItem val = Item(k + ".scanner");
		if (!ItemsUse.Dead(val))
		{
			return val.IsTag("IS_ITEM_TOGGLE_ON_TAG");
		}
		return false;
	}

	public static string Ticket(string k)
	{
		GameItem val = Item(k + ".ticket");
		if (ItemsUse.Dead(val))
		{
			return "gone";
		}
		StringBuilder stringBuilder = new StringBuilder(val.IsTag("SCRATCHER_NEW_TAG") ? "N" : "s");
		for (int i = 1; i <= 3; i++)
		{
			stringBuilder.Append(val.GetTagReadonly("SCRATCHER_STATE" + i).GetInt());
		}
		for (int j = 1; j <= 3; j++)
		{
			stringBuilder.Append(':').Append(val.GetTagReadonly("SCRATCHER_SYMBOL" + j).GetInt());
		}
		return stringBuilder.ToString();
	}

	public static int ScrapLeft(string k)
	{
		int num = 0;
		for (int i = 1; i <= 3; i++)
		{
			if (Alive(k + ".scrap" + i))
			{
				num++;
			}
		}
		return num;
	}

	public static IEnumerable<string> StateKeys()
	{
		string[] sets = Sets;
		foreach (string k in sets)
		{
			string[] array = new string[12]
			{
				"slots", "scrap", "tabs", "names", "kit", "scanner", "chem", "ticket", "tool", "charge",
				"cooldown", "elec"
			};
			foreach (string text in array)
			{
				yield return "use." + k + "." + text;
			}
		}
		yield return "use.perks";
		yield return "use.part";
	}

	public static string Drop(GameItem item, GameItem onto)
	{
		ItemMouseDragHandler current = ItemMouseDragHandler.current;
		if ((Object)(object)current == (Object)null || fCur == null || mEndDrag == null)
		{
			throw new InvalidOperationException("no drag handler");
		}
		fCur.SetValue(current, item);
		fLastItem.SetValue(current, onto);
		fLastInv.SetValue(current, null);
		fRemove.SetValue(current, item.unitCount);
		fDragging.SetValue(current, true);
		string result = "";
		try
		{
			mEndDrag.Invoke(current, null);
		}
		catch (Exception ex)
		{
			result = "; EndDrag's clean-up threw " + (ex.InnerException ?? ex).GetType().Name;
		}
		finally
		{
			fDragging.SetValue(current, false);
			fCur.SetValue(current, null);
			fLastItem.SetValue(current, null);
		}
		return result;
	}

	public static StartingPerk PickPerk(GameItem mind, string k)
	{
		List<StartingPerk> all = MindReprogrammerHelper.GetEditablePerks();
		List<StartingPerk> list = all.Where((StartingPerk p) => p != null && (int)p.type == 0 && !StartingPerk.IsPerkActive(p.id) && (p.incompatiblePerks == null || p.incompatiblePerks.Count == 0) && !all.Any((StartingPerk o) => o.incompatiblePerks != null && o.incompatiblePerks.Contains(p.id)) && MindReprogrammerHelper.CanApply(mind, p)).OrderBy<StartingPerk, string>((StartingPerk p) => p.id, StringComparer.Ordinal).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		return list[Math.Min((k == "1") ? 1 : 0, list.Count - 1)];
	}

	public static string SpawnAll()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		List<string> list = new List<string>();
		using (Net.Apply())
		{
			string[] sets = Sets;
			foreach (string text in sets)
			{
				GameGridInventory[] array = (GameGridInventory[])(object)((!(text == "s")) ? new GameGridInventory[3] { instance.hirelingInv, instance.invElement, instance.backInvinvElement } : new GameGridInventory[3] { instance.invElement, instance.hirelingInv, instance.backInvinvElement });
				for (int j = 0; j < Kinds.Length; j++)
				{
					Fx[text + "." + Names[j]] = Spawn(Kinds[j], array, list);
				}
				for (int k = 1; k <= 3; k++)
				{
					Fx[text + ".scrap" + k] = Spawn("scrap_metal", array, list);
				}
				Ids.Find(Fx[text + ".mind"]).ModifyTag("MIND_REPROGRAMMER_CHARGE_INT", (Action<TagState>)delegate(TagState t)
				{
					t.Enable().SetInt(5);
				}, false);
			}
		}
		return Fx.Count + " fixtures: " + string.Join(", ", from p in list
			group p by p into g
			select g.Key + " x" + g.Count());
	}

	private static long Spawn(string kind, GameGridInventory[] into, List<string> where)
	{
		GameItem val = DirectoryMaster.Item(kind, true);
		if (val == null)
		{
			throw new InvalidOperationException("the game has no item '" + kind + "'");
		}
		foreach (GameGridInventory val2 in into)
		{
			if (val2 != null && Place(val, val2))
			{
				where.Add((val2 == EmporiumEntry.Instance.invElement) ? "inv" : ((val2 == EmporiumEntry.Instance.hirelingInv) ? "hireling" : ((val2 == EmporiumEntry.Instance.backInvinvElement) ? "back" : "other")));
				return Ids.Of(val);
			}
		}
		val.Destroy();
		throw new InvalidOperationException("no free cell for " + kind);
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
