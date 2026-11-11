using System;
using System.Collections.Generic;
using System.Globalization;
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

	private static float nextLook;

	private static float nextGrant;

	internal static int Grants;

	internal static int Refusals;

	/// Who alone may use this item: a bay's owner or the holder of the machine's trade (Specs). 0 = everyone.
	public static int OwnerOf(GameItem item)
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
			if (OwnerOf(item) == seat)
			{
				return item;
			}
		}
		return null;
	}

	private static string OnlyOwner(int owner)
	{
		return "Only " + Members.NameOf(owner) + " can use this storage bay.";
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
				return OnlyOwner(num2);
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
		return "This storage bay belongs to " + Members.NameOf(num) + ": it stays in the shop's storage.";
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
		if (Net.HasLocalPlayer && realtimeSinceStartup >= nextLook && !Snapshot.IsLoading)
		{
			nextLook = realtimeSinceStartup + 0.5f;
			try
			{
				LockForMe();
			}
			catch (Exception ex)
			{
				if (Log.Allow("bays|look", 30000L, out var skipped))
				{
					Log.Warn("bays: updating the locks threw" + Log.More(skipped) + ": " + ex.Message);
				}
			}
		}
		if (Net.IsServer && Server.ShopReady && realtimeSinceStartup >= nextGrant && !HostTrip.Out)
		{
			nextGrant = realtimeSinceStartup + 2f;
			GrantMissing();
		}
	}

	private static void LockForMe()
	{
		int localSeat = Members.LocalSeat;
		foreach (GameItem item in Items.All())
		{
			PixelWindow contentWindow = item.contentWindow;
			if (contentWindow == null)
			{
				continue;
			}
			int num = OwnerOf(item);
			if (num == 0)
			{
				continue;
			}
			bool flag = num != localSeat;
			if (contentWindow.isLocked != flag)
			{
				if (flag)
				{
					contentWindow.Lock();
				}
				else
				{
					contentWindow.Unlock();
				}
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
						builder.AddLine(Lang.Tr("Your storage bay: only you can open it."), true, RenderHandler.ColorPalette.LightGreen);
					}
					else
					{
						builder.AddLine(Lang.Trf("Only {0} can open this.", Members.NameOf(num)), true, RenderHandler.ColorPalette.LightRed);
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
