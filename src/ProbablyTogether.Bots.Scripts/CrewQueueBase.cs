using System;
using System.Collections.Generic;

namespace ProbablyTogether.Bots.Scripts;

public abstract class CrewQueueBase : FullDayBot
{
	protected static readonly string[] QueueParts = new string[3] { "fd.queue", "fd.cust", "fd.core" };

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	protected new static string Expect => (Args.Get("--br-crewq-expect") ?? (((Args.Get("--br-crew-anyday") ?? "") == "1") ? "extra" : "none")).Trim();

	protected BotStep CheckDecision(string name)
	{
		return WaitServer(name, 30f, delegate(IReadOnlyDictionary<string, string> d)
		{
			string text = FullDayBot.V(d, "crewq.decision");
			if (text.Length == 0)
			{
				return false;
			}
			if (!(Expect == "none"))
			{
				if (text.StartsWith("+", StringComparison.Ordinal))
				{
					return FullDayBot.I(d, "crewq.today") > 0;
				}
				return false;
			}
			return text.StartsWith("none", StringComparison.Ordinal) && FullDayBot.I(d, "crewq.today") == 0;
		}, "crewq.decision", "crewq.today", "fd.queue");
	}
}
