using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class BotSelfTest : BotScript
{
	private int frames;

	private float t0;

	public override string Name => "bot-selftest";

	public override float TimeoutSeconds => 60f;

	protected override void Build()
	{
		Do("do", delegate
		{
			t0 = Time.realtimeSinceStartup;
			return "t0 " + t0.ToString("F1");
		});
		WaitFor("wait", 10f, () => Time.realtimeSinceStartup - t0 > 1.5f, () => "1.5 s passed");
		Check("check", () => true);
		Expect("expect", () => "a", () => "a");
		Step("custom", 10f, (BotStep st) => (++frames >= 3) ? StepStatus.Pass : StepStatus.Running).Detail = "3 frames";
		Delay("delay", 0.5f);
		Check("soft-fail-continues", () => true).Soft();
		Do("vars", delegate
		{
			Vars["k"] = "v";
			return "Vars[k]=" + Vars["k"];
		});
	}
}
