using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CoreOpsA : DayNightBotBase
{
	public override string Name => "coreops-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	private static bool SawLine(string text)
	{
		return Chat.Lines.Any((Chat.Line l) => l.System && l.Text.Contains(text));
	}

	private BotStep ServerReaches(string name, float timeout, Func<IReadOnlyDictionary<string, string>, bool> cond, params string[] show)
	{
		int id = 0;
		float askedAt = 0f;
		float next = 0f;
		Dictionary<string, string> last = null;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (id != 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = 0;
					last = dictionary;
					bool flag;
					try
					{
						flag = cond(dictionary);
					}
					catch
					{
						flag = false;
					}
					if (flag)
					{
						st.Detail = "server " + BotProbes.Format(dictionary, show) + " after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
						return StepStatus.Pass;
					}
					next = Time.realtimeSinceStartup + 1f;
				}
				else if (Time.realtimeSinceStartup - askedAt > 3f)
				{
					id = 0;
				}
			}
			if (id == 0 && Time.realtimeSinceStartup >= next && Net.IsLive)
			{
				id = BotProbes.AskServer();
				askedAt = Time.realtimeSinceStartup;
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "server never got there: " + ((last == null) ? "no answer" : BotProbes.Format(last, show));
		return botStep;
	}

	protected override void Build()
	{
		JoinSteps();
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return "day " + day0;
		});
		WaitFor("say-line", 120f, () => SawLine("Hello from the panel"), () => "chat: " + string.Join(" | ", Chat.Lines.Select((Chat.Line l) => (l.System ? "[sys] " : (l.Name + ": ")) + l.Text)));
		WaitFor("bom-line", 60f, () => SawLine("external line") && SawLine("second line"), () => "lines " + Chat.Lines.Count);
		WaitFor("b-kicked", 120f, () => Net.Players.Count == 1, () => "roster " + string.Join(",", Net.Players.Values.Select((PlayerInfo p) => p.Name)));
		Do("open", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "dayNight.open sent";
		});
		WaitFor("opened", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		WaitFor("shutter-settled", 20f, () => DayNightBotBase.Ps.lockShutterButtonFlag == 0, () => "flag " + DayNightBotBase.Ps.lockShutterButtonFlag);
		Delay("server-queues-save", 3f);
		Do("queue", delegate
		{
			if (DayNightBotBase.Ps.CanEndDay())
			{
				return "every waiting customer can be sent away";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "a story customer is waiting: lab op clearqueue";
		});
		WaitFor("closable", 20f, () => DayNightBotBase.Ps.CanEndDay(), () => "queue " + DayNightBotBase.Ps.storeClientManager.clientStack.Count);
		Do("close", delegate
		{
			StoreShutterButton.Instance.EndAndClose();
			return "sent";
		});
		WaitFor("closed", 40f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State + "; refused " + DayNight.LastRefusedWhat + ": " + DayNight.LastRefusedWhy);
		Ready("ready");
		WaitFor("night", 150f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State + ", live " + DayNightBotBase.Live);
		Agree("same-night", 30f, "day", "cash");
		Do("cue-night", () => (!Chat.Send("night agreed")) ? "chat not sent" : "said 'night agreed'");
		ServerReaches("server-newrun", 240f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "ops.runs") >= 1 && DayNightBotBase.V(v, "ops.state") == "running", "ops.runs", "ops.state", "day");
		WaitFor("live-newrun", 90f, () => DayNightBotBase.Live && DayNightBotBase.Day == 1, () => "day " + DayNightBotBase.Day + ", live " + DayNightBotBase.Live + ", client " + Net.ClientState);
		Agree("same-newrun", 40f, "day", "cash");
		Do("cue-newrun", () => (!Chat.Send("newrun agreed")) ? "chat not sent" : "said 'newrun agreed'");
		ServerReaches("server-newrun2", 240f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.I(v, "ops.runs") >= 2 && DayNightBotBase.V(v, "ops.state") == "running", "ops.runs", "ops.state", "day");
		WaitFor("live-newrun2", 90f, () => DayNightBotBase.Live && DayNightBotBase.Day == 1, () => "day " + DayNightBotBase.Day + ", live " + DayNightBotBase.Live + ", client " + Net.ClientState);
		Agree("same-newrun2", 40f, "day", "cash");
		Do("leave", delegate
		{
			Net.Disconnect();
			return "left the server";
		});
	}
}
