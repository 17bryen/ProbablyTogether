using System;
using System.Collections.Generic;
using System.Globalization;
using ProbablyTogether.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public static class DialogueBotNet
{
	private static bool wired;

	private static int nextId;

	private static readonly Dictionary<int, string> replies = new Dictionary<int, string>();

	public static int BtValue = -1;

	public static int BtEnds;

	public static readonly List<string> RefusedEvents = new List<string>();

	public static int Offers;

	public static int BfEnds;

	private static readonly HashSet<string> marks = new HashSet<string>(StringComparer.Ordinal);

	public static void Ensure()
	{
		DialogueSync.RegisterRoot("bt", BuildTestGraph);
		DialogueSync.RegisterRoot("bf", BuildFlushGraph);
		if (wired)
		{
			return;
		}
		wired = true;
		DialogueSync.Refused += delegate(string ch, string what, string why)
		{
			lock (RefusedEvents)
			{
				RefusedEvents.Add(ch + "|" + what + "|" + why);
			}
		};
		Net.On("dialogue.bot", delegate(DialogueBotCmd m, int from)
		{
			Net.ToPlayer(from, "dialogue.bot.r", new DialogueBotReply
			{
				id = m.id,
				text = Run(m.cmd)
			});
		}, null);
		Net.On("dialogue.bot.r", null, delegate(DialogueBotReply m)
		{
			lock (replies)
			{
				replies[m.id] = m.text ?? "";
			}
		});
		Net.On("dialogue.bot.flush", null, delegate(DialogueBotFlush m)
		{
			using (Replay.Client(m.seed))
			{
				DialogUIManager instance = DialogUIManager.Instance;
				if (instance != null)
				{
					instance.FlushScheduledEndActions();
				}
			}
		});
		Net.On<DialogueBotOffer>("dialogue.bot.offer", null, delegate
		{
			Offers++;
			try
			{
				DialogUIManager instance = DialogUIManager.Instance;
				if (instance != null)
				{
					instance.StartDialogue(BuildTestGraph(), false);
				}
			}
			catch (Exception ex)
			{
				Log.Warn("dialogue bot: offer start threw " + ex.Message);
			}
		});
	}

	public static Dialogue BuildFlushGraph()
	{
		Dialogue obj = new Dialogue().SetText("Bot", "flush me").SetEndAction((Action)delegate
		{
			if (PlayerStore.HasInstance)
			{
				PlayerStore instance = PlayerStore.Instance;
				instance.playerCash += 7;
			}
			BfEnds++;
		});
		obj.endActionDelay = 25f;
		return obj;
	}

	public static int Send(string cmd)
	{
		int num;
		lock (replies)
		{
			num = ++nextId;
		}
		Net.ToServer("dialogue.bot", new DialogueBotCmd
		{
			id = num,
			cmd = cmd
		});
		return num;
	}

	public static string Take(int id)
	{
		lock (replies)
		{
			if (!replies.TryGetValue(id, out var value))
			{
				return null;
			}
			replies.Remove(id);
			return value;
		}
	}

	public static Dialogue BuildTestGraph()
	{
		Dialogue obj = new Dialogue().SetText("Bot", "line one").SetStartAction((Action)delegate
		{
			SetMonologue(on: true);
			BtValue = RNG.GetRandomInt(1, 1000000);
		});
		Dialogue obj2 = obj.NextDialogue().SetText("Bot", "line two: pick one");
		Dialogue val = new Dialogue().SetText("PLAYER", "A");
		val.NextDialogue().SetText("Bot", "picked A").SetEndAction((Action)delegate
		{
			if (PlayerStore.HasInstance)
			{
				PlayerStore instance = PlayerStore.Instance;
				instance.playerCash += 5;
			}
			BtEnds++;
			SetMonologue(on: false);
		});
		Dialogue val2 = new Dialogue().SetText("PLAYER", "B");
		val2.NextDialogue().SetText("Bot", "picked B").SetEndAction((Action)delegate
		{
			BtEnds++;
			SetMonologue(on: false);
		});
		obj2.choices.Add((null, val, delegate
		{
			if (PlayerStore.HasInstance)
			{
				PlayerStore instance = PlayerStore.Instance;
				instance.playerCash += 3;
			}
		}));
		obj2.choices.Add((null, val2, null));
		return obj;
	}

	private static void SetMonologue(bool on)
	{
		if ((Object)(object)DialogUIManager.Instance != (Object)null)
		{
			DialogUIManager.Instance.isSecMonologuePlaying = on;
		}
	}

	private static string Run(string cmd)
	{
		try
		{
			if (!Net.IsServer)
			{
				return "not the server";
			}
			if (cmd == null)
			{
				return "no command";
			}
			if (cmd.StartsWith("phone-open:", StringComparison.Ordinal))
			{
				long num = long.Parse(cmd.Substring(11), CultureInfo.InvariantCulture);
				if (!StorePhoneClient.IsNumberExist(num))
				{
					return "no number " + num;
				}
				PlayerStore.Instance.PhoneClientDict[num].phoneState = (StorePhoneClient.PhoneState)4;
				return "ok " + num + " answers=" + PhoneUIManager.WillAnswerCall(num);
			}
			if (cmd.StartsWith("mark:", StringComparison.Ordinal))
			{
				marks.Add(cmd.Substring(5));
				return "ok";
			}
			if (cmd.StartsWith("has:", StringComparison.Ordinal))
			{
				return marks.Contains(cmd.Substring(4)) ? "yes" : "no";
			}
			switch (cmd)
			{
			case "start-bt":
			{
				Dialogue val = DialogueSync.BuildRoot("bt");
				return ((Object)(object)DialogUIManager.Instance != (Object)null && DialogUIManager.Instance.StartDialogue(val, false)) ? ("ok started seq " + DialogueSync.Seq("counter")) : "refused (busy?)";
			}
			case "offer-bt":
				if ((Object)(object)DialogUIManager.Instance == (Object)null || DialogUIManager.Instance.currentDialog != null)
				{
					return "refused (the counter is talking)";
				}
				Net.ToAll("dialogue.bot.offer", new DialogueBotOffer
				{
					n = 1
				});
				return DialogUIManager.Instance.StartDialogue(BuildTestGraph(), false) ? ("ok inline start, seq " + DialogueSync.Seq("counter")) : "refused (busy?)";
			case "start-bf":
			{
				Dialogue val2 = DialogueSync.BuildRoot("bf");
				return ((Object)(object)DialogUIManager.Instance != (Object)null && DialogUIManager.Instance.StartDialogue(val2, false)) ? ("ok started seq " + DialogueSync.Seq("counter")) : "refused (busy?)";
			}
			case "flush-embedded":
			{
				int num2 = DialogueSystem.Counter.Scheduled?.Count ?? (-1);
				Seed seed;
				using (Replay.Server(out seed))
				{
					DialogUIManager.Instance.FlushScheduledEndActions();
				}
				Net.ToAll("dialogue.bot.flush", new DialogueBotFlush
				{
					seed = seed
				});
				return "ok flushed " + num2 + " scheduled end action(s) inside a seeded replay; seq still " + DialogueSync.Seq("counter");
			}
			case "wait-lou":
				return "ok " + Customers.BotDo("wait", "lou");
			case "timer-dismiss":
				return "ok " + Customers.BotTimerDismiss();
			default:
				if (cmd.StartsWith("queue:", StringComparison.Ordinal))
				{
					return "ok " + Customers.BotDo("add", cmd.Substring(6));
				}
				if (cmd == "achievement")
				{
					int blockedAchievements = Suppress.BlockedAchievements;
					if ((Object)(object)AchievementTracker.Instance == (Object)null)
					{
						return "no tracker";
					}
					AchievementTracker.Instance.CheckTutorialComplete();
					return "blocked " + (Suppress.BlockedAchievements - blockedAchievements);
				}
				return "unknown command " + cmd;
			}
		}
		catch (Exception ex)
		{
			return "threw " + ex.GetType().Name + ": " + ex.Message;
		}
	}
}
