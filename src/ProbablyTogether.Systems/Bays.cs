using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

public sealed class MsgBayGrant
{
	public int seat;
}

/// Each player's own storage bay: the owner is a saved tag on the item (PT_OWNER = seat); other players
/// see it locked and the server refuses their requests into it.
public static class Bays
{
	public const string OwnerTag = "PT_OWNER";

	private const string BayItem = "storage_bay";

	private static readonly HashSet<PixelWindow> lockedByUs = new HashSet<PixelWindow>();

	private static readonly HashSet<GameInventory> opened = new HashSet<GameInventory>();

	private static float nextLook;

	private static float nextGrant;

	/// Bays and trades are handed out (server.json playerOwnership).
	public static bool Enabled = true;

	internal static int Grants;

	internal static int Refusals;

	public static int TagOwner(GameItem item)
	{
		try
		{
			if (item == null || !item.IsTag("PT_OWNER"))
			{
				return 0;
			}
			return item.GetTagReadonly("PT_OWNER").GetInt();
		}
		catch
		{
			return 0;
		}
	}

	/// Who alone may use this item: a bay's owner or the holder of the machine's trade (Specs). 0 = everyone.
	public static int OwnerOf(GameItem item)
	{
		int num = TagOwner(item);
		if (num != 0 || item == null)
		{
			return num;
		}
		try
		{
			if (!GeneralHelper.IsItemOwned(item))
			{
				return 0;
			}
			return Specs.OwnerOfType(item.identifier);
		}
		catch
		{
			return 0;
		}
	}

	/// The owner of the bay this node is or is inside (0: nobody's).
	public static int OwnerAround(GraphNodeStorage node, out GameItem bay)
	{
		bay = null;
		GameItem val = (GameItem)(object)((node is GameItem) ? node : null);
		if (val == null)
		{
			GameInventory val2 = (GameInventory)(object)((node is GameInventory) ? node : null);
			val = ((val2 != null) ? val2.GetParentItem() : null);
		}
		int num = 0;
		while (val != null && num++ < 16)
		{
			int num2 = OwnerOf(val);
			if (num2 != 0)
			{
				bay = val;
				return num2;
			}
			GameInventory parentInventory = val.parentInventory;
			val = ((parentInventory != null) ? parentInventory.GetParentItem() : null);
		}
		return 0;
	}

	public static GameItem BayOf(int seat)
	{
		if (seat <= 0)
		{
			return null;
		}
		foreach (GameItem item in Items.All())
		{
			if (TagOwner(item) == seat)
			{
				return item;
			}
		}
		return null;
	}

	private static string OnlyOwner(int owner, GameItem what)
	{
		if (TagOwner(what) != 0)
		{
			return "Only " + Members.NameOf(owner) + " can use this storage bay.";
		}
		return "Only " + Members.NameOf(owner) + " can use this: it belongs to their trade.";
	}

	/// Server: null if this player may touch these places.
	internal static string Refusal(int player, params GraphNodeStorage[] nodes)
	{
		if (player <= 0 || nodes == null)
		{
			return null;
		}
		int num = Members.SeatOf(player);
		foreach (GraphNodeStorage val in nodes)
		{
			if (val == null)
			{
				continue;
			}
			GameItem bay;
			int num2 = OwnerAround(val, out bay);
			// The inventory a bay sits in is not inside the bay.
			if (num2 != 0 && num2 != num)
			{
				Refusals++;
				return OnlyOwner(num2, bay);
			}
		}
		return null;
	}

	/// Server: another player's bay only moves within the shop's own storage.
	internal static string MoveRefusal(int player, GameItem item, GraphNodeStorage target)
	{
		if (player <= 0 || item == null)
		{
			return null;
		}
		int num = OwnerOf(item);
		if (num == 0 || num == Members.SeatOf(player))
		{
			return null;
		}
		if (target != null && InShopStorage(target))
		{
			return null;
		}
		Refusals++;
		return ((TagOwner(item) != 0) ? "This storage bay belongs to " : "This belongs to ") + Members.NameOf(num) + ": it stays in the shop's storage.";
	}

