using System;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

/// The night without a reload on a real save: a second player joins, both sleep, and the copy compares each block.
/// Run with hostsuite -Save <a save_N.es3>.
public abstract class NightRealBase : DayNightBotBase
{
	protected static string Where => "day " + DayNightBotBase.Day + " " + DayNightBotBase.State;
}

public sealed class NightRealH : NightRealBase
{
	public override string Name => "nr-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && DayNightBotBase.Live, () => "role " + Net.Role + ", live " + Net.IsLive + ", " + Where);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Delay("b-settles", 15f);
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return Where;
		});
		Do("open-if-morning", delegate
		{
			if (DayNightBotBase.State == "MORNING")
			{
				StoreShutterButton.Instance.OnClick();
				return "shutter opened";
			}
			return "already " + DayNightBotBase.State;
		});
		WaitFor("open", 30f, () => DayNightBotBase.State == "OPEN" || DayNightBotBase.State == "CLOSED", () => Where);
		Do("queue", delegate
		{
			if (DayNightBotBase.State != "OPEN" || DayNightBotBase.Ps.CanEndDay())
			{
				return "nothing to clear";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "lab op clearqueue";
		});
		WaitFor("closable", 60f, () => DayNightBotBase.State == "CLOSED" || (DayNightBotBase.Ps.lockShutterButtonFlag == 0 && DayNightBotBase.Ps.CanEndDay()), () => "queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count + ", " + Where);
		Do("close", delegate
		{
			if (DayNightBotBase.State == "OPEN")
			{
				StoreShutterButton.Instance.EndAndClose();
				return "sent";
			}
			return "already " + DayNightBotBase.State;
		});
		WaitFor("closed", 30f, () => DayNightBotBase.State == "CLOSED", () => Where);
		Delay("past-close-timers", 14f);
		Ready("ready");
		WaitFor("night", 300f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => Where);
		Check("the-night-in-blocks-ran", () => NightBlocks.DeltaItems >= 0, () => "night delta steps " + NightBlocks.DeltaItems);
		Delay("b-compares", 60f);
	}
}

public sealed class NightRealB : NightRealBase
{
	public override string Name => "nr-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		JoinSteps();
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return Where;
		});
		WaitFor("closed-by-host", 300f, () => DayNightBotBase.State == "CLOSED", () => Where);
		Delay("past-close-timers", 6f);
		Ready("ready");
		WaitFor("night", 300f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => Where);
		Check("the-night-start-was-compared", () => NightCheck.Checks >= 1, () => NightCheck.LastSummary);
		Check("block-s1-ran", () => NightBlocks.Runs >= 1, () => NightBlocks.LastSummary);
		Check("block-s2-ran", () => NightBlocks.S2Runs >= 1, () => NightBlocks.S2Summary);
		Check("the-night-start-was-the-same", () => NightCheck.Differ + NightCheck.OnlyServer + NightCheck.OnlyHere == 0, () => NightCheck.LastSummary).Soft();
		Check("block-s1-the-same", () => NightBlocks.S1Bad == 0, () => "s1 differences " + NightBlocks.S1Bad).Soft();
		Check("the-morning-came-in-place", () => NightInPlace.Mornings >= 1, () => "in place " + NightInPlace.Mornings + "; why not: " + NightInPlace.LastWhyNot);
		Delay("the-morning-settles", 15f);
		Check("no-resync-after-the-morning", () => Snapshot.AskCount == 0 && Digest.ReloadsAsked == 0, () => "resyncs asked " + Snapshot.AskCount + ", reloads asked " + Digest.ReloadsAsked);
		Check("block-s2-the-same", () => NightBlocks.S2Bad == 0, () => NightBlocks.S2Summary).Soft();
	}
}
