using System;
using System.Runtime.CompilerServices;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public abstract class PicsBase : UiCoopBase
{
	protected const string LeadChat = "Shutter goes up soon. I've got the phone.";

	protected const string WatchChat = "On it. I'll watch the back room.";

	protected const float Hold = 9f;

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	protected static PlayerStore Ps => Game.Store;

	protected static string State
	{
		get
		{
			if (!Game.ShopLoaded)
			{
				return "";
			}
			return Ps.storeState.ToString();
		}
	}

	protected void Join()
	{
		Do("wire", delegate
		{
			DialogueBotNet.Ensure();
			return "dialogue lab channel wired";
		});
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("shop-live", 240f, () => Ui.ShopOnline && DialogueSystem.Phone.Ready, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		WaitFor("two-players", 400f, () => Net.Players.Count == 2 && Presence.RosterTexts.Count == 2 && Net.IsPlayerLive(UiCoopBase.Other()), () => Net.Players.Count + " player(s)");
		Delay("settle", 3f);
	}

	protected void Pic(string name, Func<string> what)
	{
		Do(name, () => "PIC " + name + ": " + what());
		Delay(name + "-hold", 9f);
	}
}
