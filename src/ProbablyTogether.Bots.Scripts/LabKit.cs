using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class MsgLab
{
	public string op;

	public string a;

	public string b;

	public string c;

	public Seed seed;
}

/// Edits the shop's state: asked of the server, applied there, replayed on every copy and logged for late joiners.
///   set <path> <value>   a field of the shop, e.g. set playerCash 5000, set health.woundState 2,
///                         set clients.isJacksonIntroduced true, set station.dayCounter 12
///   give <item> [n] [inv|back|showcase|hidden]
///   rentpaid              the game's own rent-paid step, on every copy
///   save                  the server writes its save slot now
public static class LabKit
{
	private static bool wired;

	/// Edits applied on this copy so far, and what the last one did.
	public static int Applied;

	public static string Last = "";

	internal static void Wire()
	{
		if (wired)
		{
			return;
		}
		wired = true;
		Net.On("lab.do", delegate(MsgLab m, int from)
		{
			ServerOp(m.op, m.a, m.b, m.c, from);
		}, null);
		Net.On("lab.did", null, delegate(MsgLab m)
		{
			using (Replay.Client(m.seed))
			{
				Last = Run(m.op, m.a, m.b, m.c);
				Applied++;
			}
		});
	}

	public static void Do(string op, string a = null, string b = null, string c = null)
	{
		if (Net.IsClient)
		{
			Net.ToServer("lab.do", new MsgLab
			{
				op = op,
				a = a,
				b = b,
				c = c
			});
		}
		else
		{
			ServerOp(op, a, b, c, 0);
		}
	}

	/// One line of a setup file or a -Setup switch, e.g. set playerCash 5000.
	public static void DoLine(string line)
	{
		string[] array = (line ?? "").Trim().Split(new char[2] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length != 0 && !array[0].StartsWith("#", StringComparison.Ordinal))
		{
			Do(array[0], (array.Length > 1) ? array[1] : null, (array.Length > 2) ? array[2] : null, (array.Length > 3) ? array[3] : null);
		}
	}

	private static void ServerOp(string op, string a, string b, string c, int from)
	{
		if (!Net.IsServer)
		{
			return;
		}
		if (op == "save")
		{
			Last = ServerMode.SaveCommand();
			Applied++;
			Log.Info("lab: save -> " + Last);
			return;
		}
		Seed seed;
		string text;
		using (Replay.Server(out seed))
		{
			text = Run(op, a, b, c);
		}
		Last = text;
		Applied++;
		Net.ToAll("lab.did", new MsgLab
		{
			op = op,
			a = a,
			b = b,
			c = c,
			seed = seed
		});
		Log.Info("lab: " + op + " " + a + " " + b + " " + c + " -> " + text + ((from > 0) ? (" (asked by " + Net.PlayerName(from) + ")") : ""));
	}

	private static string Run(string op, string a, string b, string c)
	{
		try
		{
			using (Net.Apply())
			{
				switch (op)
				{
				case "set":
					return Set(a, b);
				case "give":
					return Give(a, b, c);
				case "rentpaid":
				{
					int playerCash = Game.Store.playerCash;
					Game.Store.OnRentPaid();
					return "rent paid: cash " + playerCash + " -> " + Game.Store.playerCash;
				}
				default:
					return "unknown op '" + op + "'";
				}
			}
		}
		catch (Exception ex)
		{
			return "threw " + ex.GetType().Name + ": " + ex.Message;
		}
	}

	// The game's mod scanner refuses reflection on computed types, so every lookup names its type outright.
	// A path is <field> on the shop, or <part>.<field> with part = health, clients or station.
	private static FieldInfo Find(ref string path, out object on)
	{
		PlayerStore store = Game.Store;
		string text = path ?? "";
		int num = text.IndexOf('.');
		string text2 = ((num < 0) ? "store" : text.Substring(0, num));
		string text3 = ((num < 0) ? text : text.Substring(num + 1));
		path = text3;
		switch (text2)
		{
		case "health":
		case "healthData":
			on = store?.healthData;
			return AccessTools.Field(typeof(HealthData), text3);
		case "clients":
		case "storeClientData":
			on = PlayerStore.GetStoreClientData();
			return AccessTools.Field(typeof(StoreClientData), text3);
		case "station":
			on = StoreStation.Instance;
			return AccessTools.Field(typeof(StoreStation), text3);
		case "store":
			on = store;
			return AccessTools.Field(typeof(PlayerStore), text3);
		default:
			on = null;
			return null;
		}
	}

	/// Reads a field by path, e.g. health.woundState; empty when it does not exist.
	public static string Get(string path)
	{
		try
		{
			string path2 = path;
			FieldInfo fieldInfo = Find(ref path2, out var on);
			if (fieldInfo == null || on == null)
			{
				return "";
			}
			return Convert.ToString(fieldInfo.GetValue(on), CultureInfo.InvariantCulture) ?? "";
		}
		catch
		{
			return "";
		}
	}

