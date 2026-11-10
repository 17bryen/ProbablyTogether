using System.Linq;

namespace ProbablyTogether.Bots.Scripts;

public sealed class MachinesXProbes : BotProbeSet
{
	public override void Register()
	{
		MachinesX.Wire();
		BotProbes.Add("machx.marks", MachinesX.Marks);
		BotProbes.Add("machx.ready", () => (!MachinesX.Ready) ? "0" : "1");
		BotProbes.Add("machx.fx", MachinesX.FxText);
		BotProbes.Add("machx.cp", MachinesX.Cp);
		BotProbes.Add("machx.trackstale", () => BotProbeSet.Str(Machines.TrackStale));
		BotProbes.Add("machx.rats", MachinesX.Rats);
		BotProbes.Add("machx.presets", MachinesX.Presets);
		BotProbes.Add("machx.temp", () => BotProbeSet.Str(MachinesX.Temp()));
		BotProbes.Add("machx.temp2", () => BotProbeSet.Str(MachinesX.Temp2()));
		BotProbes.Add("machx.fridge", MachinesX.FridgeDiag);
		BotProbes.Add("machx.boxout", () => BotProbeSet.Str(MachinesX.BoxOut()));
		BotProbes.Add("machx.dealon", MachinesX.DealOn);
		BotProbes.Add("machx.dealenergy", () => BotProbeSet.Str(MachinesX.DealEnergy()));
		BotProbes.Add("machx.refused", () => BotProbeSet.Str(Machines.RefusedCount));
		BotProbes.Add("machx.repairs", () => BotProbeSet.Str(Machines.RepairsSent));
		BotProbes.Add("machx.repthrottled", () => BotProbeSet.Str(Machines.RepairsThrottled));
		BotProbes.Add("machx.chlor", () => MachinesX.WaterLabel(MachinesX.Item("x.chlor")));
		BotProbes.Add("machx.repwhy", delegate
		{
			string text2 = Machines.LastRepairWhy ?? "";
			return text2.Length + ":" + (text2.Any((char c) => char.IsControl(c)) ? "control" : "clean");
		});
		BotProbes.Add("machx.butch2", delegate
		{
			GameItem val = MachinesX.Item("x.butch2");
			GameItem val2 = MachinesX.Item("x.rat");
			GameGridInventory val3 = ((val != null) ? MachineAutoButcher.GetOutputGrid(val) : null);
			return ((val3 != null) ? ((GameInventory)val3).childItems.Count : (-1)) + " in the tray; rat " + ((val2 == null) ? "gone" : (HusbandryHelper.IsAnimalDead(val2) ? "dead" : "alive"));
		});
		BotProbes.Add("machx.lock", delegate
		{
			GameItem val = MachinesX.Item("x.coord");
			return (val != null) ? BotProbeSet.Str(Locks.HeldBy(Machines.LockName(val.uniqueId))) : "";
		});
		string[] dgNames = MachinesX.DgNames;
		foreach (string text in dgNames)
		{
			string name = text;
			BotProbes.Add("machx.dg." + name, () => MachinesX.Dg(name));
		}
	}
}
