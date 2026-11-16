using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

internal static class SelfTest
{
	public sealed class TickMsg
	{
		public int n;

		public int delta;

		public Seed seed;
	}

	public sealed class FloodMsg
	{
		public int n;

		public int from;
	}

	private static Dictionary<string, float> steps;

	private static readonly HashSet<string> done = new HashSet<string>();

	private static bool pathsDone;

	private static float liveAt = -1f;

	private static int dayStep;

	private static float dayNext;

	private static int ticks;

	private static float nextTick;

	private static bool registered;

	private static int fillApplied;

	private static int fillReplayed;

	private static int floodSeen;

	private static Dictionary<string, float> Steps
	{
		get
		{
			if (steps != null)
			{
				return steps;
			}
			steps = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
			string selfTest = Args.SelfTest;
			if (string.IsNullOrEmpty(selfTest))
			{
				return steps;
			}
			string[] array = selfTest.Split(',', ';', ' ');
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				if (text.Length != 0)
				{
					int num = text.IndexOf('@');
					float result = 0f;
					if (num > 0)
					{
						float.TryParse(text.Substring(num + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
						text = text.Substring(0, num);
					}
					steps[text] = result;
				}
			}
			return steps;
		}
	}

	private static void Register()
	{
		if (registered || string.IsNullOrEmpty(Args.SelfTest))
		{
			return;
		}
		registered = true;
		Net.On("selftest.tick", null, delegate(TickMsg m)
		{
			using (Replay.Client(m.seed))
			{
				ApplyTick(m);
			}
		});
		Net.On<FloodMsg>("selftest.fill", null, delegate
		{
			fillApplied++;
			if (DayLog.Replaying)
			{
				fillReplayed++;
			}
		});
		Net.On("selftest.flood", delegate(FloodMsg m, int from)
		{
			Net.ToAll("selftest.flooded", new FloodMsg
			{
				n = m.n,
				from = from
			});
		}, null);
		Net.On<FloodMsg>("selftest.flooded", null, delegate
		{
			floodSeen++;
		});
		Snapshot.Loaded += delegate
		{
			Log.Info("selftest: snapshot loaded; selftest.fill applied " + fillApplied + " (from the day log " + fillReplayed + "), selftest.flooded seen " + floodSeen);
			fillApplied = (fillReplayed = 0);
		};
	}

	private static void FillTick(float since)
	{
		if (Game.ShopLoaded && (Due("fill", since) || Due("fill2", since)))
		{
			int num = 25000;
			if (int.TryParse(Args.Get("--br-selftest-fill") ?? "", out var result) && result > 0 && result <= 300000)
			{
				num = result;
			}
			long ms = Clock.Ms;
			int count = DayLog.Count;
			for (int i = 1; i <= num; i++)
			{
				Net.ToAll("selftest.fill", new FloodMsg
				{
					n = i
				});
			}
			Log.Info("selftest fill: " + num + " server broadcasts in " + (Clock.Ms - ms) + " ms; today's log " + count + " -> " + DayLog.Count + " message(s), " + DayLog.Bytes / 1024 + " KB" + (DayLog.CapHit ? "; the cap was hit" : ""));
		}
	}

	private static void ApplyTick(TickMsg m)
	{
		PlayerStore store = Game.Store;
		EmporiumEntry instance = EmporiumEntry.Instance;
		if (store == null || (Object)(object)instance == (Object)null)
		{
			Log.Warn("selftest tick #" + m.n + ": no shop");
			return;
		}
		int randomInt = RNG.GetRandomInt(1, 50);
		store.playerCash += m.delta + randomInt;
		GameItem val = DirectoryMaster.Item("newspaper", true);
		int num = GraphUtils.TryAcceptAll((GraphNodeStorage)(object)instance.invElement, val, -1);
		Log.Info("selftest tick #" + m.n + (DayLog.Replaying ? " (replayed)" : "") + ": roll " + randomInt + ", cash " + store.playerCash + ", new item uid " + val.uniqueId + ((num == 0) ? " placed" : (" NOT placed (" + num + " left over)")) + "; replay digest " + Replay.Digest().ToString("x"));
	}

	internal static bool On(string step)
	{
		return Steps.ContainsKey(step);
	}

	internal static void AtMenu()
	{
		if (pathsDone || !On("paths"))
		{
			return;
		}
		pathsDone = true;
		try
		{
			string text = (Args.Name ?? "copy") + "@" + DateTime.UtcNow.ToString("HH:mm:ss");
			ES3.Save<string>("probablyTogetherSelfTest", text);
			Paths.IndexSave<string>("probablyTogetherSelfTest", text);
			ES3Settings val = new ES3Settings((string)null, (ES3Settings)null);
			Log.Info("selftest paths: marker '" + text + "' written; ES3 settings file -> " + val.FullPath + "; save index -> " + new ES3Settings(Paths.File("saves_index.es3"), (ES3Settings)null).FullPath + "; SaveFiles.AbsolutePath(save_1.es3) -> " + SaveFiles.AbsolutePath(SaveFiles.Slot(1)) + "; account folder " + Paths.Original + "; read back " + ES3.Load<string>("probablyTogetherSelfTest", new ES3Settings((string)null, (ES3Settings)null)));
		}
		catch (Exception ex)
		{
			Log.Error("selftest paths failed: " + ex);
		}
	}

	internal static void Tick()
	{
		if (Steps.Count == 0)
		{
			return;
		}
		Register();
		bool num;
		if (!Net.IsClient)
		{
			if (!Net.IsServer)
			{
				goto IL_004a;
			}
			num = Server.ShopReady;
		}
		else
		{
			num = Client.State == ClientState.Ready;
		}
		if (num && liveAt < 0f)
		{
			liveAt = Time.realtimeSinceStartup;
		}
		goto IL_004a;
		IL_004a:
		if (liveAt < 0f)
		{
			return;
		}
		float since = Time.realtimeSinceStartup - liveAt;
		if (Net.IsClient)
		{
			if (Due("echo", since))
			{
				Net.Echo("hello from " + (Args.Name ?? "a bot") + " at " + DateTime.UtcNow.ToString("HH:mm:ss"));
			}
			if (Due("resync", since))
			{
				Log.Info("selftest: asking for a resync");
				Snapshot.RequestResync();
			}
			if (Due("leave", since))
			{
				Log.Info("selftest: leaving");
				Net.Disconnect();
			}
		}
		if (Net.IsServer)
		{
			TickTick(since);
			DayTick(since);
			FillTick(since);
		}
	}

	private static void TickTick(float since)
	{
		if (Steps.TryGetValue("tick", out var value) && !(since < value) && ticks < 4 && !(Time.realtimeSinceStartup < nextTick) && Game.ShopLoaded)
		{
			ticks++;
			nextTick = Time.realtimeSinceStartup + 5f;
			TickMsg tickMsg = new TickMsg
			{
				n = ticks,
				delta = 10 * ticks
			};
			using (Replay.Server(out tickMsg.seed))
			{
				ApplyTick(tickMsg);
			}
			Net.ToAll("selftest.tick", tickMsg);
		}
	}

	private static bool Due(string step, float since)
	{
		if (done.Contains(step) || !Steps.TryGetValue(step, out var value) || since < value)
		{
			return false;
		}
		done.Add(step);
		return true;
	}

	private static void DayTick(float since)
	{
		if (dayStep == 0)
		{
			if (!Due("day", since))
			{
				return;
			}
			dayStep = 1;
			dayNext = Time.realtimeSinceStartup;
		}
		if (dayStep > 5 || Time.realtimeSinceStartup < dayNext || !Game.ShopLoaded)
		{
			return;
		}
		PlayerStore store = Game.Store;
		try
		{
			switch (dayStep)
			{
			case 1:
				StoreShutterButton.Instance.OpenShutter();
				Log.Info("selftest day: shutter opened; state=" + store.storeState.ToString());
				break;
			case 2:
			{
				store.TryCallNextClient();
				StoreClientInstance currentClientInstance = store.currentClientInstance;
				Log.Info("selftest day: called next customer -> " + (((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier) ?? "none"));
				break;
			}
			case 3:
				store.DismissCurrentClient(false, true, true);
				Log.Info("selftest day: dismissed; queue=" + (store.storeClientManager?.clientStack?.Count ?? (-1)));
				break;
			case 4:
				StoreShutterButton.Instance.EndAndClose();
				Log.Info("selftest day: closed up; state=" + store.storeState.ToString());
				break;
			case 5:
				EndOfDayUIManager.Instance.OnEndDayButtonClick();
				Log.Info("selftest day: end of day pressed");
				break;
			}
		}
		catch (Exception ex)
		{
			Log.Error("selftest day step " + dayStep + " threw: " + ex);
			dayStep = 9;
			return;
		}
		dayStep++;
		dayNext = Time.realtimeSinceStartup + ((dayStep == 5) ? 12f : 8f);
	}
}
