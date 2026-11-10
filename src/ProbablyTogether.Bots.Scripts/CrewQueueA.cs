namespace ProbablyTogether.Bots.Scripts;

public sealed class CrewQueueA : CrewQueueBase
{
	public override string Name => "crewq-a";

	protected override void Build()
	{
		JoinSteps();
		Do("queue-before", () => "queue before opening " + BotProbes.Read("fd.queue") + "; expect " + CrewQueueBase.Expect);
		Do("open", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "dayNight.open sent";
		});
		WaitFor("opened", 30f, () => FullDayBot.State == "OPEN", FullDayBot.Shop);
		CheckDecision("decision");
		Same("after-open", 25f, CrewQueueBase.QueueParts);
		CountersSoft("no-mismatch-after-open");
		Mark("a-opened");
		WaitMark("wait-b-done", "b-done", 600f);
		Same("after-b", 25f, CrewQueueBase.QueueParts);
		CountersSoft("no-mismatch-end");
		Mark("a-done");
	}
}
