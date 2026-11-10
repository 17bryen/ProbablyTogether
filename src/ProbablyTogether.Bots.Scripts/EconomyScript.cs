using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public abstract class EconomyScript : BotScript
{
	protected int mark;

	protected void Joined(bool fixturesFresh = true)
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "player " + Net.LocalPlayer);
		WaitFor("shop-loaded", 180f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene);
		if (fixturesFresh)
		{
			WaitFor("fixtures", 120f, () => Game.Store.playerCash == 5000 && Game.Store.commissaryData.redeemedCredit == 100 && EconomyLab.Req() != null && EconomyLab.Owned("food_stamp") == 2, () => Economy.Describe() + ", stamps " + EconomyLab.Owned("food_stamp"));
		}
		else
		{
			WaitFor("economy-loaded", 120f, () => Game.Store.commissaryData != null && Game.Store.commissaryData.redeemedCredit > 0, () => Economy.Describe());
		}
		Delay("settle", 1.5f);
	}

	protected void Same(string tag, params string[] probes)
	{
		AskServer("ask-" + tag);
		foreach (string text in probes)
		{
			SameAsServer("same-" + tag + "-" + text.Replace("econ.", ""), text);
		}
	}

	protected BotStep Act(string name, Func<string> action, Func<bool> done, float timeout = 20f, Func<string> detail = null)
	{
		Do(name, delegate
		{
			mark = Economy.Applied + Economy.Refusals;
			return action();
		});
		return WaitFor(name + "-applied", timeout, done, detail ?? ((Func<string>)(() => Economy.Describe() + "; last refusal '" + Economy.LastRefusal + "'")));
	}

	protected bool Answered()
	{
		return Economy.Applied + Economy.Refusals > mark;
	}

	protected void MarkServer(string name)
	{
		Do("mark-" + name, delegate
		{
			EconomyLab.Op("mark", name);
			return "told the server " + name;
		});
	}

	private static string F(float s)
	{
		return s.ToString("F1", CultureInfo.InvariantCulture);
	}

	protected BotStep Agree(string name, float timeout, params string[] probes)
	{
		int id = 0;
		float lastAsk = -10f;
		string diff = "no answer yet";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (id != 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = 0;
					List<string> list = new List<string>();
					string[] array = probes;
					foreach (string text in array)
					{
						string text2 = BotProbes.Read(text);
						if (!dictionary.TryGetValue(text, out var value) || text2 != value)
						{
							list.Add(text + " here '" + text2 + "' server '" + value + "'");
						}
					}
					diff = string.Join("; ", list);
					if (list.Count == 0)
					{
						st.Detail = string.Join(" ", probes.Select((string p) => p + "=" + BotProbes.Read(p))) + " (same as the server) after " + F(st.Elapsed) + " s";
						return StepStatus.Pass;
					}
				}
				else if (Time.realtimeSinceStartup - lastAsk > 10f)
				{
					id = 0;
				}
			}
			if (id == 0 && Time.realtimeSinceStartup - lastAsk > 1f)
			{
				lastAsk = Time.realtimeSinceStartup;
				id = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "still different after " + F(timeout) + " s: " + diff;
		return botStep;
	}

	protected BotStep WaitMark(string name, string markName, float timeout)
	{
		int id = 0;
		float lastAsk = -10f;
		string last = "";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (id != 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = 0;
					dictionary.TryGetValue("econ.marks", out last);
					if (("," + last + ",").Contains("," + markName + ","))
					{
						st.Detail = "mark " + markName + " set after " + F(st.Elapsed) + " s";
						return StepStatus.Pass;
					}
				}
				else if (Time.realtimeSinceStartup - lastAsk > 10f)
				{
					id = 0;
				}
			}
			if (id == 0 && Time.realtimeSinceStartup - lastAsk > 1f)
			{
				lastAsk = Time.realtimeSinceStartup;
				id = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "mark " + markName + " never set; marks '" + last + "'";
		return botStep;
	}

	protected BotStep ServerSays(string name, float timeout, string probe, string expected)
	{
		int id = 0;
		float lastAsk = -10f;
		string got = "no answer yet";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (id != 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = 0;
					dictionary.TryGetValue(probe, out got);
					if (got == expected)
					{
						st.Detail = "server " + probe + " = " + got + " after " + F(st.Elapsed) + " s";
						return StepStatus.Pass;
					}
				}
				else if (Time.realtimeSinceStartup - lastAsk > 10f)
				{
					id = 0;
				}
			}
			if (id == 0 && Time.realtimeSinceStartup - lastAsk > 1f)
			{
				lastAsk = Time.realtimeSinceStartup;
				id = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "server " + probe + " = " + got + ", wanted " + expected;
		return botStep;
	}

	protected static string Dlg()
	{
		return "line " + DialogueSync.CurrentFp("counter") + " seq " + DialogueSync.Seq("counter") + ", unfinished " + DialogueSystem.Counter.LineUnfinished + ", at the counter " + EconomyLab.CounterId() + " (holder " + Locks.HeldBy("counter") + "), network window " + WildNetworkUI.IsOpen + ", " + Economy.Describe();
	}
}
