using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsOffline : ItemsBotBase
{
	private static readonly FieldInfo fCur = AccessTools.Field(typeof(ItemMouseDragHandler), "currentItem");

	private static readonly FieldInfo fLastItem = AccessTools.Field(typeof(ItemMouseDragHandler), "lastItem");

	private static readonly FieldInfo fLastInv = AccessTools.Field(typeof(ItemMouseDragHandler), "lastInventory");

	private static readonly FieldInfo fRemove = AccessTools.Field(typeof(ItemMouseDragHandler), "currentRemove");

	private static readonly FieldInfo fDragging = AccessTools.Field(typeof(ItemMouseDragHandler), "isDragging");

	private static readonly MethodInfo mEndDrag = AccessTools.Method(typeof(ItemMouseDragHandler), "EndDrag", (Type[])null, (Type[])null);

	private static readonly FieldInfo fSelected = AccessTools.Field(typeof(ItemMultiSelectHandler), "selectedItems");

	private static readonly MethodInfo mPrecise = AccessTools.Method(typeof(ItemMultiSelectHandler), "CommitPrecisePlacement", (Type[])null, (Type[])null);

	private long bagId;

	private long yId;

	private long zId;

	private long cId;

	private long vId;

	private long pId;

	private List<long> pair = new List<long>();

	private Dictionary<long, (int, int)> pairFrom = new Dictionary<long, (int, int)>();

	private int pdx;

	private int pdy;

	private int uid0;

	private string beforeSort = "";

	public override string Name => "offline-items";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 600f;

	private GameItem I(long id)
	{
		return Items.Find(id);
	}

	private static string Untouched()
	{
		if (ItemsMove.SentCount != 0 || ItemsMove.AppliedCount != 0 || ItemsMove.RejectCount != 0 || ItemsCapture.Current != null || Net.Applying)
		{
			return "FAIL sent " + ItemsMove.SentCount + ", applied " + ItemsMove.AppliedCount + ", rejects " + ItemsMove.RejectCount + ", capture " + (ItemsCapture.Current?.kind ?? "none");
		}
		return "no request, nothing applied, no capture";
	}

	private BotStep Local(string name, Func<string> act, Func<bool> movedNow)
	{
		return Step(name, 0f, delegate(BotStep st)
		{
			string text;
			try
			{
				text = act() ?? "";
			}
			catch (Exception ex)
			{
				Exception ex2 = ((ex is TargetInvocationException { InnerException: not null } ex3) ? ex3.InnerException : ex);
				text = "FAIL threw " + ex2.GetType().Name + ": " + ex2.Message;
			}
			bool flag = !text.StartsWith("FAIL", StringComparison.Ordinal) && !text.StartsWith("SKIP", StringComparison.Ordinal) && movedNow();
			string text2 = Untouched();
			st.Detail = text + "; " + (flag ? "moved at once (same frame)" : "NOT moved") + "; " + text2;
			return (flag && !text2.StartsWith("FAIL", StringComparison.Ordinal)) ? StepStatus.Pass : StepStatus.Fail;
		});
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
			return "new WATER_MERCHANT run (normal, intro skipped), loading InventoryScene; data folder " + (Paths.Redirecting ? Paths.Override : "the account's");
		});
		WaitFor("shop", 240f, () => Items.Ready, () => "scene " + BotProbes.Scene);
		Delay("shop-settle", 3f);
		Do("state", delegate
		{
			uid0 = Game.Store.currentUniqueId;
			return "role " + Net.Role.ToString() + ", day " + Game.Day + ", cash " + Game.Store.playerCash + ", slot " + Game.Store.saveSlotId + ", " + ItemsMoveProbes.IdCheck() + ", uid " + uid0;
		});
		Do("bag", delegate
		{
			GameItem val = DirectoryMaster.Item("backpack_small", true);
			GeneralHelper.SetItemOwned(val, true);
			SlotMarker val2 = ItemsBotBase.E.invElement.TryFindOneValidInventorySlot(val, false);
			if (val2?.itemGridShape == null)
			{
				return "FAIL no room for a bag";
			}
			val.modifiedShape = (GridShape)(object)new GridShapeBuilder(val.modifiedShape).SetTransform(val2.itemGridShape);
			((GameInventory)ItemsBotBase.E.invElement).UncheckedAccept(val);
			bagId = Ids.Of(val);
			return "bag #" + bagId + " in the main storage";
		});
		Local("drag-insert", delegate
		{
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			GameItem bag = I(bagId);
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement, (GameItem i) => i is GameItemElement && Ids.Of(i) != bagId)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => bag.MayHaveValidInventorySlot(i) && bag.TryFindOneValidInventorySlot(i) != null));
			if ((Object)(object)current == (Object)null || val == null)
			{
				return "SKIP no drag handler or nothing fits the bag";
			}
			yId = Ids.Of(val);
			fCur.SetValue(current, val);
			fLastItem.SetValue(current, bag);
			fLastInv.SetValue(current, null);
			fRemove.SetValue(current, val.unitCount);
			fDragging.SetValue(current, true);
			string text = "";
			try
			{
				mEndDrag.Invoke(current, null);
			}
			catch (Exception ex)
			{
				text = "; EndDrag's clean-up threw " + (ex.InnerException ?? ex).GetType().Name;
			}
			fDragging.SetValue(current, false);
			fCur.SetValue(current, null);
			fLastItem.SetValue(current, null);
			return val.identifier + "#" + yId + " dropped on bag#" + bagId + " by EndDrag" + text;
		}, delegate
		{
			GameItem obj = I(yId);
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
			return obj2 == I(bagId);
		});
		Local("quick", delegate
		{
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement, (GameItem i) => Ids.Of(i) != bagId)).FirstOrDefault((Func<GameItem, bool>)delegate(GameItem i)
			{
				SlotMarker obj = ItemsBotBase.E.showcaseWindow.TryFindOneValidInventorySlot(i);
				return obj != null && obj.IsValid();
			});
			if (val == null)
			{
				return "SKIP nothing fits the display case";
			}
			zId = Ids.Of(val);
			int num = GraphUtils.TryAcceptOnce((GraphNodeStorage)(object)ItemsBotBase.E.showcaseWindow, val, -1);
			return val.identifier + "#" + zId + " quick -> showcase.w (left " + num + ")";
		}, delegate
		{
			GameItem obj = I(zId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.showcaseElement;
		});
		Local("group-precise", delegate
		{
			ItemMultiSelectHandler current = ItemMultiSelectHandler.current;
			if ((Object)(object)current == (Object)null)
			{
				return "SKIP no multi-select handler";
			}
			List<GameItem> list = ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement, (GameItem i) => i is GameItemElement && Ids.Of(i) != bagId);
			for (int num = 0; num < list.Count; num++)
			{
				for (int num2 = num + 1; num2 < list.Count; num2++)
				{
					(int, int)[] array = new(int, int)[6]
					{
						(1, 0),
						(-1, 0),
						(0, 1),
						(0, -1),
						(2, 0),
						(0, 2)
					};
					for (int num3 = 0; num3 < array.Length; num3++)
					{
						(int, int) tuple = array[num3];
						List<GameItem> list2 = new List<GameItem>
						{
							list[num],
							list[num2]
						};
						if (ItemsMove.FormationProblem(list2, (GameInventory)(object)ItemsBotBase.E.invElement, tuple.Item1, tuple.Item2) == null)
						{
							pair = list2.Select(Ids.Of).ToList();
							pdx = tuple.Item1;
							pdy = tuple.Item2;
							pairFrom = list2.ToDictionary(Ids.Of, (GameItem i) => (minX: i.modifiedShape.minX, minY: i.modifiedShape.minY));
							List<GameItemElement> list3 = (List<GameItemElement>)fSelected.GetValue(current);
							list3.Clear();
							list3.Add((GameItemElement)list[num]);
							list3.Add((GameItemElement)list[num2]);
							try
							{
								mPrecise.Invoke(current, new object[3]
								{
									ItemsBotBase.E.invElement,
									pdx,
									pdy
								});
							}
							finally
							{
								list3.Clear();
							}
							return string.Join("+", list2.Select((GameItem i) => i.identifier + "#" + Ids.Of(i))) + " by " + pdx + "," + pdy;
						}
					}
				}
			}
			return "SKIP no pair can shift";
		}, () => pair.Count == 2 && pair.All((long id) => I(id) != null && I(id).modifiedShape.minX == pairFrom[id].Item1 + pdx && I(id).modifiedShape.minY == pairFrom[id].Item2 + pdy));
		Local("sort", delegate
		{
			beforeSort = ItemsMove.Digest();
			if (!InventorySortHelper.CanSort(ItemsBotBase.E.invElement))
			{
				return "SKIP the main storage cannot be sorted";
			}
			bool flag = InventorySortHelper.Sort(ItemsBotBase.E.invElement, true, false);
			return "Sort returned " + flag + ", digest " + beforeSort + " -> " + ItemsMove.Digest();
		}, () => ItemsMove.Digest() != beforeSort);
		Local("undo", delegate
		{
			bool flag = InventorySortHelper.Undo(ItemsBotBase.E.invElement);
			return "Undo returned " + flag + ", digest now " + ItemsMove.Digest() + " (before the sort " + beforeSort + ")";
		}, () => ItemsMove.Digest() == beforeSort);
		Local("pin", delegate
		{
			GameItem val = ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement, (GameItem i) => Ids.Of(i) != bagId).FirstOrDefault();
			if (val == null)
			{
				return "SKIP nothing to pin";
			}
			pId = Ids.Of(val);
			bool flag = ItemPinHelper.TogglePinned(val);
			return val.identifier + "#" + pId + " TogglePinned returned " + flag;
		}, () => ItemPinHelper.IsPinned(I(pId)));
		Local("trash", delegate
		{
			GameItem trashcan = ItemsBotBase.E.trashcan;
			object obj;
			if (trashcan == null)
			{
				obj = null;
			}
			else
			{
				PixelWindow contentWindow = trashcan.contentWindow;
				obj = ((contentWindow != null) ? contentWindow.child : null);
			}
			GameInventory trash = (GameInventory)((obj is GameInventory) ? obj : null);
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement, (GameItem i) => Ids.Of(i) != bagId)).FirstOrDefault((Func<GameItem, bool>)delegate(GameItem i)
			{
				GameInventory obj2 = trash;
				return ((obj2 != null) ? obj2.TryFindOneValidInventorySlot(i) : null) != null;
			});
			if (val == null)
			{
				return "SKIP nothing fits the trash can";
			}
			vId = Ids.Of(val);
			int num = GraphUtils.TryAcceptAll((GraphNodeStorage)(object)trash, val, -1);
			return val.identifier + "#" + vId + " -> trash (left " + num + ")";
		}, delegate
		{
			GameItem obj = I(vId);
			GameInventory obj2 = ((obj != null) ? obj.parentInventory : null);
			GameItem trashcan = ItemsBotBase.E.trashcan;
			object obj3;
			if (trashcan == null)
			{
				obj3 = null;
			}
			else
			{
				PixelWindow contentWindow = trashcan.contentWindow;
				obj3 = ((contentWindow != null) ? contentWindow.child : null);
			}
			return obj2 == obj3;
		});
		Local("unload", delegate
		{
			I(bagId).TryUnloadAll(true);
			return "bag#" + bagId + " unloaded";
		}, delegate
		{
			if (I(yId) != null)
			{
				GameInventory parentInventory = I(yId).parentInventory;
				return ((parentInventory != null) ? parentInventory.GetParentItem() : null) != I(bagId);
			}
			return false;
		});
		Local("to-counter", delegate
		{
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement, (GameItem i) => Ids.Of(i) != bagId)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.E.backInvinvElement.TryFindOneValidInventorySlot(i, false) != null));
			if (val == null)
			{
				return "SKIP nothing fits the back counter";
			}
			cId = Ids.Of(val);
			int num = GraphUtils.TryAcceptAll((GraphNodeStorage)(object)ItemsBotBase.E.backInvinvElement, val, -1);
			return val.identifier + "#" + cId + " -> back counter (left " + num + ")";
		}, delegate
		{
			GameItem obj = I(cId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.backInvinvElement;
		});
		Local("transfer-button", delegate
		{
			StoreTransferButton val = Object.FindObjectOfType<StoreTransferButton>();
			if ((Object)(object)val == (Object)null)
			{
				return "SKIP no transfer button in the scene";
			}
			val.OnClick();
			return "StoreTransferButton.OnClick";
		}, delegate
		{
			GameItem obj = I(cId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.invElement;
		});
		Check("ids", () => ItemsMoveProbes.IdCheck().Contains("dup=0 zero=0"), () => ItemsMoveProbes.IdCheck() + ", uid " + uid0 + " -> " + Game.Store.currentUniqueId);
		Check("still-offline", () => Net.Role == ProbablyTogether.Role.Off && ItemsMove.SentCount == 0 && ItemsMove.AppliedCount == 0, () => "role " + Net.Role.ToString() + ", requests " + ItemsMove.SentCount + ", applied " + ItemsMove.AppliedCount);
		Do("final", () => "digest " + ItemsMove.Digest() + "; " + ItemsMoveProbes.IdCheck() + "; cash " + Game.Store.playerCash);
	}
}