	private static string Set(string path, string value)
	{
		string path2 = path;
		FieldInfo fieldInfo = Find(ref path2, out var on);
		if (fieldInfo == null || on == null)
		{
			return "no field '" + path + "' (paths: <field>, health.<field>, clients.<field>, station.<field>)";
		}
		Type fieldType = fieldInfo.FieldType;
		object value2;
		if (fieldType == typeof(string))
		{
			value2 = value;
		}
		else if (fieldType == typeof(bool))
		{
			value2 = value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
		}
		else if (fieldType == typeof(int))
		{
			value2 = int.Parse(value, CultureInfo.InvariantCulture);
		}
		else if (fieldType == typeof(long))
		{
			value2 = long.Parse(value, CultureInfo.InvariantCulture);
		}
		else if (fieldType == typeof(float))
		{
			value2 = float.Parse(value, CultureInfo.InvariantCulture);
		}
		else if (fieldType == typeof(double))
		{
			value2 = double.Parse(value, CultureInfo.InvariantCulture);
		}
		else
		{
			if (!fieldType.IsEnum)
			{
				return "'" + path + "' is a " + fieldType.Name + ": only numbers, true/false, text and numbered choices can be set";
			}
			value2 = Enum.ToObject(fieldType, int.Parse(value, CultureInfo.InvariantCulture));
		}
		object value3 = fieldInfo.GetValue(on);
		fieldInfo.SetValue(on, value2);
		return path + ": " + Convert.ToString(value3, CultureInfo.InvariantCulture) + " -> " + Convert.ToString(fieldInfo.GetValue(on), CultureInfo.InvariantCulture);
	}

	private static string Give(string id, string count, string where)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null || string.IsNullOrEmpty(id))
		{
			return "give needs an item and a loaded shop";
		}
		int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result);
		result = Mathf.Clamp(result, 1, 50);
		GraphNodeStorage val;
		switch (where ?? "inv")
		{
		case "back":
			val = (GraphNodeStorage)(object)instance.backInvinvElement;
			break;
		case "showcase":
			val = (GraphNodeStorage)(object)instance.showcaseElement;
			break;
		case "hidden":
			val = (GraphNodeStorage)(object)instance.hiddenElement;
			break;
		default:
			val = (GraphNodeStorage)(object)instance.invElement;
			break;
		}
		int num = 0;
		for (int i = 0; i < result; i++)
		{
			GameItem val2 = ItemSpawner.Spawn(id);
			if (val2 == null)
			{
				return "no item '" + id + "'";
			}
			GeneralHelper.SetItemOwned(val2, true);
			if (GraphUtils.TryAccept(val, val2) != -1)
			{
				num++;
			}
			else
			{
				val2.Destroy();
			}
		}
		return num + " x " + id + " into " + (where ?? "inv") + ((num < result) ? (" (" + (result - num) + " did not fit)") : "");
	}

	/// This copy's setup lines: lab-setup.txt in its data folder.
	internal static string[] SetupLines()
	{
		try
		{
			string text = Path.Combine(Args.Data ?? "", "lab-setup.txt");
			return File.Exists(text) ? File.ReadAllLines(text) : new string[0];
		}
		catch
		{
			return new string[0];
		}
	}
}

public sealed class LabProbes : BotProbeSet
{
	public override void Register()
	{
		LabKit.Wire();
		BotProbes.Add("lab.applied", () => BotProbeSet.Str(LabKit.Applied));
		BotProbes.Add("lab.wound", () => LabKit.Get("healthData.woundState"));
	}
}

/// Runs the shop's setup lines on the server (or host) once the shop is up. Put it first: --br-bot=lab-setup,<the test>.
public sealed class LabSetup : BotScript
{
	private int sent;

	public override string Name => "lab-setup";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 400f;

	protected override void Build()
	{
		WaitFor("server", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => BotProbes.InShop && Net.IsLive && Game.ShopLoaded, () => "scene " + BotProbes.Scene);
		Do("setup", delegate
		{
			List<string> list = new List<string>();
			string[] array = LabKit.SetupLines();
			foreach (string text in array)
			{
				if (text.Trim().Length != 0 && !text.TrimStart().StartsWith("#", StringComparison.Ordinal))
				{
					LabKit.DoLine(text);
					sent++;
					list.Add(text.Trim() + " => " + LabKit.Last);
				}
			}
			return (sent == 0) ? "no lab-setup.txt lines" : string.Join(" | ", list);
		});
		WaitFor("applied", 20f, () => LabKit.Applied >= sent, () => LabKit.Applied + " of " + sent + " edit(s) applied; last: " + LabKit.Last);
		Do("state", () => "day " + LabKit.Get("station.dayCounter") + ", cash " + LabKit.Get("playerCash") + ", wound " + LabKit.Get("healthData.woundState") + ", " + Items.All().Count + " items");
	}
}

/// A client that joins, must end up with the same shop as the server, and reports what it sees.
public sealed class LabCheck : DayNightBotBase
{
	public override string Name => "lab-check";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 400f;

	protected override void Build()
	{
		JoinSteps();
		Delay("settle", 3f);
		Agree("same-as-server", 40f, "core.digest", "items.digest", "cash", "day", "dn.state", "lab.wound");
		Do("state", () => "day " + LabKit.Get("station.dayCounter") + ", cash " + LabKit.Get("playerCash") + ", wound " + LabKit.Get("healthData.woundState") + ", " + Items.All().Count + " items");
	}
}
