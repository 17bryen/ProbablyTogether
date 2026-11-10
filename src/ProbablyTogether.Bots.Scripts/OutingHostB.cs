using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

/// The host travels, B stays home: B keeps the server's shop and uses the back counter while Dr. Jackson's stall holds it on the host's screen.
public sealed class OutingHostB : DayNightBotBase
{
	private static readonly string[] Same = new string[9] { "core.digest", "items.digest", "dn.digest", "econ.digest", "nego.digest", "events.hash", "cash", "dn.visit", "dn.state" };

	private int moveQ;

	private long yId;

	public override string Name => "outing-hb";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	private static EmporiumEntry E => EmporiumEntry.Instance;

	protected override void Build()
	{
		JoinSteps();
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return "day " + day0;
		});
		WaitFor("closed-by-host", 300f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		Delay("past-close-timers", 5f);
		Agree("same-closed", 30f, Same);
		WaitFor("host-out", 120f, () => Outings.AnyOut, () => "nobody is out yet");
		Check("still-in-shop", () => DayNightOuting.LocationId == "" && !TransitUI.IsOpen && !Outings.Private && Presence.LocalView == "store", () => "at '" + DayNightOuting.LocationId + "', map " + TransitUI.IsOpen + ", view " + Presence.LocalView);
		Delay("host-travels", 8f);
		Agree("same-while-host-at-dump", 30f, Same);
		WaitFor("host-home", 240f, () => !Outings.AnyOut, () => Outings.WhoIsOut() + " is still out");
		Delay("settle", 3f);
		Agree("same-after-dump", 30f, Same);
		WaitServer("host-at-jackson", 240f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "out.jackson") == "1", "out.jackson", "out.count");
		Check("still-in-shop-2", () => DayNightOuting.LocationId == "" && DayNightBotBase.Ps.currentClientInstance == null && !Outings.Private, () => "at '" + DayNightOuting.LocationId + "', customer " + ((DayNightBotBase.Ps.currentClientInstance != null) ? "at the counter" : "none"));
		Agree("same-while-host-at-jackson", 30f, Same);
		Step("y-on-counter", 25f, delegate(BotStep st)
		{
			if (moveQ == 0)
			{
				int x = 0;
				int y = 0;
				GameItem val = ItemsBotBase.Movables((GameInventory)(object)E.invElement).FirstOrDefault((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)E.backInvinvElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y));
				if (val == null)
				{
					st.Detail = "no item fits the back counter";
					return StepStatus.Fail;
				}
				yId = Ids.Of(val);
				ItemsBotBase.Spot((GameInventory)(object)E.backInvinvElement, val, val.unitCount, val.modifiedShape.orientation, out x, out y);
				moveQ = ItemsMove.Move(val, (GraphNodeStorage)(object)E.backInvinvElement, x, y, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "bot");
				st.Detail = val.identifier + "#" + yId + " -> back counter " + x + "," + y + " while the stall is on it for the host";
				return StepStatus.Running;
			}
			string text = ItemsMove.Outcome(moveQ);
			if (text.StartsWith("rejected", StringComparison.Ordinal) || text == "applied-mismatch")
			{
				st.Detail = st.Detail + "; server " + text;
				return StepStatus.Fail;
			}
			GameItem val2 = Ids.Find(yId);
			return (text == "applied" && val2 != null && (object)val2.parentInventory == E.backInvinvElement) ? StepStatus.Pass : StepStatus.Running;
		});
		Agree("same-after-my-move", 30f, Same);
		WaitFor("host-home-2", 240f, () => !Outings.AnyOut, () => Outings.WhoIsOut() + " is still out");
		Delay("settle-2", 3f);
		Check("y-still-on-counter", delegate
		{
			GameItem val = Ids.Find(yId);
			return val != null && (object)val.parentInventory == E.backInvinvElement;
		}, () => "my item #" + yId + " is where I put it");
		Agree("nego-after-jackson", 12f, "out.nego").ContinueOnFail = true;
		Agree("same-after-jackson", 30f, Same);
		Check("no-reloads", () => DayNight.Mismatches == 0, () => "day/night mismatches " + DayNight.Mismatches);
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Agree("same-night", 30f, "day", "cash", "dn.state", "dn.digest", "items.digest");
	}
}
