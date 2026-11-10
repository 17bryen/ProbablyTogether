using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DialogueServer : BotScript
{
	public override string Name => "dialogue-server";

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
		Check("dialogue-options", () => !EmporiumOption.Instance.enableTypewriterEffect && !EmporiumOption.Instance.autoNextDialog, () => "typewriter " + EmporiumOption.Instance.enableTypewriterEffect + " autoNext " + EmporiumOption.Instance.autoNextDialog + " speed " + EmporiumOption.Instance.dialogSpeed);
		Do("achievement-blocked", delegate
		{
			if ((Object)(object)AchievementTracker.Instance == (Object)null)
			{
				return "no AchievementTracker on this copy (nothing could be written)";
			}
			int blockedAchievements = Suppress.BlockedAchievements;
			AchievementTracker.Instance.CheckTutorialComplete();
			if (Suppress.BlockedAchievements != blockedAchievements + 1)
			{
				throw new Exception("Unlock was not blocked (" + blockedAchievements + " -> " + Suppress.BlockedAchievements + ")");
			}
			return "ACH_TUTORIAL not written (blocked " + Suppress.BlockedAchievements + ")";
		});
		Check("analytics-off", () => !EmporiumAnalytics.isAnalyticsInitialized, () => "isAnalyticsInitialized " + EmporiumAnalytics.isAnalyticsInitialized);
		Do("state", () => DialogueSync.Digest() + " story " + DialogueEvents.StoryHash() + " events " + DialogueEvents.EventsHash());
	}
}
