using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CounterProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("ctr.lock", () => BotProbeSet.Str(Locks.HeldBy(Customers.CounterLockNow)));
		BotProbes.Add("ctr.jlock", () => BotProbeSet.Str(Locks.HeldBy("outing.jackson")));
		BotProbes.Add("ctr.loc", delegate
		{
			if (!Game.ShopLoaded)
			{
				return "";
			}
			return (!(DayNightOuting.LocationId == "")) ? DayNightOuting.LocationId : "home";
		});
		BotProbes.Add("ctr.nego", delegate
		{
			try
			{
				return Game.ShopLoaded ? Negotiation.Hash().ToString("x16") : "";
			}
			catch
			{
				return "!";
			}
		});
		BotProbes.Add("ctr.feat", () => (!Game.ShopLoaded) ? "" : CounterBot.FeatureText());
		BotProbes.Add("ctr.intent", () => Negotiation.CurrentCustomer()?.clientIntent.ToString() ?? "none");
		BotProbes.Add("ctr.rep", () => (!Game.ShopLoaded) ? "" : CounterBot.RepText());
		BotProbes.Add("ctr.flags", () => PlayerStore.HasInstance ? ((PlayerStore.Instance.isBargainIntroduced ? "b" : "-") + (PlayerStore.Instance.isAugIntroduced ? "a" : "-")) : "");
	}
}
