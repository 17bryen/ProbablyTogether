using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public abstract class CarryBotBase : ItemsBotBase
{
	protected static readonly FieldInfo fCur = AccessTools.Field(typeof(ItemMouseDragHandler), "currentItem");

	protected static readonly FieldInfo fLastItem = AccessTools.Field(typeof(ItemMouseDragHandler), "lastItem");

	protected static readonly FieldInfo fLastInv = AccessTools.Field(typeof(ItemMouseDragHandler), "lastInventory");

	protected static readonly FieldInfo fRemove = AccessTools.Field(typeof(ItemMouseDragHandler), "currentRemove");

	protected static readonly FieldInfo fDragging = AccessTools.Field(typeof(ItemMouseDragHandler), "isDragging");

	protected static readonly MethodInfo mStart = AccessTools.Method(typeof(ItemMouseDragHandler), "StartDrag", new Type[0], (Type[])null);

	protected static readonly MethodInfo mEnd = AccessTools.Method(typeof(ItemMouseDragHandler), "EndDrag", new Type[0], (Type[])null);

	protected static readonly MethodInfo mAbort = AccessTools.Method(typeof(ItemMouseDragHandler), "AbortDrag", new Type[0], (Type[])null);

	protected static readonly FieldInfo fSelected = AccessTools.Field(typeof(ItemMultiSelectHandler), "selectedItems");

	protected static readonly MethodInfo mGroupStart = AccessTools.Method(typeof(ItemMultiSelectHandler), "StartGroupDrag", new Type[0], (Type[])null);

	protected static GameItem Bag => ItemsBotBase.Tagged("BR_CARRY_BAG");

	protected static GameItem I(long id)
	{
		return Ids.Find(id);
	}

	protected static string F(float v)
	{
		return v.ToString("F1", CultureInfo.InvariantCulture);
	}

	protected static int Other()
	{
		foreach (int key in Net.Players.Keys)
		{
			if (key != Net.LocalPlayer)
			{
				return key;
			}
		}
		return -1;
	}

	protected void Mark(string name, Func<string> mark)
	{
		Do(name, delegate
		{
			string text = mark();
			Net.ToServer("carry.bot", new CarryBotMsg
			{
				op = "mark",
				a = text
			});
			return "mark " + text;
		});
	}

	protected BotStep WaitMark(string name, float timeout, string prefix, Action<string> got = null)
	{
		int ask = 0;
		float askedAt = -10f;
		string seen = "";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (ask == 0 || Time.realtimeSinceStartup - askedAt > 1.5f)
			{
				ask = BotProbes.AskServer();
				askedAt = Time.realtimeSinceStartup;
			}
			Dictionary<string, string> dictionary = BotProbes.TakeReply(ask);
			if (dictionary == null)
			{
				return StepStatus.Running;
			}
			ask = 0;
			dictionary.TryGetValue("carry.marks", out seen);
			string[] array = (seen ?? "").Split(',');
			foreach (string text in array)
			{
				int num = text.IndexOf(':');
				if (num > 0 && !(text.Substring(0, num) == Net.LocalPlayer.ToString(CultureInfo.InvariantCulture)))
				{
					string text2 = text.Substring(num + 1);
					if (text2.StartsWith(prefix, StringComparison.Ordinal))
					{
						got?.Invoke(text2.Substring(prefix.Length));
						st.Detail = "saw " + text + " after " + F(st.Elapsed) + " s";
						return StepStatus.Pass;
					}
				}
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "no mark '" + prefix + "' from another copy; marks: " + seen;
		return botStep;
	}

	protected BotStep WaitServer(string name, float timeout, Func<IReadOnlyDictionary<string, string>, bool> test, string probe)
	{
		int ask = 0;
		float askedAt = -10f;
		string last = "";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (ask == 0 || Time.realtimeSinceStartup - askedAt > 1f)
			{
				ask = BotProbes.AskServer();
				askedAt = Time.realtimeSinceStartup;
			}
			Dictionary<string, string> dictionary = BotProbes.TakeReply(ask);
			if (dictionary == null)
			{
				return StepStatus.Running;
			}
			ask = 0;
			dictionary.TryGetValue(probe, out last);
			if (!test(dictionary))
			{
				return StepStatus.Running;
			}
			st.Detail = probe + " '" + last + "' after " + F(st.Elapsed) + " s";
			return StepStatus.Pass;
		});
		botStep.TimeoutDetail = () => probe + " still '" + last + "'";
		return botStep;
	}

	protected void JoinAndMeet()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("shop-live", 180f, () => Ui.ShopOnline && Items.Ready, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		Do("pointer", delegate
		{
			Presence.FakePointer = new Vector2(0.3f + 0.1f * (float)Mathf.Clamp(Net.LocalPlayer, 1, 5), 0.6f);
			return "my pointer at game-area " + ((object)Presence.FakePointer.Value).ToString() + ", view " + Presence.LocalView;
		});
		WaitServer("both-live", 180f, (IReadOnlyDictionary<string, string> v) => v.TryGetValue("carry.live", out var value) && int.TryParse(value, out var result) && result >= 2, "carry.live");
		WaitFor("other-known", 30f, () => Other() > 0, () => Net.Players.Count + " player(s)");
	}
}
