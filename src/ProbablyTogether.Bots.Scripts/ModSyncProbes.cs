namespace ProbablyTogether.Bots.Scripts;

public sealed class ModSyncProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("mods.set", () => ModSyncBot.Set());
		BotProbes.Add("mods.count", () => BotProbeSet.Str(ModSyncBot.Mine().Count));
	}
}
