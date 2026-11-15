using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

/// An offer for one player: the merchant brings it only for the player with standing; the founder cannot close
/// the deal at the counter, the other player can.
public sealed class RegularsH : StandingBotBase
{
	private int cash0;

	private readonly List<string> heard = new List<string>();

	private float lastTry = -100f;

	private float arrivedAt = -1f;

	private int tries;

	public override string Name => "rg-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 600f;

	private static string Tags => "merchant: " + Regulars.Word(Regulars.KindOf("merchant")) + " seat " + Regulars.SeatOf("merchant") + " [" + Regulars.LinesOf("merchant") + "]; tagged so far " + Regulars.Tagged + " (" + Regulars.LastTagged + "); only-now seat " + Regulars.OnlyNow() + "; labels " + string.Join(" | ", Presence.LockLabelTexts.Select((KeyValuePair<string, string> p) => p.Key + "=" + p.Value)) + "; " + Ledger;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2 && OtherSeat > 1, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Delay("b-settles", 6f);
		Do("they-are-well-known-everywhere", delegate
		{
			Regulars.Chance = 100;
			Regulars.Gap = 0;
			Regulars.ShopAt = -1000.0;
			foreach (StoreReputation storeReputation in DayNightBotBase.Ps.storeReputations)
			{
				Standing.LabGrant(OtherSeat, storeReputation.factionId, 40.0);
			}
			LabKit.Do("give", "flashlight", "1");
			DayNightBotBase.Ps.isBargainIntroduced = true;
			return Ledger;
		});
		WaitFor("a-flashlight", 20f, () => Lights.Count >= 1, () => Ledger);
		Do("open-click", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		WaitFor("shutter-settled", 20f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0, () => "flag " + DayNightBotBase.Ps.lockShutterButtonFlag);
		Do("queue", delegate
		{
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "lab op clearqueue";
		});
		WaitFor("empty-queue", 15f, () => DayNightBotBase.Ps.storeClientManager.clientStack.Count == 0, () => "queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count);
		Do("a-merchant-and-a-look-at-the-queue", delegate
		{
			string text = Customers.BotDo("add", "merchant");
			// Credit the other player with thirty sales of what this buyer comes for.
			StoreClient c = DayNightBotBase.Ps.storeClientManager.clientStack.FirstOrDefault((StoreClient x) => x != null && x.identifier == "merchant");
			string keys = "";
			if (c != null)
			{
				List<string> list = new List<string> { c.clientFaction + "/*" };
				list.AddRange((c.clientBuyingTagList ?? new List<string>()).Where((string t) => !string.IsNullOrEmpty(t) && !t.StartsWith("ALL_TYPE", StringComparison.Ordinal)).Select((string t) => c.clientFaction + "/" + t));
				list.AddRange((c.clientBuyingIdList ?? new List<string>()).Where((string t) => !string.IsNullOrEmpty(t)).Select((string t) => c.clientFaction + "/#" + t));
				foreach (string item in list.Distinct())
				{
					Standing.LabGoods(OtherSeat, item, 30, "flashlight");
				}
				keys = "; buys [" + string.Join(",", c.clientBuyingTagList ?? new List<string>()) + "] ids [" + string.Join(",", c.clientBuyingIdList ?? new List<string>()) + "] faction " + c.clientFaction + " intent " + c.clientIntent;
			}
			Regulars.LabRetag();
			return text + keys + "; " + Tags;
		});
		Check("the-merchant-brings-an-offer-only-for-them", () => Regulars.KindOf("merchant") == Regulars.Offer && Regulars.SeatOf("merchant") == OtherSeat && Regulars.LinesOf("merchant").Contains(Members.NameOf(OtherSeat)), () => Tags);
		float lastRing = -100f;
		Step("merchant-at-the-counter-says-who-they-came-for", 120f, delegate(BotStep st)
		{
			st.Detail = At + "; heard: " + string.Join(" / ", heard);
			string text = DialogueSystem.Counter.Current?.plainText;
			if (!string.IsNullOrEmpty(text) && !heard.Contains(text))
			{
				heard.Add(text);
			}
			if (FullDayLab.CurrentId() == "merchant" && DayNightBotBase.Ps.isClientArrived)
			{
				if (arrivedAt < 0f)
				{
					arrivedAt = Time.realtimeSinceStartup;
				}
				// The first line starts a moment after the customer arrives.
				if (Talking() || (heard.Count == 0 && Time.realtimeSinceStartup - arrivedAt < 8f))
				{
					return StepStatus.Running;
				}
				return StepStatus.Pass;
			}
			if (FullDayLab.CurrentId() == "none" && DayNightBotBase.Ps.lockNextBellFlag == 0 && Time.realtimeSinceStartup - lastRing > 6f)
			{
				lastRing = Time.realtimeSinceStartup;
				StoreBell val = Object.FindObjectOfType<StoreBell>();
				if ((Object)(object)val != (Object)null)
				{
					val.OnClick();
				}
			}
			return StepStatus.Running;
		});
		Check("their-name-was-said-in-lines-of-its-own", () => heard.Count >= 2 && heard.Take(2).Any((string l) => l.Contains(Members.NameOf(OtherSeat))), () => "heard: " + string.Join(" / ", heard));
		PlaceStep("i-put-the-flashlight-on-the-counter", () => Lights.FirstOrDefault());
		Do("before", delegate
		{
			cash0 = DayNightBotBase.Ps.playerCash;
			return Tags;
		});
		Step("i-try-to-sell-three-times", 40f, delegate(BotStep st)
		{
			st.Detail = tries + " tries; " + Tags;
			if (Talking() || Time.realtimeSinceStartup - lastTry < 3f)
			{
				return StepStatus.Running;
			}
			if (tries >= 3)
			{
				return StepStatus.Pass;
			}
			lastTry = Time.realtimeSinceStartup;
			tries++;
			List<GameItem> sellableItem = NegociationUIManager.Instance.GetSellableItem();
			if (sellableItem.Count > 0)
			{
				NegociationUIManager.Instance.SellMultiple(sellableItem, false);
			}
			return StepStatus.Running;
		});
		Check("no-deal-for-me-and-the-counter-says-why", () => DayNightBotBase.Ps.playerCash == cash0 && (Regulars.Refusal(Net.LocalPlayer) ?? "").Contains("only deal with " + Members.NameOf(OtherSeat)) && Regulars.Refusal(Other) == null && Presence.LockLabelTexts.ContainsKey("only") && !Presence.LockLabelTexts.ContainsKey("counter"), () => Tags + "; cash before " + cash0 + "; refusal for me [" + Regulars.Refusal(Net.LocalPlayer) + "]");
		WaitFor("they-could-sell", 120f, () => DayNightBotBase.Ps.playerCash > cash0, () => Tags);
		Delay("b-checks", 40f);
	}
}

public sealed class RegularsB : StandingBotBase
{
	private int cash1;

