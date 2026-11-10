using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public abstract class UiCoopBase : BotScript
{
	protected static int leaderId = -1;

	protected static bool IAmLeader => Net.LocalPlayer == Leader();

	protected static int Leader()
	{
		if (leaderId <= 0)
		{
			if (Net.Players.Count != 0)
			{
				return Net.Players.Keys.Min();
			}
			return -1;
		}
		return leaderId;
	}

	protected static int Other()
	{
		foreach (int key in Net.Players.Keys)
		{
			if (key != Net.LocalPlayer)
			{
				return key;
			}
		}
		return -1;
	}

	protected static string F(float s)
	{
		return s.ToString("F1", CultureInfo.InvariantCulture);
	}

	protected void Command(string name, string cmd, Func<string, bool> ok, float timeout = 15f)
	{
		int id = 0;
		Step(name, timeout, delegate(BotStep st)
		{
			string text = DialogueBotNet.Take(id);
			if (text == null)
			{
				return StepStatus.Running;
			}
			st.Detail = cmd + " -> " + text;
			return ok(text) ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			id = DialogueBotNet.Send(cmd);
		}).TimeoutDetail = () => "no answer to '" + cmd + "'";
	}

	protected void Mark(string name, string mark)
	{
		Command(name, "mark:" + mark, (string r) => r == "ok");
	}

	protected void WaitMark(string name, string mark, float timeout)
	{
		int id = -1;
		float last = -10f;
		Step(name, timeout, delegate(BotStep st)
		{
			if (id >= 0)
			{
				string text = DialogueBotNet.Take(id);
				if (text == null)
				{
					return StepStatus.Running;
				}
				id = -1;
				if (text == "yes")
				{
					st.Detail = "mark " + mark + " set (" + F(st.Elapsed) + " s)";
					return StepStatus.Pass;
				}
			}
			if (st.Elapsed - last >= 1f)
			{
				last = st.Elapsed;
				id = DialogueBotNet.Send("has:" + mark);
			}
			return StepStatus.Running;
		}).TimeoutDetail = () => "mark " + mark + " never set";
	}

	protected BotStep S(string name, float timeout, Func<BotStep, StepStatus> poll)
	{
		BotStep step = null;
		step = Step(name, timeout, poll);
		step.TimeoutDetail = () => "timed out; " + step.Detail;
		return step;
	}

	protected static string Row(int id)
	{
		if (!Presence.RosterTexts.TryGetValue(id, out var value))
		{
			return "(no row)";
		}
		return value;
	}

	protected static string Labels()
	{
		if (Presence.LockLabelTexts.Count != 0)
		{
			return string.Join(" / ", Presence.LockLabelTexts.Select((KeyValuePair<string, string> kv) => kv.Key + ": " + kv.Value));
		}
		return "(no labels)";
	}
}
