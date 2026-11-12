using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

/// The other player gets badly hurt at the dump: the wound, refused door, bandage and night healing apply to them only.
public abstract class WoundsBotBase : DayNightBotBase
{
	protected static int Other => Net.Players.Keys.FirstOrDefault((int p) => p != Net.LocalPlayer);

	protected static int OtherSeat => Members.SeatOf(Other);

	protected static HealthData H => DayNightBotBase.Ps.healthData;

	protected static string Where => "records [" + Wounds.Raw + "]; on this screen: wound " + H.woundState + (H.isWoundStable ? " stable" : " not stable") + (H.isWoundedFresh ? " fresh" : "") + "; my seat " + Members.LocalSeat;

	protected static void Door()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		GameItem selectedBackpack = ((IEnumerable<GameItem>)instance.GetAllOwnedItemsByTag("BACKPACK_TAG")).FirstOrDefault((Func<GameItem, bool>)delegate(GameItem b)
		{
			GameItem bay;
			int num = Bays.OwnerAround((GraphNodeStorage)(object)b.parentInventory, out bay);
			return num == 0 || num == Members.LocalSeat;
		});
		instance.selectedBackpack = selectedBackpack;
		MapUIManager.Instance.OpenUI();
	}
}

public sealed class WoundsH : WoundsBotBase
{
	private int hurt;

	public override string Name => "wd-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			LabKit.Do("give", "bandage_item", "2");
			return "individual outings " + Outings.Individual + "; wounds of their own " + Wounds.Active + "; " + Where;
		});
		Check("everyone-healthy", () => Wounds.Active && Wounds.Of(Members.LocalSeat).State == 0 && Wounds.Of(OtherSeat).State == 0, () => Where);
		Delay("b-settles", 6f);
		Do("open-click", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		WaitFor("shutter-settled", 20f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0, () => "flag " + DayNightBotBase.Ps.lockShutterButtonFlag);
		Do("queue", delegate
		{
			if (DayNightBotBase.Ps.CanEndDay())
			{
				return "every waiting customer can be sent away";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "lab op clearqueue";
		});
		WaitFor("closable", 15f, () => DayNightBotBase.Ps.CanEndDay(), () => "queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count);
		Do("close", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "sent";
		});
		WaitFor("closed", 20f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		WaitFor("b-came-home-badly-hurt", 240f, () => Wounds.Of(OtherSeat).State > 5 && !Outings.AnyOut, () => Where);
		Check("the-wound-is-theirs-not-mine", delegate
		{
			hurt = Wounds.Of(OtherSeat).State;
			return Wounds.Of(Members.LocalSeat).State == 0 && H.woundState == 0 && H.isWoundStable && !Wounds.Of(OtherSeat).Stable;
		}, () => Where);
		WaitFor("b-was-refused-the-door", 120f, () => Outings.LastDoorRefusal.Contains("hurt"), () => "last refusal at the door: [" + Outings.LastDoorRefusal + "]");
		Do("my-door-still-opens", delegate
		{
			Door();
			return "asked to go out; I am healthy";
		});
		WaitFor("i-am-out", 20f, () => Outings.Private && HostTrip.Out && TransitUI.IsOpen, () => "private " + Outings.Private + ", host out " + HostTrip.Out + ", refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Do("travel", delegate
		{
			MapUIManager.Instance.Travel(AfterhourLocation.Get("dumping_ground"));
			return "travelling";
		});
		WaitFor("at-the-dump", 20f, () => DayNightOuting.LocationId == "dumping_ground", () => "at " + DayNightOuting.LocationId);
		Check("my-own-wound-on-my-trip", () => H.woundState == 0, () => Where);
		Delay("b-sees-me-out", 10f);
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "coming home";
		});
		WaitFor("home", 30f, () => !HostTrip.Out && !Outings.Private && DayNightOuting.LocationId == "", () => "host out " + HostTrip.Out);
		WaitFor("b-bandaged-their-wound", 120f, () => Wounds.Of(OtherSeat).Stable && Wounds.Of(OtherSeat).State == hurt, () => Where);
		Check("my-wound-untouched", () => Wounds.Of(Members.LocalSeat).State == 0 && H.woundState == 0, () => Where);
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Check("the-night-healed-theirs-and-left-me-alone", () => Wounds.Of(OtherSeat).State == Math.Max(0, hurt - 4) && Wounds.Of(Members.LocalSeat).State == 0 && H.woundState == 0 && Wounds.Nights >= 1, () => Where + "; it was " + hurt + " before the night; nights run " + Wounds.Nights);
		Check("the-morning-report-names-them", () => DayNightBotBase.Ps.nightLogs != null && DayNightBotBase.Ps.nightLogs.Any((string l) => l.Contains(Members.NameOf(OtherSeat) + ": ")), () => "night log: " + string.Join(" | ", (DayNightBotBase.Ps.nightLogs ?? new List<string>()).Take(8)));
		Delay("b-checks", 25f);
	}
}

