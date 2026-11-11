using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

/// Keeps the dragged picture in the local hand while replicated changes redraw the inventory.
/// A redraw puts the picture back in its slot; Before/After remember the hand and restore it.
internal static class DragKeeper
{
	private static readonly FieldInfo removeField = Safe(() => AccessTools.Field(typeof(ItemMouseDragHandler), "currentRemove"));

	private static GameItemElement held;

	private static Vector3 position;

	private static Quaternion rotation;

	private static int units;

	private static bool armed;

	internal static int Restored;

	private static FieldInfo Safe(Func<FieldInfo> find)
	{
		try
		{
			return find();
		}
		catch
		{
			return null;
		}
	}

	/// Called when the outermost Net.Apply scope opens.
	internal static void Before()
	{
		armed = false;
		try
		{
			if (!Net.HasLocalPlayer)
			{
				return;
			}
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			if ((Object)(object)current == (Object)null || !current.IsDraggingItem)
			{
				return;
			}
			GameItem currentItem = current.GetCurrentItem();
			held = (GameItemElement)(object)((currentItem is GameItemElement) ? currentItem : null);
			if (held == null || held.fakeMiddle == null || held.fakeRotator == null || held.fakeBackground == null)
			{
				return;
			}
			position = held.fakeMiddle.rectTransform.position;
			rotation = held.fakeRotator.rectTransform.localRotation;
			units = ((removeField != null) ? ((int)removeField.GetValue(current)) : ((GameItem)held).unitCount);
			armed = true;
		}
		catch
		{
			armed = false;
		}
	}

	internal static void After()
	{
		if (!armed)
		{
			return;
		}
		armed = false;
		try
		{
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			if ((Object)(object)current == (Object)null || !current.IsDraggingItem || current.GetCurrentItem() != held || held.IsDestroyed())
			{
				return;
			}
			if (held.fakeBackground.image.enabled)
			{
				return;
			}
			held.UpdateFakeReal(Mathf.Clamp(units, 1, Mathf.Max(1, ((GameItem)held).unitCount)));
			held.ToggleFakeBackground(true, UISettings.current.dragOpacity, 1f);
			held.fakeMiddle.rectTransform.position = position;
			held.fakeRotator.rectTransform.localRotation = rotation;
			Restored++;
		}
		catch (Exception ex)
		{
			if (Log.Allow("ui|dragkeeper", 30000L, out var skipped))
			{
				Log.Warn("ui: keeping the held item in hand threw" + Log.More(skipped) + ": " + ex.Message);
			}
		}
		finally
		{
			held = null;
		}
	}
}

/// An item another player holds cannot be picked up until they let go.
[HarmonyPatch]
internal static class HoldGuardPatch
{
	private static readonly MethodBase target = Find();

	internal static int Blocked;

	private static MethodBase Find()
	{
		try
		{
			return AccessTools.Method(typeof(ItemMouseDragHandler), "StartDrag", (Type[])null, (Type[])null);
		}
		catch
		{
			return null;
		}
	}

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	/// The player holding this item (0: nobody else).
	internal static int HeldBy(GameItem item)
	{
		if (item == null || !Net.IsOnline)
		{
			return 0;
		}
		long num = Ids.Of(item);
		if (num == 0L)
		{
			return 0;
		}
		foreach (KeyValuePair<int, Presence.Remote> remote in Presence.Remotes)
		{
			if (remote.Key != Net.LocalPlayer && remote.Value != null && remote.Value.Drag == num)
			{
				return remote.Key;
			}
		}
		return 0;
	}

	private static bool Prefix(ItemMouseDragHandler __instance)
	{
		try
		{
			if (!Net.IsOnline || !Net.HasLocalPlayer || Net.Applying)
			{
				return true;
			}
			int num = HeldBy(__instance.GetCurrentItem());
			if (num == 0)
			{
				return true;
			}
			Blocked++;
			Notices.Show(Lang.Trf("{0} is holding that.", Kit.Name(Net.PlayerName(num))), "hold");
			return false;
		}
		catch
		{
			return true;
		}
	}
}
