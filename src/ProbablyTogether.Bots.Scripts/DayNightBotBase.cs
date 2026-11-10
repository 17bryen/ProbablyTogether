using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public abstract class DayNightBotBase : BotScript
{
	protected int day0;

	protected static PlayerStore Ps => PlayerStore.Instance;

	protected static string State
	{
		get
		{
			if (!BotProbes.InShop)
			{
				return "";
			}
			return Ps.storeState.ToString();
		}
	}

	protected static int Day
	{
		get
		{
			if (!BotProbes.InShop)
			{
				return 0;
			}
			return StoreStation.Instance.dayCounter;
		}
	}

	protected static bool Live
	{
		get
		{
			if (Net.IsClient && Net.IsLive && BotProbes.InShop)
			{
				return Game.ShopLoaded;
			}
			return false;
		}
	}

	protected static int SentOf(string kind)
	{
		if (!DayNight.Sent.TryGetValue(kind, out var value))
		{
			return 0;
		}
		return value;
	}

	protected BotStep Agree(string name, float timeout, params string[] keys)
	{
		int id = 0;
		float next = 0f;
		string last = "no answer yet";
		int tries = 0;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (id != 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = 0;
					tries++;
					List<string> list = new List<string>();
					List<string> list2 = new List<string>();
					string[] array = keys;
					foreach (string text in array)
					{
						string text2 = BotProbes.Read(text);
						dictionary.TryGetValue(text, out var value);
						if (text2 != (value ?? "") || text2 == "")
						{
							list.Add(text + " here " + ((text2 == "") ? "-" : text2) + ", server " + (value ?? "-"));
						}
						else
						{
							list2.Add(text + "=" + text2);
						}
					}
					if (list.Count == 0)
					{
						st.Detail = string.Join(" ", list2) + " (same as the server" + ((tries > 1) ? (", try " + tries) : "") + ")";
						return StepStatus.Pass;
					}
					last = string.Join("; ", list);
					next = Time.realtimeSinceStartup + 0.7f;
				}
			}
			if (id == 0 && Time.realtimeSinceStartup >= next)
			{
				id = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "never agreed in " + timeout.ToString("F0", CultureInfo.InvariantCulture) + " s (" + tries + " tries): " + last;
		return botStep;
	}

	protected BotStep WaitServer(string name, float timeout, Func<IReadOnlyDictionary<string, string>, bool> cond, params string[] show)
	{
		int id = 0;
		float next = 0f;
		Dictionary<string, string> last = null;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (id != 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = 0;
					last = dictionary;
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
					next = Time.realtimeSinceStartup + 1f;
				}
			}
			if (id == 0 && Time.realtimeSinceStartup >= next)
			{
				id = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "server never got there: " + ((last == null) ? "no answer" : BotProbes.Format(last, show));
		return botStep;
	}

	protected static string V(IReadOnlyDictionary<string, string> d, string k)
	{
		if (!d.TryGetValue(k, out var value))
		{
			return "";
		}
		return value;
	}

	protected static int I(IReadOnlyDictionary<string, string> d, string k)
	{
		if (!int.TryParse(V(d, k), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return -1;
		}
		return result;
	}

	protected void JoinSteps()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "player " + Net.LocalPlayer);
		WaitFor("shop", 180f, () => Live, () => "scene " + BotProbes.Scene + ", day " + Day);
	}

	protected void Ready(string name)
	{
		Do(name, delegate
		{
			EndOfDayUIManager.Instance.OpenUI();
			EndOfDayUIManager.Instance.OnEndDayButtonClick();
			return "End Day pressed; dayNight.ready sent " + SentOf("dayNight.ready") + " time(s) so far";
		});
	}
}
