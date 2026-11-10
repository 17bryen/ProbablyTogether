using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public abstract class ProductionScript : BotScript
{
	protected IReadOnlyDictionary<string, string> Srv = new Dictionary<string, string>();

	protected BotStep UntilServer(string name, float timeout, Func<IReadOnlyDictionary<string, string>, bool> ok, Func<IReadOnlyDictionary<string, string>, string> detail)
	{
		int id = 0;
		float next = 0f;
		Dictionary<string, string> last = null;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (id == 0)
			{
				if (realtimeSinceStartup < next)
				{
					return StepStatus.Running;
				}
				id = BotProbes.AskServer();
				next = realtimeSinceStartup + 10f;
				return StepStatus.Running;
			}
			Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
			if (dictionary == null)
			{
				if (realtimeSinceStartup > next)
				{
					id = 0;
				}
				return StepStatus.Running;
			}
			id = 0;
			next = realtimeSinceStartup + 0.5f;
			last = dictionary;
			Srv = dictionary;
			if (!ok(dictionary))
			{
				return StepStatus.Running;
			}
			st.Detail = detail(dictionary) + "; after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
			return StepStatus.Pass;
		}, delegate
		{
			id = 0;
			next = 0f;
		});
		botStep.TimeoutDetail = () => (last != null) ? detail(last) : "no probe reply from the server";
		return botStep;
	}

	private static string V(IReadOnlyDictionary<string, string> r, string k)
	{
		if (r == null || !r.TryGetValue(k, out var value))
		{
			return "?";
		}
		return value;
	}

	protected BotStep SameSoon(string name, string probe, float timeout = 30f)
	{
		return SameSoon(name, () => probe, timeout);
	}

	protected BotStep SameSoon(string name, Func<string> probe, float timeout = 30f)
	{
		return UntilServer(name, timeout, delegate(IReadOnlyDictionary<string, string> r)
		{
			string text = probe();
			string text2 = BotProbes.Read(text);
			return text2 != "" && V(r, text) == text2;
		}, (IReadOnlyDictionary<string, string> r) => probe() + " here " + BotProbes.Read(probe()) + ", server " + V(r, probe()));
	}

	protected BotStep AllSame(string name, float timeout, Func<IEnumerable<string>> probes)
	{
		return UntilServer(name, timeout, (IReadOnlyDictionary<string, string> r) => probes().All(delegate(string p)
		{
			string text = BotProbes.Read(p);
			return text != "" && V(r, p) == text;
		}), delegate(IReadOnlyDictionary<string, string> r)
		{
			List<string> list = (from p in probes()
				where V(r, p) != BotProbes.Read(p)
				select p).ToList();
			return (list.Count != 0) ? (list.Count + " differ: " + string.Join(" ", from p in list.Take(4)
				select p + " here " + BotProbes.Read(p) + " server " + V(r, p))) : ("all " + probes().Count() + " equal the server's");
		});
	}

	protected static string Probe(string k)
	{
		return BotProbes.Read(k);
	}
}
