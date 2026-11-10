using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CarryProbes : BotProbeSet
{
	internal const string BagTag = "BR_CARRY_BAG";

	internal const string StackTag = "BR_CARRY_STACK";

	internal static readonly List<string> Marks = new List<string>();

	private static bool wired;

	private static bool spawned;

	public override void Register()
	{
		BotProbes.Add("carry.marks", () => string.Join(",", Marks));
		BotProbes.Add("carry.live", () => (!Net.IsServer) ? "" : Str(Net.Players.Keys.Count(Net.IsPlayerLive)));
		BotProbes.Add("carry.srv", () => (!Net.IsServer) ? "" : string.Join(";", from k in Net.Players.Keys
			orderby k
			select "p" + k + ":" + Carry.ServerDescribe(k)));
		BotProbes.Add("carry.counts", () => (!Net.IsServer) ? "" : ("invalid=" + Carry.ServerInvalid + " hidden=" + Carry.ServerHidden + " cleared=" + Carry.ServerCleared));
		if (!wired)
		{
			wired = true;
			Net.On<CarryBotMsg>("carry.bot", OnServer, null);
		}
	}

	private static void OnServer(CarryBotMsg m, int from)
	{
		if (m == null)
		{
			return;
		}
		if (m.op == "mark")
		{
			string text = Clean(m.a);
			if (text.Length != 0 && Marks.Count < 300)
			{
				Marks.Add(from.ToString(CultureInfo.InvariantCulture) + ":" + text);
				BotLog.Info("carry-server", "mark " + from + ":" + text);
			}
		}
		else
		{
			if (!(m.op == "spawn"))
			{
				return;
			}
			if (!spawned && Items.Ready && !Items.All().Any((GameItem i) => i.IsTag("BR_CARRY_BAG")))
			{
				spawned = true;
				EmporiumEntry e = EmporiumEntry.Instance;
				bool flag = Items.ServerAction("carry.spawn", delegate
				{
					Place((GameInventory)(object)e.invElement, "backpack_small", "BR_CARRY_BAG", 1);
					Place((GameInventory)(object)e.invElement, "glass_shard", "BR_CARRY_STACK", 3);
				});
				BotLog.Info("carry-server", "test bag for " + Net.PlayerName(from) + ": " + (flag ? "sent" : "NOTHING SENT"));
			}
		}
	}

	private static void Place(GameInventory inv, string ident, string tag, int count)
	{
		GameItem val = DirectoryMaster.Item(ident, true);
		val.EnableTag(tag, false);
		SlotMarker val2 = inv.TryFindOneValidInventorySlot(val);
		if (val2?.itemGridShape == null)
		{
			BotLog.Info("carry-server", "no room for " + ident);
			val.Destroy();
		}
		else
		{
			val.unitCount = count;
			val.modifiedShape = (GridShape)(object)new GridShapeBuilder(val.modifiedShape).SetTransform(val2.itemGridShape);
			inv.UncheckedAccept(val);
		}
	}

	private static string Clean(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		if (s.Length > 64)
		{
			s = s.Substring(0, 64);
		}
		string text = s;
		foreach (char c in text)
		{
			if (!char.IsLetterOrDigit(c) && c != '-' && c != ':' && c != '_')
			{
				return "";
			}
		}
		return s;
	}

	private new static string Str(object o)
	{
		return Convert.ToString(o, CultureInfo.InvariantCulture);
	}
}
