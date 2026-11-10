using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CarryOffline : CarryBotBase
{
	private long bagId;

	private long yId;

	public override string Name => "carry-offline";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 600f;

	private static string Quiet()
	{
		if (Carry.StartsByHook != 0 || Carry.StartsByPoll != 0 || Carry.EndsByHook != 0 || Carry.LocalItem != 0L || ItemsMove.SentCount != 0 || Presence.Remotes.Count != 0 || (!((Object)(object)Overlay.Root == (Object)null) && ((Component)Overlay.Root).gameObject.activeInHierarchy) || (!((Object)(object)Overlay.Cursors == (Object)null) && !((Component)Overlay.Cursors).GetComponentsInChildren<Transform>(true).All((Transform t) => ((Object)t).name != "carry")))
		{
			return "FAIL starts " + Carry.StartsByHook + "/" + Carry.StartsByPoll + ", ends " + Carry.EndsByHook + ", local " + Carry.LocalItem + ", sent " + ItemsMove.SentCount + ", remotes " + Presence.Remotes.Count + ", overlay " + ((Object)(object)Overlay.Root != (Object)null && ((Component)Overlay.Root).gameObject.activeInHierarchy);
		}
		return "carry untouched (no start, no end, nothing sent, no overlay, no icon)";
	}

	protected override void Build()
	{
		Check("offline", () => !Net.IsOnline && Net.Role == ProbablyTogether.Role.Off, () => "role " + Net.Role);
		WaitFor("menu", 180f, () => BotProbes.Scene == "EmporiumMenu" && (Object)(object)NewGameData.Instance != (Object)null, () => "scene " + BotProbes.Scene);
		Delay("menu-settle", 3f);
		Do("new-run", delegate
		{
			NewGameData instance = NewGameData.Instance;
			instance.saveFile = null;
			instance.isLoadFromMainMenu = false;
			instance.saveSlot = -1;
			instance.startType = (NewGameData.StartType)1;
			instance.startScenarioId = "";
			instance.skipIntro = true;
			instance.disableTutorialHint = true;
			instance.difficultyId = "normal";
			EmporiumEntry.sceneLoadStartTime = Time.realtimeSinceStartup;
			SceneManager.LoadScene("InventoryScene", (LoadSceneMode)0);
			return "new WATER_MERCHANT run, loading InventoryScene";
		});
		WaitFor("shop", 240f, () => Items.Ready, () => "scene " + BotProbes.Scene);
		Delay("shop-settle", 3f);
		Do("bag", delegate
		{
			GameItem val = DirectoryMaster.Item("backpack_small", true);
			GeneralHelper.SetItemOwned(val, true);
			SlotMarker val2 = ItemsBotBase.E.invElement.TryFindOneValidInventorySlot(val, false);
			if (val2?.itemGridShape == null)
			{
				throw new Exception("no room for a bag");
			}
			val.modifiedShape = (GridShape)(object)new GridShapeBuilder(val.modifiedShape).SetTransform(val2.itemGridShape);
			((GameInventory)ItemsBotBase.E.invElement).UncheckedAccept(val);
			bagId = Ids.Of(val);
			return "bag #" + bagId + " in the main storage (test setup through the game's own calls)";
		});
		Step("drag-start", 0f, delegate(BotStep st)
		{
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			GameItem bag = CarryBotBase.I(bagId);
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement, (GameItem i) => i is GameItemElement && Ids.Of(i) != bagId)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => bag.MayHaveValidInventorySlot(i) && bag.TryFindOneValidInventorySlot(i) != null));
			if ((Object)(object)current == (Object)null || val == null)
			{
				st.Detail = "no drag handler or nothing fits the bag";
				return StepStatus.Fail;
			}
			yId = Ids.Of(val);
			CarryBotBase.fCur.SetValue(current, val);
			CarryBotBase.mStart.Invoke(current, null);
			string text = Quiet();
			st.Detail = val.identifier + "#" + yId + " picked up by the game's StartDrag: dragging " + current.IsDraggingItem + "; " + text;
			return (current.IsDraggingItem && !text.StartsWith("FAIL", StringComparison.Ordinal)) ? StepStatus.Pass : StepStatus.Fail;
		});
		Delay("held", 2f);
		Check("held-quiet", () => ItemMouseDragHandler.current.IsDraggingItem && !Quiet().StartsWith("FAIL", StringComparison.Ordinal), () => "still dragging " + ItemMouseDragHandler.current.IsDraggingItem + "; " + Quiet());
		Step("drop", 0f, delegate(BotStep st)
		{
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			CarryBotBase.fLastItem.SetValue(current, CarryBotBase.I(bagId));
			CarryBotBase.fLastInv.SetValue(current, null);
			string text = "";
			try
			{
				CarryBotBase.mEnd.Invoke(current, null);
			}
			catch (Exception ex)
			{
				text = "; EndDrag's clean-up threw " + (ex.InnerException ?? ex).GetType().Name;
			}
			CarryBotBase.fCur.SetValue(current, null);
			CarryBotBase.fLastItem.SetValue(current, null);
			GameItem obj = CarryBotBase.I(yId);
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				GameInventory parentInventory = obj.parentInventory;
				obj2 = ((parentInventory != null) ? parentInventory.GetParentItem() : null);
			}
			bool flag = obj2 == CarryBotBase.I(bagId);
			string text2 = Quiet();
			st.Detail = "the game's EndDrag on the bag: " + (flag ? "moved into the bag at once" : "NOT moved") + ", dragging " + current.IsDraggingItem + text + "; " + text2;
			return (flag && !current.IsDraggingItem && !text2.StartsWith("FAIL", StringComparison.Ordinal)) ? StepStatus.Pass : StepStatus.Fail;
		});
	}
}
