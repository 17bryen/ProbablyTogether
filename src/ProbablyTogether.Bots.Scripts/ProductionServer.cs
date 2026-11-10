using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ProductionServer : ProductionScript
{
	private int day0;

	public override string Name => "production-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene + ", live " + Net.IsLive);
		Do("spawn", delegate
		{
			string text = ProductionLab.SpawnAll();
			if (!Snapshot.NewMorning("lab fixtures (production bot)"))
			{
				throw new InvalidOperationException("the fresh morning snapshot failed");
			}
			if (Net.Players.Count > 0)
			{
				Snapshot.SendToAll();
			}
			ProductionLab.Ready = true;
			day0 = Game.Day;
			return text + "; " + ProductionLab.FxText();
		});
		Do("fixture-hashes", () => string.Join(" ", from k in ProductionLab.HashKeys()
			select k + "=" + ProductionLab.H(k)) + "; labels " + string.Join(" ", ProductionLab.Sets.Select((string x) => ProductionLab.Water(x + ".water") + " " + ProductionLab.Water(x + ".water2") + " " + ProductionLab.Chem(x + ".chem"))) + " hw " + ProductionLab.Water("hw") + "; digest part " + Production.DigestPart());
		WaitFor("player", 170f, () => Net.Players.Count >= 1, () => Net.Players.Count + " player(s)");
		WaitFor("day-done", 420f, () => ProductionLab.MarkCount("day-done") >= Math.Max(1, Net.Players.Count), () => ProductionLab.MarkCount("day-done") + " of " + Net.Players.Count + " player(s) done; applied " + Production.AppliedCount + ", refused " + Production.RefusedCount + ", server-originated " + Production.ServerOriginatedCount + ", cash fixtures " + ProductionLab.Cashed + ", unlocks by production " + Production.UnlockMachineCount + "/" + Production.UnlockFountainCount);
		Check("server-day-clean", () => Production.UnseededServerDay == 0, () => "unseeded server rolls during the day " + Production.UnseededServerDay + "; last action " + Production.LastDone).Soft();
		Do("pre-night", () => string.Join(" ", from k in ProductionLab.HashKeys()
			select k + "=" + ProductionLab.H(k)) + " cash=" + Game.Store.playerCash);
		Do("close-up", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "state " + Game.Store.storeState.ToString();
		});
		Delay("close-anim", 6f);
		Do("end-day", delegate
		{
			EndOfDayUIManager.Instance.OnEndDayButtonClick();
			return "end of day pressed on day " + Game.Day;
		});
		WaitFor("night-ran", 120f, () => Game.Day == day0 + 1 && (int)Game.Store.storeState == 2, () => "day " + Game.Day + " state " + (Game.Store?.storeState).ToString());
		Delay("night-snapshot", 5f);
		Do("post-night", () => string.Join(" ", from k in ProductionLab.HashKeys()
			select k + "=" + ProductionLab.H(k)) + " cash=" + Game.Store.playerCash + "; rats " + string.Join("/", ProductionLab.Sets.Select(ProductionLab.LiveRats)) + ", kcal " + string.Join("/", ProductionLab.Sets.Select((string s) => ProductionLab.Tag(s + ".feeder", "STORED_KCAL_TAG"))));
		WaitFor("night-done", 240f, () => ProductionLab.MarkCount("night-done") >= Math.Max(1, Net.Players.Count), () => ProductionLab.MarkCount("night-done") + " of " + Net.Players.Count).Soft();
	}
}
