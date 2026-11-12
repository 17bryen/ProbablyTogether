using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

/// The commissary's credit and shelves stay the shop's on a private trip: each player buys and redeems in turn,
/// and every copy ends with the same numbers without a reload.
public abstract class CommissaryTripBotBase : DayNightBotBase
{
	protected static int Other => Net.Players.Keys.FirstOrDefault((int p) => p != Net.LocalPlayer);

	protected static int Credit => DayNightBotBase.Ps?.commissaryData?.redeemedCredit ?? (-1);

	protected static int Fruit => DayNightBotBase.Ps?.commissaryData?.GetItemData("nutrifruit")?.StockCount ?? (-1);

	protected static int StampsInTheShop => Items.All().Count((GameItem i) => i != null && i.IsTag("FOOD_STAMP_TAG") && !HostTrip.NotAtHome(i) && Ids.Of(i) < HostTrip.PrivateIdBase);

	protected static string Where => "credit " + Credit + ", nutrifruit on the shelf " + Fruit + ", stamps in the shop " + StampsInTheShop + "; commissary changes sent " + Outings.CommissarySent + " / taken " + Outings.CommissaryTaken + "; redeemed carried " + Economy.RedeemedCarried + ", asked at home " + Economy.RedeemedHomeAsked + ", done at home " + Economy.RedeemedHome + "; at '" + DayNightOuting.LocationId + "'";

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

public sealed class CommissaryTripH : CommissaryTripBotBase
{
	public override string Name => "cm-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Delay("b-settles", 6f);
		Do("the-shop-has-credit-and-stamps", delegate
		{
			EconomyLab.Op("credit", "100");
			EconomyLab.Op("stock", "nutrifruit", "5");
			EconomyLab.Op("spawn", "food_stamp", "2");
			return "lab ops: 100 credit, 5 nutrifruit on the shelf, 2 real stamps (40 each) in the shop";
		});
		WaitFor("set", 20f, () => Credit == 100 && Fruit == 5 && StampsInTheShop == 2, () => Where);
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
		Check("still-set-after-the-day", () => Credit == 100 && Fruit == 5 && StampsInTheShop == 2, () => Where);
		WaitFor("their-trip-reached-the-shop", 240f, () => Credit == 156 && Fruit == 3 && StampsInTheShop == 0, () => Where);
		Delay("nothing-counted-twice", 6f);
		Check("the-shop-has-exactly-that", () => Credit == 156 && Fruit == 3 && StampsInTheShop == 0 && Economy.RedeemedHome == 2, () => Where);
		WaitFor("b-is-home", 120f, () => !Outings.AnyOut, () => Outings.WhoIsOut() + " is still out");
		Delay("b-compares", 15f);
		Do("my-turn", delegate
		{
			Door();
			return "asked to go out";
		});
		WaitFor("i-am-out", 20f, () => Outings.Private && HostTrip.Out && TransitUI.IsOpen, () => "private " + Outings.Private + ", host out " + HostTrip.Out + ", refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Do("travel", delegate
		{
			MapUIManager.Instance.Travel(AfterhourLocation.Get("commissary"));
			return "travelling";
		});
		WaitFor("at-the-commissary", 20f, () => DayNightOuting.LocationId == "commissary", () => Where);
		Check("i-find-what-they-left", () => Credit == 156 && Fruit == 3, () => Where);
		Do("buy-one", delegate
		{
			DayNightBotBase.Ps.commissaryData.BuyItem("nutrifruit");
			return Where;
		});
		Check("bought", () => Credit == 144 && Fruit == 2, () => Where);
		Delay("b-sees-it-while-i-am-out", 12f);
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "coming home";
		});
		WaitFor("home", 30f, () => !HostTrip.Out && !Outings.Private && DayNightOuting.LocationId == "", () => "host out " + HostTrip.Out);
		Check("the-shop-after-both-trips", () => Credit == 144 && Fruit == 2 && StampsInTheShop == 0, () => Where);
		Delay("b-checks", 45f);
	}
}

public sealed class CommissaryTripB : CommissaryTripBotBase
{
	public override string Name => "cm-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("closed-by-host", 300f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		Delay("past-close-timers", 8f);
		Check("same-start", () => Credit == 100 && Fruit == 5 && StampsInTheShop == 2, () => Where);
		Do("door", delegate
		{
			Door();
			return "asked to go out";
		});
		WaitFor("private", 20f, () => Outings.Private && TransitUI.IsOpen, () => "private " + Outings.Private + ", refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Do("travel", delegate
		{
			MapUIManager.Instance.Travel(AfterhourLocation.Get("commissary"));
			return "travelling";
		});
		WaitFor("at-the-commissary", 20f, () => DayNightOuting.LocationId == "commissary", () => Where);
		Do("buy-two-nutrifruit", delegate
		{
			DayNightBotBase.Ps.commissaryData.BuyItem("nutrifruit");
			DayNightBotBase.Ps.commissaryData.BuyItem("nutrifruit");
			return Where;
		});
		Check("bought-here", () => Credit == 76 && Fruit == 3, () => Where);
		WaitServer("the-shop-knows-what-i-bought", 20f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "econ.tokens").StartsWith("76/", StringComparison.Ordinal), "econ.tokens");
		Do("redeem-all", delegate
		{
			// The two stamps are in the shop at home, not in the bag.
			CommissaryUIManager.Instance.OnRedeemAllButtonClick();
			return Where;
		});
		WaitFor("the-stamps-at-home-became-credit", 20f, () => Credit == 156 && StampsInTheShop == 0, () => Where);
		WaitServer("for-the-shop-too", 20f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "econ.tokens").StartsWith("156/", StringComparison.Ordinal), "econ.tokens");
		Delay("nothing-counted-twice", 5f);
		Check("still-156", () => Credit == 156 && Fruit == 3, () => Where);
		Do("return", delegate
		{
			MapUIManager.Instance.ReturnNow();
			return "coming home";
		});
		WaitFor("home", 60f, () => !Outings.Private && DayNightBotBase.Live && DayNightOuting.LocationId == "", () => "private " + Outings.Private + ", at " + DayNightOuting.LocationId);
		// The fruit stays at the counter; only credit, shelves and stamps are checked.
		Check("home-with-the-same-numbers", () => Credit == 156 && Fruit == 3 && StampsInTheShop == 0, () => Where);
		Agree("same-shop-as-the-host", 30f, "core.digest", "econ.tokens", "econ.hash", "items.digest");
		WaitServer("the-host-goes", 120f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "out.host") == "1", "out.host");
		WaitFor("i-see-what-the-host-bought", 60f, () => Credit == 144 && Fruit == 2, () => Where);
		WaitFor("the-host-is-back", 120f, () => !Outings.AnyOut, () => Outings.WhoIsOut() + " is still out");
		Delay("two-comparisons-with-the-server", 22f);
		Agree("same-after-both-trips", 30f, "core.digest", "econ.tokens", "econ.hash", "items.digest");
		Check("no-reload", () => Digest.ReloadsAsked == 0 && DayNight.Mismatches == 0 && Credit == 144 && Fruit == 2, () => "reloads asked " + Digest.ReloadsAsked + "; day/night mismatches " + DayNight.Mismatches + "; " + Where);
	}
}
