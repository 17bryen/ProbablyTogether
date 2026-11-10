using ProbablyTogether.Systems;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PicsNightWatch : PicsBase2
{
	public override string Name => "pics-night-watch";

	protected override void Build()
	{
		JoinN(2);
		Pic("pic-nshop", () => "roster: " + string.Join(" / ", Presence.RosterTexts.Values));
		Mark("m-nshop", "pics-nshop-seen");
		WaitFor("lead-map", 120f, () => DayNightOuting.MapOpen || UiCoopBase.Row(UiCoopBase.Other()).Contains("choosing"), () => "map " + DayNightOuting.MapOpen + ", row '" + UiCoopBase.Row(UiCoopBase.Other()) + "', labels " + UiCoopBase.Labels());
		Delay("map-settle", 2f);
		Pic("pic-map", () => "map " + DayNightOuting.MapOpen + ", row '" + UiCoopBase.Row(UiCoopBase.Other()) + "', labels " + UiCoopBase.Labels());
		Mark("m-map", "pics-map-seen");
		WaitFor("at-jackson", 60f, () => DayNightOuting.LocationId == "dr_jackson" && Customers.SomeoneAtCounter, () => "at '" + DayNightOuting.LocationId + "'");
		Delay("arrive-settle", 4f);
		Pic("pic-jarrive", () => "at Dr. Jackson's; row '" + UiCoopBase.Row(UiCoopBase.Other()) + "', labels " + UiCoopBase.Labels());
		Mark("m-jarrive", "pics-jarrive-seen");
		WaitFor("lead-haggling", 60f, () => Locks.HeldBy("outing.jackson") == UiCoopBase.Other(), () => "outing.jackson " + Locks.HeldBy("outing.jackson") + ", row '" + UiCoopBase.Row(UiCoopBase.Other()) + "', labels " + UiCoopBase.Labels());
		Delay("haggle-settle", 1.5f);
		Pic("pic-jackson", () => "row '" + UiCoopBase.Row(UiCoopBase.Other()) + "', labels " + UiCoopBase.Labels());
		Mark("m-jackson", "pics-jackson-seen");
	}
}
