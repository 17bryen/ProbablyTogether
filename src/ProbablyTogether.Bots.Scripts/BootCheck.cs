using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class BootCheck : BotScript
{
	public override string Name => "boot";

	public override float TimeoutSeconds => 120f;

	protected override void Build()
	{
		WaitFor("menu", 60f, () => BotProbes.Scene == "EmporiumMenu" || BotProbes.Scene == "InventoryScene", () => "scene " + BotProbes.Scene);
		Check("mod", () => "0.2.8".Length > 0, () => "the upstream mod 0.2.8 protocol " + 2 + ", game " + Application.version);
		Do("probes", () => BotProbes.Format(BotProbes.ReadAll()));
		Check("frame-cap", () => true, () => "targetFrameRate " + Application.targetFrameRate);
	}
}
