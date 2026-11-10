using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsUseLate : BotScript
{
	private float lastAsk;

	private int askId = -1;

	public override string Name => "items-use-late";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 300f;

	protected override void Build()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "player " + Net.LocalPlayer);
		WaitFor("shop-loaded", 180f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene + ", live " + Net.IsLive);
		Check("replayed-quietly", () => BotProbes.Read("use.confirmui") == "0" && BotProbes.Read("use.scratchui") == "" && BotProbes.Read("use.perkui") == "0", () => ItemsUse.AppliedCount + " uses replayed from today's log; confirm panel " + BotProbes.Read("use.confirmui") + ", scratch panel '" + BotProbes.Read("use.scratchui") + "', perk window " + BotProbes.Read("use.perkui") + ", sounds blocked/stopped " + BotProbes.Read("use.quietsounds"));
		AskServer("ask-server");
		string value;
		Do("fixtures", delegate
		{
			ItemsUseLab.ParseFx(base.Server.TryGetValue("use.fx", out value) ? value : "");
			string value2;
			return ItemsUseLab.Fx.Count + " fixture ids; server applied " + (base.Server.TryGetValue("use.applied", out value2) ? value2 : "?") + " uses";
		});
		Expect("same-use-count", () => (!base.Server.TryGetValue("use.applied", out value)) ? "?" : value, () => BotScript.Str(ItemsUse.AppliedCount));
		Step("converged", 30f, delegate(BotStep st)
		{
			if (askId >= 0)
			{
				Dictionary<string, string> r = BotProbes.TakeReply(askId);
				if (r != null)
				{
					askId = -1;
					List<string> list = (from key in ItemsUseLab.StateKeys()
						where !r.TryGetValue(key, out value) || value != BotProbes.Read(key)
						select key + " here " + BotProbes.Read(key) + " server " + (r.TryGetValue(key, out value) ? value : "?")).ToList();
					st.Detail = ((list.Count == 0) ? ("all " + ItemsUseLab.StateKeys().Count() + " fixture values equal the server's after the day-log replay: " + BotProbes.Format(r, ItemsUseLab.StateKeys().ToArray())) : (list.Count + " differ: " + string.Join("; ", list.Take(8))));
					if (list.Count == 0)
					{
						return StepStatus.Pass;
					}
				}
			}
			if (askId < 0 && Time.realtimeSinceStartup - lastAsk > 3f)
			{
				lastAsk = Time.realtimeSinceStartup;
				askId = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		SameAsServer("same-uid", "use.uid");
		SameAsServer("same-digest-part", "use.part");
		Check("no-mismatch", () => ItemsUse.MismatchCount == 0, () => ItemsUse.MismatchCount + " replay mismatch(es) (uid counter and action digest checked after every replayed use)");
	}
}
