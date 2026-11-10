using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace ProbablyTogether;

internal static class Game
{
	private static readonly FieldInfo storeField = AccessTools.Field(typeof(PlayerStore), "instance");

	private static readonly FieldInfo stationField = AccessTools.Field(typeof(StoreStation), "instance");

	internal static PlayerStore Store
	{
		get
		{
			try
			{
				object? obj = storeField?.GetValue(null);
				return (PlayerStore)((obj is PlayerStore) ? obj : null);
			}
			catch
			{
				return null;
			}
		}
	}

	internal static StoreStation Station
	{
		get
		{
			try
			{
				object? obj = stationField?.GetValue(null);
				return (StoreStation)((obj is StoreStation) ? obj : null);
			}
			catch
			{
				return null;
			}
		}
	}

	internal static bool ShopLoaded
	{
		get
		{
			if (Boot.Scene != "InventoryScene")
			{
				return false;
			}
			if ((Object)(object)EmporiumEntry.Instance != (Object)null && Store != null)
			{
				return Store.saveSlotId != -1;
			}
			return false;
		}
	}

	internal static int Day => (ShopLoaded ? Station : null)?.dayCounter ?? 0;

	internal static List<GraphNodeStorage> Roots()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		List<GraphNodeStorage> list = new List<GraphNodeStorage>();
		if ((Object)(object)instance == (Object)null)
		{
			return list;
		}
		list.Add((GraphNodeStorage)(object)instance.invElement);
		list.Add(Systems.HostTrip.Back(instance));
		list.Add(Systems.HostTrip.Front(instance));
		list.Add((GraphNodeStorage)(object)instance.docInvElement);
		list.Add((GraphNodeStorage)(object)instance.showcaseElement);
		list.Add((GraphNodeStorage)(object)instance.trashInvElement);
		list.Add((GraphNodeStorage)(object)instance.hiddenElement);
		list.Add((GraphNodeStorage)(object)instance.vendingFountainElement);
		list.Add((GraphNodeStorage)(object)instance.vendingMachineElement);
		return list;
	}

	internal static void ToMainMenu(string why)
	{
		Log.Info("leaving the shop for the main menu: " + why);
		try
		{
			PlayerStore store = Store;
			if (store != null)
			{
				store.Reset();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("PlayerStore.Reset: " + ex.Message);
		}
		ResetShopStatics();
		if ((Object)(object)NewGameData.Instance != (Object)null)
		{
			NewGameData.Instance.isLoadFromMainMenu = true;
			NewGameData.Instance.saveSlot = -1;
			NewGameData.Instance.saveFile = null;
		}
		SceneManager.LoadScene("EmporiumMenu", (LoadSceneMode)0);
	}

	internal static void ResetShopStatics()
	{
		try
		{
			EmporiumEntry.ResetData();
		}
		catch (Exception ex)
		{
			Log.Warn("EmporiumEntry.ResetData: " + ex.Message);
		}
		try
		{
			PlayerStore.ResetData();
		}
		catch (Exception ex2)
		{
			Log.Warn("PlayerStore.ResetData: " + ex2.Message);
		}
		try
		{
			StoreStation.ResetData();
		}
		catch (Exception ex3)
		{
			Log.Warn("StoreStation.ResetData: " + ex3.Message);
		}
	}
}
