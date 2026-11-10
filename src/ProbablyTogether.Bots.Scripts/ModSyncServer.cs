using System;
using System.Collections.Generic;
using System.Linq;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ModSyncServer : BotScript
{
	public override string Name => "modsync-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 400f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop, () => "scene " + BotProbes.Scene);
		Do("server-mods", delegate
		{
			List<ModList.Info> list = ModList.ServerSet(ProbablyTogether.Server.Config.WorkshopIdOf);
			return list.Count + " mod(s) besides ours: " + ModSyncBot.Describe(list) + "; modsMustMatch " + ProbablyTogether.Server.Config.modsMustMatch;
		});
		Check("named-mods-loaded", () => Unknown().Count == 0, delegate
		{
			List<ServerConfig.ServerMod> list = ProbablyTogether.Server.Config.mods ?? new List<ServerConfig.ServerMod>();
			List<string> list2 = Unknown();
			return "server.json names " + list.Count + " Workshop mod(s)" + ((list.Count > 0) ? (" (" + string.Join(", ", list.Select((ServerConfig.ServerMod m) => m.id + "=" + m.ws)) + ")") : "") + ((list2.Count > 0) ? ("; NOT loaded here: " + string.Join(", ", list2)) : "; every one is loaded");
		}).Soft();
		Do("probe", () => "mods.set " + ModSyncBot.Set());
	}

	private static List<string> Unknown()
	{
		HashSet<string> loaded = new HashSet<string>(ModList.Ids, StringComparer.Ordinal);
		return (from m in ProbablyTogether.Server.Config.mods ?? new List<ServerConfig.ServerMod>()
			where m != null && !loaded.Contains((m.id ?? "").Trim())
			select m.id).ToList();
	}
}