	private static bool InShopStorage(GraphNodeStorage node)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return false;
		}
		GraphNodeStorage[] array = (GraphNodeStorage[])(object)new GraphNodeStorage[6]
		{
			(GraphNodeStorage)instance.invElement,
			(GraphNodeStorage)instance.showcaseElement,
			(GraphNodeStorage)instance.hiddenElement,
			(GraphNodeStorage)instance.invWindow,
			(GraphNodeStorage)instance.showcaseWindow,
			(GraphNodeStorage)instance.hiddenWindow
		};
		foreach (GraphNodeStorage val in array)
		{
			if (val != null && (node == val || GraphUtils.FindParentEquals(node, val)))
			{
				return true;
			}
		}
		return false;
	}

	[SystemInit]
	internal static void Init()
	{
		Net.On<MsgBayGrant>("bays.grant", ServerGrant, null);
	}

	[SystemTick]
	private static void Tick()
	{
		if (!Net.IsOnline || !Game.ShopLoaded || Dispatch.InRequest)
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (realtimeSinceStartup >= nextLook && !Snapshot.IsLoading)
		{
			nextLook = realtimeSinceStartup + 0.5f;
			try
			{
				Look();
			}
			catch (Exception ex)
			{
				if (Log.Allow("bays|look", 30000L, out var skipped))
				{
					Log.Warn("bays: updating the locks threw" + Log.More(skipped) + ": " + ex.Message);
				}
			}
		}
		if (Enabled && Net.IsServer && Server.ShopReady && realtimeSinceStartup >= nextGrant && !HostTrip.Out)
		{
			nextGrant = realtimeSinceStartup + 2f;
			GrantMissing();
		}
	}

	/// An owned bay takes anything the shop owns. The rule is not saved, so every copy sets it again after loading.
	internal static void TakeAnything(GameItem bay)
	{
		PixelWindow obj = ((bay != null) ? bay.contentWindow : null);
		GraphNodeStorage val = ((obj != null) ? obj.child : null);
		GameInventory val2 = (GameInventory)(object)((val is GameInventory) ? val : null);
		if (val2 == null || !opened.Add(val2))
		{
			return;
		}
		GameItem self = bay;
		// Like the game's own containers: only what the shop owns.
		val2.mayInventoryAddItemFunc = (GameItem added, GameInventory inv) => added != null && added != self && GeneralHelper.IsItemOwned(added) && !GraphUtils.FindParentEquals((GraphNodeStorage)(object)self, (GraphNodeStorage)(object)added);
	}

	/// Sets the rule now on owned bays under root; the timed look-over does not run while a shop loads.
	internal static void OpenTree(GameItem root)
	{
		if (root == null)
		{
			return;
		}
		try
		{
			if (TagOwner(root) != 0)
			{
				TakeAnything(root);
			}
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)root, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (TagOwner(item) != 0)
				{
					TakeAnything(item);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("bays: opening an arrived bay threw: " + ex.Message);
		}
	}

	/// Every owned bay takes anything; called after a load, before the day's log is replayed.
	internal static void OpenAll()
	{
		try
		{
			opened.Clear();
			foreach (GameItem item in Items.All())
			{
				if (TagOwner(item) != 0)
				{
					TakeAnything(item);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("bays: opening the bays after a load threw: " + ex.Message);
		}
	}

	/// Owned bays take anything; on a player's copy, other players' bays are locked.
	private static void Look()
	{
		int localSeat = Members.LocalSeat;
		bool hasLocalPlayer = Net.HasLocalPlayer;
		if (opened.Count > 256)
		{
			opened.Clear();
		}
		foreach (GameItem item in Items.All())
		{
			if (TagOwner(item) != 0)
			{
				TakeAnything(item);
			}
			PixelWindow contentWindow = item.contentWindow;
			if (contentWindow == null || !hasLocalPlayer)
			{
				continue;
			}
			int num = OwnerOf(item);
			bool flag = num != 0 && num != localSeat;
			if (flag && !contentWindow.isLocked)
			{
				contentWindow.Lock();
				lockedByUs.Add(contentWindow);
			}
			else if (!flag && contentWindow.isLocked && lockedByUs.Remove(contentWindow))
			{
				contentWindow.Unlock();
			}
		}
	}

	[HarmonyPatch(typeof(LockHelper), "CreateLockerContainerTooltip")]
	private static class TooltipPatch
	{
		private static void Postfix(RichTextBuilder builder, GameItem item)
		{
			try
			{
				int num = OwnerOf(item);
				if (num != 0 && builder != null)
				{
					if (num == Members.LocalSeat)
					{
						builder.AddLine((TagOwner(item) != 0) ? Lang.Tr("Your storage bay: only you can open it.") : Lang.Tr("Part of your trade: only you can use it."), true, RenderHandler.ColorPalette.LightGreen);
					}
					else
					{
						builder.AddLine(Lang.Trf((TagOwner(item) != 0) ? "Only {0} can open this." : "Only {0} can use this.", Members.NameOf(num)), true, RenderHandler.ColorPalette.LightRed);
					}
				}
			}
			catch
			{
			}
		}
	}

	/// Connected players without a bay get one, once (the member record remembers).
	private static void GrantMissing()
	{
		foreach (PlayerInfo value in Net.Players.Values)
		{
			if (value.Seat > 0 && Members.Fact(value.Seat, "bay") == null)
			{
				MsgBayGrant msgBayGrant = new MsgBayGrant
				{
					seat = value.Seat
				};
				if (Net.IsHost)
				{
					Net.ToServer("bays.grant", msgBayGrant);
				}
				else
				{
					ServerGrant(msgBayGrant, 0);
				}
				break;
			}
		}
	}

	private static void ServerGrant(MsgBayGrant m, int from)
	{
		if (m == null || !Net.IsServer || !Game.ShopLoaded || Members.BySeat(m.seat) == null || Members.Fact(m.seat, "bay") != null)
		{
			return;
		}
		EmporiumEntry instance = EmporiumEntry.Instance;
		GameItem val = null;
		try
		{
			using (Net.Apply())
			{
				val = ItemFeatureHelper.SetShabby(DirectoryMaster.Item("storage_bay"));
				if (val == null)
				{
					Log.Warn("bays: the game has no item 'storage_bay'");
					return;
				}
				GeneralHelper.SetItemOwned(val, true);
				TakeAnything(val);
				int seat = m.seat;
				val.ModifyTag("PT_OWNER", delegate(TagState state)
				{
					state.Enable().SetInt(seat);
				}, false);
				val.name = val.name + " (" + Members.NameOf(m.seat) + ")";
				GraphNodeStorage[] array = (GraphNodeStorage[])(object)new GraphNodeStorage[4]
				{
					(GraphNodeStorage)instance.invElement,
					(GraphNodeStorage)instance.showcaseElement,
					(GraphNodeStorage)instance.hiddenElement,
					(GraphNodeStorage)instance.backInvinvElement
				};
				foreach (GraphNodeStorage val2 in array)
				{
					if (val2 != null && GraphUtils.TryAccept(val2, val) != -1)
					{
						break;
					}
				}
				if (val.parentInventory == null)
				{
					val.Destroy();
					if (Log.Allow("bays|noroom|" + m.seat, 60000L, out var skipped))
					{
						Log.Warn("bays: no room in the shop for " + Members.NameOf(m.seat) + "'s storage bay; trying again later" + Log.More(skipped));
					}
					return;
				}
				Ids.Assign(val);
				// A fanny pack in the bay, except for a new shop's founder: the game's own pack goes there (Specs.ServerStock).
				bool gamesOwnPack = false;
				try
				{
					gamesOwnPack = m.seat == 1 && Game.Day == 1 && ((GameInventory)instance.invElement).childItems.Any((GameItem i) => i != null && i.identifier == "fanny_pack" && GeneralHelper.IsItemOwned(i));
				}
				catch
				{
				}
				try
				{
					GameItem val3 = gamesOwnPack ? null : DirectoryMaster.Item("fanny_pack");
					PixelWindow contentWindow = val.contentWindow;
					GraphNodeStorage val4 = ((contentWindow != null) ? contentWindow.child : null);
					if (val3 != null && val4 != null)
					{
						GeneralHelper.SetItemOwned(val3, true);
						if (GraphUtils.TryAccept(val4, val3) != -1)
						{
							Ids.Assign(val3);
						}
						else
						{
							val3.Destroy();
						}
					}
				}
				catch (Exception ex2)
				{
					Log.Warn("bays: no fanny pack for the new bay: " + ex2.Message);
				}
			}
			ItemsMove.BroadcastSubtree(val, "bay");
			Members.SetFact(m.seat, "bay", Ids.Of(val).ToString(CultureInfo.InvariantCulture));
			Grants++;
			Log.Info("bays: " + Members.NameOf(m.seat) + " (seat " + m.seat + ") gets storage bay #" + Ids.Of(val));
		}
		catch (Exception ex)
		{
			Log.Error("bays: handing out a storage bay threw: " + ex);
		}
	}
}
