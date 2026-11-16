using System;
using System.Collections.Generic;
using System.Threading;

namespace ProbablyTogether;

/// Runs work through ModHelper.RunInBackground; the game refuses mods that start threads of their own.
/// Work asked for before the mod is in the game's list waits for later ticks.
internal static class Background
{
	private sealed class Waiting
	{
		public string Name;

		public Action Work;

		public float Since;
	}

	private static readonly object gate = new object();

	private static readonly List<Waiting> waiting = new List<Waiting>();

	internal static int Started;

	internal static int WaitingCount
	{
		get
		{
			lock (gate)
			{
				return waiting.Count;
			}
		}
	}

	internal static void Run(string name, Action work)
	{
		if (Try(name, work))
		{
			return;
		}
		lock (gate)
		{
			waiting.Add(new Waiting
			{
				Name = name,
				Work = work,
				Since = -1f
			});
		}
	}

	private static bool Try(string name, Action work)
	{
		try
		{
			if (ModHelper.RunInBackground(Mod.Id, delegate(CancellationToken token)
			{
				work();
			}))
			{
				Interlocked.Increment(ref Started);
				return true;
			}
		}
		catch (Exception ex)
		{
			if (Log.Allow("background|" + name, 30000L, out var skipped))
			{
				Log.Warn("background: the game did not take '" + name + "'" + Log.More(skipped) + ": " + ex.Message);
			}
		}
		return false;
	}

	/// Main thread, every frame.
	internal static void Tick()
	{
		List<Waiting> list;
		lock (gate)
		{
			if (waiting.Count == 0)
			{
				return;
			}
			list = new List<Waiting>(waiting);
		}
		float realtimeSinceStartup = UnityEngine.Time.realtimeSinceStartup;
		foreach (Waiting item in list)
		{
			if (item.Since < 0f)
			{
				item.Since = realtimeSinceStartup;
			}
			if (Try(item.Name, item.Work))
			{
				lock (gate)
				{
					waiting.Remove(item);
				}
				if (realtimeSinceStartup - item.Since > 1f)
				{
					Log.Info("background: '" + item.Name + "' started after waiting " + (realtimeSinceStartup - item.Since).ToString("0.0") + " s for the game to list the mod");
				}
			}
			else if (realtimeSinceStartup - item.Since > 20f && Log.Allow("background|late|" + item.Name, 30000L, out var skipped))
			{
				Log.Error("background: '" + item.Name + "' is still not started after " + (realtimeSinceStartup - item.Since).ToString("0") + " s: the game does not take background work from this mod" + Log.More(skipped));
			}
		}
	}
}
