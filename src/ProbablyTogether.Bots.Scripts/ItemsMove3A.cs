using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsMove3A : ItemsBot3Base
{
	private long rId;

	private long dragId;

	private long cId;

	private int tx;

	private int ty;

	private GameInventory dragTo;

	private readonly List<long> stolen = new List<long>();

	public override string Name => "items-move3-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 1400f;

	private GameItem I(long id)
	{
		return Items.Find(id);
	}

	protected override void Build()
	{
		JoinSteps();
		Quiet();
		DigestSame("join-digest");
		Act("to-afterhour", 20f, delegate
		{
			int x;
			int y;
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.afterhourInventory, i, i.unitCount, i.modifiedShape.orientation, out x, out y) && ((GameInventory)ItemsBotBase.E.afterhourPocketSlotInvLeft).TryFindOneValidInventorySlot(i) != null));
			if (val == null)
			{
				return "SKIP no owned item fits the afterhour ground and a pocket";
			}
			rId = Ids.Of(val);
			ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.afterhourInventory, val, val.unitCount, val.modifiedShape.orientation, out tx, out ty);
			return val.identifier + "#" + rId + " -> afterhour " + tx + "," + ty;
		}, () => ItemsMove.Move(I(rId), (GraphNodeStorage)(object)ItemsBotBase.E.afterhourInventory, tx, ty, I(rId).modifiedShape.flipped, I(rId).modifiedShape.orientation, -1, rep: false, "bot"), delegate
		{
			GameItem obj = I(rId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.afterhourInventory;
		});
		DigestSame("afterhour-digest");
		Act("to-pocket", 20f, () => "#" + rId + " -> the left pocket (slot inventory, server picks the slot)", () => ItemsMove.QuickMove(I(rId), (GraphNodeStorage)(object)ItemsBotBase.E.afterhourPocketSlotInvLeft, all: false, -1, "bot"), delegate
		{
			GameItem obj = I(rId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.afterhourPocketSlotInvLeft;
		});
		DigestSame("pocket-digest");
		Act("to-counter-stash", 20f, () => (!ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.backInvinvElementCounter, I(rId), I(rId).unitCount, I(rId).modifiedShape.orientation, out tx, out ty)) ? "SKIP no spot in the counter stash" : ("#" + rId + " -> backc " + tx + "," + ty), () => ItemsMove.Move(I(rId), (GraphNodeStorage)(object)ItemsBotBase.E.backInvinvElementCounter, tx, ty, I(rId).modifiedShape.flipped, I(rId).modifiedShape.orientation, -1, rep: false, "bot"), delegate
		{
			GameItem obj = I(rId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.backInvinvElementCounter;
		});
		DigestSame("stash-digest");
		Act("back-to-inv", 20f, () => "#" + rId + " -> inv (quick)", () => ItemsMove.QuickMove(I(rId), (GraphNodeStorage)(object)ItemsBotBase.E.invElement, all: false, -1, "bot"), delegate
		{
			GameItem obj = I(rId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.invElement;
		});
		DigestSame("roots-back-digest");
		Cmd3("mark-1", "mark", 0L);
		WaitServer("b-dragging", 120f, (Dictionary<string, string> s) => ItemsBot3Base.Int(s["items3.drag"]) > 0, "items3.drag");
		Act("move-dragged", 20f, delegate
		{
			dragId = ItemsBot3Base.Int(Theirs("items3.drag"));
			GameItem val = I(dragId);
			if (val == null)
			{
				return "SKIP B's item #" + dragId + " not found";
			}
			dragTo = (GameInventory)(object)(ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.showcaseElement, val, val.unitCount, val.modifiedShape.orientation, out tx, out ty) ? ItemsBotBase.E.showcaseElement : ItemsBotBase.E.hiddenElement);
			return ((object)dragTo == ItemsBotBase.E.hiddenElement && !ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.hiddenElement, val, val.unitCount, val.modifiedShape.orientation, out tx, out ty)) ? "SKIP no spot for B's item" : (val.identifier + "#" + dragId + " (B is dragging it) -> " + Items.RefOf((GraphNodeStorage)(object)dragTo)?.ToString() + " " + tx + "," + ty);
		}, () => ItemsMove.Move(I(dragId), (GraphNodeStorage)(object)dragTo, tx, ty, I(dragId).modifiedShape.flipped, I(dragId).modifiedShape.orientation, -1, rep: false, "bot"), delegate
		{
			GameItem obj = I(dragId);
			return ((obj != null) ? obj.parentInventory : null) == dragTo;
		});
		DigestSame("dragged-digest");
		Do("open", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "dayNight.open sent (state " + Game.Store.storeState.ToString() + ")";
		});
		WaitFor("opened", 30f, () => (int)Game.Store.storeState == 1, () => "state " + Game.Store.storeState.ToString());
		WaitFor("queue", 30f, () => ItemsBot3Base.Int(ItemsBot3Base.Mine("cust.queue")) > 0 || ItemsBot3Base.Cur() != "none", () => "queue " + ItemsBot3Base.Mine("cust.queue") + ", at the counter " + ItemsBot3Base.Cur());
		Same("same-open", 20f, "cust.state", "cust.queue", "cust.digest", "items.digest");
		Do("call", delegate
		{
			if (ItemsBot3Base.Cur() != "none")
			{
				return "already at the counter: " + ItemsBot3Base.Cur();
			}
			Customers.RequestCall();
			return "customers.call sent, queue " + ItemsBot3Base.Mine("cust.queue");
		});
		WaitFor("called", 30f, () => ItemsBot3Base.Cur() != "none" && Locks.HeldBy("counter") == Net.LocalPlayer, () => "at the counter " + ItemsBot3Base.Cur() + ", holder " + Locks.HeldBy("counter") + ", me " + Net.LocalPlayer);
		Same("same-customer", 20f, "cust.current", "cust.holder", "cust.digest");
		Check("lock-held", () => Locks.HeldBy("counter") == Net.LocalPlayer && ItemsBot3Base.Mine("cust.holder") == Theirs("cust.holder"), () => "holder here " + Locks.HeldBy("counter") + ", server " + Theirs("cust.holder") + ", me " + Net.LocalPlayer + ", customer " + ItemsBot3Base.Cur());
		Act("holder-moves", 20f, delegate
		{
			int x;
			int y;
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.backInvinvElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y)));
			if (val == null)
			{
				return "SKIP nothing fits the back counter";
			}
			cId = Ids.Of(val);
			ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.backInvinvElement, val, val.unitCount, val.modifiedShape.orientation, out tx, out ty);
			return val.identifier + "#" + cId + " -> back counter " + tx + "," + ty + " holding the counter (customer " + ItemsBot3Base.Cur() + ")";
		}, () => ItemsMove.Move(I(cId), (GraphNodeStorage)(object)ItemsBotBase.E.backInvinvElement, tx, ty, I(cId).modifiedShape.flipped, I(cId).modifiedShape.orientation, -1, rep: false, "bot"), delegate
		{
			GameItem obj = I(cId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.backInvinvElement;
		});
		Same("nego-same", 20f, "items3.nego", "items.digest");
		Check("nego-is-it", () => ItemsBot3Base.Mine("items3.nego") == BotScript.Str(cId), () => "negotiated item " + ItemsBot3Base.Mine("items3.nego") + ", moved #" + cId);
		Cmd3("mark-2", "mark", 0L);
		WaitServer("b-refused", 120f, (Dictionary<string, string> s) => ItemsBot3Base.Int(s["items3.marks"]) >= 3, "items3.marks");
		Act("transfer-back", 20f, () => "transfer button: the back counter's owned items to the main storage", () => ItemsMove.TransferButton(frontVisible: false, "bot"), delegate
		{
			GameItem obj = I(cId);
			return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.invElement;
		});
		Same("after-transfer", 20f, "items3.nego", "items.digest");
		Do("dismiss", delegate
		{
			Customers.RequestDismiss(callNext: false);
			return "customers.dismiss sent for " + ItemsBot3Base.Cur();
		});
		bool cleared = false;
		Step("gone", 40f, delegate(BotStep st)
		{
			if (ItemsBot3Base.Cur() == "none")
			{
				st.Detail = "the counter is empty" + (cleared ? " (ended by the server)" : "") + " after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
				return StepStatus.Pass;
			}
			if (!cleared && st.Elapsed > 8f)
			{
				cleared = true;
				Net.ToServer("items.bot3", new ItemsBot3Cmd
				{
					cmd = "clear"
				});
			}
			return StepStatus.Running;
		});
		Same("same-empty", 20f, "cust.current", "cust.holder", "cust.digest", "items.digest");
		Cmd3("ask-stolen", "stolen", 0L, 5, 50);
		WaitFor("stolen-here", 30f, () => Items.All().Count((GameItem i) => i.IsTag("BR_BOT_STOLEN")) >= 5, () => "stolen items here " + Items.All().Count((GameItem i) => i.IsTag("BR_BOT_STOLEN")));
		Do("stolen-ids", delegate
		{
			stolen.Clear();
			stolen.AddRange(from i in Items.All()
				where i.IsTag("BR_BOT_STOLEN")
				orderby i.uniqueId
				select Ids.Of(i));
			return string.Join(", ", stolen.Select((long id) => I(id).identifier + "#" + id + " heat " + StolenHelper.GetFoundChance(I(id))));
		});
		Cmd3("ask-inspector", "inspector", 0L);
		WaitFor("inspector-queued", 30f, () => ItemsBot3Base.Mine("cust.head") == "securityInspector", () => "queue head " + ItemsBot3Base.Mine("cust.head"));
		Same("same-queue", 20f, "cust.head", "cust.queue", "cust.digest", "items.digest", "items3.sec");
		Do("call-inspector", delegate
		{
			Customers.RequestCall();
			return "customers.call sent";
		});
		WaitFor("inspector-here", 30f, () => ItemsBot3Base.Cur() == "securityInspector" && Locks.HeldBy("counter") == Net.LocalPlayer, () => "at the counter " + ItemsBot3Base.Cur() + ", holder " + Locks.HeldBy("counter"));
		WaitFor("arrived", 40f, () => ItemsBot3Base.Mine("cust.arrived") == "1", () => "arrived " + ItemsBot3Base.Mine("cust.arrived"));
		Delay("let-lines-settle", 4f);
		Same("same-arrival", 20f, "cust.current", "cust.arrived", "cust.digest", "items3.sec");
		Same("same-dialogue", 20f, "items3.dlg").Soft();
		for (int num = 0; num < 5; num++)
		{
			int kk = num;
			int fx = 0;
			int fy = 0;
			Act("stolen-" + (num + 1), 20f, delegate
			{
				if (kk >= stolen.Count)
				{
					return "SKIP only " + stolen.Count + " stolen item(s)";
				}
				GameItem val = I(stolen[kk]);
				if (val == null)
				{
					return "SKIP stolen #" + stolen[kk] + " is gone";
				}
				return (!ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.frontInvinvElement, val, val.unitCount, val.modifiedShape.orientation, out fx, out fy)) ? "SKIP no spot on the front counter" : (val.identifier + "#" + stolen[kk] + " (heat " + StolenHelper.GetFoundChance(val) + ") -> front counter " + fx + "," + fy + " in front of " + ItemsBot3Base.Cur());
			}, () => ItemsMove.Move(I(stolen[kk]), (GraphNodeStorage)(object)ItemsBotBase.E.frontInvinvElement, fx, fy, I(stolen[kk]).modifiedShape.flipped, I(stolen[kk]).modifiedShape.orientation, -1, rep: false, "bot"), delegate
			{
				GameItem obj = I(stolen[kk]);
				return (object)((obj != null) ? obj.parentInventory : null) == ItemsBotBase.E.frontInvinvElement;
			}, delegate
			{
				string[] obj = new string[6]
				{
					"here: heat now ",
					StolenHelper.GetFoundChance(I(stolen[kk])).ToString(),
					", customer ",
					null,
					null,
					null
				};
				StoreClientInstance currentClientInstance = Game.Store.currentClientInstance;
				obj[3] = ((currentClientInstance != null && currentClientInstance.GetClientBlueprint()?.dismissable == true) ? "dismissable" : "not dismissable");
				obj[4] = "; random state after this move vs the server's: ";
				obj[5] = ItemsMove.LastRandomCheck;
				return string.Concat(obj);
			});
			Check("stolen-" + (num + 1) + "-rolls", () => ItemsMove.LastRandomCheck == "same" && ItemsMove.LastApplied?.kind == "items.move", () => "random state after the replay vs the server's: " + ItemsMove.LastRandomCheck + " (last applied " + ItemsMove.LastApplied?.kind + " #" + ItemsMove.LastApplied?.seq + ")");
			Same("stolen-" + (num + 1) + "-same", 20f, "items3.sec", "items.digest", "cust.digest");
		}
		Do("rolls", delegate
		{
			int num2 = stolen.Count((long id) => I(id) != null && StolenHelper.GetFoundChance(I(id)) == 100);
			return num2 + " of " + stolen.Count + " stolen item(s) found by the inspector (heat 100), " + (stolen.Count - num2) + " not; sec " + ItemsBot3Base.Mine("items3.sec");
		});
		Cmd3("tell-done", "done", 0L);
		Same("final-dialogue", 20f, "items3.dlg").Soft();
		NoMismatch("no-mismatch");
	}
}
