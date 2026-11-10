using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public abstract class NegotiationRealScript : BotScript
{
	protected Dictionary<string, string> Srv = new Dictionary<string, string>();

	protected static readonly string[] Shop = new string[9] { "cash", "favor", "nego.cust", "nego.counter", "nego.rep", "nego.budget", "nego.sold", "nego.hash", "nego.digest" };

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 1500f;

	protected static string Mine(string p)
	{
		return BotProbes.Read(p);
	}

	protected static int Int(string s)
	{
		if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return -9999;
		}
		return result;
	}

	protected static string F(float s)
	{
		return s.ToString("F1", CultureInfo.InvariantCulture);
	}

	protected static string Show()
	{
		return BotProbes.Format(BotProbes.ReadAll(), "nego.cust", "nego.arrived", "cash", "nego.counter", "nego.dlg", "nego.tablet", "nego.gate");
	}

	protected void Joined()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "player " + Net.LocalPlayer);
		WaitFor("shop-loaded", 180f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene);
		Do("options", delegate
		{
			EmporiumOption instance = EmporiumOption.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				return "no options object";
			}
			instance.autoNextDialog = false;
			instance.autoCallNext = false;
			return "autoNextDialog off, autoCallNext off (the bot paces), AutoOpenNegotiationUI " + EmporiumOption.AutoOpenNegotiationUI;
		});
		Delay("settle", 1f);
	}

	protected void Mark(string mark)
	{
		Do("mark-" + mark, delegate
		{
			Net.ToServer("bot.nego.mark", new BotNegoMark
			{
				mark = mark
			});
			return mark;
		});
	}

	protected BotStep PollServer(string name, float timeout, Func<Dictionary<string, string>, bool> cond, Func<string> detail = null)
	{
		int id = 0;
		float lastAsk = -10f;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (id != 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = 0;
					Srv = dictionary;
					if (cond(dictionary))
					{
						st.Detail = ((detail != null) ? detail() : "") + " after " + F(st.Elapsed) + " s";
						return StepStatus.Pass;
					}
				}
				else if (Time.realtimeSinceStartup - lastAsk > 10f)
				{
					id = 0;
				}
			}
			if (id == 0 && Time.realtimeSinceStartup - lastAsk > 0.7f)
			{
				lastAsk = Time.realtimeSinceStartup;
				id = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "server never matched; here " + Show();
		return botStep;
	}

	protected BotStep WaitMark(string mark, float timeout)
	{
		string value;
		return PollServer("wait-" + mark, timeout, (Dictionary<string, string> r) => r.TryGetValue("nego.marks", out value) && ("," + value + ",").Contains("," + mark + ","));
	}

	protected BotStep Converge(string name, float timeout, params string[] probes)
	{
		int id = 0;
		float lastAsk = -10f;
		string diff = "";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (id != 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = 0;
					Srv = dictionary;
					List<string> list = new List<string>();
					string[] array = probes;
					foreach (string text in array)
					{
						string text2 = Mine(text);
						string value;
						string text3 = (dictionary.TryGetValue(text, out value) ? value : "");
						if (text2 != text3 || text2 == "")
						{
							list.Add(text + " here " + text2 + " server " + text3);
						}
					}
					diff = string.Join("; ", list);
					if (list.Count == 0)
					{
						st.Detail = string.Join(" ", probes.Select((string p) => p + "=" + Mine(p))) + " (same as the server) after " + F(st.Elapsed) + " s";
						return StepStatus.Pass;
					}
				}
				else if (Time.realtimeSinceStartup - lastAsk > 10f)
				{
					id = 0;
				}
			}
			if (id == 0 && Time.realtimeSinceStartup - lastAsk > 0.7f)
			{
				lastAsk = Time.realtimeSinceStartup;
				id = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "still different: " + diff;
		return botStep;
	}
}
