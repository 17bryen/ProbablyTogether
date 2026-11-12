using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

/// A call belongs to whoever holds the line: (1) the night starts during another player's call,
/// (2) a forgotten call is hung up when its hold times out, (3) the host's own dial is not hung up.
public abstract class PhoneNightBotBase : DayNightBotBase
{
	protected const long Wild = 8376L;

	protected static int Other => Net.Players.Keys.FirstOrDefault((int p) => p != Net.LocalPlayer);

	protected static string Where => "talking " + PhoneChannel.Talking + ", line held by " + Locks.HeldBy("phone") + " (me " + Net.LocalPlayer + ", other " + Other + "); server hang-ups " + DialogueSystem.EndedCalls + " [" + DialogueSystem.LastEndedCall + "]; refusals " + DialogueSync.RefusedCount + " (" + DialogueSystem.LastRefused + ")";
}

public sealed class PhoneNightH : PhoneNightBotBase
{
	public override string Name => "pn-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return Where;
		});
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
		// (1) the night comes while the other player is on the phone
		WaitFor("b-is-ready-and-on-the-phone", 180f, () => PhoneChannel.Talking && Locks.HeldBy("phone") == Other && Other > 0, () => Where);
		Ready("ready-while-they-talk");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Check("the-night-ended-the-call", () => !PhoneChannel.Talking && DialogueSystem.Phone.Current == null && Locks.HeldBy("phone") < 0 && DialogueSystem.EndedCalls == 1 && DialogueSystem.LastEndedCall.Contains("the night"), () => Where);
		// (2) a call its player forgot
		Do("short-hold", delegate
		{
			Locks.TimeoutSeconds = 12;
			return "a hold on anything times out after 12 s without a word (60 in play)";
		});
		WaitFor("b-on-the-phone-again", 180f, () => PhoneChannel.Talking && Locks.HeldBy("phone") == Other, () => Where);
		WaitFor("the-forgotten-call-was-hung-up", 60f, () => !PhoneChannel.Talking && Locks.HeldBy("phone") < 0 && DialogueSystem.EndedCalls == 2 && DialogueSystem.LastEndedCall.Contains("nobody holds"), () => Where);
		Do("usual-hold", delegate
		{
			Locks.TimeoutSeconds = 60;
			return "60 s again";
		});
		// (3) the host's own dial
		Delay("b-sees-it", 3f);
		Do("my-own-dial", delegate
		{
			// What the keypad does once the number answers.
			PhoneUIManager.Instance.StartPhoneDialog(Wild);
			return "dialled Joe Wild on the keypad";
		});
		WaitFor("i-hold-the-line", 15f, () => PhoneChannel.Talking && Locks.HeldBy("phone") == Net.LocalPlayer, () => Where);
		Delay("longer-than-an-unheld-call-lives", 8f);
		Check("my-call-is-still-on", () => PhoneChannel.Talking && Locks.HeldBy("phone") == Net.LocalPlayer && DialogueSystem.EndedCalls == 2, () => Where);
		Do("hang-up", delegate
		{
			PhoneUIManager.Instance.StopCall();
			return "hung up";
		});
		WaitFor("line-free", 15f, () => !PhoneChannel.Talking && Locks.HeldBy("phone") < 0, () => Where);
		Check("only-two-server-hang-ups", () => DialogueSystem.EndedCalls == 2, () => Where);
		Delay("b-checks", 40f);
	}
}

public sealed class PhoneNightB : PhoneNightBotBase
{
	public override string Name => "pn-b";

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
		Do("wild-phone", delegate
		{
			EconomyLab.Op("phone", "8376", "Regular");
			return "Joe Wild can be called (lab op)";
		});
		WaitFor("wild-phone-set", 20f, () => (int)EconomyLab.WildPhone() == 4, () => "Wild's phone " + ((object)EconomyLab.WildPhone()).ToString());
		Ready("ready-first");
		Delay("then-one-more-call", 2f);
		Do("quick-dial-joe-wild", delegate
		{
			bool flag = DialogueSync.RequestCall(Wild, quick: true);
			return "asked for the call: " + flag;
		});
		// The host is Ready as soon as it sees this call: the night starts under it.
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State + "; " + Where);
		Delay("the-morning-lands", 3f);
		Check("no-call-here", () => !PhoneChannel.Talking && Locks.HeldBy("phone") < 0, () => Where);
		Delay("three-comparisons-with-the-server", 32f);
		Agree("same-conversations-as-the-host", 30f, "core.digest");
		Check("nobody-reloaded-over-it", () => Digest.ReloadsAsked == 0 && DayNight.Mismatches == 0, () => "reloads asked " + Digest.ReloadsAsked + "; day/night mismatches " + DayNight.Mismatches + "; " + Where);
		Do("wild-phone-again", delegate
		{
			EconomyLab.Op("phone", "8376", "Regular");
			return "Joe Wild can be called (lab op)";
		});
		WaitFor("wild-phone-set-again", 20f, () => (int)EconomyLab.WildPhone() == 4, () => "Wild's phone " + ((object)EconomyLab.WildPhone()).ToString());
		Delay("the-host-shortened-the-hold", 4f);
		Do("call-and-forget", delegate
		{
			bool flag = DialogueSync.RequestCall(Wild, quick: true);
			return "asked for the call: " + flag;
		});
		WaitFor("on-the-phone", 20f, () => PhoneChannel.Talking && Locks.HeldBy("phone") == Net.LocalPlayer, () => Where);
		WaitFor("the-server-hung-up-my-forgotten-call", 60f, () => !PhoneChannel.Talking && Locks.HeldBy("phone") < 0, () => Where);
		WaitFor("the-host-calls", 60f, () => PhoneChannel.Talking && Locks.HeldBy("phone") == Other, () => Where);
		WaitFor("the-host-hung-up", 60f, () => !PhoneChannel.Talking && Locks.HeldBy("phone") < 0, () => Where);
		Delay("two-comparisons-with-the-server", 22f);
		Agree("same-after", 30f, "core.digest");
		Check("no-reload", () => Digest.ReloadsAsked == 0 && DayNight.Mismatches == 0, () => "reloads asked " + Digest.ReloadsAsked + "; day/night mismatches " + DayNight.Mismatches + "; " + Where);
	}
}
