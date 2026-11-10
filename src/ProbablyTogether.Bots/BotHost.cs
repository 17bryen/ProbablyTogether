using System;
using UnityEngine;

namespace ProbablyTogether.Bots;

internal sealed class BotHost : MonoBehaviour
{
	private void Update()
	{
		try
		{
			BotRunner.Tick();
		}
		catch (Exception ex)
		{
			BotLog.Fail("bot", "tick", ex.GetType().Name + ": " + ex.Message);
		}
	}
}
