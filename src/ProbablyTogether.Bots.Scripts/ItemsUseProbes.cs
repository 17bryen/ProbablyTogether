using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsUseProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("use.ready", () => (!ItemsUseLab.Ready) ? "0" : "1");
		BotProbes.Add("use.fx", ItemsUseLab.FxText);
		string[] sets = ItemsUseLab.Sets;
		foreach (string text in sets)
		{
			string set = text;
			BotProbes.Add("use." + set + ".slots", () => BotProbeSet.Str(ItemsUseLab.Slots(set)));
			BotProbes.Add("use." + set + ".scrap", () => BotProbeSet.Str(ItemsUseLab.ScrapLeft(set)));
			BotProbes.Add("use." + set + ".tabs", () => BotProbeSet.Str(ItemsUseLab.Tabs(set)));
			BotProbes.Add("use." + set + ".names", () => ItemsUseLab.TabName(set, 0) + "|" + ItemsUseLab.TabName(set, 1) + "|" + ItemsUseLab.TabName(set, 2));
			BotProbes.Add("use." + set + ".kit", () => (!ItemsUseLab.Alive(set + ".kit")) ? "0" : "1");
			BotProbes.Add("use." + set + ".scanner", () => (!ItemsUseLab.ScannerOn(set)) ? "0" : "1");
			BotProbes.Add("use." + set + ".chem", () => (!ItemsUseLab.Alive(set + ".chem")) ? "0" : "1");
			BotProbes.Add("use." + set + ".ticket", () => ItemsUseLab.Ticket(set));
			BotProbes.Add("use." + set + ".tool", () => ItemsUseLab.Tool(set));
			BotProbes.Add("use." + set + ".charge", () => BotProbeSet.Str(ItemsUseLab.Charge(set)));
			BotProbes.Add("use." + set + ".cooldown", () => BotProbeSet.Str(ItemsUseLab.Cooldown(set)));
			BotProbes.Add("use." + set + ".elec", () => (!ItemsUseLab.Alive(set + ".elec")) ? "0" : "1");
		}
		BotProbes.Add("use.perks", ItemsUseLab.Perks);
		BotProbes.Add("use.part", ItemsUse.DigestPart);
		BotProbes.Add("use.perkui", () => (!ItemsUseLab.PerkWindowOpen()) ? "0" : "1");
		BotProbes.Add("use.quietsounds", () => ItemsUse.QuietSoundsBlocked + "/" + ItemsUse.QuietSoundsStopped);
		BotProbes.Add("use.registered", ItemsUse.Registered);
		BotProbes.Add("use.uid", () => (!PlayerStore.HasInstance) ? "" : BotProbeSet.Str(PlayerStore.Instance.currentUniqueId));
		BotProbes.Add("use.applied", () => BotProbeSet.Str(ItemsUse.AppliedCount));
		BotProbes.Add("use.refused", () => BotProbeSet.Str(ItemsUse.RefusedCount));
		BotProbes.Add("use.mismatch", () => BotProbeSet.Str(ItemsUse.MismatchCount));
		BotProbes.Add("use.confirmui", delegate
		{
			GenericConfirmationUIManager instance = GenericConfirmationUIManager.Instance;
			return (!((Object)(object)instance != (Object)null) || !((Object)(object)instance.panel != (Object)null) || !instance.panel.activeSelf) ? "0" : "1";
		});
		BotProbes.Add("use.scratchui", delegate
		{
			ScratcherUIManager instance = ScratcherUIManager.Instance;
			return (!((Object)(object)instance != (Object)null) || instance.currentActiveScratcherItem == null) ? "" : BotProbeSet.Str(Ids.Of(instance.currentActiveScratcherItem));
		});
	}
}