public sealed class WoundsB : WoundsBotBase
{
	private long bandage;

	public override string Name => "wd-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		JoinSteps();
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return Where;
		});
		WaitFor("closed-by-host", 300f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		Delay("past-close-timers", 8f);
		Do("door", delegate
		{
			Door();
			return "asked to go out";
		});
		WaitFor("private", 20f, () => Outings.Private && TransitUI.IsOpen, () => "private " + Outings.Private + ", refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Do("travel", delegate
		{
			MapUIManager.Instance.Travel(AfterhourLocation.Get("dumping_ground"));
			return "travelling";
		});
		WaitFor("at-the-dump", 20f, () => DayNightOuting.LocationId == "dumping_ground", () => "at " + DayNightOuting.LocationId);
		Do("get-badly-hurt", delegate
		{
			// A bad search at the dump, twice, through the game's own calls.
			HealthData.ReceiveMinorWound();
			HealthData.ReceiveMajorWound();
			return Where;
		});
		Check("hurt-on-my-trip", () => H.woundState == 7 && !H.isWoundStable, () => Where);
		WaitServer("the-shop-does-not-know-yet", 20f, (IReadOnlyDictionary<string, string> v) => !DayNightBotBase.V(v, "wd.raw").Contains("=7"), "wd.raw");
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "coming home";
		});
		WaitFor("home", 60f, () => !Outings.Private && DayNightBotBase.Live && DayNightOuting.LocationId == "", () => "private " + Outings.Private + ", at " + DayNightOuting.LocationId);
		WaitFor("my-record-has-my-wound", 20f, () => Wounds.Of(Members.LocalSeat).State == 7 && !Wounds.Of(Members.LocalSeat).Stable, () => Where);
		Check("my-screen-shows-it", () => H.woundState == 7 && !H.isWoundStable, () => Where);
		Check("the-host-is-not-hurt", () => Wounds.Of(1).State == 0 && Wounds.Of(1).Stable, () => Where);
		Agree("same-records-as-the-host", 30f, "core.digest", "wd.raw");
		Do("the-door-again", delegate
		{
			Door();
			return "asked to go out with a bad wound";
		});
		WaitFor("refused-too-badly-hurt", 20f, () => DayNight.LastRefusedWhy.Contains("hurt") && !Outings.Private, () => "refused " + DayNight.LastRefusedWhat + ": [" + DayNight.LastRefusedWhy + "]; private " + Outings.Private);
		WaitServer("the-host-could-go", 120f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "out.host") == "1", "out.host");
		WaitFor("the-host-is-back", 120f, () => !Outings.AnyOut, () => Outings.WhoIsOut() + " is still out");
		Check("my-wound-is-still-mine-after-their-trip", () => H.woundState == 7 && Wounds.Of(Members.LocalSeat).State == 7 && Wounds.Of(1).State == 0, () => Where);
		Step("bandage-my-wound", 30f, delegate(BotStep st)
		{
			if (bandage == 0L)
			{
				GameItem val = Items.All().FirstOrDefault((GameItem i) => i.identifier == "bandage_item");
				if (val == null)
				{
					st.Detail = "no bandage in the shop yet";
					return StepStatus.Running;
				}
				bandage = Ids.Of(val);
				st.Detail = ItemsUse.RequestActivate(val) ? ("used bandage #" + bandage) : ("the game would not let me use bandage #" + bandage);
				return StepStatus.Running;
			}
			return Wounds.Of(Members.LocalSeat).Stable ? StepStatus.Pass : StepStatus.Running;
		});
		Check("treated-on-my-screen", () => H.isWoundStable && H.woundState == 7 && Wounds.Of(1).State == 0, () => Where);
		Agree("same-records-after-the-bandage", 30f, "core.digest", "wd.raw");
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Delay("the-morning-lands", 3f);
		Check("the-night-healed-mine", () => Wounds.Of(Members.LocalSeat).State == 3 && H.woundState == 3 && Wounds.Of(1).State == 0, () => Where);
		Agree("same-records-in-the-morning", 30f, "core.digest", "wd.raw");
		Check("no-reload", () => Digest.ReloadsAsked == 0 && DayNight.Mismatches == 0, () => "reloads asked " + Digest.ReloadsAsked + "; day/night mismatches " + DayNight.Mismatches);
	}
}

public sealed class WoundsProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("wd.raw", () => Wounds.Raw);
	}
}
