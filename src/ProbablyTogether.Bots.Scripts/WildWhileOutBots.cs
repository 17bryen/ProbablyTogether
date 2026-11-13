using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

/// While the host is at Dr. Jackson's, the other player calls Joe Wild and must get his window on their own screen only.
public sealed class WildWhileOutH : DayNightBotBase
{
	public override string Name => "wo-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 600f;

	private static EmporiumEntry E => EmporiumEntry.Instance;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
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
		Delay("past-close-timers", 14f);
		Do("door", delegate
		{
			GameItem val = ((IEnumerable<GameItem>)E.GetAllOwnedItemsByTag("BACKPACK_TAG")).FirstOrDefault((Func<GameItem, bool>)delegate(GameItem b)
			{
				GameItem bay;
				int num = Bays.OwnerAround((GraphNodeStorage)(object)b.parentInventory, out bay);
				return num == 0 || num == Members.LocalSeat;
			});
			E.selectedBackpack = val;
			MapUIManager.Instance.OpenUI();
			return "asked to go out with backpack " + ((val != null) ? Ids.Of(val) : 0);
		});
		WaitFor("private", 20f, () => Outings.Private && HostTrip.Out && TransitUI.IsOpen, () => "private " + Outings.Private + ", host out " + HostTrip.Out + ", refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Do("travel", delegate
		{
			MapUIManager.Instance.Travel(AfterhourLocation.Get("dr_jackson"));
			return "travelling";
		});
		WaitFor("at-jackson", 20f, () => DayNightOuting.LocationId == "dr_jackson" && HostTrip.CountersBorrowed, () => "at " + DayNightOuting.LocationId + ", counters borrowed " + HostTrip.CountersBorrowed);
		Delay("b-calls-joe-wild-meanwhile", 75f);
		Check("his-window-is-not-on-my-screen", () => !WildNetworkUI.IsOpen && DayNightOuting.LocationId == "dr_jackson", () => "Wild's window open here: " + WildNetworkUI.IsOpen + "; I am at " + DayNightOuting.LocationId + "; windows kept off this screen " + Economy.WindowsSkipped);
		Check("his-delivery-is-not-on-dr-jacksons-counter", () => !((GameInventory)E.backInvinvElement).childItems.Any((GameItem i) => i != null && i.identifier.StartsWith("wine_", StringComparison.Ordinal)) && HostTrip.HomeCounterRemaps >= 8, () => "wine things on the counter in front of me (Dr. Jackson's): " + ((GameInventory)E.backInvinvElement).childItems.Count((GameItem i) => i != null && i.identifier.StartsWith("wine_", StringComparison.Ordinal)) + "; deliveries sent to the shop's counter instead: " + HostTrip.HomeCounterRemaps + "; winemaker entry " + (DayNightBotBase.Ps.networkUpgrade.TryGetValue("WINEMAKER", out var value) ? value.state : (-1)));
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "coming home";
		});
		WaitFor("home", 30f, () => !HostTrip.Out && !Outings.Private && DayNightOuting.LocationId == "", () => "host out " + HostTrip.Out + ", at " + DayNightOuting.LocationId);
		Check("his-delivery-is-on-the-shops-counter", () => ((GameInventory)E.backInvinvElement).childItems.Count((GameItem i) => i != null && i.identifier == "wine_yeast") == 3, () => "wine yeast on the shop's counter: " + ((GameInventory)E.backInvinvElement).childItems.Count((GameItem i) => i != null && i.identifier == "wine_yeast"));
		Delay("b-finishes", 25f);
	}
}

public sealed class WildWhileOutB : DayNightBotBase
{
	public override string Name => "wo-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("closed-by-host", 300f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		WaitServer("host-at-jackson", 240f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "out.jackson") == "1", "out.jackson", "out.count");
		Do("wild-phone", delegate
		{
			EconomyLab.Op("phone", "8376", "Regular");
			return "Joe Wild can be called (lab op)";
		});
		WaitFor("wild-phone-set", 20f, () => (int)EconomyLab.WildPhone() == 4, () => "Wild's phone " + ((object)EconomyLab.WildPhone()).ToString());
		Step("call-wild-while-the-host-is-at-jackson", 50f, EconomyLab.PhoneBrowsePoll());
		Check("his-window-is-on-my-screen", () => WildNetworkUI.IsOpen, () => "Wild's window open here: " + WildNetworkUI.IsOpen + "; dialogue refusals " + DialogueSync.RefusedCount + " (" + DialogueSystem.LastRefused + ")");
		WaitServer("the-host-is-still-there", 10f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "out.jackson") == "1", "out.jackson");
		Agree("same-conversation-as-the-host", 30f, "core.digest");
		Do("buy-the-winemakers-referral", () => EconomyLab.ClickBuy("WINEMAKER"));
		WaitFor("his-delivery-is-on-my-counter", 20f, () => DayNightBotBase.Ps.networkUpgrade.TryGetValue("WINEMAKER", out var value) && value.state == 2 && ((GameInventory)EmporiumEntry.Instance.backInvinvElement).childItems.Count((GameItem i) => i != null && i.identifier == "wine_yeast") == 3, () => "winemaker entry " + (DayNightBotBase.Ps.networkUpgrade.TryGetValue("WINEMAKER", out var value2) ? value2.state : (-1)) + "; wine yeast on my counter " + ((GameInventory)EmporiumEntry.Instance.backInvinvElement).childItems.Count((GameItem i) => i != null && i.identifier == "wine_yeast") + "; cash " + DayNightBotBase.Ps.playerCash + "; last refusal " + Economy.LastRefusal);
		WaitServer("the-host-is-still-at-jackson", 10f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "out.jackson") == "1", "out.jackson");
		Agree("same-shop-as-the-host-with-the-delivery", 30f, "core.digest", "items.digest");
		Do("hang-up", delegate
		{
			WildNetworkUI.Close();
			return "closed his list";
		});
		WaitFor("host-home", 240f, () => !Outings.AnyOut, () => Outings.WhoIsOut() + " is still out");
		Delay("settle", 4f);
		Agree("same-after", 30f, "core.digest", "items.digest", "cash");
		Check("no-reload", () => Digest.ReloadsAsked == 0 && DayNight.Mismatches == 0, () => "reloads asked " + Digest.ReloadsAsked + "; day/night mismatches " + DayNight.Mismatches);
	}
}