	public override string Name => "rg-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		JoinSteps();
		Do("same-odds-here", delegate
		{
			Regulars.Chance = 100;
			Regulars.Gap = 0;
			Regulars.ShopAt = -1000.0;
			return "every customer who could, does (as on the founder's game)";
		});
		WaitFor("the-merchant-brings-an-offer-only-for-me-here-too", 240f, () => Regulars.KindOf("merchant") == Regulars.Offer && Regulars.SeatOf("merchant") == Members.LocalSeat && Regulars.LinesOf("merchant").Contains(Members.NameOf(Members.LocalSeat)), () => "merchant: " + Regulars.Word(Regulars.KindOf("merchant")) + " seat " + Regulars.SeatOf("merchant") + " [" + Regulars.LinesOf("merchant") + "]; " + Ledger);
		WaitFor("the-founders-flashlight-is-on-the-counter", 180f, () => FullDayLab.CurrentId() == "merchant" && OnCounter(Lights.FirstOrDefault()), () => Ledger);
		Delay("the-founder-tries-and-is-refused", 16f);
		Check("the-counter-is-mine-for-this-one", () => Regulars.OnlyNow() == Members.LocalSeat, () => "only-now seat " + Regulars.OnlyNow() + "; " + Ledger);
		Do("before", delegate
		{
			cash1 = DayNightBotBase.Ps.playerCash;
			return Ledger;
		});
		SellStep("i-press-sell", () => cash1);
		Delay("two-comparisons-with-the-server", 22f);
		Agree("same-shop-at-the-end", 30f, "core.digest", "cash");
		Check("no-reload", () => Digest.ReloadsAsked == 0 && DayNight.Mismatches == 0, () => "reloads asked " + Digest.ReloadsAsked + "; day/night mismatches " + DayNight.Mismatches + "; " + Ledger);
	}
}
