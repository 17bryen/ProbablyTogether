using System;
using System.Collections.Generic;
using System.Linq;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsMoveProbes : BotProbeSet
{
	internal static bool Done;

	internal static long RaceId;

	private static bool wired;

	private static bool spawned;

	public override void Register()
	{
		BotProbes.Add("items.digest", ItemsMove.Digest);
		BotProbes.Add("items.uid", () => (Game.Store == null) ? "" : BotProbeSet.Str(Game.Store.currentUniqueId));
		BotProbes.Add("items.idcheck", IdCheck);
		BotProbes.Add("items.botdone", () => (!Done) ? "0" : "1");
		BotProbes.Add("items.race", () => BotProbeSet.Str(RaceId));
		BotProbes.Add("items.live", () => (!Net.IsServer) ? "" : BotProbeSet.Str(Server.ByPlayer.Values.Count((Server.Peer p) => p.State == Server.PeerState.Ready)));
		if (!wired)
		{
			wired = true;
			Net.On<ItemsBotCmd>("items.botcmd", OnServerCmd, null);
		}
	}

	internal static string IdCheck()
	{
		if (!Items.Ready)
		{
			return "";
		}
		HashSet<long> hashSet = new HashSet<long>();
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		foreach (GameItem item in Items.All())
		{
			num3++;
			long num4 = Ids.Of(item);
			if (num4 == 0L)
			{
				num2++;
			}
			else if (!hashSet.Add(num4))
			{
				num++;
			}
		}
		return "items=" + num3 + " dup=" + num + " zero=" + num2;
	}

	private static void OnServerCmd(ItemsBotCmd m, int from)
	{
		if (m == null || !Items.Ready)
		{
			return;
		}
		EmporiumEntry e = EmporiumEntry.Instance;
		string text = Net.PlayerName(from);
		try
		{
			switch (m.cmd)
			{
			case "spawn":
			{
				if (spawned)
				{
					BotLog.Info("items-move-server", "spawn asked again by " + text + " (already done)");
					break;
				}
				spawned = true;
				bool flag = Items.ServerAction("bot.spawn", delegate
				{
					Place((GameInventory)(object)e.invElement, "backpack_small", "BR_BOT_BAG", 1);
					Place((GameInventory)(object)e.invElement, "backpack_small", "BR_BOT_BOX", 1, "TRACK_OPENED_TAG");
					Place((GameInventory)(object)e.invElement, "glass_shard", "BR_BOT_SHARD", 3);
				});
				BotLog.Info("items-move-server", "spawned the test items for " + text + ": " + (flag ? "sent" : "NOTHING SENT"));
				break;
			}
			case "replace":
			{
				GameItem val2 = ((IEnumerable<GameItem>)Items.All()).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => i.IsTag("BR_BOT_BAG")));
				if (val2 == null)
				{
					BotLog.Info("items-move-server", "replace: no test bag");
					break;
				}
				val2.unitValue = 4321L;
				PixelWindow contentWindow = val2.contentWindow;
				GraphNodeStorage obj = ((contentWindow != null) ? contentWindow.child : null);
				GameInventory val3 = (GameInventory)(object)((obj is GameInventory) ? obj : null);
				GameItem val4 = DirectoryMaster.Item("glass_shard", true);
				val4.EnableTag("BR_BOT_INNER", false);
				SlotMarker val5 = ((val3 != null) ? val3.TryFindOneValidInventorySlot(val4) : null);
				if (val5?.itemGridShape != null)
				{
					val4.modifiedShape = (GridShape)(object)new GridShapeBuilder(val4.modifiedShape).SetTransform(val5.itemGridShape);
					val3.UncheckedAccept(val4);
				}
				Items.BroadcastSubtree(val2, "bot-replace");
				BotLog.Info("items-move-server", "replace: bag #" + Ids.Of(val2) + " value 4321, inner #" + Ids.Of(val4) + " placed " + (val4.parentInventory != null));
				break;
			}
			case "stackable":
				Items.ServerAction("bot.stackable", delegate
				{
					Place((GameInventory)(object)e.invElement, "glass_shard", "BR_BOT_STK1", 2, null, stackable: true);
					Place((GameInventory)(object)e.invElement, "glass_shard", "BR_BOT_STK2", 3, null, stackable: true);
				});
				BotLog.Info("items-move-server", "stackable test items placed for " + text);
				break;
			case "race-arm":
			{
				GameItem val6 = ((IEnumerable<GameItem>)((GameInventory)e.invElement).childItems).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => GeneralHelper.IsItemOwned(i) && i.MayRemove() && i.contentWindow == null && !ItemPinHelper.IsPinned(i) && !i.IsTag("BR_BOT_BAG") && !i.IsTag("BR_BOT_BOX") && !i.IsTag("BR_BOT_SHARD") && e.showcaseElement.TryFindOneValidInventorySlot(i, false) != null));
				RaceId = ((val6 != null) ? Ids.Of(val6) : 0);
				BotLog.Info("items-move-server", "race item " + ((val6 != null) ? (val6.identifier + "#" + RaceId) : "none"));
				break;
			}
			case "race-go":
			{
				GameItem val = Items.Find(RaceId);
				if (val != null)
				{
					ItemsMove.Pin(val, pinned: true, "bot-race");
				}
				BotLog.Info("items-move-server", "race go: pinned #" + RaceId);
				break;
			}
			case "done":
				Done = true;
				BotLog.Info("items-move-server", text + " finished the item tests");
				break;
			default:
				BotLog.Info("items-move-server", "unknown bot command '" + m.cmd + "' from " + text);
				break;
			}
		}
		catch (Exception ex)
		{
			BotLog.Info("items-move-server", "bot command " + m.cmd + " threw: " + ex);
		}
	}

	private static void Place(GameInventory inv, string ident, string tag, int count, string extraTag = null, bool stackable = false)
	{
		if (!SecurityLabItems.Real(ident))
		{
			BotLog.Info("items-move-server", "skipped '" + ident + "': not an item in this game version");
			return;
		}
		GameItem val = DirectoryMaster.Item(ident, true);
		val.EnableTag(tag, false);
		if (stackable)
		{
			((GameItemFunc)val).mayStackItemFunc = (GameItem a, GameItem b) => a.identifier == b.identifier;
			((GameItemFunc)val).maxSlotAmountItemFunc = (GameItem a, GameInventory i, SlotMarker sm) => 10;
		}
		if (extraTag != null)
		{
			val.EnableTag(extraTag, false);
		}
		SlotMarker val2 = inv.TryFindOneValidInventorySlot(val);
		if (val2?.itemGridShape == null)
		{
			BotLog.Info("items-move-server", "no room in " + Items.RefOf((GraphNodeStorage)(object)inv)?.ToString() + " for " + ident);
			val.Destroy();
		}
		else
		{
			val.unitCount = count;
			val.modifiedShape = (GridShape)(object)new GridShapeBuilder(val.modifiedShape).SetTransform(val2.itemGridShape);
			inv.UncheckedAccept(val);
		}
	}
}
