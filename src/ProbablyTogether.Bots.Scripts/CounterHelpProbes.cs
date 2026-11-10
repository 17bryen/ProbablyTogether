using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CounterHelpProbes : BotProbeSet
{
	private static bool wired;

	internal static int Serial;

	internal static string LastResult = "";

	internal static readonly SortedDictionary<long, string> RealViews = new SortedDictionary<long, string>();

	internal static readonly Dictionary<string, long> Lab = new Dictionary<string, long>(StringComparer.Ordinal);

	public override void Register()
	{
		BotProbes.Add("help.views", () => CounterHelp.ViewsText((RealViews.Count > 0) ? RealViews : CounterHelp.Views));
		BotProbes.Add("help.count", () => (!Net.IsServer) ? BotProbeSet.Str(CounterHelp.Received) : BotProbeSet.Str(CounterHelp.Announced));
		BotProbes.Add("help.strips", () => (!Game.ShopLoaded) ? "" : BotProbeSet.Str(SpentStrips()));
		BotProbes.Add("help.ops", () => BotProbeSet.Str(Serial));
		BotProbes.Add("help.goods", () => (!Game.ShopLoaded) ? "" : GoodsText());
		BotProbes.Add("help.srv", () => Net.IsServer ? ("admitted " + CounterHelp.Admitted + ", rate-refused " + CounterHelp.RateRefused + ", announced " + CounterHelp.Announced) : "");
		if (!wired)
		{
			wired = true;
			Net.On<MsgHelpBot>("help.bot.do", null, ClientOp);
		}
	}

	internal static int SpentStrips()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if (((instance != null) ? instance.invElement : null) == null)
		{
			return -1;
		}
		int num = 0;
		foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)instance.invElement, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
		{
			if (item != null && item.identifier == "water_test_strip" && item.IsTag("IS_SPENT"))
			{
				num++;
			}
		}
		return num;
	}

	internal static GameItem LabItem(string role)
	{
		if (!Lab.TryGetValue(role, out var value))
		{
			return null;
		}
		return Ids.Find(value);
	}

	private static string GoodsText()
	{
		List<string> list = new List<string>();
		string[] array = new string[5] { "water", "water2", "cig", "seed", "ours" };
		foreach (string text in array)
		{
			GameItem val = LabItem(text);
			list.Add(text + "=" + ((val == null) ? "none" : (val.uniqueId + (ItemsUse.InCounter(val) ? "@counter" : ("@" + ((val.parentInventory == null) ? "gone" : "elsewhere"))) + (GeneralHelper.IsItemOwned(val) ? ":own" : ":cust"))));
		}
		return string.Join(" ", list);
	}

	internal static string Do(string op)
	{
		if (!Net.IsServer || !Game.ShopLoaded)
		{
			return "not the server's shop";
		}
		List<GameItem> list = new List<GameItem>();
		List<string> roles = new List<string>();
		Seed seed;
		string text;
		using (Replay.Server(out seed))
		{
			text = Run(op, list, roles);
		}
		MsgHelpBot msgHelpBot = new MsgHelpBot
		{
			op = op,
			seed = seed,
			created = ((IEnumerable<GameItem>)list).Select((Func<GameItem, long>)((GameItem x) => x.uniqueId)).ToArray(),
			result = text,
			serial = ++Serial
		};
		LastResult = text;
		Net.ToAll("help.bot.do", msgHelpBot);
		BotLog.Info("counterhelp", "op " + op + ": " + text + "; ids " + string.Join(",", msgHelpBot.created));
		return text;
	}

	private static void ClientOp(MsgHelpBot m)
	{
		if (!Net.IsClient || m == null || m.op == null || !Game.ShopLoaded)
		{
			return;
		}
		List<GameItem> list = new List<GameItem>();
		List<string> list2 = new List<string>();
		string text;
		using (Replay.Client(m.seed))
		{
			text = Run(m.op, list, list2);
		}
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			if (m.created == null)
			{
				break;
			}
			if (i >= m.created.Length)
			{
				break;
			}
			if (list[i].uniqueId != m.created[i])
			{
				Ids.Bind(list[i], m.created[i]);
				num++;
			}
			if (i < list2.Count)
			{
				Lab[list2[i]] = m.created[i];
			}
		}
		Serial = m.serial;
		LastResult = text;
		BotLog.Info("counterhelp", "op " + m.op + ": " + text + " here, server " + m.result + "; " + num + " id(s) bound (0 = the seeded replay made the same ids)");
	}

	private static string Run(string op, List<GameItem> made, List<string> roles)
	{
		PlayerStore instance = PlayerStore.Instance;
		EmporiumEntry instance2 = EmporiumEntry.Instance;
		if (!(op == "tools"))
		{
			if (op == "goods")
			{
				List<string> list = new List<string>();
				Place("water", WaterPremadeHelper.GhostWaterAsPureWater("bottled_water"), ours: false, instance, instance2, made, roles, list);
				Place("water2", WaterPremadeHelper.GhostWaterAsPureWater("bottled_water"), ours: false, instance, instance2, made, roles, list);
				Place("cig", PreBuiltItemHelper.FakeCigarette(), ours: false, instance, instance2, made, roles, list);
				Place("seed", DirectoryMaster.Item("fillerweed_seed", true), ours: false, instance, instance2, made, roles, list);
				Place("ours", DirectoryMaster.Item("flashlight", true), ours: true, instance, instance2, made, roles, list);
				return "goods " + string.Join(",", list);
			}
			return "unknown op " + op;
		}
		List<string> list2 = new List<string>();
		string[] array = new string[7] { "magnifier", "aquascan", "chem_scanner", "water_test_strip", "water_test_strip", "plant_analyser", "labeler_blue" };
		foreach (string text in array)
		{
			GameItem val = DirectoryMaster.Item(text, true);
			if (val == null)
			{
				list2.Add(text + ":missing");
				continue;
			}
			GeneralHelper.SetItemOwned(val, true);
			int num = GraphUtils.TryAcceptAll((GraphNodeStorage)(object)instance2.invElement, val, -1);
			string text2 = (roles.Contains(text) ? (text + "2") : text);
			made.Add(val);
			roles.Add(text2);
			Lab[text2] = val.uniqueId;
			list2.Add(text + ((val.parentInventory != null) ? "" : (":NOT-PLACED(" + num + ")")));
		}
		return "tools " + string.Join(",", list2);
	}

	private static void Place(string role, GameItem it, bool ours, PlayerStore ps, EmporiumEntry e, List<GameItem> made, List<string> roles, List<string> rows)
	{
		if (it == null)
		{
			rows.Add(role + ":missing");
			return;
		}
		if (!ps.TryAddDirectSellingItemToTable(it, ours, false, false, 0) || it.parentInventory == null)
		{
			GeneralHelper.SetItemOwned(it, ours);
			GraphUtils.TryAcceptAll((GraphNodeStorage)(object)e.backInvinvElement, it, -1);
		}
		made.Add(it);
		roles.Add(role);
		Lab[role] = it.uniqueId;
		string text = (((object)it.parentInventory == e.frontInvinvElement) ? "front" : (((object)it.parentInventory == e.backInvinvElement) ? "back" : ((it.parentInventory == null) ? "NOT-PLACED" : "elsewhere")));
		rows.Add(role + ":" + it.identifier + "#" + it.uniqueId + "@" + text);
	}
}
