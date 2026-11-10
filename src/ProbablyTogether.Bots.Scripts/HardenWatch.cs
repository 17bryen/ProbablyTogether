using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class HardenWatch : BotScript
{
	public override string Name => "harden-watch";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	protected override void Build()
	{
		int rounds = 60;
		if (int.TryParse(Args.Get("--br-harden-rounds") ?? "", out var result) && result > 0)
		{
			rounds = result;
		}
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("shop-loaded", 180f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene + ", state " + Net.ClientState);
		int done = 0;
		int slow = 0;
		int id = -1;
		float askedAt = 0f;
		float nextAt = 0f;
		float max = 0f;
		float sum = 0f;
		Step("watch", (float)rounds * 3f + 60f, delegate(BotStep st)
		{
			if (!Net.IsClient || Net.ClientState != ClientState.Ready)
			{
				st.Detail = "left the server after " + done + " rounds: " + Net.LastDisconnectReason;
				return StepStatus.Fail;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (id >= 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary == null)
				{
					if (realtimeSinceStartup - askedAt > 10f)
					{
						st.Detail = "no probe reply in 10 s (round " + (done + 1) + ")";
						return StepStatus.Fail;
					}
					return StepStatus.Running;
				}
				float num = realtimeSinceStartup - askedAt;
				id = -1;
				done++;
				sum += num;
				if (num > max)
				{
					max = num;
				}
				if (num > 2f)
				{
					slow++;
				}
				if (done % 10 == 0)
				{
					BotLog.Info(Name, "round " + done + "/" + rounds + ": rtt " + Ms(num) + ", max " + Ms(max) + ", players " + BotProbes.Format(dictionary, "players"));
				}
				nextAt = realtimeSinceStartup + 3f;
			}
			st.Detail = done + " rounds, rtt avg " + Ms((done > 0) ? (sum / (float)done) : 0f) + " max " + Ms(max) + ", " + slow + " over 2 s, still joined";
			if (done >= rounds)
			{
				if (slow != 0)
				{
					return StepStatus.Fail;
				}
				return StepStatus.Pass;
			}
			if (id < 0 && realtimeSinceStartup >= nextAt)
			{
				id = BotProbes.AskServer();
				askedAt = realtimeSinceStartup;
			}
			return StepStatus.Running;
		});
		AskServer("ask-server");
		SameAsServer("same-day", "day");
		SameAsServer("same-cash", "cash");
	}

	private static string Ms(float s)
	{
		return (s * 1000f).ToString("F0", CultureInfo.InvariantCulture) + " ms";
	}
}
