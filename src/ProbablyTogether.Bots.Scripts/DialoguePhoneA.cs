using System;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DialoguePhoneA : DialogueBotBase
{
	private const long Wild = 8376L;

	private int seqAtChoose;

	public override string Name => "dialogue-phone-a";

	protected override void Build()
	{
		Join();
		Check("devtools-off", () => !DevTools.IsEnabled, () => "DevTools.IsEnabled " + DevTools.IsEnabled + " while connected");
		WaitMark("watcher-ready", "phone-b", 300f);
		Command("phone-open", "phone-open:" + 8376L, (string r) => r.StartsWith("ok", StringComparison.Ordinal));
		RequestThen("call", 15f, () => DialogueSync.RequestCall(8376L), () => PhoneChannel.Talking && PhoneChannel.Caller?.dialogFuncId == "Wild" && DialogueSystem.Phone.Current != null, DialogueBotBase.Ph);
		Check("holds-phone", () => Locks.HeldBy("phone") == Net.LocalPlayer, DialogueBotBase.Ph);
		SameAsServerSoon("same-call", 10f, "dlg.phone.at", "dlg.phone.caller", "dlg.phone.talking");
		RequestThen("shown-1", 10f, () => DialogueSync.RequestShown("phone"), () => !DialogueSystem.Phone.LineUnfinished, DialogueBotBase.Ph);
		RequestThen("advance", 10f, () => DialogueSync.RequestAdvance("phone"), () => DialogueSystem.Phone.Current != null && DialogueSystem.Phone.Current.choices.Count == 2, DialogueBotBase.Ph);
		RequestThen("shown-2", 10f, () => DialogueSync.RequestShown("phone"), () => !DialogueSystem.Phone.LineUnfinished, DialogueBotBase.Ph);
		Delay("choices-up", 1.5f);
		SameAsServerSoon("same-choice-line", 10f, "dlg.phone.at");
		Do("note-seq", delegate
		{
			seqAtChoose = DialogueSync.Seq("phone");
			return "seq " + seqAtChoose;
		});
		RequestThen("choose-end-call", 10f, () => DialogueSync.RequestChoose("phone", 1), () => DialogueSync.Seq("phone") >= seqAtChoose + 2 && DialogueSystem.Phone.Current != null && DialogueSystem.Phone.Current.endAction != null && DialogueSystem.Phone.Current.choices.Count == 0, DialogueBotBase.Ph);
		RequestThen("shown-goodbye", 15f, () => DialogueSync.RequestShown("phone"), () => !PhoneChannel.Talking && PhoneChannel.Caller == null, DialogueBotBase.Ph);
		WaitFor("line-free", 10f, () => Locks.HeldBy("phone") < 0, DialogueBotBase.Ph);
		SameAsServerSoon("same-after-call", 15f, "dlg.phone.at", "dlg.phone.talking", "dlg.phone.caller", "cash", "lock.phone", "dlg.digest");
		Check("no-mismatch", () => DialogueSync.MismatchCount == 0, () => "stepDone mismatches " + DialogueSync.MismatchCount);
	}
}
