using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsUseServer : BotScript
{
	private int panelsSeen;

	public override string Name => "items-use-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 420f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene + ", live " + Net.IsLive);
		Do("spawn", delegate
		{
			string text = ItemsUseLab.SpawnAll();
			if (!Snapshot.NewMorning("lab fixtures (items-use bot)"))
			{
				throw new InvalidOperationException("the fresh morning snapshot failed");
			}
			if (Net.Players.Count > 0)
			{
				Snapshot.SendToAll();
			}
			return text + "; " + ItemsUseLab.FxText();
		});
		WaitFor("player", 170f, () => Net.Players.Count >= 1, () => Net.Players.Count + " player(s)");
		Delay("clients-load", 25f);
		Do("server-use", delegate
		{
			Vars["slots"] = ItemsUseLab.Slots("s").ToString(CultureInfo.InvariantCulture);
			GameItem val = ItemsUseLab.Item("s.scrap1");
			GameItem val2 = ItemsUseLab.Item("s.toolbox");
			if (!val.MayTarget(val2) || !val.CanTarget(val2))
			{
				throw new InvalidOperationException("the game says scrap cannot upgrade this toolbox (" + ItemsUseLab.Slots("s") + " slots)");
			}
			val.Target(val2);
			return "scrap on toolbox by the server; applied count " + ItemsUse.AppliedCount;
		});
		Expect<string>("server-use-applied", () => int.Parse(Vars["slots"], CultureInfo.InvariantCulture) + 1 + " slots, scrap gone", () => ItemsUseLab.Slots("s") + " slots, scrap " + (ItemsUseLab.Alive("s.scrap1") ? "still there" : "gone"));
		Check("neighbours-registered", () => ItemsUse.Registered().Contains("machines"), () => ItemsUse.Registered());
		Check("register-idempotent", delegate
		{
			ItemsUse.RegisterValidator("items-use-lab", (UseContext c) => (string)null);
			ItemsUse.RegisterValidator("items-use-lab", (UseContext c) => (string)null);
			ItemsUse.RegisterReplayMode("items-use-lab", (UseContext c) => (UseReplay?)null);
			string text = ItemsUse.Registered();
			int num = text.Split(new string[1] { "items-use-lab" }, StringSplitOptions.None).Length - 1;
			Vars["reg"] = text;
			bool flag = ItemsUse.Unregister("items-use-lab") && !ItemsUse.Registered().Contains("items-use-lab");
			Dictionary<string, string> vars = Vars;
			vars["reg"] = vars["reg"] + " -> after Unregister: " + ItemsUse.Registered();
			return num == 2 && flag;
		}, () => (!Vars.TryGetValue("reg", out var value)) ? "" : value);
		Do("ready", delegate
		{
			ItemsUseLab.Ready = true;
			return "fixtures ready for the clients";
		});
		Step("clients-work", 170f, delegate(BotStep st)
		{
			if (Probe("use.confirmui") == "1" || Probe("use.scratchui") != "" || Probe("use.perkui") == "1")
			{
				panelsSeen++;
			}
			int count = Net.Players.Count;
			int num = 1 + 16 * Math.Max(1, count);
			st.Detail = ItemsUse.AppliedCount + " of " + num + " uses applied, " + count + " player(s), refused requests logged above";
			return (ItemsUse.AppliedCount >= num) ? StepStatus.Pass : StepStatus.Running;
		}).Soft();
		Check("server-stayed-quiet", () => panelsSeen == 0 && Probe("use.confirmui") == "0" && Probe("use.scratchui") == "" && Probe("use.perkui") == "0", () => "panel frames seen " + panelsSeen + ", confirm panel " + Probe("use.confirmui") + ", scratch panel '" + Probe("use.scratchui") + "', perk window " + Probe("use.perkui"));
		Check("server-sounds-quiet", () => ItemsUse.QuietSoundsBlocked > 0, () => "quiet sounds blocked/stopped " + Probe("use.quietsounds") + " over " + ItemsUse.AppliedCount + " uses");
		Do("final", () => BotProbes.Format(BotProbes.ReadAll(), ItemsUseLab.StateKeys().Concat(new string[2] { "use.uid", "use.applied" }).ToArray()));
	}

	private static string Probe(string k)
	{
		return BotProbes.Read(k);
	}
}
