using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PicsNightLead : PicsBase2
{
	private long jId;

	public override string Name => "pics-night-lead";

	protected override void Build()
	{
		JoinN(2);
		WaitMark("w-nshop", "pics-nshop-seen", 120f);
		OpenShop();
		CloseShop();
		Do("door", delegate
		{
			PicsBase2.E.selectedBackpack = null;
			MapUIManager.Instance.OpenUI();
			return "dayNight.goOut sent";
		});
		WaitFor("map-open", 20f, () => DayNightOuting.MapOpen, () => "map open " + DayNightOuting.MapOpen + ", refused " + DayNight.LastRefusedWhy);
		WaitMark("w-map", "pics-map-seen", 60f);
		Do("travel-jackson", delegate
		{
			AfterhourLocation val = AfterhourLocation.Get("dr_jackson");
			MapUIManager.Instance.Travel(val);
			return "discovered " + (val != null && val.IsDiscovered()) + ", available " + (val != null && val.IsAvailable());
		});
		WaitFor("at-jackson", 30f, () => DayNightOuting.LocationId == "dr_jackson" && Customers.SomeoneAtCounter, () => "at '" + DayNightOuting.LocationId + "', someone at the counter " + Customers.SomeoneAtCounter + "; refused " + DayNight.LastRefusedWhy);
		WaitMark("w-jarrive", "pics-jarrive-seen", 60f);
		S("take-jackson", 20f, delegate(BotStep st)
		{
			if (jId == 0L)
			{
				GameItem val = PicsBase2.CheapestGoods();
				if (val == null)
				{
					st.Detail = "Dr. Jackson has nothing on the counter";
					return StepStatus.Fail;
				}
				jId = Ids.Of(val);
				ItemsMove.QuickMove(val, (GraphNodeStorage)(object)PicsBase2.E.backInvinvElement, all: true, -1, "bot");
				return StepStatus.Running;
			}
			st.Detail = "#" + jId + " -> back counter; outing.jackson " + Locks.HeldBy("outing.jackson") + ", me " + Net.LocalPlayer;
			return (Locks.HeldBy("outing.jackson") == Net.LocalPlayer) ? StepStatus.Pass : StepStatus.Running;
		});
		WaitMark("w-jackson", "pics-jackson-seen", 90f);
	}
}
