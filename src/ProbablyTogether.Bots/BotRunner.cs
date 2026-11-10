using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots;

public static class BotRunner
{
	private static bool started;

	private static readonly Queue<string> pending = new Queue<string>();

	private static BotScript current;

	private static int index;

	private static int passed;

	private static int failed;

	private static int counted;

	private static string firstFail;

	private static float scriptStart;

	private static bool stepBegun;

	public const string LabRoot = "C:\\ProgramData\\SSLabs\\";

	internal static bool WasStarted => started;

	public static string Current => current?.Name;

	public static bool Enabled
	{
		get
		{
			string why;
			if (!string.IsNullOrEmpty(Args.Bot))
			{
				return LabAllowed(out why);
			}
			return false;
		}
	}

	public static bool LabAllowed(out string why)
	{
		string data = Args.Data;
		if (string.IsNullOrEmpty(data))
		{
			why = "no --br-data (lab copies always have one inside C:\\ProgramData\\SSLabs\\)";
			return false;
		}
		string text;
		try
		{
			text = Path.GetFullPath(data.Trim().Replace('/', '\\'));
		}
		catch (Exception ex)
		{
			why = "--br-data '" + data + "' is not a usable path (" + ex.GetType().Name + ")";
			return false;
		}
		if (!text.EndsWith("\\", StringComparison.Ordinal))
		{
			text += "\\";
		}
		if (text.StartsWith("C:\\ProgramData\\SSLabs\\", StringComparison.OrdinalIgnoreCase))
		{
			why = null;
			return true;
		}
		why = "--br-data '" + data + "' is not inside C:\\ProgramData\\SSLabs\\";
		return false;
	}

	internal static IEnumerable<Type> OwnTypes()
	{
		try
		{
			return typeof(BotRunner).Assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where((Type t) => t != null).ToArray();
		}
	}

	public static Dictionary<string, Type> Discover()
	{
		Dictionary<string, Type> dictionary = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
		foreach (Type item in OwnTypes())
		{
			if (!typeof(BotScript).IsAssignableFrom(item) || item.IsAbstract)
			{
				continue;
			}
			try
			{
				BotScript botScript = (BotScript)Activator.CreateInstance(item);
				if (dictionary.ContainsKey(botScript.Name))
				{
					BotLog.Info("bot", "two scripts named " + botScript.Name + ": " + dictionary[botScript.Name].Name + " and " + item.Name + "; the first wins");
				}
				else
				{
					dictionary[botScript.Name] = item;
				}
			}
			catch (Exception ex)
			{
				BotLog.Info("bot", "script class " + item.Name + " cannot be created: " + ex.Message);
			}
		}
		return dictionary;
	}

