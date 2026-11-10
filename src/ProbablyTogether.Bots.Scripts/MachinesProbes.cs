namespace ProbablyTogether.Bots.Scripts;

public sealed class MachinesProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("mach.ready", () => (!MachinesLab.Ready) ? "0" : "1");
		BotProbes.Add("mach.fx", MachinesLab.FxText);
		BotProbes.Add("mach.press", () => MachinesLab.ServerPress);
		string[] sets = MachinesLab.Sets;
		foreach (string text in sets)
		{
			string set = text;
			BotProbes.Add("mach.box." + set, () => MachinesLab.Box(set));
			BotProbes.Add("mach.plate." + set, () => BotProbeSet.Str(MachinesLab.Tag(set + ".plate", "HEATING_PLATE_STATE")));
			BotProbes.Add("mach.feed." + set, () => BotProbeSet.Str(MachinesLab.Tag(set + ".feed", "FEED_DISPENSER_TARGET_PERCENT_INT")));
			BotProbes.Add("mach.hop." + set, () => MachinesLab.Hop(set));
			BotProbes.Add("mach.chem." + set, () => MachinesLab.TagS(set + ".chem", "SELECTED_CHEM_ID_TAG"));
			BotProbes.Add("mach.farm." + set, () => MachinesLab.Farm(set));
			BotProbes.Add("mach.temp." + set, () => BotProbeSet.Str(MachinesLab.Temp(set)));
			BotProbes.Add("mach.feedkcal." + set, () => BotProbeSet.Str(MachinesLab.Tag(set + ".feed", "STORED_KCAL_TAG")) + "|pellet " + (Items.IsGone(MachinesLab.Item(set + ".pellet")) ? "blended" : "here"));
			BotProbes.Add("mach.lock." + set, delegate
			{
				GameItem val = MachinesLab.Item(set + ".chem");
				return (val != null) ? BotProbeSet.Str(Locks.HeldBy(Machines.LockName(val.uniqueId))) : "";
			});
			string[] array = new string[8] { "box", "plate", "feed", "chem", "in", "farm", "butch1", "butch2" };
			foreach (string text2 in array)
			{
				string name = set + "." + text2;
				BotProbes.Add("mach.dg." + name, () => MachinesLab.Dg(name));
			}
		}
		BotProbes.Add("mach.applied", () => BotProbeSet.Str(Machines.AppliedCount));
		BotProbes.Add("mach.vend", () => BotProbeSet.Str(MachinesLab.Tag("v.vend", "VENDING_CASH")));
		BotProbes.Add("mach.rejects", () => BotProbeSet.Str(Machines.RejectCount));
		BotProbes.Add("mach.refused", () => BotProbeSet.Str(Machines.RefusedCount));
		BotProbes.Add("mach.mismatch", () => BotProbeSet.Str(Machines.MismatchCount));
		BotProbes.Add("mach.repairs", () => BotProbeSet.Str(Machines.RepairsSent));
		BotProbes.Add("mach.ticks", () => BotProbeSet.Str(Machines.TicksSent) + "/" + BotProbeSet.Str(Machines.TicksApplied));
		BotProbes.Add("mach.suppressed", () => BotProbeSet.Str(Machines.SuppressedTicks) + "/" + BotProbeSet.Str(Machines.SuppressedAuto));
	}
}
