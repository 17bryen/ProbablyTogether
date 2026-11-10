using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public abstract class CustomersBot : BotScript
{
	protected Dictionary<string, string> Srv = new Dictionary<string, string>();

	protected const string Seller = "scavGeneral";

	protected static readonly string[] TalkProbes = new string[10] { "cust.current", "cust.visit", "cust.seq", "cust.dlgseq", "cust.dlgfp", "cust.dlgon", "cust.started", "cust.stock", "cust.digest", "cust.action" };

	protected static readonly string[] AfterProbes = new string[10] { "cust.current", "cust.last", "cust.holder", "cust.queue", "cust.stock", "cust.tool", "cash", "cust.dm", "cust.action", "cust.digest" };

	protected static string Mine(string probe)
	{
		return BotProbes.Read(probe);
	}

	protected string Theirs(string probe)
	{
		if (!Srv.TryGetValue(probe, out var value))
		{
			return "";
		}
		return value;
	}

	protected static int Int(string s)
	{
		if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return -9999;
		}
		return result;
	}

	private static string F(float s)
	{
		return s.ToString("F1", CultureInfo.InvariantCulture);
	}

	protected static string Show(IReadOnlyDictionary<string, string> v)
	{
		return BotProbes.Format(v, "cust.state", "cust.queue", "cust.head", "cust.current", "cust.visit", "cust.holder", "cust.arrived", "cust.dlgseq", "cust.dlgon", "cust.stock", "cash", "cust.refusals", "cust.digest");
	}

	protected static string ShowMine()
	{
		return Show(BotProbes.ReadAll());
	}

	internal static string StockText()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return "";
		}
		List<string> list = new List<string>();
		GameGridInventory[] array = (GameGridInventory[])(object)new GameGridInventory[2] { instance.frontInvinvElement, instance.backInvinvElement };
		foreach (GameGridInventory val in array)
		{
			if (((val != null) ? ((GameInventory)val).childItems : null) == null)
			{
				continue;
			}
			foreach (GameItem childItem in ((GameInventory)val).childItems)
			{
				if (childItem != null && !GeneralHelper.IsItemOwned(childItem))
				{
					list.Add(childItem.uniqueId.ToString(CultureInfo.InvariantCulture) + ":" + childItem.identifier);
				}
			}
		}
		list.Sort(StringComparer.Ordinal);
		if (list.Count != 0)
		{
			return string.Join("+", list);
		}
		return "none";
	}

	internal static GameItem CounterTool()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		object obj;
		if (instance == null)
		{
			obj = null;
		}
		else
		{
			GameGridInventory frontInvinvElement = instance.frontInvinvElement;
			obj = ((frontInvinvElement != null) ? ((GameInventory)frontInvinvElement).childItems : null);
		}
		if (obj == null)
		{
			return null;
		}
		foreach (GameItem childItem in ((GameInventory)instance.frontInvinvElement).childItems)
		{
			if (childItem != null && childItem.identifier == "gene_scanner" && GeneralHelper.IsItemOwned(childItem))
			{
				return childItem;
			}
		}
		return null;
	}

	internal static string ToolStateText()
	{
		GameItem val = CounterTool();
		if (val != null)
		{
			if (!val.IsTag("IS_ITEM_TOGGLE_ON_TAG"))
			{
				return "off";
			}
			return "on";
		}
		return "none";
	}

	protected static BotStep Unless(Func<bool> skip, string why, BotStep s)
	{
		Func<BotStep, StepStatus> poll = s.Poll;
		Action<BotStep> begin = s.Begin;
		bool? skipping = null;
		s.Begin = null;
		s.Poll = delegate(BotStep st)
		{
			if (!skipping.HasValue)
			{
				skipping = skip();
				if (skipping == false)
				{
					begin?.Invoke(st);
				}
			}
			if (skipping == true)
			{
				st.Detail = "skipped: " + why;
				return StepStatus.Pass;
			}
			return poll(st);
		};
		return s;
	}

	protected BotStep PollServer(string name, float timeout, Func<Dictionary<string, string>, bool> cond)
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
						st.Detail = "server " + Show(dictionary) + " after " + F(st.Elapsed) + " s";
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
		botStep.TimeoutDetail = () => "server never matched in " + F(timeout) + " s; last server " + Show(Srv) + "; marks " + Theirs("cust.mk") + "; here " + ShowMine();
		return botStep;
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
						string text3 = Theirs(text);
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

	protected bool Has(string mark)
	{
		return ("," + Theirs("cust.mk") + ",").Contains("," + mark + ",");
	}

	protected BotStep WaitMark(string name, float timeout, params string[] marks)
	{
		return PollServer(name, timeout, delegate(Dictionary<string, string> s)
		{
			string value;
			string mk = "," + (s.TryGetValue("cust.mk", out value) ? value : "") + ",";
			return marks.Any((string m) => mk.Contains("," + m + ","));
		});
	}

	protected BotStep Mark(string mark)
	{
		return Do("mark-" + mark.Replace(':', '-'), delegate
		{
			Customers.BotMark(mark);
			return "told the server " + mark;
		});
	}

	protected void Joined()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("shop-loaded", 180f, () => Net.IsLive && BotProbes.InShop, () => "scene " + BotProbes.Scene + ", state " + Net.ClientState);
	}

	protected BotStep ReportDone()
	{
		return Do("report-done", delegate
		{
			Customers.BotReportDone(Name, ok: true);
			return "told the server";
		});
	}
}
