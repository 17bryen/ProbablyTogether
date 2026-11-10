using System;
using System.Collections.Generic;
using System.Linq;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsMoveB : ItemsBotBase
{
	private long mId;

	private long raceId;

	private int tx;

	private int ty;

	private int raceQ;

	private int rx;

	private int ry;

	private GameInventory target;

	public override string Name => "items-move-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 1300f;

	protected override void Build()
	{
		JoinSteps();
		DigestSame("join-digest");
		int id = 0;
		float at = -10f;
		Step("wait-a", 1100f, delegate(BotStep st)
		{
			if (raceId != 0L && raceQ == 0)
			{
				GameItem val = Items.Find(raceId);
				if (val != null && ItemPinHelper.IsPinned(val) && ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.showcaseElement, val, val.unitCount, val.modifiedShape.orientation, out rx, out ry))
				{
					raceQ = ItemsMove.Move(val, (GraphNodeStorage)(object)ItemsBotBase.E.showcaseElement, rx, ry, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "bot-race");
				}
			}
			if (id == 0)
			{
				if (st.Elapsed - at < 3f)
				{
					return StepStatus.Running;
				}
				id = BotProbes.AskServer();
				at = st.Elapsed;
				return StepStatus.Running;
			}
			Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
			if (dictionary == null)
			{
				return StepStatus.Running;
			}
			id = 0;
			if (raceId == 0L && dictionary.TryGetValue("items.race", out var value) && long.TryParse(value, out var result))
			{
				raceId = result;
			}
			if (dictionary.TryGetValue("items.botdone", out var value2) && value2 == "1")
			{
				st.Detail = "client A done; " + ItemsMove.AppliedCount + " action(s) applied here";
				return StepStatus.Pass;
			}
			return StepStatus.Running;
		});
		Step("race", 5f, delegate(BotStep st)
		{
			if (raceId == 0L)
			{
				st.Detail = "the server never named a race item";
				return StepStatus.Fail;
			}
			if (raceQ == 0)
			{
				st.Detail = "race item #" + raceId + ": this client never sent its move";
				return StepStatus.Fail;
			}
			string text = ItemsMove.Outcome(raceQ);
			if (text == "pending")
			{
				return StepStatus.Running;
			}
			GameItem val = Items.Find(raceId);
			bool flag = val != null && (object)val.parentInventory == ItemsBotBase.E.showcaseElement && val.modifiedShape.minX == rx && val.modifiedShape.minY == ry;
			string text2 = ItemsMove.RejectTextOf(raceQ);
			st.Detail = "race item #" + raceId + ": my move to the display case " + text + ((text2 != null) ? (" (\"" + text2 + "\")") : "") + "; item now in " + Items.RefOf((GraphNodeStorage)(object)((val != null) ? val.parentInventory : null))?.ToString() + (flag ? " (mine)" : "");
			if (text == "rejected:moved" && text2 != "Someone else moved that item first.")
			{
				return StepStatus.Fail;
			}
			return ((text == "applied" && flag) || (text.StartsWith("rejected", StringComparison.Ordinal) && !flag)) ? StepStatus.Pass : StepStatus.Fail;
		});
		DigestSame("watch-digest");
		Act("b-move", 20f, delegate
		{
			int x;
			int y;
			GameItem val = ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.showcaseElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y))) ?? ((IEnumerable<GameItem>)ItemsBotBase.Movables((GameInventory)(object)ItemsBotBase.E.invElement)).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.hiddenElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y)));
			if (val == null)
			{
				return "SKIP nothing to move";
			}
			target = (GameInventory)(object)(ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.showcaseElement, val, val.unitCount, val.modifiedShape.orientation, out tx, out ty) ? ItemsBotBase.E.showcaseElement : ItemsBotBase.E.hiddenElement);
			if ((object)target == ItemsBotBase.E.hiddenElement)
			{
				ItemsBotBase.Spot((GameInventory)(object)ItemsBotBase.E.hiddenElement, val, val.unitCount, val.modifiedShape.orientation, out tx, out ty);
			}
			mId = Ids.Of(val);
			return val.identifier + "#" + mId + " -> " + Items.RefOf((GraphNodeStorage)(object)target)?.ToString() + " " + tx + "," + ty;
		}, () => ItemsMove.Move(Items.Find(mId), (GraphNodeStorage)(object)target, tx, ty, Items.Find(mId).modifiedShape.flipped, Items.Find(mId).modifiedShape.orientation, -1, rep: false, "bot"), delegate
		{
			GameItem obj = Items.Find(mId);
			return ((obj != null) ? obj.parentInventory : null) == target;
		});
		DigestSame("b-move-digest");
		Check("no-mismatch", () => ItemsMove.ApplyMismatches == 0 && Items.TapeMismatches == 0, () => "applied " + ItemsMove.AppliedCount + ", mismatches " + ItemsMove.ApplyMismatches + ", tape mismatches " + Items.TapeMismatches);
	}
}
