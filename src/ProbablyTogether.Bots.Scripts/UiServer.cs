using System;
using System.Linq;
using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class UiServer : BotScript
{
	private int holder = -1;

	private float lastTouch;

	public override string Name => "ui-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 420f;

	private static int LiveCount()
	{
		return Net.Players.Keys.Count(Net.IsPlayerLive);
	}

	protected override void Build()
	{
		Do("plain-words", delegate
		{
			(string, string, string)[] array = new(string, string, string)[5]
			{
				("password", "Wrong password.", "Wrong password."),
				("full", "The server is full (2/2 players)", "The server is full (2/2 players)."),
				("password", "", "Wrong password."),
				("kicked", "", "Removed by the server owner."),
				("bad", "<color=red>flood</color> (over 60 frames/s)", "‹color=red>flood‹/color> (over 60 frames/s).")
			};
			(string, string, string)[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				(string, string, string) tuple = array2[i];
				string text = JoinMenu.Refusal(tuple.Item1, tuple.Item2);
				if (text != tuple.Item3)
				{
					throw new Exception("refusal " + tuple.Item1 + " '" + tuple.Item2 + "' -> '" + text + "', want '" + tuple.Item3 + "'");
				}
			}
			(string, string)[] array3 = new(string, string)[3]
			{
				("server stopping: stop file", "The server stopped. Try again in a minute."),
				("could not reach the server: no answer in 10 s", "Could not reach the server. Check the address and port (no answer in 10 s)."),
				("server silent for 30 s", "Lost the connection: the server stopped answering.")
			};
			(string, string)[] array4 = array3;
			for (int i = 0; i < array4.Length; i++)
			{
				(string, string) tuple2 = array4[i];
				string text2 = JoinMenu.Plain(tuple2.Item1);
				if (text2 != tuple2.Item2)
				{
					throw new Exception("'" + tuple2.Item1 + "' -> '" + text2 + "', want '" + tuple2.Item2 + "'");
				}
			}
			return array.Length + " refusals (from code + reason data, '<' escaped) and " + array3.Length + " disconnect reasons worded as expected";
		});
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop", 300f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene);
		Do("morning-line", delegate
		{
			Chat.Announce("Lab: morning line from the server");
			return "posted before anyone joined (day log)";
		});
		WaitFor("two-live", 200f, () => LiveCount() >= 2, () => LiveCount() + " live player(s)");
		Do("announce", delegate
		{
			Chat.Announce("Lab: the server says hello");
			return "system line #" + Chat.ServerSeq;
		});
		Do("lock-take", delegate
		{
			holder = Net.Players.Keys.Min();
			return "took 'bot.lock' for " + Net.PlayerName(holder) + ": " + Locks.TryTake("bot.lock", holder);
		});
		Step("lock-hold", 60f, delegate(BotStep st)
		{
			if (Time.realtimeSinceStartup - lastTouch > 5f)
			{
				lastTouch = Time.realtimeSinceStartup;
				Locks.Touch("bot.lock", holder);
			}
			if (st.Elapsed < 45f)
			{
				return StepStatus.Running;
			}
			st.Detail = "held 45 s, holder " + Locks.Holder("bot.lock");
			return (Locks.Holder("bot.lock") == holder) ? StepStatus.Pass : StepStatus.Fail;
		});
		Do("lock-release", delegate
		{
			Locks.Release("bot.lock", holder);
			return "released; holder now " + Locks.Holder("bot.lock");
		});
		Do("chat-total", () => "server posted " + Chat.ServerSeq + " chat line(s)");
	}
}
