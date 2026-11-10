using System;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CrewProbes : BotProbeSet
{
	public override void Register()
	{
		CrewLab.Wire();
		BotProbes.Add("crew.table", delegate
		{
			if (!Net.IsServer)
			{
				return Crew.Canon(Crew.Today);
			}
			return (!Game.ShopLoaded) ? "" : Crew.Canon(Crew.Build("probe"));
		});
		BotProbes.Add("crew.on", () => (!Net.IsServer || !Game.ShopLoaded) ? "" : string.Join(",", (from r in Crew.Build("probe").rows
			where r.@on
			select r.n).OrderBy<string, string>((string n) => n, StringComparer.Ordinal)));
		BotProbes.Add("crew.sent", () => BotProbeSet.Str(Crew.Sent));
		BotProbes.Add("crew.last", () => (Crew.LastSent != null) ? (Crew.LastSent.seq + ":" + Crew.LastSent.why) : "");
		BotProbes.Add("crew.recv", () => BotProbeSet.Str(Crew.Received) + "/" + BotProbeSet.Str(Crew.ReceivedInReplay));
		BotProbes.Add("crew.panel", () => (!CrewPanel.Visible) ? "0" : "1");
		BotProbes.Add("crew.lines", () => CrewPanel.ShownLines);
		BotProbes.Add("crew.anchor", () => CrewPanel.Anchor);
		BotProbes.Add("crew.fx", CrewLab.FxText);
		BotProbes.Add("crew.marks", () => string.Join(",", CrewLab.Marks));
		BotProbes.Add("crew.live", () => (!Net.IsServer) ? "" : BotProbeSet.Str(Server.ByPlayer.Values.Count((Server.Peer p) => p.State == Server.PeerState.Ready)));
		BotProbes.Add("crew.cage", () => (!Game.ShopLoaded) ? "" : BotProbeSet.Str(CrewLab.CageHealth()));
		BotProbes.Add("crew.out", () => (!Game.ShopLoaded) ? "" : BotProbeSet.Str(CrewLab.PrinterOut()));
		BotProbes.Add("crew.counted", () => BotProbeSet.Str(Crew.Counted));
	}
}
