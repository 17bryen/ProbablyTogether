using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PresenceUiProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("ui.chat.last", () => BotProbeSet.Str(Net.IsServer ? Chat.ServerSeq : Chat.LastSeen));
		BotProbes.Add("ui.view", () => (!Net.IsServer) ? Presence.LocalView : "");
		BotProbes.Add("ui.overlay", () => (!((Object)(object)Overlay.Root != (Object)null) || !((Component)Overlay.Root).gameObject.activeInHierarchy) ? "0" : "1");
	}
}
