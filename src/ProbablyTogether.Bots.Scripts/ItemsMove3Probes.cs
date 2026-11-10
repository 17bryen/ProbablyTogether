using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsMove3Probes : BotProbeSet
{
	internal static int Marks;

	internal static int Done;

	internal static long DragArmed;

	private static bool wired;

	private static readonly string[] stolenKinds = new string[5] { "bandage_item", "glass_shard", "topical_bandage_item", "bottled_water", "magnifier" };

	public override void Register()
	{
		BotProbes.Add("items3.marks", () => BotProbeSet.Str(Marks));
		BotProbes.Add("items3.done", () => BotProbeSet.Str(Done));
		BotProbes.Add("items3.drag", () => BotProbeSet.Str(DragArmed));
		BotProbes.Add("items3.sec", Sec);
		BotProbes.Add("items3.heat", () => Items.Ready ? string.Join(",", from i in Items.All()
			where i.IsTag("BR_BOT_STOLEN")
			orderby i.uniqueId
			select i.uniqueId + ":" + (StolenHelper.IsStolenItem(i) ? StolenHelper.GetFoundChance(i) : 0) + ":" + Items.RootOf((GraphNodeStorage)(object)i.parentInventory)) : "");
		BotProbes.Add("items3.nego", () => (PlayerStore.HasInstance && Items.Ready) ? ((PlayerStore.Instance.currentNegociatedItem != null) ? BotProbeSet.Str(Ids.Of(PlayerStore.Instance.currentNegociatedItem)) : "none") : "");
		BotProbes.Add("items3.dlg", () => (!Items.Ready) ? "" : (DialogueSync.Seq("counter") + "/" + DialogueSync.CurrentFp("counter")));
		if (wired)
		{
			return;
		}
		wired = true;
		Net.On<ItemsBot3Cmd>("items.bot3", OnCmd, null);
		Net.On("items.bot3.seeded", null, delegate(ItemsBot3Seeded m)
		{
			using (Replay.Client(m.seed))
			{
				Seeded(m.what);
			}
		});
	}

	internal static string Sec()
	{
		if (!Items.Ready)
		{
			return "";
		}
		List<string> list = new List<string>();
		foreach (GameItem item in from i in Items.All()
			where i.IsTag("BR_BOT_STOLEN")
			orderby i.uniqueId
			select i)
		{
			list.Add(item.uniqueId + ":" + (StolenHelper.IsStolenItem(item) ? StolenHelper.GetFoundChance(item) : 0) + ":" + Items.RootOf((GraphNodeStorage)(object)item.parentInventory));
		}
		PlayerStore store = Game.Store;
		object obj;
		if (store == null)
		{
			obj = null;
		}
		else
		{
			StoreClientInstance currentClientInstance = store.currentClientInstance;
			obj = ((currentClientInstance != null) ? currentClientInstance.GetClientBlueprint() : null);
		}
		StoreClient val = (StoreClient)obj;
		return string.Join(",", list) + "|" + ((val == null) ? "none" : (val.identifier + (val.dismissable ? ":d" : ":nd")));
	}

	private static bool SecurityBotsReal(string id)
	{
		return SecurityLabItems.Real(id);
	}

	private static void OnCmd(ItemsBot3Cmd m, int from)
	{
		if (m == null || !Items.Ready)
		{
			return;
		}
		string text = Net.PlayerName(from);
		try
		{
			switch (m.cmd)
			{
			case "mark":
				Marks++;
				BotLog.Info("items-move3-server", "mark " + Marks + " from " + text);
				break;
			case "done":
				Done++;
				BotLog.Info("items-move3-server", text + " finished (" + Done + ")");
				break;
			case "drag":
				DragArmed = m.id;
				BotLog.Info("items-move3-server", text + " is dragging #" + m.id);
				break;
			case "clear":
				BotLog.Info("items-move3-server", "clear for " + text + ": " + Customers.BotTimerDismiss());
				break;
			case "stolen":
			{
				int placed = 0;
				EmporiumEntry e = EmporiumEntry.Instance;
				Items.ServerAction("bot.stolen", delegate
				{
					for (int i = 0; i < Math.Min(m.n, 8); i++)
					{
						GameItem val = null;
						foreach (string item in stolenKinds.Skip(i % stolenKinds.Length).Concat(stolenKinds))
						{
							if (SecurityBotsReal(item))
							{
								val = DirectoryMaster.Item(item, true);
								if (val != null && !val.IsTag("CONTRABAND_ITEM_TAG"))
								{
									break;
								}
								if (val != null)
								{
									val.Destroy();
								}
								val = null;
							}
						}
						if (val == null)
						{
							break;
						}
						GeneralHelper.SetItemOwned(val, true);
						StolenHelper.SetHeat(val, m.v);
						val.EnableTag("BR_BOT_STOLEN", false);
						SlotMarker val2 = e.invElement.TryFindOneValidInventorySlot(val, false);
						if (val2?.itemGridShape == null)
						{
							val.Destroy();
							break;
						}
						val.modifiedShape = (GridShape)(object)new GridShapeBuilder(val.modifiedShape).SetTransform(val2.itemGridShape);
						if (((GameInventory)e.invElement).UncheckedAccept(val))
						{
							placed++;
						}
					}
				});
				BotLog.Info("items-move3-server", "stolen x" + placed + " (heat " + m.v + ") placed for " + text);
				break;
			}
			case "inspector":
			{
				PlayerStore store = Game.Store;
				if (store?.storeClientManager != null)
				{
					Seed seed;
					using (Replay.Server(out seed))
					{
						Seeded("inspector");
					}
					Net.ToAll("items.bot3.seeded", new ItemsBot3Seeded
					{
						what = "inspector",
						seed = seed
					});
					BotLog.Info("items-move3-server", "inspector at the front of the queue for " + text + ": queue " + store.storeClientManager.clientStack.Count);
				}
				break;
			}
			default:
				BotLog.Info("items-move3-server", "unknown bot command '" + m.cmd + "' from " + text);
				break;
			}
		}
		catch (Exception ex)
		{
			BotLog.Info("items-move3-server", "bot command " + m.cmd + " threw: " + ex);
		}
	}

	private static void Seeded(string what)
	{
		PlayerStore store = Game.Store;
		if (Items.Ready && store?.storeClientManager != null && what == "inspector")
		{
			StoreClient val = StoreClientList.CreateInspectionClient(0, false);
			store.storeClientManager.clientStack.Insert(0, val);
			BotLog.Info("items-move3", "inspector '" + val.identifier + "' queued first" + (DayLog.Replaying ? " (replayed)" : ""));
		}
	}
}
