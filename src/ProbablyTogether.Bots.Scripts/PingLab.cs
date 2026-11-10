using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public static class PingLab
{
	public const string KCmd = "ping.lab";

	public const string KReply = "ping.lab.r";

	private static bool wired;

	private static int nextId;

	private static readonly Dictionary<int, string> replies = new Dictionary<int, string>();

	public static readonly Dictionary<string, string> Store = new Dictionary<string, string>(StringComparer.Ordinal);

	public static int Loads;

	public static void Ensure()
	{
		if (wired || !BotRunner.Enabled)
		{
			return;
		}
		wired = true;
		Net.On("ping.lab", delegate(PingLabCmd m, int from)
		{
			Net.ToPlayer(from, "ping.lab.r", new PingLabReply
			{
				id = m.id,
				text = Run(m)
			});
		}, null);
		Net.On("ping.lab.r", null, delegate(PingLabReply m)
		{
			lock (replies)
			{
				replies[m.id] = m.text ?? "";
			}
		});
		Snapshot.Loaded += delegate
		{
			Loads++;
		};
	}

	private static string Run(PingLabCmd m)
	{
		string key = Text.Clip(m.k ?? "", 64);
		if (m.op == "set")
		{
			if (Store.Count < 200)
			{
				Store[key] = Text.Clip(m.v ?? "", 200);
			}
			return "ok";
		}
		if (m.op == "get")
		{
			if (!Store.TryGetValue(key, out var value))
			{
				return "";
			}
			return value;
		}
		return "unknown op";
	}

	public static int Send(string op, string k, string v = null)
	{
		int num;
		lock (replies)
		{
			num = ++nextId;
		}
		Net.ToServer("ping.lab", new PingLabCmd
		{
			id = num,
			op = op,
			k = k,
			v = v
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

	public static int PingFramesInLog(out int total)
	{
		total = 0;
		int num = 0;
		byte[] bytes = Encoding.UTF8.GetBytes("{\"k\":\"ping.");
		foreach (byte[] frame in DayLog.Frames)
		{
			total++;
			if (frame == null || frame.Length < 4 + bytes.Length || (frame[0] & 0x80) != 0)
			{
				continue;
			}
			bool flag = true;
			for (int i = 0; i < bytes.Length; i++)
			{
				if (frame[4 + i] != bytes[i])
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				num++;
			}
		}
		return num;
	}

	public unsafe static string KeysFree(out bool ok)
	{
		ok = true;
		List<string> list = new List<string>();
		KeyCode pointKey = Pointing.PointKey;
		string text = Pointing.GameBindUsing(pointKey);
		if (text != null)
		{
			ok = false;
			list.Add("game key bind '" + text + "' uses " + pointKey.ToString());
		}
		else
		{
			list.Add("no game key bind uses " + pointKey.ToString() + " (" + KeyBindHelper.Bindings.Count + " binds)");
		}
		if (Chat.ChatKey == pointKey)
		{
			ok = false;
			list.Add("the chat key is the point key");
		}
		if (!Pointing.KeyboardKey(pointKey))
		{
			ok = false;
			list.Add("the point key " + pointKey.ToString() + " is not a keyboard key");
		}
		KeyCode pointKey2 = Pointing.PointKey;
		Pointing.PointKey = (KeyCode)325;
		if (Pointing.PointKey != pointKey2)
		{
			ok = false;
			Pointing.PointKey = pointKey2;
			list.Add("the point key took the middle mouse button (put back to " + pointKey2.ToString() + ")");
		}
		else
		{
			list.Add("the point key refuses the middle mouse button (stays " + pointKey2.ToString() + ")");
		}
		InputActionManager current = InputActionManager.current;
		if ((Object)(object)current == (Object)null)
		{
			list.Add("no InputActionManager in this scene");
		}
		else
		{
			List<string> list2 = new List<string>();
			foreach (InputActionHandler actionHandler in current.actionHandlers)
			{
				if (!((Object)(object)actionHandler == (Object)null))
				{
					List<KeyCode> keyListeners;
					try
					{
						keyListeners = actionHandler.keyListeners;
					}
					catch
					{
						continue;
					}
					if (keyListeners != null && keyListeners.Contains(pointKey))
					{
						list2.Add(((object)actionHandler).GetType().Name);
					}
				}
			}
			if (list2.Count > 0)
			{
				ok = false;
				list.Add("input handlers on " + pointKey.ToString() + ": " + string.Join(",", list2));
			}
			else
			{
				list.Add("no input handler on " + pointKey.ToString() + " (" + current.actionHandlers.Count + " handlers)");
			}
		}
		list.Add(AnyButtonControls());
		return string.Join("; ", list);
	}

	private static string AnyButtonControls()
	{
		try
		{
			int num = 0;
			SortedDictionary<string, int> sortedDictionary = new SortedDictionary<string, int>(StringComparer.Ordinal);
			TreeNodeRender[] array = Object.FindObjectsOfType<TreeNodeRender>();
			foreach (TreeNodeRender val in array)
			{
				MultiEventHandler val2 = (((Object)(object)val != (Object)null && ((Component)val).gameObject.activeInHierarchy) ? val.eventHandler : null);
				if (val2 != null && val2.mouseDownFunc.Count + val2.mouseClickFunc.Count + val2.mouseUpFunc.Count != 0)
				{
					num++;
					string key = (((object)val.FindParentElement()) ?? ((object)val.FindParentWindow()))?.GetType().Name ?? "node";
					sortedDictionary[key] = ((!sortedDictionary.TryGetValue(key, out var value)) ? 1 : (value + 1));
				}
			}
			return "middle button: " + num + " game node(s) drawn now run their click handlers on any mouse button (" + string.Join(", ", sortedDictionary.Select((KeyValuePair<string, int> kv) => kv.Key + " x" + kv.Value)) + "), so no mouse button points";
		}
		catch (Exception ex)
		{
			return "middle button: node survey failed (" + ex.GetType().Name + ")";
		}
	}
}
