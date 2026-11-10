using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public abstract class PingBotBase : BotScript
{
	protected static readonly Vector2 SpotAt = new Vector2(0.3f, 0.6f);

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

	protected static string F(float s)
	{
		return s.ToString("0.0#", CultureInfo.InvariantCulture);
	}

	protected static string Who(int p)
	{
		return Kit.Safe(Kit.Name(Net.PlayerName(p)));
	}

	protected static int Lines(string needle)
	{
		return Chat.Lines.Count((Chat.Line l) => l.Text != null && l.Text.Contains(needle));
	}

	protected static int PointLines()
	{
		return Chat.Lines.Count((Chat.Line l) => l.Text != null && (l.Text.Contains(" points at ") || l.Text.StartsWith("You point at ", StringComparison.Ordinal)));
	}

	protected static int I(IReadOnlyDictionary<string, string> d, string k)
	{
		if (!d.TryGetValue(k, out var value) || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return -1;
		}
		return result;
	}

	protected BotStep S(string name, float timeout, Func<BotStep, StepStatus> poll)
	{
		BotStep step = null;
		step = Step(name, timeout, poll);
		step.TimeoutDetail = () => "timed out; " + step.Detail;
		return step;
	}

	protected void SetKey(string name, string k, Func<string> v)
	{
		int id = 0;
		string val = "";
		Step(name, 20f, delegate(BotStep st)
		{
			string text = PingLab.Take(id);
			if (text == null)
			{
				return StepStatus.Running;
			}
			st.Detail = k + "=" + val + " -> " + text;
			return (text == "ok") ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			val = v();
			id = PingLab.Send("set", k, val);
		}).TimeoutDetail = () => "no answer setting " + k;
	}

	protected void WaitKey(string name, string k, float timeout)
	{
		int id = -1;
		float last = -10f;
		Step(name, timeout, delegate(BotStep st)
		{
			if (id >= 0)
			{
				string text = PingLab.Take(id);
				if (text == null)
				{
					if (st.Elapsed - last < 5f)
					{
						return StepStatus.Running;
					}
					id = -1;
				}
				else
				{
					id = -1;
					if (text.Length > 0)
					{
						Vars[k] = text;
						st.Detail = k + "=" + text + " (" + F(st.Elapsed) + " s)";
						return StepStatus.Pass;
					}
				}
			}
			if (st.Elapsed - last >= 0.5f && Net.IsLive)
			{
				last = st.Elapsed;
				id = PingLab.Send("get", k);
			}
			return StepStatus.Running;
		}).TimeoutDetail = () => k + " never set";
	}

	protected static long PickItem(out string how)
	{
		long num = 0L;
		long num2 = 0L;
		long num3 = 0L;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		foreach (GameItem item in Ids.All())
		{
			if (item == null || item.uniqueId <= 0 || Ids.IsStandIn(item))
			{
				continue;
			}
			num4++;
			if (num3 == 0L || item.uniqueId < num3)
			{
				num3 = item.uniqueId;
			}
			if (!PointMarks.ItemArea(item, out var area) || area.width < 0.004f || area.height < 0.004f)
			{
				continue;
			}
			num5++;
			if (num2 == 0L || item.uniqueId < num2)
			{
				num2 = item.uniqueId;
			}
			if (Pointing.ItemUnder(Kit.ToScreen(area.center)) == item.uniqueId && PointMarks.OnThisScreen(item, out var _, area.center))
			{
				num6++;
				if (num == 0L || item.uniqueId < num)
				{
					num = item.uniqueId;
				}
			}
		}
		string text = num4 + " items, " + num5 + " drawn, " + num6 + " found by the hover raycast at their centre";
		if (num != 0L)
		{
			how = "the lowest id the hover raycast reaches (" + text + ")";
			return num;
		}
		if (num2 != 0L)
		{
			how = "the lowest id drawn; none reached by the raycast (" + text + ")";
			return num2;
		}
		how = "no item drawn in this copy (" + text + "): the lowest id";
		return num3;
	}

	protected static long PickHidden(out string how)
	{
		long num = 0L;
		int num2 = 0;
		int num3 = 0;
		foreach (GameItem item in Ids.All())
		{
			if (item == null || item.uniqueId <= 0 || Ids.IsStandIn(item))
			{
				continue;
			}
			num2++;
			if (!PointMarks.ItemArea(item, out var _))
			{
				num3++;
				if (!string.IsNullOrEmpty(item.identifier) && (num == 0L || item.uniqueId < num))
				{
					num = item.uniqueId;
				}
			}
		}
		how = num3 + " of " + num2 + " items not drawn here";
		return num;
	}

	protected static long PickCovered(out string how)
	{
		long num = 0L;
		int num2 = 0;
		int num3 = 0;
		foreach (GameItem item in Ids.All())
		{
			if (item == null || item.uniqueId <= 0 || Ids.IsStandIn(item) || !PointMarks.ItemArea(item, out var area))
			{
				continue;
			}
			num2++;
			if (!PointMarks.OnThisScreen(item, out var _, area.center))
			{
				num3++;
				if (num == 0L || item.uniqueId < num)
				{
					num = item.uniqueId;
				}
			}
		}
		how = num3 + " of " + num2 + " drawn items covered at every sample";
		return num;
	}

	protected static string RaycastSurvey(out int onScreen, out int self)
	{
		onScreen = 0;
		self = 0;
		int num = 0;
		List<string> list = new List<string>();
		foreach (GameItem item in Ids.All())
		{
			if (item == null || item.uniqueId == 0 || Ids.IsStandIn(item) || !PointMarks.ItemArea(item, out var area))
			{
				continue;
			}
			onScreen++;
			if (PointMarks.OnThisScreen(item, out var _, area.center))
			{
				num++;
			}
			long num2 = Pointing.ItemUnder(Kit.ToScreen(area.center));
			if (num2 == item.uniqueId)
			{
				self++;
			}
			else if (list.Count < 10)
			{
				string[] obj = new string[13]
				{
					item.uniqueId.ToString(),
					" ",
					item.identifier,
					" box ",
					F(area.xMin),
					"-",
					F(area.xMax),
					",",
					F(area.yMin),
					"-",
					F(area.yMax),
					" -> ",
					null
				};
				object obj3;
				if (num2 != 0L)
				{
					string text = num2.ToString(CultureInfo.InvariantCulture);
					GameItem obj2 = Ids.Find(num2);
					obj3 = text + " " + (((obj2 != null) ? obj2.identifier : null) ?? "?");
				}
				else
				{
					obj3 = "nothing";
				}
				obj[12] = (string)obj3;
				list.Add(string.Concat(obj));
			}
		}
		GameItem val = Ids.Find(-102L);
		Rect area3;
		string text2 = ((val != null && PointMarks.ItemArea(val, out area3)) ? ("trash can box " + F(area3.xMin) + "-" + F(area3.xMax) + "," + F(area3.yMin) + "-" + F(area3.yMax)) : "trash can not on screen");
		return self + " of " + onScreen + " drawn items found by the hover raycast at their own centre, " + num + " at any marker sample (would get a frame); " + text2 + ((list.Count > 0) ? ("; others: " + string.Join(" | ", list)) : "");
	}

	protected static Vector2 CenterOf(long id)
	{
		GameItem val = Ids.Find(id);
		if (val == null || !PointMarks.ItemArea(val, out var area))
		{
			return new Vector2(0.5f, 0.5f);
		}
		return new Vector2(Mathf.Clamp01(area.center.x), Mathf.Clamp01(area.center.y));
	}

	protected unsafe BotStep RaycastCheck(string name, Func<int> player, Func<long> item)
	{
		return Step(name, 0f, delegate(BotStep st)
		{
			if (!PointMarks.Marks.TryGetValue(player(), out var value))
			{
				st.Detail = "no marker";
				return StepStatus.Fail;
			}
			if (!value.OnItem)
			{
				st.Detail = "the marker is not on the item here (" + value.Describe() + "): nothing to cast at";
				return StepStatus.Pass;
			}
			Vector2 val = Kit.ToScreen(value.ItemArea.center);
			long num = Pointing.ItemUnder(val);
			string[] obj = new string[10]
			{
				"raycast at the marker centre ",
				F(val.x),
				",",
				F(val.y),
				" px finds item ",
				num.ToString(),
				" (want ",
				item().ToString(),
				"), box ",
				null
			};
			Rect itemArea = value.ItemArea;
			obj[9] = itemArea.ToString();
			st.Detail = string.Concat(obj);
			return (num == item()) ? StepStatus.Pass : StepStatus.Fail;
		}).Soft();
	}

	protected BotStep KeysStep(string name)
	{
		return Step(name, 0f, delegate(BotStep st)
		{
			st.Detail = PingLab.KeysFree(out var ok);
			return ok ? StepStatus.Pass : StepStatus.Fail;
		}).Soft();
	}
}
