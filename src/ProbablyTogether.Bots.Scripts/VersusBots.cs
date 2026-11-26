using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

/// Versus suite: a host and two players each run a shop of their own (water merchant, gunsmith, farmer). The game
/// layer plays solo on every copy; the bedtime waits for every shop or its timer, which closes a lagging shop; the
/// host keeps a hash-checked copy of every save; the board ranks by cash minus debt; the run ends after day 2.
public abstract class VersusBotBase : DayNightBotBase
{
	protected static string Where => "link " + Net.Link + ", game role " + Net.Role + ", versus " + Net.Versus + ", shop up " + Versus.ShopUp + ", day " + DayNightBotBase.Day + " " + DayNightBotBase.State + ", ready " + Versus.LocalReady + ", nights " + Versus.NightsRun + " (forced " + Versus.NightsForced + "), saves confirmed " + Versus.SavesConfirmed + "; board [" + Versus.BoardLine() + "]";

	protected static VsRow RowOf(string spec)
	{
		return Versus.Board.FirstOrDefault((VsRow r) => r.spec == spec);
	}

	protected static VsRow Mine => Versus.Board.FirstOrDefault((VsRow r) => r.player == Net.LocalPlayer);

	protected static bool AllOnDay(int day)
	{
		if (Versus.Board.Count == 3)
		{
			return Versus.Board.All((VsRow r) => r.day == day);
		}
		return false;
	}

	protected void SoloChecks()
	{
		WaitFor("own-shop-up", 240f, () => Versus.ShopUp && Game.ShopLoaded && DayNightBotBase.Day == 1, () => Where);
		Check("the-game-layer-plays-solo", () => !Net.IsOnline && !Net.IsServer && !Net.IsClient && !Net.IsLive && Net.LinkOnline, () => Where);
		Check("the-shop-saves-in-the-run's-folder", () => Paths.Root.Replace('\\', '/').EndsWith("/versus/" + Versus.Rules.run, StringComparison.Ordinal), () => "data folder " + Paths.Root + ", run " + Versus.Rules?.run);
	}

	protected void SetCash(int cash)
	{
		Do("set-own-cash-" + cash, delegate
		{
			DayNightBotBase.Ps.playerCash = cash;
			return "cash " + DayNightBotBase.Ps.playerCash;
		});
		WaitFor("the-board-shows-it", 30f, () => Mine?.cash == cash, () => Where);
	}

	protected void CloseAndGoToBed(string day)
	{
		Do("open-" + day, delegate
		{
			if ((int)DayNightBotBase.Ps.storeState == 2)
			{
				StoreShutterButton.Instance.OpenShutter();
			}
			return "state " + DayNightBotBase.State;
		});
		Delay("open-a-moment-" + day, 4f);
		Do("clear-queue-" + day, delegate
		{
			DayNightBotBase.Ps.storeClientManager?.clientStack?.Clear();
			return "queue cleared";
		});
		WaitFor("closable-" + day, 20f, () => DayNightBotBase.Ps.CanEndDay(), () => Where);
		Do("close-" + day, delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "closing";
		});
		WaitFor("closed-" + day, 20f, () => DayNightBotBase.State == "CLOSED", () => Where);
		Delay("closed-a-moment-" + day, 3f);
		Do("end-day-button-" + day, delegate
		{
			EndOfDayUIManager.Instance.OnEndDayButtonClick();
			return "pressed";
		});
		// The last shop to ask ends the wait at once: the night or the run's end follows.
		WaitFor("asked-for-bed-" + day, 15f, () => (Versus.LocalReady && (Mine?.ready ?? false)) || Versus.Ended || Versus.NightsRun > ((day == "day1") ? 0 : 1), () => Where);
	}

	protected void FinalChecks()
	{
		WaitFor("the-run-ends-after-day-2", 180f, () => Versus.Ended, () => Where);
		Check("ranked-by-cash-minus-debt", () => Versus.Board.Count == 3 && Versus.Board[0].spec == "GUNSMITH" && Versus.Board[1].spec == "WATER_MERCHANT" && Versus.Board[2].spec == "FARMER" && Versus.Board[0].Worth > Versus.Board[1].Worth && Versus.Board[1].Worth > Versus.Board[2].Worth, () => Where);
		Check("no-night-after-the-last-day", () => DayNightBotBase.Day == 2 && Versus.NightsRun == 1, () => Where);
	}
}

