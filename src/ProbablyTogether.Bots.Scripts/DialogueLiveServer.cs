using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DialogueLiveServer : BotScript
{
	public override string Name => "dialogue-live-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 300f;

	protected override void Build()
	{
		Do("wire", delegate
		{
			DialogueBotNet.Ensure();
			return "test graph registered, lab commands wired";
		});
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 240f, () => BotProbes.InShop && Game.ShopLoaded, () => "scene " + BotProbes.Scene);
		Delay("settle", 2f);
		Check("tutorial-off", () => !PlayerStore.Instance.tutorialEnable, () => "tutorialEnable " + PlayerStore.Instance.tutorialEnable);
		Check("dialogue-options", () => !EmporiumOption.Instance.enableTypewriterEffect && !EmporiumOption.Instance.autoNextDialog, () => "typewriter " + EmporiumOption.Instance.enableTypewriterEffect + " autoNext " + EmporiumOption.Instance.autoNextDialog);
		Check("analytics-off", () => !EmporiumAnalytics.isAnalyticsInitialized, () => "isAnalyticsInitialized " + EmporiumAnalytics.isAnalyticsInitialized);
		Check("system-hooks", () => SystemHooks.TickCount > 0, () => SystemHooks.TickCount + " tick hook(s); digest parts " + string.Join(",", Digest.Parts()?.Keys ?? new Dictionary<string, string>().Keys));
		Do("state", () => DialogueSystem.DigestPart() + " story " + DialogueEvents.StoryHash() + " events " + DialogueEvents.EventsHash() + " state " + Game.Store.storeState.ToString());
	}
}
