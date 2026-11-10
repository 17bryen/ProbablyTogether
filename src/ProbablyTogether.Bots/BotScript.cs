using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ProbablyTogether.Bots;

public abstract class BotScript
{
	internal readonly List<BotStep> Steps = new List<BotStep>();

	protected readonly Dictionary<string, string> Vars = new Dictionary<string, string>();

	private Dictionary<string, string> server = new Dictionary<string, string>();

	public abstract string Name { get; }

	public virtual BotRole Role => BotRole.Any;

	public virtual float TimeoutSeconds => 600f;

	protected IReadOnlyDictionary<string, string> Server => server;

	protected abstract void Build();

	internal void BuildSteps()
	{
		Steps.Clear();
		Build();
	}

	protected BotStep Step(string name, float timeoutSeconds, Func<BotStep, StepStatus> poll, Action<BotStep> begin = null)
	{
		BotStep botStep = new BotStep
		{
			Name = BotLog.Token(name),
			Timeout = timeoutSeconds,
			Poll = poll,
			Begin = begin
		};
		Steps.Add(botStep);
		return botStep;
	}

	protected BotStep WaitFor(string name, float timeoutSeconds, Func<bool> condition, Func<string> detail = null)
	{
		BotStep botStep = Step(name, timeoutSeconds, delegate(BotStep st)
		{
			if (!condition())
			{
				return StepStatus.Running;
			}
			st.Detail = ((detail != null) ? (Safe(detail) + "; ") : "") + "after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
			return StepStatus.Pass;
		});
		botStep.TimeoutDetail = () => "still false after " + timeoutSeconds.ToString("F0", CultureInfo.InvariantCulture) + " s" + ((detail != null) ? ("; " + Safe(detail)) : "");
		return botStep;
	}

	protected BotStep Do(string name, Func<string> action)
	{
		return Step(name, 0f, delegate(BotStep st)
		{
			st.Detail = action() ?? "done";
			return StepStatus.Pass;
		});
	}

	protected BotStep Do(string name, Action action)
	{
		return Step(name, 0f, delegate(BotStep st)
		{
			action();
			st.Detail = "done";
			return StepStatus.Pass;
		});
	}

	protected BotStep Check(string name, Func<bool> condition, Func<string> detail = null)
	{
		return Step(name, 0f, delegate(BotStep st)
		{
			bool flag = condition();
			st.Detail = ((detail != null) ? Safe(detail) : (flag ? "true" : "false"));
			return flag ? StepStatus.Pass : StepStatus.Fail;
		});
	}

	protected BotStep Expect<T>(string name, Func<T> expected, Func<T> actual)
	{
		return Step(name, 0f, delegate(BotStep st)
		{
			string text = Str(expected());
			string text2 = Str(actual());
			st.Detail = "expected " + text + ", got " + text2;
			return (text == text2) ? StepStatus.Pass : StepStatus.Fail;
		});
	}

	protected BotStep Delay(string name, float seconds)
	{
		BotStep botStep = Step(name, 0f, (BotStep st) => (st.Elapsed >= seconds) ? StepStatus.Pass : StepStatus.Running);
		botStep.Quiet = true;
		botStep.Detail = "waited " + seconds.ToString("F1", CultureInfo.InvariantCulture) + " s";
		return botStep;
	}

	protected BotStep AskServer(string name, float timeoutSeconds = 20f)
	{
		int id = 0;
		BotStep botStep = Step(name, timeoutSeconds, delegate(BotStep st)
		{
			Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
			if (dictionary == null)
			{
				return StepStatus.Running;
			}
			server = dictionary;
			st.Detail = "server " + BotProbes.Format(dictionary, "role", "scene", "day", "cash", "players") + " in " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
			return StepStatus.Pass;
		}, delegate
		{
			id = BotProbes.AskServer();
		});
		botStep.TimeoutDetail = () => "no probe reply from the server in " + timeoutSeconds.ToString("F0", CultureInfo.InvariantCulture) + " s (role " + Net.Role.ToString() + ", live " + Net.IsLive + ", asks sent again so far " + BotProbes.Resends + "; does the server run a --br-bot script?)";
		return botStep;
	}

	protected BotStep SameAsServer(string name, string probe)
	{
		return Step(name, 0f, delegate(BotStep st)
		{
			string text = BotProbes.Read(probe);
			if (!server.TryGetValue(probe, out var value))
			{
				st.Detail = "server sent no '" + probe + "' (did AskServer run?)";
				return StepStatus.Fail;
			}
			st.Detail = probe + " here " + text + ", server " + value;
			return (text == value && text != "") ? StepStatus.Pass : StepStatus.Fail;
		});
	}

	protected BotStep SettlesLikeServer(string name, string probe, float seconds)
	{
		int id = -1;
		float lastAsk = -99f;
		string last = "no reply yet";
		BotStep botStep = Step(name, seconds, delegate(BotStep st)
		{
			if (id >= 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = -1;
					string text = BotProbes.Read(probe);
					dictionary.TryGetValue(probe, out var value);
					last = probe + " here " + text + ", server " + (value ?? "?");
					st.Detail = last + " after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
					if (text == value && text != "")
					{
						return StepStatus.Pass;
					}
				}
			}
			if (id < 0 && Time.realtimeSinceStartup - lastAsk > 2f)
			{
				lastAsk = Time.realtimeSinceStartup;
				id = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "still different after " + seconds.ToString("F0", CultureInfo.InvariantCulture) + " s: " + last;
		return botStep;
	}

	protected static string Str(object o)
	{
		if (o != null)
		{
			return Convert.ToString(o, CultureInfo.InvariantCulture);
		}
		return "null";
	}

	private static string Safe(Func<string> f)
	{
		try
		{
			return f();
		}
		catch (Exception ex)
		{
			return "(detail threw " + ex.GetType().Name + ")";
		}
	}
}