	internal static void Start(string spec)
	{
		if (started || string.IsNullOrEmpty(spec))
		{
			return;
		}
		started = true;
		if (!LabAllowed(out var why))
		{
			Log.Warn("bot: --br-bot ignored: " + why + " (lab bots run only on our lab machines; no bot script runs and no lab kind is registered)");
			return;
		}
		try
		{
			BotProbes.Init();
			Dictionary<string, Type> dictionary = Discover();
			string[] array = spec.Split(',', ';', '+');
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				if (text.Length == 0)
				{
					continue;
				}
				if (!dictionary.ContainsKey(text))
				{
					Skip(text, "start", "no bot script named '" + text + "'; known: " + string.Join(", ", dictionary.Keys.OrderBy((string k) => k)));
				}
				else
				{
					pending.Enqueue(text);
				}
			}
			BotLog.Info("bot", "starting " + string.Join(",", pending) + " (" + dictionary.Count + " scripts known; role switches: " + (Args.Server ? "server" : ((Args.Join != null) ? ("client of " + Args.Join) : "none")) + ")");
			GameObject val = new GameObject("ProbablyTogether.Bot");
			Object.DontDestroyOnLoad((Object)val);
			val.AddComponent<BotHost>();
		}
		catch (Exception ex)
		{
			BotLog.Fail("bot", "start", ex.GetType().Name + ": " + ex.Message);
		}
	}

	internal static void Tick()
	{
		BotProbes.Tick();
		if (current == null)
		{
			if (pending.Count == 0)
			{
				return;
			}
			NextScript();
			if (current == null)
			{
				return;
			}
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (index >= current.Steps.Count)
		{
			FinishScript();
			return;
		}
		BotStep botStep = current.Steps[index];
		try
		{
			if (!stepBegun)
			{
				stepBegun = true;
				botStep.StartedAt = realtimeSinceStartup;
				if (botStep.Timeout > 0f)
				{
					BotLog.Info(current.Name, "step " + botStep.Name + " (up to " + botStep.Timeout.ToString("F0", CultureInfo.InvariantCulture) + " s)");
				}
				botStep.Begin?.Invoke(botStep);
			}
			StepStatus stepStatus = botStep.Poll(botStep);
			if (stepStatus == StepStatus.Running)
			{
				if (botStep.Timeout > 0f && botStep.Elapsed > botStep.Timeout)
				{
					botStep.Detail = ((botStep.TimeoutDetail != null) ? botStep.TimeoutDetail() : ("timed out after " + botStep.Timeout.ToString("F0", CultureInfo.InvariantCulture) + " s"));
					End(botStep, ok: false);
				}
				else if (realtimeSinceStartup - scriptStart > current.TimeoutSeconds)
				{
					botStep.Detail = "script timeout (" + current.TimeoutSeconds.ToString("F0", CultureInfo.InvariantCulture) + " s) while in this step";
					End(botStep, ok: false);
				}
			}
			else
			{
				End(botStep, stepStatus == StepStatus.Pass);
			}
		}
		catch (Exception ex)
		{
			botStep.Detail = ex.GetType().Name + ": " + ex.Message;
			End(botStep, ok: false);
		}
	}

	private static void NextScript()
	{
		string text = pending.Dequeue();
		try
		{
			BotScript botScript = (BotScript)Activator.CreateInstance(Discover()[text]);
			if (botScript.Role == BotRole.Server && !Args.Server)
			{
				Skip(botScript.Name, "role", "server script on a copy without --br-server");
				AllDone();
				return;
			}
			if (botScript.Role == BotRole.Client && Args.Join == null)
			{
				Skip(botScript.Name, "role", "client script on a copy without --br-join");
				AllDone();
				return;
			}
			botScript.BuildSteps();
			current = botScript;
			index = 0;
			passed = 0;
			failed = 0;
			counted = 0;
			firstFail = null;
			stepBegun = false;
			scriptStart = Time.realtimeSinceStartup;
			BotLog.Info(botScript.Name, "start: " + botScript.Steps.Count + " steps (" + string.Join(" ", botScript.Steps.Select((BotStep x) => x.Name)) + ")");
		}
		catch (Exception ex)
		{
			Skip(text, "build", ex.GetType().Name + ": " + ex.Message);
			current = null;
			AllDone();
		}
	}

	private static void Skip(string name, string step, string detail)
	{
		BotLog.Fail(name, step, detail);
		BotLog.Fail(name, "done", "not run (" + step + ")");
	}

	private static void End(BotStep step, bool ok)
	{
		if (step.Quiet && ok)
		{
			BotLog.Info(current.Name, step.Name + " " + step.Detail);
		}
		else
		{
			counted++;
			if (ok)
			{
				passed++;
				BotLog.Pass(current.Name, step.Name, step.Detail);
			}
			else
			{
				failed++;
				firstFail = firstFail ?? step.Name;
				BotLog.Fail(current.Name, step.Name, step.Detail);
			}
		}
		stepBegun = false;
		index++;
		if (!ok && !step.ContinueOnFail)
		{
			FinishScript();
		}
	}

	private static void FinishScript()
	{
		float num = Time.realtimeSinceStartup - scriptStart;
		int num2 = current.Steps.Count((BotStep s) => !s.Quiet);
		string text = num.ToString("F1", CultureInfo.InvariantCulture);
		if (failed == 0 && counted == num2)
		{
			BotLog.Pass(current.Name, "done", passed + " steps passed in " + text + " s");
		}
		else
		{
			BotLog.Fail(current.Name, "done", failed + (num2 - counted) + " of " + num2 + " steps failed or not run (first: " + (firstFail ?? "-") + ") in " + text + " s");
		}
		if (BotProbes.Resends > 0 || BotProbes.Held > 0)
		{
			BotLog.Info(current.Name, "probe asks so far: " + BotProbes.Resends + " asked again (no answer in 3 s), " + BotProbes.Held + " held while loading");
		}
		BotProbes.ForgetAsks();
		current = null;
		AllDone();
	}

	private static void AllDone()
	{
		if (pending.Count <= 0 && current == null)
		{
			BotLog.Info("bot", "all scripts finished");
			if (Args.Has("--br-bot-exit"))
			{
				Application.Quit();
			}
		}
	}
}
