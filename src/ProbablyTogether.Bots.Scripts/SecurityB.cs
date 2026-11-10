using System;
using System.Collections.Generic;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class SecurityB : SecurityClientScript
{
	private int phase;

	public override string Name => "security-b";

	private void Compare(string tag, Action body, float timeout = 400f)
	{
		phase += 2;
		WaitPhase(tag + "-a-done", phase - 1, timeout);
		body?.Invoke();
		Cmd(tag + "-phase", "phase", "", phase);
	}

	private void AfterIdle(string tag, params string[] probes)
	{
		WaitIdle(tag + "-idle", 60f);
		Delay(tag + "-settle", 2f);
		SameAll(tag, probes);
		SameSoft(tag, "sec.items");
	}

	protected override void Build()
	{
		Joined();
		WaitServer("both-players", 300f, (IReadOnlyDictionary<string, string> v) => v.TryGetValue("players", out var value) && value == "2", "players");
		Compare("opened", delegate
		{
			Do("poster-before", () => "poster " + SecurityClientScript.Mine("sec.poster"));
			Do("poster-up", delegate
			{
				StorePoster.Instance.OnClick();
				return "clicked (request sent)";
			});
			WaitFor("poster-flipped", 10f, () => SecurityClientScript.Mine("sec.poster") == "1", () => "poster " + SecurityClientScript.Mine("sec.poster"));
			SameAll("poster-up", "sec.poster", "sec.fp");
			Do("poster-down", delegate
			{
				StorePoster.Instance.OnClick();
				return "clicked";
			});
			WaitFor("poster-back", 10f, () => SecurityClientScript.Mine("sec.poster") == "0", () => "poster " + SecurityClientScript.Mine("sec.poster"));
			SameAll("poster-down", "sec.poster", "sec.fp");
		});
		Compare("wrongful", delegate
		{
			AfterIdle("wrongful", "sec.wrongful", "cash", "sec.slips", "sec.rep", "sec.fp", "sec.part");
		});
		Compare("wanted-held", delegate
		{
			WaitFor("wanted-here", 30f, () => SecurityClientScript.Mine("sec.cust") == "wanted8", () => "cust " + SecurityClientScript.Mine("sec.cust"));
			Do("report-not-holder", delegate
			{
				Vars["r0"] = BotScript.Str(Security.RefusalCount);
				Vars["w1"] = SecurityClientScript.Mine("sec.wrongful");
				Net.ToServer("sec.report", new MsgSecAct
				{
					visit = Security.VisitKey()
				});
				return "sent sec.report while the counter is held by player " + SecurityClientScript.Mine("sec.lock");
			});
			WaitFor("refused", 10f, () => Security.RefusalCount > SecurityClientScript.N(Vars["r0"]), () => "refusal '" + Security.LastRefusal + "' (" + Security.RefusalCount + ")");
			Delay("refused-settle", 1.5f);
			Check("nothing-happened", () => SecurityClientScript.Mine("sec.arresting") == "0" && SecurityClientScript.Mine("sec.wrongful") == Vars["w1"], () => "arresting " + SecurityClientScript.Mine("sec.arresting") + ", wrongful " + SecurityClientScript.Mine("sec.wrongful"));
		});
		Compare("wanted", delegate
		{
			AfterIdle("wanted", "sec.wanted8", "cash", "sec.slips", "sec.rep", "sec.fp", "sec.part");
		});
		Compare("holster-aiming", delegate
		{
			WaitFor("sees-a-aiming", 10f, () => Security.AimingPlayer > 0 && Security.AimingPlayer != Net.LocalPlayer, () => "aiming player " + Security.AimingPlayer);
			Do("forged-holster", delegate
			{
				Vars["r1"] = BotScript.Str(Security.RefusalCount);
				Net.ToServer("sec.holster", new MsgSecAct
				{
					visit = Security.VisitKey(),
					id = Security.CustomerId()
				});
				return "sent sec.holster while player " + Security.AimingPlayer + " aims";
			});
			WaitFor("holster-refused", 10f, () => Security.RefusalCount > SecurityClientScript.N(Vars["r1"]), () => "refusal '" + Security.LastRefusal + "' (" + Security.RefusalCount + ")");
			Check("customer-stays", () => SecurityClientScript.Mine("sec.cust") == "scavGeneral", () => "cust " + SecurityClientScript.Mine("sec.cust"));
		});
		Compare("holster", delegate
		{
			WaitFor("aim-cleared", 10f, () => Security.AimingPlayer < 0, () => "aiming player " + Security.AimingPlayer);
			AfterIdle("holster", "sec.counter", "sec.lock", "sec.rep", "cash", "sec.fp", "sec.part");
		});
		Compare("aug-alive", delegate
		{
			AfterIdle("aug-alive", "sec.aug", "sec.fp", "sec.part", "cash");
		});
		Compare("cc", delegate
		{
			AfterIdle("cc", "sec.stolen", "sec.rep", "sec.inspLoss", "cash", "sec.fp", "sec.part");
		});
		Compare("insp", delegate
		{
			AfterIdle("insp", "sec.contraband", "sec.hidden", "sec.rep", "sec.inspLoss", "cash", "sec.fp", "sec.part");
		});
		Compare("bribe", delegate
		{
			AfterIdle("bribe", "cash", "sec.bribe", "sec.contraband", "sec.rep", "sec.fp", "sec.part");
		});
		Compare("fine", delegate
		{
			AfterIdle("fine", "cash", "sec.contraband", "sec.rep", "sec.inspLoss", "sec.fp", "sec.part");
		});
		Compare("eleanor", delegate
		{
			AfterIdle("eleanor", "sec.eleanor", "sec.crate", "cash", "sec.slips", "sec.rep", "sec.fp", "sec.part");
		});
		Compare("aug", delegate
		{
			AfterIdle("aug", "sec.aug", "cash", "sec.slips", "sec.rep", "sec.crimes", "sec.fp", "sec.part", "sec.unlocked");
		});
		Compare("manslaughter", delegate
		{
			AfterIdle("manslaughter", "sec.aug", "cash", "sec.slips", "sec.rep", "sec.crimes", "sec.fp", "sec.part");
			Do("resync", delegate
			{
				Vars["loads"] = SecurityClientScript.Mine("sec.loads");
				Snapshot.RequestResync();
				return "asked (loads so far " + Vars["loads"] + ")";
			});
			WaitFor("reloaded", 150f, () => SecurityClientScript.N("sec.loads") > SecurityClientScript.N(Vars["loads"]) && Net.IsLive && BotProbes.InShop, () => "loads " + SecurityClientScript.Mine("sec.loads") + ", state " + Net.ClientState);
			Delay("replay-settle", 8f);
			SameAll("replayed", "sec.wrongful", "sec.wanted8", "sec.contraband", "sec.hidden", "sec.inspLoss", "sec.poster", "sec.unlocked", "sec.counter", "sec.arresting", "sec.shot", "sec.shootout", "sec.fp", "sec.part", "cash", "sec.rep", "sec.slips", "sec.aug", "sec.crimes", "sec.bribe", "sec.eleanor", "sec.crate", "sec.stolen");
			SameSoft("replayed", "sec.items");
		}, 900f);
		Compare("end", delegate
		{
			WaitFor("end-seen", 30f, () => SecurityClientScript.Mine("sec.ended") == "1:arrest", () => "ended " + SecurityClientScript.Mine("sec.ended") + ", shown " + DayNightRun.LastShown);
			SameAll("end", "sec.ended");
		});
		Cmd("done", "done");
	}
}
