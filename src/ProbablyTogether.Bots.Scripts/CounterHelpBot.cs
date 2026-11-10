using System;
using ProbablyTogether.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public abstract class CounterHelpBot : CounterBot
{
	protected static readonly string[] DealState = new string[10] { "cust.current", "cust.visit", "cust.holder", "ctr.lock", "ctr.nego", "ctr.feat", "cash", "cust.stock", "cust.dlgfp", "cust.digest" };

	protected static GameItem Tool(string id)
	{
		foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)CounterBot.E.invElement, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
		{
			if (item != null && item.identifier == id && GeneralHelper.IsItemOwned(item) && !ItemsUse.InCounter(item) && (!(id == "water_test_strip") || !item.IsTag("IS_SPENT")))
			{
				return item;
			}
		}
		return null;
	}

	protected static string Flat(string s)
	{
		if (s != null)
		{
			return s.Replace("\r", "").Replace("\n", "/");
		}
		return "null";
	}

	protected static bool LookOpenOn(GameItem t)
	{
		InspectionUIManager instance = InspectionUIManager.Instance;
		if ((Object)(object)instance != (Object)null && (Object)(object)instance.panel != (Object)null && instance.panel.activeSelf)
		{
			if (t != null)
			{
				return instance.currentInspectingItem == t;
			}
			return true;
		}
		return false;
	}

	protected static bool ScanOpenOn(GameItem t)
	{
		WaterScannerUIManager instance = WaterScannerUIManager.Instance;
		if ((Object)(object)instance != (Object)null && (Object)(object)instance.panel != (Object)null && instance.panel.activeSelf)
		{
			if (t != null)
			{
				return instance.currentWaterContainer == t;
			}
			return true;
		}
		return false;
	}

	protected static bool StripOpenOn(GameItem t)
	{
		WaterStripUIManager instance = WaterStripUIManager.Instance;
		if ((Object)(object)instance != (Object)null && (Object)(object)instance.panel != (Object)null && instance.panel.activeSelf)
		{
			if (t != null)
			{
				return instance.currentWaterContainer == t;
			}
			return true;
		}
		return false;
	}

	protected static string Panels()
	{
		return "look " + LookOpenOn(null) + ", scan " + ScanOpenOn(null) + ", strip " + StripOpenOn(null) + "; closed gone " + CounterHelp.ClosedGone + ", held " + CounterHelp.ClosedHeld + ", last " + CounterHelp.LastClosed;
	}
}
