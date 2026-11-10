namespace ProbablyTogether.Bots.Scripts;

public sealed class NegotiationRealB : NegotiationRealScript
{
	private int skipped;

	public override string Name => "nego3-b";

	private void Phase(string mark, string tag, float timeout, params string[] probes)
	{
		WaitMark(mark, timeout);
		Converge("same-" + tag, 20f, probes);
		Check("tablet-stayed-closed-" + tag, () => !NegotiationRealProbes.TabletOpen(), () => "tablet " + NegotiationRealScript.Mine("nego.tablet") + ", skipped " + NegotiationRealScript.Mine("nego.tskip") + ", last gate " + NegotiationRealScript.Mine("nego.gate"));
		Mark("b-" + tag);
	}

	protected override void Build()
	{
		Joined();
		Mark("b-ready");
		WaitMark("a-stocked", 180f);
		Do("note-skips", delegate
		{
			skipped = Negotiation.TabletSkipped;
			return "tablet opens skipped so far " + skipped;
		});
		Phase("a-sell", "sell", 240f, NegotiationRealScript.Shop);
		Check("skipped-a-moves", () => Negotiation.TabletSkipped > skipped, () => "skipped " + skipped + " -> " + Negotiation.TabletSkipped + " (" + Negotiation.LastTabletGate + ")");
		Phase("a-budget", "budget", 120f, NegotiationRealScript.Shop);
		Check("no-confirm-here", () => Negotiation.ConfirmsAsked == 0 && !NegotiationRealProbes.ConfirmOpen(), () => "confirms " + Negotiation.ConfirmsAsked);
		Phase("a-walkout", "walkout", 180f, "cash", "nego.cust", "nego.counter", "nego.rep", "nego.hash", "nego.digest");
		Phase("a-buy", "buy", 240f, NegotiationRealScript.Shop);
		Phase("a-barter", "barter", 240f, NegotiationRealScript.Shop);
		Phase("a-gun", "gun", 240f, NegotiationRealScript.Shop);
		Phase("a-info", "info", 240f, NegotiationRealScript.Shop);
		Check("info-gate", () => Negotiation.TabletSkipped > skipped, () => "last gate " + Negotiation.LastTabletGate);
		Phase("a-water", "water", 240f, NegotiationRealScript.Shop);
		Phase("a-label", "label", 120f, "nego.label", "nego.name", "nego.guids", "nego.hash");
		Check("no-labeler-click-here", () => Negotiation.LabelerClicks == 0, () => "labeler clicks here " + Negotiation.LabelerClicks);
		Phase("a-estimate", "estimate", 240f, "cash", "nego.cust", "nego.counter", "nego.career", "nego.hash", "nego.digest");
		Phase("a-walkout2", "appraise", 300f, "cash", "nego.cust", "nego.counter", "nego.career", "nego.hash", "nego.digest");
		Phase("a-done", "block", 120f, "nego.career", "nego.hash", "nego.digest");
		Check("no-mismatch-live", () => Replay.ChecksBad == 0, () => "seeded checks ok " + Replay.ChecksOk + " bad " + Replay.ChecksBad + " (" + Replay.LastMismatch + ")").Soft();
		int loads = 0;
		Do("resync", delegate
		{
			loads = NegotiationProbes.LoadedCount;
			Snapshot.RequestResync();
			return "asked for the morning snapshot + day log";
		});
		WaitFor("resynced", 120f, () => NegotiationProbes.LoadedCount > loads && Net.IsLive && BotProbes.InShop, () => "loads " + NegotiationProbes.LoadedCount + ", live " + Net.IsLive);
		Delay("settle-after-resync", 2f);
		Converge("same-replayed", 30f, "cash", "favor", "nego.cust", "nego.counter", "nego.rep", "nego.sold", "nego.career", "nego.label", "nego.name", "nego.guids", "nego.hash", "nego.digest");
		Check("tablet-closed-after-rebuild", () => !NegotiationRealProbes.TabletOpen(), () => "tablet " + NegotiationRealScript.Mine("nego.tablet") + " (" + NegotiationRealScript.Mine("nego.gate") + ")");
		Mark("b-done");
	}
}
