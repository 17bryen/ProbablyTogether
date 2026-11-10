using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CrewQueueB : CrewQueueBase
{
	private string address;

	private string pw;

	private string name;

	public override string Name => "crewq-b";

	protected override void Build()
	{
		JoinSteps();
		WaitMark("wait-a-opened", "a-opened", 300f);
		Same("opened", 25f, CrewQueueBase.QueueParts);
		CountersSoft("no-mismatch-opened");
		if ((Args.Get("--br-crewq-late") ?? "") == "1")
		{
			Do("leave", delegate
			{
				address = Args.Join;
				name = Net.PlayerName(Net.LocalPlayer);
				pw = Args.Password ?? "";
				Net.Disconnect();
				return "left (" + name + ")";
			});
			WaitFor("at-menu", 90f, () => Net.Role == ProbablyTogether.Role.Off && Ui.AtMenu, () => "role " + Net.Role.ToString() + ", scene " + BotProbes.Scene);
			Do("rejoin-old", delegate
			{
				Client.LabHelloMod = "0.2.7";
				Net.Connect(address, pw, name);
				return "joining again, naming the upstream mod 0.2.7";
			});
			WaitFor("refused-old", 60f, () => Net.Role == ProbablyTogether.Role.Off && Client.LastRefuseCode != null, () => "role " + Net.Role.ToString() + ", state " + Net.ClientState.ToString() + ", refusal '" + Client.LastRefuseCode + "' " + Client.LastRefuseReason);
			Check("refusal-words", () => Client.LastRefuseCode == "protocol" && (Client.LastRefuseReason ?? "").Contains("extra customers"), () => "code '" + Client.LastRefuseCode + "', reason '" + Client.LastRefuseReason + "'");
			Delay("between", 3f);
			Do("rejoin", delegate
			{
				Client.LabHelloMod = null;
				Net.Connect(address, pw, name);
				return "joining again as 0.2.8";
			});
			WaitFor("live-again", 240f, () => FullDayBot.Live, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene + ", refusal '" + Client.LastRefuseCode + "'");
			Same("late-join", 30f, CrewQueueBase.QueueParts);
			CountersSoft("no-mismatch-late");
		}
		Mark("b-done");
	}
}
