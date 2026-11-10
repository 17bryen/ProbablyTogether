using System;
using UnityEngine;

namespace ProbablyTogether.Bots;

public sealed class BotStep
{
	public string Name;

	public float Timeout;

	public string Detail;

	internal float StartedAt;

	internal bool ContinueOnFail;

	internal bool Quiet;

	internal Action<BotStep> Begin;

	internal Func<BotStep, StepStatus> Poll;

	internal Func<string> TimeoutDetail;

	public float Elapsed => Time.realtimeSinceStartup - StartedAt;

	public BotStep Soft()
	{
		ContinueOnFail = true;
		return this;
	}
}
