using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

[HarmonyPatch(typeof(PlayerStore), "LoadGame")]
internal static class LoadGameFeaturesPatch
{
	internal static bool InLoad;

	internal static List<ItemFeature> Captured;

	internal static int LastItems;

	internal static int LastFeatures;

	private static bool Prepare()
	{
		if (AccessTools.Method(typeof(PlayerStore), "LoadGame", Type.EmptyTypes, (Type[])null) != null)
		{
			return AccessTools.Method(typeof(PlayerStore), "DecodeSaveItem", new Type[2]
			{
				typeof(string),
				typeof(GameInventory)
			}, (Type[])null) != null;
		}
		return false;
	}

	private static void Prefix()
	{
		InLoad = Net.IsOnline;
		Captured = null;
	}

	private static void Postfix(PlayerStore __instance)
	{
		if (!InLoad)
		{
			return;
		}
		InLoad = false;
		List<ItemFeature> captured = Captured;
		Captured = null;
		if (captured == null)
		{
			return;
		}
		try
		{
			Reattach(captured);
		}
		catch (Exception ex)
		{
			Log.Error("re-attaching saved item features (hireling, sold, cassette player) failed: " + ex);
		}
	}

	private static Exception Finalizer(Exception __exception)
	{
		InLoad = false;
		Captured = null;
		return __exception;
	}

	private static void Reattach(List<ItemFeature> saved)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		HashSet<int> hashSet = new HashSet<int>();
		foreach (GameItem allItem in instance.GetAllItems())
		{
			hashSet.Add(allItem.uniqueId);
		}
		Dictionary<int, List<ItemFeature>> dictionary = new Dictionary<int, List<ItemFeature>>();
		foreach (ItemFeature item in saved)
		{
			if (item != null)
			{
				if (!dictionary.TryGetValue(item.parentItemUniqueId, out var value))
				{
					value = (dictionary[item.parentItemUniqueId] = new List<ItemFeature>());
				}
				value.Add(item);
			}
		}
		int num = 0;
		int num2 = 0;
		HashSet<int> hashSet2 = new HashSet<int>();
		GraphNodeStorage[] array = (GraphNodeStorage[])(object)new GraphNodeStorage[3]
		{
			(GraphNodeStorage)instance.hirelingInv,
			(GraphNodeStorage)instance.soldElement,
			(GraphNodeStorage)instance.cassettePlayerElement
		};
		foreach (GraphNodeStorage val in array)
		{
			if (val == null)
			{
				continue;
			}
			foreach (GameItem item2 in GraphUtils.FindAllChildrenType<GameItem>(val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				int uniqueId = item2.uniqueId;
				if (hashSet.Contains(uniqueId) || !hashSet2.Add(uniqueId))
				{
					continue;
				}
				num++;
				item2.RemoveAllFeature();
				if (!dictionary.TryGetValue(uniqueId, out var value2))
				{
					continue;
				}
				foreach (ItemFeature item3 in value2)
				{
					item2.itemFeatures.Add(item3);
					num2++;
				}
			}
		}
		LastItems = num;
		LastFeatures = num2;
		if (num > 0)
		{
			Log.Info("load: saved item features re-attached to " + num + " item(s) in the hireling inventory, sold list and cassette player (" + num2 + " feature(s))");
		}
	}
}
