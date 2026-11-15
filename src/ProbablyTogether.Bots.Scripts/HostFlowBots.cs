using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

/// Walks the host flow on the game's own screens headlessly. hf-new: the new shop has the picked trade, founder
/// perks and name; hf-again: the shop continued from the list is the same shop.
public abstract class HostFlowBotBase : DayNightBotBase
{
	protected static string State => "trade " + (Specs.Of(1) ?? "-") + "; shop's start type " + DayNightBotBase.Ps.startType + "; perks {" + Perks.Describe() + "}; shop's list [" + string.Join(",", (DayNightBotBase.Ps.startingPerks ?? new List<StartingPerk>()).Select((StartingPerk p) => p.id)) + "]; day " + DayNightBotBase.Day + ", cash " + DayNightBotBase.Ps.playerCash + "; shop '" + Net.ServerName + "'; name on the save '" + (SaveFiles.IndexLoad<SavePreviewData>("preview_" + DayNightBotBase.Ps.saveSlotId, null)?.customName ?? "") + "' (slot " + DayNightBotBase.Ps.saveSlotId + "); flow reached " + ProbablyTogether.UI.HostFlow.Reached;
}

public sealed class HostFlowNew : HostFlowBotBase
{
	public override string Name => "hf-new";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 240f;

	protected override void Build()
	{
		WaitFor("hosting", 200f, () => Net.IsHost && Net.IsLive && DayNightBotBase.Live && Members.LocalSeat == 1, () => "role " + Net.Role + ", live " + Net.IsLive + ", state " + Net.ClientState);
		WaitFor("the-gunsmith's-shop-from-the-trade-screen", 60f, () => Specs.Of(1) == "GUNSMITH" && DayNightBotBase.Ps.startType.ToString() == "GUNSMITH", () => State);
		WaitFor("the-founder-has-the-perks-from-the-perk-screen", 30f, () => Perks.Picked(1) && Perks.Has(1, "saving") && Perks.Has(1, "illiterate") && !Perks.Has(1, "alert"), () => State);
		Check("the-personal-one-is-not-in-the-shop's-list", () => DayNightBotBase.Ps.startingPerks.Any((StartingPerk p) => p.id == "saving") && !DayNightBotBase.Ps.startingPerks.Any((StartingPerk p) => p.id == "illiterate") && StartingPerk.IsIlliterate(), () => State);
		Check("the-shop's-name-is-on-its-save", () => (SaveFiles.IndexLoad<SavePreviewData>("preview_" + DayNightBotBase.Ps.saveSlotId, null)?.customName ?? "") == Net.ServerName && Net.ServerName.Length > 0, () => State);
		Check("the-flow-got-there-through-the-game's-screens", () => ProbablyTogether.UI.HostFlow.Reached == 1 && !ProbablyTogether.UI.HostFlow.Active, () => State);
	}
}

public sealed class HostFlowAgain : HostFlowBotBase
{
	public override string Name => "hf-again";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 240f;

	protected override void Build()
	{
		WaitFor("hosting", 200f, () => Net.IsHost && Net.IsLive && DayNightBotBase.Live && Members.LocalSeat == 1, () => "role " + Net.Role + ", live " + Net.IsLive + ", state " + Net.ClientState);
		WaitFor("the-same-shop", 60f, () => Specs.Of(1) == "GUNSMITH" && Perks.Picked(1) && Perks.Has(1, "saving") && Perks.Has(1, "illiterate"), () => State);
		Check("its-name-is-still-on-its-save", () => (SaveFiles.IndexLoad<SavePreviewData>("preview_" + DayNightBotBase.Ps.saveSlotId, null)?.customName ?? "") == Net.ServerName && Net.ServerName.Length > 0, () => State);
		Check("continued-from-the-list", () => ProbablyTogether.UI.HostFlow.Reached == 1, () => State);
	}
}
