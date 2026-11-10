using System;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class MachinesXServer : BotScript
{
	public override string Name => "machines-x-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 420f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene + ", live " + Net.IsLive);
		Do("spawn", delegate
		{
			string text = MachinesX.SpawnAll();
			if (!Snapshot.NewMorning("lab fixtures (machines-x bot)"))
			{
				throw new InvalidOperationException("the fresh morning snapshot failed");
			}
			if (Net.Players.Count > 0)
			{
				Snapshot.SendToAll();
			}
			return text + "; " + MachinesX.FxText();
		});
		Do("ready", delegate
		{
			MachinesX.Ready = true;
			return BotProbes.Format(BotProbes.ReadAll(), "machx.cp", "machx.rats", "machx.temp", "machx.presets", "machx.fridge");
		});
		Delay("fridge-5s", 5f);
		Do("fridge-diag", () => MachinesX.FridgeDiag());
		Step("queue-customer", 300f, delegate(BotStep st)
		{
			if (Game.Store == null || (int)Game.Store.storeState != 1)
			{
				st.Detail = "waiting for client A's shutter (state " + (Game.Store?.storeState).ToString() + ")";
				return StepStatus.Running;
			}
			st.Detail = Customers.BotDo("add", "scavGeneral");
			return StepStatus.Pass;
		});
		Step("clients-work", 190f, delegate(BotStep st)
		{
			st.Detail = Machines.AppliedCount + " machine actions, " + Net.Players.Count + " player(s), refused " + Machines.RefusedCount + ", repairs " + Machines.RepairsSent + ", stale track reports " + Machines.TrackStale + "; " + MachinesX.Cp();
			return (Machines.AppliedCount >= 6 && Machines.RepairsSent >= 1 && Machines.RefusedCount >= 2 && Machines.TrackStale >= 1) ? StepStatus.Pass : StepStatus.Running;
		}).Soft();
		Delay("last-replies", 10f);
		Do("final", () => BotProbes.Format(BotProbes.ReadAll(), new string[8] { "machx.cp", "machx.rats", "machx.presets", "machx.temp", "machx.dealon", "machx.refused", "machx.repairs", "machx.trackstale" }.Concat(MachinesX.DgNames.Select((string n) => "machx.dg." + n)).ToArray()));
	}
}
