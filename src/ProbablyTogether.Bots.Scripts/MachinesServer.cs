using System;
using System.Linq;

namespace ProbablyTogether.Bots.Scripts;

public sealed class MachinesServer : BotScript
{
	private int maxPlayers;

	public override string Name => "machines-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 420f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene + ", live " + Net.IsLive);
		Do("spawn", delegate
		{
			string text = MachinesLab.SpawnAll();
			if (!Snapshot.NewMorning("lab fixtures (machines bot)"))
			{
				throw new InvalidOperationException("the fresh morning snapshot failed");
			}
			if (Net.Players.Count > 0)
			{
				Snapshot.SendToAll();
			}
			return text + "; " + MachinesLab.FxText();
		});
		Do("server-press", delegate
		{
			GameItem val = MachinesLab.Item("s.box");
			object obj = MachinesLab.Widget<MachineryButton>(val, 0);
			MachineryButton val2 = (MachineryButton)((obj is MachineryButton) ? obj : null);
			if (val2 == null)
			{
				throw new InvalidOperationException("s.box has no button (" + Machines.Widgets(val).Count + " widgets)");
			}
			int appliedCount = Machines.AppliedCount;
			MachinesLab.Press(val2);
			MachinesLab.ServerPress = "1";
			return "pressed '" + val2.text + "' on s.box; actions sent " + appliedCount + " -> " + Machines.AppliedCount;
		});
		Step("server-press-result", 5f, delegate(BotStep st)
		{
			if (MachinesLab.OutCount("s") == 1 && MachinesLab.Energy("s") == 46)
			{
				st.Detail = "1 bottle, 46 energy: " + MachinesLab.Box("s");
				return StepStatus.Pass;
			}
			st.Detail = MachinesLab.Diag("s");
			return StepStatus.Running;
		}).TimeoutDetail = () => MachinesLab.Diag("s");
		Do("ready", delegate
		{
			MachinesLab.Ready = true;
			return "fixtures ready: " + BotProbes.Format(BotProbes.ReadAll(), "mach.box.s", "mach.hop.0", "mach.hop.1");
		});
		Step("clients-work", 300f, delegate(BotStep st)
		{
			int count = Net.Players.Count;
			maxPlayers = Math.Max(maxPlayers, count);
			int num = 2 + 5 * Math.Max(1, maxPlayers);
			string text = MachinesX.Marks();
			bool flag = MachinesLab.HasMark(text, "mach-done-0") && (!MachinesLab.HasMark(text, "mach-live-1") || MachinesLab.HasMark(text, "mach-done-1"));
			st.Detail = Machines.AppliedCount + " of " + num + " machine actions, " + count + " player(s), " + Machines.RepairsSent + " repairs; marks " + text;
			return (Machines.AppliedCount >= num && flag) ? StepStatus.Pass : StepStatus.Running;
		}).Soft();
		Delay("last-replies", 8f);
		Do("final", () => BotProbes.Format(BotProbes.ReadAll(), MachinesLab.Sets.SelectMany(MachinesLab.StateKeys).Concat(new string[3] { "mach.applied", "mach.repairs", "mach.ticks" }).ToArray()));
	}
}
