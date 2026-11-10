using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public abstract class ItemsBot3Base : ItemsBotBase
{
	protected Dictionary<string, string> Srv = new Dictionary<string, string>();

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

	protected void Cmd3(string name, string cmd, long id = 0L, int n = 0, int v = 0)
	{
		Do(name, delegate
		{
			Net.ToServer("items.bot3", new ItemsBot3Cmd
			{
				cmd = cmd,
				id = id,
				n = n,
				v = v
			});
			return "sent " + cmd + ((id != 0L) ? (" #" + id) : "") + ((n != 0) ? (" n=" + n) : "") + ((v != 0) ? (" v=" + v) : "");
		});
	}

	protected BotStep Quiet()
	{
		return Do("quiet-dialogue", delegate
		{
			EmporiumOption instance = EmporiumOption.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				return "no options object";
			}
			instance.autoNextDialog = false;
			return "autoNextDialog off here (nobody advances the inspector's lines during the test)";
		});
	}

	protected BotStep WaitServer(string name, float timeout, Func<Dictionary<string, string>, bool> cond, params string[] show)
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
		botStep.TimeoutDetail = () => "server never matched in " + timeout + " s; last server " + BotProbes.Format(Srv, show);
		return botStep;
	}

	protected BotStep Same(string name, float timeout, params string[] probes)
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
						st.Detail = string.Join(" ", probes.Select((string p) => p + "=" + Mine(p))) + " (same as the server)";
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
		botStep.TimeoutDetail = () => "still different after " + timeout + " s: " + diff;
		return botStep;
	}

	protected static string Cur()
	{
		PlayerStore store = Game.Store;
		object obj;
		if (store == null)
		{
			obj = null;
		}
		else
		{
			StoreClientInstance currentClientInstance = store.currentClientInstance;
			obj = ((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier);
		}
		if (obj == null)
		{
			obj = "none";
		}
		return (string)obj;
	}

	protected BotStep NoMismatch(string name)
	{
		return Check(name, () => ItemsMove.ApplyMismatches == 0 && Items.TapeMismatches == 0 && ItemsMove.RandomMismatches == 0, () => "items applied " + ItemsMove.AppliedCount + ", mismatches " + ItemsMove.ApplyMismatches + ", tape " + Items.TapeMismatches + ", random " + ItemsMove.RandomMismatches + ", nego fixes " + ItemsMove.NegoFixes + ", rejects " + ItemsMove.RejectCount + "; replay checks ok " + Replay.ChecksOk + " bad " + Replay.ChecksBad + ((Replay.ChecksBad > 0) ? (" (last: " + Replay.LastMismatch + ")") : ""));
	}
}
