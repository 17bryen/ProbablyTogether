using System;
using System.Collections.Generic;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

/// Individual outings, the player at home: stays in the shop while the other is out, and both end in the same shop.
public sealed class OutingB : DayNightBotBase
{
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
		WaitFor("a-home", 240f, () => !Outings.AnyOut, () => Outings.WhoIsOut() + " is still out");
		Delay("settle", 3f);
		Agree("same-after", 30f, "dn.digest", "cash", "dn.visit", "dn.state");
		Ready("ready");
		WaitFor("night", 150f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Agree("same-night", 30f, "day", "cash", "dn.state", "dn.digest");
	}
}
