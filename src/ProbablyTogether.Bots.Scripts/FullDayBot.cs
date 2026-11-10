using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ProbablyTogether.Systems;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public abstract class FullDayBot : BotScript
{
	protected static readonly string[] All = new string[9] { "fd.core", "fd.parts", "fd.cust", "fd.items", "fd.econ", "fd.dlg", "fd.sec", "fd.mach", "fd.replay" };

	private int fxAsk;

	protected int Dlg0;

	protected float TalkStart;

	private static readonly MethodInfo onToggled = AccessTools.Method(typeof(EndOfDayUIManager), "OnToggled", new Type[2]
	{
		typeof(StoreService),
		typeof(bool)
	}, (Type[])null);

	protected static PlayerStore Ps => Game.Store;

	protected static EmporiumEntry E => EmporiumEntry.Instance;

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

	protected static string State
	{
		get
		{
			if (!Game.ShopLoaded)
			{
				return "";
			}
			return Ps.storeState.ToString();
		}
	}

	protected static int Day => Game.Day;

	protected static float Now => Time.realtimeSinceStartup;

	protected static bool NoDialogue => DialogueSystem.Counter.Current == null;

	protected static string S(object o)
	{
		return Convert.ToString(o, CultureInfo.InvariantCulture);
	}

	protected static string F(float f)
	{
		return f.ToString("F1", CultureInfo.InvariantCulture);
	}

	protected static string V(IReadOnlyDictionary<string, string> d, string k)
	{
		if (d == null || !d.TryGetValue(k, out var value))
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

	protected static bool HasMark(IReadOnlyDictionary<string, string> d, string mark)
	{
		return ("," + V(d, "fd.marks") + ",").Contains("," + mark + ",");
	}

	protected static string Shop()
	{
		return "state " + State + ", day " + Day + ", cash " + (Game.ShopLoaded ? S(Ps.playerCash) : "-") + ", customer " + (Game.ShopLoaded ? FullDayLab.CurrentId() : "-") + " (arrived " + (Game.ShopLoaded && Ps.isClientArrived) + "), counter holder " + FullDayLab.Holder() + ", queue " + BotProbes.Read("fd.queue") + ", " + FullDayLab.Counter();
	}

	protected BotStep Same(string name, float timeout, params string[] keys)
	{
		if (keys == null || keys.Length == 0)
		{
			keys = All;
		}
		int id = 0;
		int tries = 0;
		float next = 0f;
		float askedAt = 0f;
		string last = "no answer yet";
		BotStep botStep = Step("same-" + name, timeout, delegate(BotStep st)
		{
			if (id != 0 && Time.realtimeSinceStartup - askedAt > 5f)
			{
				id = 0;
			}
			if (id != 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = 0;
					tries++;
					List<string> list = new List<string>();
					string[] array = keys;
					foreach (string text in array)
					{
						string text2 = BotProbes.Read(text);
						string text3 = V(dictionary, text);
						if (text2 != text3 || text2 == "")
						{
							list.Add(text + ": " + Explain(text2, text3));
						}
					}
					if (list.Count == 0)
					{
						st.Detail = keys.Length + " part(s) equal (" + string.Join(" ", from k in keys.Where(delegate(string k)
							{
								switch (k)
								{
								default:
									return k == "fd.customer";
								case "fd.core":
								case "fd.counter":
								case "fd.holder":
									return true;
								}
							})
							select k + "=" + BotProbes.Read(k)) + ")" + ((tries > 1) ? (" on try " + tries) : "");
						return StepStatus.Pass;
					}
					last = string.Join("; ", list);
					next = Time.realtimeSinceStartup + 0.7f;
				}
			}
			if (id == 0 && Time.realtimeSinceStartup >= next)
			{
				id = BotProbes.AskServer();
				askedAt = Time.realtimeSinceStartup;
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "still different after " + F(timeout) + " s (" + tries + " tries): " + last;
		return botStep.Soft();
	}

	protected BotStep Same(string name, params string[] keys)
	{
		return Same(name, 25f, keys);
	}

	private static string Explain(string mine, string theirs)
	{
		if (mine.Contains("=") && theirs.Contains("="))
		{
			Dictionary<string, string> dictionary = Split(mine);
			Dictionary<string, string> dictionary2 = Split(theirs);
			List<string> list = new List<string>();
			foreach (string item in dictionary.Keys.Union(dictionary2.Keys))
			{
				dictionary.TryGetValue(item, out var value);
				dictionary2.TryGetValue(item, out var value2);
				if (value != value2)
				{
					list.Add(item + " here " + (value ?? "-") + " server " + (value2 ?? "-"));
				}
			}
			if (list.Count > 0)
			{
				return string.Join(", ", list);
			}
		}
		return "here " + ((mine == "") ? "-" : mine) + ", server " + ((theirs == "") ? "-" : theirs);
	}

	private static Dictionary<string, string> Split(string s)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		string[] array = s.Split('|');
		foreach (string text in array)
		{
			int num = text.IndexOf('=');
			if (num > 0)
			{
				dictionary[text.Substring(0, num)] = text.Substring(num + 1);
			}
		}
		return dictionary;
	}

	protected BotStep WaitServer(string name, float timeout, Func<IReadOnlyDictionary<string, string>, bool> cond, params string[] show)
	{
		int id = 0;
		float next = 0f;
		float askedAt = 0f;
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
						st.Detail = "server " + BotProbes.Format(dictionary, show) + " after " + F(st.Elapsed) + " s";
						return StepStatus.Pass;
					}
					next = Time.realtimeSinceStartup + 1f;
				}
				else if (Time.realtimeSinceStartup - askedAt > 10f)
				{
					id = 0;
				}
			}
			if (id == 0 && Time.realtimeSinceStartup >= next)
			{
				id = BotProbes.AskServer();
				askedAt = Time.realtimeSinceStartup;
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "server never got there: " + ((last == null) ? "no answer" : BotProbes.Format(last, show)) + "; here " + Shop();
		return botStep;
	}

	protected BotStep WaitMark(string name, string mark, float timeout)
	{
		return WaitServer(name, timeout, (IReadOnlyDictionary<string, string> d) => HasMark(d, mark), "fd.marks");
	}

	protected BotStep Mark(string mark)
	{
		return Do("mark-" + mark, delegate
		{
			Net.ToServer("fullday.lab", new MsgFullDayLab
			{
				op = "mark",
				a = mark
			});
			return "told the server";
		});
	}

	protected static void Lab(string op, string a = null)
	{
		Net.ToServer("fullday.lab", new MsgFullDayLab
		{
			op = op,
			a = a
		});
	}

	protected void JoinSteps()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("live", 240f, () => Live, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		Do("options", delegate
		{
			EmporiumOption instance = EmporiumOption.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				return "no options object";
			}
			instance.autoCallNext = false;
			instance.autoNextDialog = false;
			return "autoCallNext off, autoNextDialog off (player " + Net.LocalPlayer + " '" + Net.PlayerName(Net.LocalPlayer) + "')";
		});
		WaitServer("both-live", 240f, (IReadOnlyDictionary<string, string> d) => I(d, "fd.live") >= 2 && V(d, "fd.fx") != "0" && V(d, "fd.fx") != "", "fd.live", "fd.fx", "players");
		WaitFor("fixture-here", 30f, SyncFx, () => "printer #" + FullDayLab.Fx + ": " + FullDayLab.Box());
		Same("join", All.Concat(new string[1] { "fd.events" }).ToArray());
	}

	private bool SyncFx()
	{
		if (FullDayLab.Machine() != null)
		{
			return true;
		}
		if (fxAsk == 0)
		{
			fxAsk = BotProbes.AskServer();
		}
		Dictionary<string, string> dictionary = BotProbes.TakeReply(fxAsk);
		if (dictionary == null)
		{
			return false;
		}
		fxAsk = 0;
		if (long.TryParse(V(dictionary, "fd.fx"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			FullDayLab.Fx = result;
		}
		return FullDayLab.Machine() != null;
	}

	protected static string DlgText()
	{
		CounterChannel counter = DialogueSystem.Counter;
		return "dialogue step " + DialogueSync.Seq("counter") + ", line " + ((counter.Current == null) ? "none" : (counter.LineUnfinished ? "typing" : (counter.IsChoiceNow ? (counter.Current.choices.Count + " choices") : "shown"))) + ", refused " + DialogueSync.RefusedCount + ((DialogueSync.LastRefused.Length > 0) ? (" (" + DialogueSync.LastRefused + ")") : "");
	}

	protected BotStep Talk(string name, float timeout, Func<Dialogue, int> choose, Func<bool> done)
	{
		float lastNudge = 0f;
		int shown = 0;
		int spaces = 0;
		string picks = "";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (done())
			{
				st.Detail = "steps " + Dlg0 + " -> " + DialogueSync.Seq("counter") + " (typewriter " + shown + ", Space " + spaces + ((picks.Length > 0) ? (", chose " + picks) : "") + "); " + DlgText() + "; " + Shop() + " after " + F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			CounterChannel counter = DialogueSystem.Counter;
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (counter.Current == null || realtimeSinceStartup - lastNudge < 1.2f)
			{
				return StepStatus.Running;
			}
			lastNudge = realtimeSinceStartup;
			DialogUIManager instance = DialogUIManager.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				return StepStatus.Running;
			}
			if (counter.LineUnfinished)
			{
				instance.OnCurrentTextDisplayed();
				shown++;
			}
			else if (counter.IsChoiceNow)
			{
				int num = Math.Max(0, Math.Min(choose(counter.Current), counter.Current.choices.Count - 1));
				instance.SelectChoice(num);
				picks = picks + ((picks.Length > 0) ? "," : "") + num + "/" + counter.Current.choices.Count;
			}
			else
			{
				instance.ContinueConversation();
				spaces++;
			}
			return StepStatus.Running;
		}, delegate
		{
			Dlg0 = DialogueSync.Seq("counter");
			TalkStart = Now;
		});
		botStep.TimeoutDetail = () => "not done: " + DlgText() + "; typewriter " + shown + ", Space " + spaces + ", chose " + picks + "; " + Shop();
		return botStep;
	}

	protected static string RingBell()
	{
		StoreBell obj = Object.FindObjectOfType<StoreBell>();
		if ((Object)(object)obj == (Object)null)
		{
			throw new InvalidOperationException("no StoreBell in the scene");
		}
		obj.OnClick();
		return "bell clicked (bell flag " + Ps.lockNextBellFlag + ", state " + State + ", customer " + FullDayLab.CurrentId() + ")";
	}

	protected BotStep DismissUntilGone(string name, float timeout)
	{
		float lastTry = -100f;
		float lastNudge = 0f;
		int tries = 0;
		int spaces = 0;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (FullDayLab.CurrentId() == "none")
			{
				st.Detail = tries + " dismiss press(es), " + spaces + " dialogue nudge(s); " + Shop() + " after " + F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			CounterChannel counter = DialogueSystem.Counter;
			if (counter.Current != null && realtimeSinceStartup - lastNudge > 1.2f)
			{
				lastNudge = realtimeSinceStartup;
				DialogUIManager instance = DialogUIManager.Instance;
				if (counter.LineUnfinished)
				{
					instance.OnCurrentTextDisplayed();
				}
				else if (counter.IsChoiceNow)
				{
					instance.SelectChoice(0);
				}
				else
				{
					instance.ContinueConversation();
				}
				spaces++;
				return StepStatus.Running;
			}
			if (counter.Current == null && Ps.isClientArrived && realtimeSinceStartup - lastTry > 4f)
			{
				lastTry = realtimeSinceStartup;
				tries++;
				Ps.TryPlayerDismissCurrentClient(false);
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "customer still here after " + tries + " dismiss press(es): dismissable " + (Game.ShopLoaded && Ps.IsClientDimissable()) + "; " + DlgText() + "; " + Shop() + "; notes: " + string.Join(" / ", FullDayLab.Notes.Skip(Math.Max(0, FullDayLab.Notes.Count - 4)));
		return botStep;
	}

	protected static List<GameItem> Movables(GameInventory inv)
	{
		GameItem fx = FullDayLab.Machine();
		return inv.childItems.Where(delegate(GameItem i)
		{
			if (i != null && !Items.IsGone(i) && GeneralHelper.IsItemOwned(i) && i.MayRemove() && i.MaxNumRemove() >= 1 && i.contentWindow == null && !ItemPinHelper.IsPinned(i))
			{
				if (fx != null)
				{
					if (i != fx)
					{
						GameInventory parentInventory = i.parentInventory;
						return ((parentInventory != null) ? parentInventory.GetParentItem() : null) != fx;
					}
					return false;
				}
				return true;
			}
			return false;
		}).ToList();
	}

	protected static bool Spot(GameInventory inv, GameItem item, out int x, out int y)
	{
		x = (y = 0);
		int num = 30;
		int num2 = 30;
		GameGridInventory val = (GameGridInventory)(object)((inv is GameGridInventory) ? inv : null);
		if (val != null && val.inventoryShape != null)
		{
			num = val.inventoryShape.width;
			num2 = val.inventoryShape.height;
		}
		int orientation = item.modifiedShape.orientation;
		for (int i = 0; i < num2; i++)
		{
			for (int j = 0; j < num; j++)
			{
				if (item.parentInventory != inv || j != item.modifiedShape.minX || i != item.modifiedShape.minY)
				{
					SlotMarker val2 = inv.TryInventorySlot(item, item.unitCount, Vector2.negativeInfinity, Items.Shape(item, j, i, item.modifiedShape.flipped, orientation), item.modifiedState);
					if (val2 != null && val2.numTransfer >= item.unitCount && val2.targetItem == null)
					{
						x = j;
						y = i;
						return true;
					}
				}
			}
		}
		return false;
	}

	protected BotStep CallStep(string name, bool standIn)
	{
		float lastRing = -100f;
		int rings = 0;
		bool asked = false;
		BotStep botStep = Step(name, 60f, delegate(BotStep st)
		{
			if (FullDayLab.CurrentId() != "none" && FullDayLab.Holder() == Net.LocalPlayer && Ps.isClientArrived)
			{
				st.Detail = rings + " bell press(es)" + (asked ? " after a stand-in seller" : "") + ": " + Shop() + " after " + F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			if (FullDayLab.CurrentId() != "none" || Ps.lockNextBellFlag > 0 || Now - lastRing < 8f)
			{
				return StepStatus.Running;
			}
			if (standIn && !asked && BotProbes.Read("fd.queue") == "0")
			{
				asked = true;
				BotLog.Info(Name, "stand-in: the queue is empty; asking the server for a seller");
				Lab("add", "scavGeneral");
				lastRing = Now - 5f;
				return StepStatus.Running;
			}
			lastRing = Now;
			rings++;
			RingBell();
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => rings + " bell press(es): " + Shop() + "; notes " + string.Join(" / ", FullDayLab.Notes.Skip(Math.Max(0, FullDayLab.Notes.Count - 4)));
		return botStep.Soft();
	}

	protected static void ToggleService(StoreService sv, bool on)
	{
		onToggled.Invoke(EndOfDayUIManager.Instance, new object[2] { sv, on });
	}

	protected BotStep ReadyStep(string name, Func<int> endedBefore)
	{
		float pressedAt = -100f;
		int presses = 0;
		string log = "";
		BotStep botStep = Step(name, 60f, delegate(BotStep st)
		{
			if (DayNight.IsReady(Net.LocalPlayer) || FullDayLab.DaysEnded > endedBefore())
			{
				st.Detail = presses + " End Day press(es)" + log + "; ready " + BotProbes.Read("fd.ready") + ", days ended " + FullDayLab.DaysEnded + " after " + F(st.Elapsed) + " s";
				return StepStatus.Pass;
			}
			if (Now - pressedAt < 3f)
			{
				return StepStatus.Running;
			}
			if (presses > 0 && DayNight.LastRefusedWhat == "ready" && DayNight.LastRefusedWhy.StartsWith("Not enough cash", StringComparison.Ordinal))
			{
				List<StoreService> list = Ps.storeService.Where((StoreService x) => x != null && x.unlocked && x.isUsing && !x.isMandatory && x.cost > 0).ToList();
				if (list.Count > 0)
				{
					foreach (StoreService item in list)
					{
						onToggled.Invoke(EndOfDayUIManager.Instance, new object[2] { item, false });
					}
					log = log + "; refused ('" + DayNight.LastRefusedWhy + "'), unticked " + string.Join(",", list.Select((StoreService x) => x.id + "@" + x.cost)) + " on the tablet";
					DayNight.LastRefusedWhat = "";
					pressedAt = Now;
					return StepStatus.Running;
				}
			}
			if (presses >= 4)
			{
				return StepStatus.Running;
			}
			DayNight.LastRefusedWhat = "";
			EndOfDayUIManager.Instance.OpenUI();
			EndOfDayUIManager.Instance.OnEndDayButtonClick();
			presses++;
			pressedAt = Now;
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => presses + " press(es)" + log + "; ready " + BotProbes.Read("fd.ready") + ", last refusal '" + DayNight.LastRefusedWhy + "', services on " + string.Join(",", from x in Ps.storeService
			where x != null && x.unlocked && x.isUsing
			select x.id + "@" + x.cost) + ", cash " + Ps.playerCash;
		return botStep.Soft();
	}

	protected BotStep ReadyPressed(string name)
	{
		return Do(name, delegate
		{
			EndOfDayUIManager.Instance.OpenUI();
			EndOfDayUIManager.Instance.OnEndDayButtonClick();
			return "the tablet's End Day pressed (ready list here " + BotProbes.Read("fd.ready") + ")";
		});
	}

	protected BotStep CountersSoft(string name)
	{
		return Check(name, () => Replay.ChecksBad == 0 && Customers.Mismatches == 0 && DialogueSync.MismatchCount == 0 && ItemsMove.ApplyMismatches == 0 && Machines.MismatchCount == 0 && Economy.Mismatches == 0 && DayNight.Mismatches == 0, FullDayLab.Counters).Soft();
	}
}
