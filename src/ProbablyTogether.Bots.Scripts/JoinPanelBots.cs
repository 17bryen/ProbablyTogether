using ProbablyTogether.Systems;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

/// Joining through the game's Join panel: a wrong password is refused with the panel kept, then the right one rejoins the seat.
public sealed class JoinPanelH : PerkSuiteBase
{
	public override string Name => "jp-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 500f;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && DayNightBotBase.Live && Me == 1, () => "role " + Net.Role + ", live " + Net.IsLive);
		WaitFor("they-joined", 240f, () => ProbablyTogether.Server.PlayerCount >= 2 && Members.Online(2), () => ProbablyTogether.Server.PlayerCount + " player(s)");
		WaitFor("they-left", 120f, () => !Members.Online(2), () => ProbablyTogether.Server.PlayerCount + " player(s)");
		WaitFor("they-are-back-in-their-seat", 240f, () => Members.Online(2) && ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s); seat 2 online " + Members.Online(2));
		End(25f);
	}
}

public sealed class JoinPanelB : PerkSuiteBase
{
	private string address = "";

	private string password = "";

	private int seat;

	public override string Name => "jp-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 500f;

	private static string Where => "state " + Net.ClientState + ", at menu " + Ui.AtMenu + ", window " + JoinMenu.DialogOpen + " (game panel " + JoinMenu.OnGamePanel + "), status '" + JoinMenu.Status + "' (hint " + JoinMenu.StatusIsHint + "), loading '" + LoadingScreen.Text + "' " + LoadingScreen.Visible;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("my-seat", 60f, () => Me > 1, () => Where);
		Do("leave-for-the-menu", delegate
		{
			address = Net.ServerAddress;
			password = Args.Get("--br-password") ?? "";
			seat = Me;
			Net.Disconnect();
			return "left " + address + " (seat " + seat + ")";
		});
		WaitFor("at-the-menu", 90f, () => Net.ClientState == ClientState.Idle && Ui.AtMenu && (UnityEngine.Object)(object)JoinMenu.Button != (UnityEngine.Object)null, () => Where);
		Delay("a-moment", 4f);
		Do("open-join-a-shop", delegate
		{
			JoinMenu.OpenDialog();
			return Where;
		});
		WaitFor("the-game's-panel-is-the-join-window", 15f, () => JoinMenu.DialogOpen && JoinMenu.OnGamePanel, () => Where);
		Do("a-wrong-password", delegate
		{
			JoinMenu.LabFill(address, null, password + "-not");
			JoinMenu.Join();
			return "pressed Join";
		});
		WaitFor("refused-and-the-panel-says-why", 40f, () => Net.ClientState == ClientState.Idle && JoinMenu.DialogOpen && JoinMenu.Status.Length > 0 && !JoinMenu.StatusIsHint && !LoadingScreen.Visible, () => Where);
		Do("the-right-one", delegate
		{
			string status = JoinMenu.Status;
			JoinMenu.LabFill(address, null, password);
			JoinMenu.Join();
			return "after '" + status + "': pressed Join";
		});
		WaitFor("in-the-shop-in-my-seat", 180f, () => Net.IsLive && Game.ShopLoaded && Items.Ready && Me == seat, () => Where + "; seat " + Me);
		End(25f);
	}
}
