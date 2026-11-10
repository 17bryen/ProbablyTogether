namespace ProbablyTogether.Bots.Scripts;

public sealed class DialoguePhoneB : DialogueBotBase
{
	private const long Wild = 8376L;

	private int refusedBefore;

	private int eventsBefore;

	public override string Name => "dialogue-phone-b";

	protected override void Build()
	{
		Join();
		Command("ready", "mark:phone-b", (string r) => r == "ok");
		WaitFor("someone-calls", 300f, () => Locks.HeldBy("phone") >= 0 && Locks.HeldBy("phone") != Net.LocalPlayer && PhoneChannel.Talking, DialogueBotBase.Ph);
		Do("note-refused", delegate
		{
			refusedBefore = DialogueSync.RefusedCount;
			eventsBefore = DialogueBotBase.RefusedEventCount;
			return "refused so far " + refusedBefore + ", events " + eventsBefore;
		});
		Do("forge-call", delegate
		{
			Net.ToServer("phone.call", new MsgPhoneCall
			{
				number = 8376L
			});
			return "sent phone.call around the client gate";
		});
		WaitFor("call-refused", 10f, () => DialogueSync.RefusedCount > refusedBefore, DialogueBotBase.Ph);
		Check("refusal-names-caller", () => DialogueSync.LastRefused == "call: " + Net.PlayerName(Locks.HeldBy("phone")) + " is on the phone" && DialogueBotBase.LastRefusedEvent == "phone|call|" + Net.PlayerName(Locks.HeldBy("phone")) + " is on the phone", () => "last '" + DialogueSync.LastRefused + "', event '" + DialogueBotBase.LastRefusedEvent + "'");
		Do("note-local", delegate
		{
			refusedBefore = DialogueSync.RefusedCount;
			eventsBefore = DialogueBotBase.RefusedEventCount;
			return "refused " + refusedBefore + ", events " + eventsBefore;
		});
		Check("local-gate", () => !DialogueSync.RequestCall(8376L) && !DialogueSync.RequestAdvance("phone"), () => "RequestCall / RequestAdvance on someone else's call are not sent");
		Check("local-gate-notice", () => DialogueSync.RefusedCount == refusedBefore + 2 && DialogueBotBase.RefusedEventCount == eventsBefore + 2 && DialogueBotBase.LastRefusedEvent == "phone|advance|" + Net.PlayerName(Locks.HeldBy("phone")) + " is on the phone", () => "refused " + refusedBefore + " -> " + DialogueSync.RefusedCount + ", events " + eventsBefore + " -> " + DialogueBotBase.RefusedEventCount + ", last '" + DialogueBotBase.LastRefusedEvent + "'");
		Do("forge-advance", delegate
		{
			refusedBefore = DialogueSync.RefusedCount;
			Net.ToServer("dialogue.advance", new MsgDlgReq
			{
				ch = "phone",
				fp = DialogueSync.CurrentFp("phone")
			});
			return "sent dialogue.advance around the client gate";
		});
		WaitFor("advance-refused", 10f, () => DialogueSync.RefusedCount > refusedBefore, DialogueBotBase.Ph);
		WaitFor("call-over", 120f, () => !PhoneChannel.Talking && Locks.HeldBy("phone") < 0 && DialogueSync.Seq("phone") > 0, DialogueBotBase.Ph);
		SameAsServerSoon("same-after-call", 15f, "dlg.phone.at", "dlg.phone.talking", "dlg.phone.caller", "cash", "lock.phone", "dlg.digest");
		Check("no-mismatch", () => DialogueSync.MismatchCount == 0, () => "stepDone mismatches " + DialogueSync.MismatchCount);
	}
}