public sealed class VersusHost : VersusBotBase
{
	public override string Name => "vs-h";

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		WaitFor("lobby-open", 60f, () => Net.Versus && Net.LinkHost && ProbablyTogether.Server.Listening && Versus.Rules != null, () => Where);
		Check("rules", () => Versus.Rules.days == 2 && Versus.Rules.bedSeconds == 45 && Versus.Rules.lobbyDay == 1, () => "days " + Versus.Rules.days + ", bed " + Versus.Rules.bedSeconds + ", lobby day " + Versus.Rules.lobbyDay);
		SoloChecks();
		WaitFor("three-shops-on-the-board", 240f, () => Versus.Board.Count == 3 && Versus.Board.All((VsRow r) => r.spec.Length > 0 && r.day == 1), () => Where);
		Check("three-different-trades", () => Versus.Row(Net.LocalPlayer)?.spec == "WATER_MERCHANT" && RowOf("GUNSMITH") != null && RowOf("FARMER") != null, () => Where);
		SetCash(5000);
		CloseAndGoToBed("day1");
		WaitFor("b-is-ready", 120f, () => RowOf("GUNSMITH")?.ready ?? false, () => Where);
		Delay("the-night-waits", 8f);
		Check("the-night-waits-for-c", () => DayNightBotBase.Day == 1 && Versus.NightsRun == 0 && !(RowOf("FARMER")?.ready ?? true) && Versus.SecondsLeft > 0, () => Where + "; " + Versus.SecondsLeft + " s left");
		WaitFor("the-timer-sends-every-shop-to-bed", 120f, () => Versus.NightsRun == 1 && DayNightBotBase.Day == 2, () => Where);
		WaitFor("every-shop-on-day-2", 120f, () => AllOnDay(2), () => Where);
		WaitFor("every-day-2-save-copied", 120f, () => Versus.Board.All((VsRow r) => r.InSync && r.copyDay == 2), () => Where);
		Check("the-copies-on-disk-match", delegate
		{
			string text = Versus.RunFolder + "/players";
			if (!System.IO.Directory.Exists(text))
			{
				return false;
			}
			List<string> list = System.IO.Directory.GetFiles(text, "save.es3", SearchOption.AllDirectories).ToList();
			if (list.Count == 3)
			{
				return list.All((string f) => Versus.Board.Any((VsRow r) => r.copyHash == Versus.Hash(File.ReadAllBytes(f))));
			}
			return false;
		}, () => "copies " + string.Join(", ", System.IO.Directory.Exists(Versus.RunFolder + "/players") ? System.IO.Directory.GetFiles(Versus.RunFolder + "/players", "save.es3", SearchOption.AllDirectories) : new string[0]));
		Check("lobby-day-2", () => Versus.Rules.lobbyDay == 2 && !Versus.Ended, () => "lobby day " + Versus.Rules.lobbyDay);
		CloseAndGoToBed("day2");
		FinalChecks();
		Check("run-json-says-ended", () => File.Exists(Versus.RunFolder + "/run.json") && File.ReadAllText(Versus.RunFolder + "/run.json").Contains("\"ended\": true"), () => Versus.RunFolder + "/run.json");
	}
}

public sealed class VersusB : VersusBotBase
{
	public override string Name => "vs-b";

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		WaitFor("joined-a-versus-run", 90f, () => Net.Versus && Net.Link == ProbablyTogether.Role.Client && Versus.Rules != null, () => Where);
		Check("the-host's-rules", () => Versus.Rules.days == 2 && Versus.Rules.bedSeconds == 45, () => "days " + Versus.Rules.days + ", bed " + Versus.Rules.bedSeconds);
		SoloChecks();
		Check("own-trade-the-host's-difficulty", () => DayNightBotBase.Ps.startType.ToString() == "GUNSMITH" && NewGameData.Instance != null && NewGameData.Instance.difficultyId == Versus.Rules.difficulty, () => "trade " + DayNightBotBase.Ps.startType.ToString() + ", difficulty " + NewGameData.Instance.difficultyId + " (host's " + Versus.Rules.difficulty + ")");
		SetCash(9000);
		CloseAndGoToBed("day1");
		Delay("still-day-1", 6f);
		Check("no-night-before-everyone", () => DayNightBotBase.Day == 1 && Versus.NightsRun == 0, () => Where);
		WaitFor("the-night-comes", 150f, () => Versus.NightsRun == 1 && DayNightBotBase.Day == 2, () => Where);
		Check("not-forced", () => Versus.NightsForced == 0, () => Where);
		WaitFor("the-host-kept-our-day-2-save", 120f, () => Versus.SavesConfirmed >= 1 && (RowOf("GUNSMITH")?.InSync ?? false) && RowOf("GUNSMITH").copyDay == 2, () => Where + "; last answer " + Versus.LastSavedWhy);
		WaitFor("everyone-on-day-2", 120f, () => AllOnDay(2), () => Where);
		CloseAndGoToBed("day2");
		FinalChecks();
	}
}

public sealed class VersusC : VersusBotBase
{
	public override string Name => "vs-c";

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		WaitFor("joined-a-versus-run", 90f, () => Net.Versus && Net.Link == ProbablyTogether.Role.Client && Versus.Rules != null, () => Where);
		SoloChecks();
		SetCash(100);
		WaitFor("the-timer-ends-our-day", 240f, () => Versus.NightsRun == 1 && DayNightBotBase.Day == 2, () => Where);
		Check("our-day-was-ended-for-us", () => Versus.NightsForced == 1, () => Where);
		WaitFor("everyone-on-day-2", 120f, () => AllOnDay(2), () => Where);
		CloseAndGoToBed("day2");
		FinalChecks();
	}
}
