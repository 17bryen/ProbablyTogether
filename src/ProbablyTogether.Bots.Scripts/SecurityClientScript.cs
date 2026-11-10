using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public abstract class SecurityClientScript : BotScript
{
	protected static readonly string[] Record = new string[5] { "sec.fp", "sec.part", "cash", "sec.rep", "sec.slips" };

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 1300f;

	protected void Joined()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "player " + Net.LocalPlayer);
		WaitFor("live", 240f, () => Net.IsLive && BotProbes.InShop, () => "scene " + BotProbes.Scene + ", state " + Net.ClientState);
		Delay("settle", 3f);
	}

	protected BotStep Cmd(string name, string cmd, string arg = "", int n = 0)
	{
		return Do(name, delegate
		{
			Net.ToServer("bot.sec", new SecBotCmd
			{
				cmd = cmd,
				arg = arg,
				n = n
			});
			return "sent " + cmd + " " + arg + " " + n;
		});
	}

	protected BotStep WaitServer(string name, float timeout, Func<IReadOnlyDictionary<string, string>, bool> check, params string[] show)
	{
		int id = -1;
		float lastAsk = -10f;
		Dictionary<string, string> last = null;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (id >= 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					last = dictionary;
					id = -1;
					if (check(dictionary))
					{
						st.Detail = "server " + BotProbes.Format(dictionary, show) + " after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
						return StepStatus.Pass;
					}
				}
			}
			if (id < 0 && Time.realtimeSinceStartup - lastAsk > 1.5f)
			{
				lastAsk = Time.realtimeSinceStartup;
				id = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "server " + ((last == null) ? "never answered" : BotProbes.Format(last, show)) + " after " + timeout.ToString("F0", CultureInfo.InvariantCulture) + " s";
		return botStep;
	}

	protected static string Mine(string key)
	{
		return BotProbes.Read(key);
	}

	protected static int N(string keyOrNumber)
	{
		if (int.TryParse(keyOrNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		if (!int.TryParse(Mine(keyOrNumber), NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
		{
			return -9999;
		}
		return result;
	}

	protected BotStep WaitPhase(string name, int n, float timeout)
	{
		string value;
		int result;
		return WaitServer(name, timeout, (IReadOnlyDictionary<string, string> v) => v.TryGetValue("secbot.phase", out value) && int.TryParse(value, out result) && result >= n, "secbot.phase", "sec.lock", "sec.cust");
	}

	protected void SameAll(string prefix, params string[] probes)
	{
		AskServer(prefix + "-ask");
		foreach (string text in probes)
		{
			SameAsServer(prefix + "-" + text.Replace("sec.", ""), text);
		}
	}

	protected void SameSoft(string prefix, params string[] probes)
	{
		foreach (string text in probes)
		{
			SameAsServer(prefix + "-" + text.Replace("sec.", ""), text).Soft();
		}
	}

	protected BotStep WaitIdle(string name, float timeout)
	{
		return WaitFor(name, timeout, () => Mine("sec.visit") == "" && Mine("sec.officers") == "0" && Mine("sec.arresting") == "0" && Mine("sec.shot") == "0" && Mine("sec.shootout") == "0", () => "cust=" + Mine("sec.cust") + " officers=" + Mine("sec.officers") + " arresting=" + Mine("sec.arresting") + " shot=" + Mine("sec.shot") + " shootout=" + Mine("sec.shootout"));
	}
}
