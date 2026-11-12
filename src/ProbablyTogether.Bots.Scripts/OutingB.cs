using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

/// Individual outings, the player at home: stays in the shop while the other is out, and both end in the same shop.
public sealed class OutingB : DayNightBotBase
{
	private int moveQ;

	public override string Name => "outing-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		JoinSteps();
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return "day " + day0;
		});
		WaitFor("closed-by-a", 240f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		WaitFor("a-out", 120f, () => Outings.AnyOut, () => "nobody is out yet");
		Check("still-in-shop", () => DayNightOuting.LocationId == "" && !TransitUI.IsOpen && !Outings.Private && Presence.LocalView == "store", () => "at '" + DayNightOuting.LocationId + "', map " + TransitUI.IsOpen + ", view " + Presence.LocalView);
		Agree("same-while-out", 20f, "dn.digest", "dn.visit", "dn.state");
		Delay("a-is-at-the-dump", 6f);
		Step("move-something-while-a-is-out", 25f, delegate(BotStep st)
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			if (moveQ == 0)
			{
				int x = 0;
				int y = 0;
				GameItem val = ItemsBotBase.Movables((GameInventory)(object)instance.invElement).FirstOrDefault((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)instance.backInvinvElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y));
				if (val == null)
				{
					st.Detail = "nothing in the storage fits the back counter";
					return StepStatus.Fail;
				}
				ItemsBotBase.Spot((GameInventory)(object)instance.backInvinvElement, val, val.unitCount, val.modifiedShape.orientation, out x, out y);
				moveQ = ItemsMove.Move(val, (GraphNodeStorage)(object)instance.backInvinvElement, x, y, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "bot");
				st.Detail = val.identifier + "#" + Ids.Of(val) + " -> back counter while the other player is at the dump";
				return StepStatus.Running;
			}
			string text = ItemsMove.Outcome(moveQ);
			if (text.StartsWith("rejected", StringComparison.Ordinal))
			{
				st.Detail = st.Detail + "; server " + text;
				return StepStatus.Fail;
			}
			return (text == "applied") ? StepStatus.Pass : StepStatus.Running;
		});
		WaitFor("a-home", 240f, () => !Outings.AnyOut, () => Outings.WhoIsOut() + " is still out");
		Delay("settle", 3f);
		Agree("same-after", 30f, "dn.digest", "cash", "dn.visit", "dn.state");
		Ready("ready");
		WaitFor("night", 150f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Agree("same-night", 30f, "day", "cash", "dn.state", "dn.digest");
	}
}
