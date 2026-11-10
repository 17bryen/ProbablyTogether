namespace ProbablyTogether.Bots.Scripts;

public sealed class ProductionProbes : BotProbeSet
{
	public override void Register()
	{
		ProductionLab.Wire();
		BotProbes.Add("prod.ready", () => (!ProductionLab.Ready) ? "0" : "1");
		BotProbes.Add("prod.fx", ProductionLab.FxText);
		foreach (string item in ProductionLab.HashKeys())
		{
			string key = item;
			BotProbes.Add("prod.h." + key, () => ProductionLab.H(key));
		}
		string[] sets = ProductionLab.Sets;
		foreach (string text in sets)
		{
			string set = text;
			BotProbes.Add("prod." + set + ".kcal", () => BotProbeSet.Str(ProductionLab.Tag(set + ".feeder", "STORED_KCAL_TAG")));
			BotProbes.Add("prod." + set + ".target", () => BotProbeSet.Str(ProductionLab.Tag(set + ".feeder", "FEED_DISPENSER_TARGET_PERCENT_INT")));
			BotProbes.Add("prod." + set + ".auto", () => (!ProductionLab.TagB(set + ".feeder", "FEED_DISPENSER_AUTO_BLEND_BOOL")) ? "0" : "1");
			BotProbes.Add("prod." + set + ".harvest", () => BotProbeSet.Str(ProductionLab.Tag(set + ".butcher", "AUTO_BUTCHER_CAGE_HARVEST_INT")));
			BotProbes.Add("prod." + set + ".harvest2", () => BotProbeSet.Str(ProductionLab.Tag(set + ".butcher2", "AUTO_BUTCHER_CAGE_HARVEST_INT")));
			BotProbes.Add("prod." + set + ".count", () => BotProbeSet.Str(ProductionLab.Tag(set + ".butcher", "AUTO_BUTCHER_LAST_COUNT_INT")));
			BotProbes.Add("prod." + set + ".rats", () => BotProbeSet.Str(ProductionLab.LiveRats(set)));
			BotProbes.Add("prod." + set + ".tankharvest", () => BotProbeSet.Str(ProductionLab.Tag(set + ".rasbutcher", "AUTO_BUTCHER_TANK_HARVEST_INT")));
			BotProbes.Add("prod." + set + ".rascount", () => BotProbeSet.Str(ProductionLab.Tag(set + ".rasbutcher", "AUTO_BUTCHER_LAST_COUNT_INT")));
			BotProbes.Add("prod." + set + ".fish", () => BotProbeSet.Str(ProductionLab.LiveFish(set)));
			BotProbes.Add("prod.w." + set, () => ProductionLab.Water(set + ".water"));
			BotProbes.Add("prod.w2." + set, () => ProductionLab.Water(set + ".water2"));
			BotProbes.Add("prod.wl." + set, () => ProductionLab.WaterLabel(set + ".water"));
			BotProbes.Add("prod.wl2." + set, () => ProductionLab.WaterLabel(set + ".water2"));
			BotProbes.Add("prod.c." + set, () => ProductionLab.Chem(set + ".chem"));
			BotProbes.Add("prod.t." + set + ".water", () => Production.TagText(ProductionLab.Item(set + ".water")));
		}
		BotProbes.Add("prod.w.hw", () => ProductionLab.Water("hw"));
		BotProbes.Add("prod.vcash", () => BotProbeSet.Str(ProductionLab.Tag("vend", "VENDING_CASH")));
		BotProbes.Add("prod.fcash", () => BotProbeSet.Str(ProductionLab.Tag("fountain", "VENDING_CASH")));
		BotProbes.Add("prod.digest", Production.DigestPart);
		BotProbes.Add("prod.sent", () => BotProbeSet.Str(Production.SentCount));
		BotProbes.Add("prod.applied", () => BotProbeSet.Str(Production.AppliedCount));
		BotProbes.Add("prod.refused", () => BotProbeSet.Str(Production.RefusedCount));
		BotProbes.Add("prod.mismatch", () => BotProbeSet.Str(Production.MismatchCount));
		BotProbes.Add("prod.blockedauto", () => BotProbeSet.Str(Production.BlockedAuto));
		BotProbes.Add("prod.blockednight", () => BotProbeSet.Str(Production.BlockedNight));
		BotProbes.Add("prod.unseeded", () => BotProbeSet.Str(Net.IsServer ? Production.UnseededServerDay : Production.UnseededClient));
		BotProbes.Add("prod.serverorig", () => BotProbeSet.Str(Production.ServerOriginatedCount));
		BotProbes.Add("prod.unlock.m", () => BotProbeSet.Str(Production.UnlockMachineCount));
		BotProbes.Add("prod.unlock.f", () => BotProbeSet.Str(Production.UnlockFountainCount));
		BotProbes.Add("prod.lastdone", () => Production.LastDone);
		BotProbes.Add("prod.uid", () => (!PlayerStore.HasInstance) ? "" : BotProbeSet.Str(PlayerStore.Instance.currentUniqueId));
	}
}
