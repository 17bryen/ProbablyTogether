using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

public static class CrewLab
{
	public static readonly Dictionary<string, long> Fx = new Dictionary<string, long>(StringComparer.Ordinal);

	public static readonly List<string> Marks = new List<string>();

	private static bool wired;

	public const int Deals = 0;

	public const int Bought = 1;

	public const int Spent = 2;

	public const int Sold = 3;

	public const int Earned = 4;

	public const int Caught = 5;

	public const int Repairs = 6;

	public const int Uses = 7;

	public const int Made = 8;

	public const int Any = -1;

	public const int Plus = -2;

	public static readonly int[] WantA = new int[9] { 1, 0, 0, 1, -2, 0, 0, 0, 0 };

	public static readonly int[] WantB = new int[9] { 0, 0, 0, 0, 0, 0, 1, 1, 1 };

	public static readonly int[] AnyTimeA = new int[9] { -1, 0, 0, -1, -1, 0, 0, 0, 0 };

	public static readonly int[] AnyTimeB = new int[9] { 0, 0, 0, 0, 0, 0, -1, -1, -1 };

	private static readonly string[] Figures = new string[9] { "deals", "bought", "spent", "sold", "earned", "caught", "repairs", "uses", "made" };

	internal static void Wire()
	{
		if (!wired)
		{
			wired = true;
			Net.On<MsgCrewLab>("crew.lab", OnServerLab, null);
		}
	}

	private static void OnServerLab(MsgCrewLab m, int from)
	{
		if (m == null || m.op == null || !Net.IsServer)
		{
			return;
		}
		string op = m.op;
		if (!(op == "mark"))
		{
			if (op == "queue")
			{
				if (m.a != "merchant")
				{
					BotLog.Info("crew-server", "queue refused: only merchant");
					return;
				}
				BotLog.Info("crew-server", "queue merchant for " + Net.PlayerName(from) + ": " + Customers.BotDo("add", "merchant"));
			}
			else
			{
				BotLog.Info("crew-server", "unknown lab op '" + m.op + "' from " + Net.PlayerName(from));
			}
		}
		else
		{
			if (m.a != null && m.a.Length < 48 && !Marks.Contains(m.a) && Marks.Count < 64)
			{
				Marks.Add(m.a);
			}
			BotLog.Info("crew-server", "mark " + m.a + " from " + Net.PlayerName(from));
		}
	}

	public static GameItem Item(string k)
	{
		if (!Fx.TryGetValue(k, out var value) || value == 0L)
		{
			return null;
		}
		return Items.Find(value);
	}

	public static string FxText()
	{
		return string.Join(";", from kv in Fx.OrderBy<KeyValuePair<string, long>, string>((KeyValuePair<string, long> kv) => kv.Key, StringComparer.Ordinal)
			select kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture));
	}

	public static int ParseFx(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0;
		}
		string[] array = text.Split(';');
		foreach (string text2 in array)
		{
			int num = text2.IndexOf('=');
			if (num > 0 && long.TryParse(text2.Substring(num + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				Fx[text2.Substring(0, num)] = result;
			}
		}
		return Fx.Count;
	}

	public static int CageHealth()
	{
		GameItem val = Item("cage");
		if (val != null)
		{
			return val.GetTagReadonly("CAGE_HEALTH_TAG").GetInt();
		}
		return -1;
	}

	public static int PrinterOut()
	{
		GameItem val = Item("printer");
		GameInventory val2 = ((val != null) ? MachineHelper.GetSlot(val, 1) : null);
		if (val2 == null)
		{
			return -1;
		}
		return val2.childItems.Count;
	}

	internal static string Spawn()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		PlayerStore store = Game.Store;
		List<string> list = new List<string>();
		using (Net.Apply())
		{
			for (int i = 0; i < 2; i++)
			{
				list.Add(Put("fl" + i, DirectoryMaster.Item("flashlight", true), instance));
			}
			GameItem val = DirectoryMaster.Item("bottle_printer", true);
			list.Add(Put("printer", val, instance));
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				GeneralHelper.SetItemOwned(item, true);
			}
			GameItem val2 = PreBuiltItemHelper.EnergyCredit(50);
			GraphUtils.TryAcceptAll((GraphNodeStorage)(object)MachineHelper.GetBatterySlot(val), val2, -1);
			GeneralHelper.SetItemOwned(val2, true);
			GameItem val3 = DirectoryMaster.Item("animal_cage", true);
			list.Add(Put("cage", val3, instance));
			val3.ModifyTag("CAGE_HEALTH_TAG", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(40);
			}, false);
			list.Add(Put("scrap", DirectoryMaster.Item("scrap_metal", true), instance));
			store.isBargainIntroduced = true;
		}
		return string.Join(", ", list) + "; cage health " + CageHealth() + ", bargain buttons on";
	}

	private static string Put(string key, GameItem it, EmporiumEntry e)
	{
		if (it == null)
		{
			throw new InvalidOperationException("the game has no item for " + key);
		}
		string text = null;
		GameGridInventory[] array = (GameGridInventory[])(object)new GameGridInventory[3] { e.invElement, e.hirelingInv, e.soldElement };
		foreach (GameGridInventory val in array)
		{
			if (val != null && Place(it, val))
			{
				text = ((val == e.invElement) ? "inv" : ((val == e.hirelingInv) ? "hireling" : "sold"));
				break;
			}
		}
		if (text == null)
		{
			it.Destroy();
			throw new InvalidOperationException("no free cell for " + key);
		}
		GeneralHelper.SetItemOwned(it, true);
		Fx[key] = Ids.Of(it);
		return key + " " + it.identifier + " #" + Fx[key] + " in " + text;
	}

	private static bool Place(GameItem it, GameGridInventory inv)
	{
		if (((inv != null) ? inv.inventoryShape : null) == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			for (int num = 4; num >= 1; num--)
			{
				for (int j = 0; j < inv.inventoryShape.height; j++)
				{
					for (int k = 0; k < inv.inventoryShape.width; k++)
					{
						GridShapeBuilder val = new GridShapeBuilder(it.modifiedShape).SetTransform(k, j, i == 1, num);
						SlotMarker val2 = ((GameInventory)inv).TryInventorySlot(it, it.unitCount, (GridShape)(object)val, (TagSystem)null);
						if (val2 != null && val2.targetItem == null && (object)val2.inventory == inv && val2.numTransfer >= it.unitCount)
						{
							val2.TryAcceptOnce(-1);
							return (object)it.parentInventory == inv;
						}
					}
				}
			}
		}
		return false;
	}

	public static int[] Row(string canon, string name)
	{
		if (string.IsNullOrEmpty(canon))
		{
			return null;
		}
		string[] array = canon.Split(';');
		foreach (string text in array)
		{
			if (text.StartsWith(name + ":", StringComparison.Ordinal))
			{
				string[] array2 = text.Substring(name.Length + 1).Split(',');
				if (array2.Length != 9)
				{
					return null;
				}
				int result;
				return array2.Select((string x) => (!int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)) ? (-1) : result).ToArray();
			}
		}
		return null;
	}

	public static string Diff(string canon, string name, int[] want, bool mustExist = true)
	{
		int[] array = Row(canon, name);
		if (array == null)
		{
			if (!mustExist)
			{
				return "";
			}
			return name + " has no row";
		}
		List<string> list = new List<string>();
		for (int i = 0; i < 9; i++)
		{
			int num = want[i];
			if (num != -1 && ((num == -2) ? (array[i] <= 0) : (array[i] != num)))
			{
				list.Add(Figures[i] + " " + array[i] + " (want " + ((num == -2) ? "> 0" : num.ToString(CultureInfo.InvariantCulture)) + ")");
			}
		}
		if (list.Count != 0)
		{
			return name + ": " + string.Join(", ", list);
		}
		return "";
	}

	public static string DiffDay(string canon)
	{
		List<string> list = new List<string>();
		string text = Diff(canon, CrewBot.MeName, WantA);
		string text2 = Diff(canon, CrewBot.PartnerName, WantB);
		if (text.Length > 0)
		{
			list.Add(text);
		}
		if (text2.Length > 0)
		{
			list.Add(text2);
		}
		string[] source = (canon ?? "").Split(';');
		int num = source.Count((string x) => x.Length > 0 && !x.StartsWith("d=", StringComparison.Ordinal) && !x.StartsWith("best=", StringComparison.Ordinal));
		if (num != 2)
		{
			list.Add(num + " rows (want 2)");
		}
		string text3 = source.FirstOrDefault((string x) => x.StartsWith("best=", StringComparison.Ordinal)) ?? "";
		if (text3 != "best=-")
		{
			string[] array = (text3.StartsWith("best=" + CrewBot.MeName + ":", StringComparison.Ordinal) ? text3.Substring(("best=" + CrewBot.MeName + ":").Length).Split(':') : new string[0]);
			if (array.Length != 2 || !int.TryParse(array[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || result <= 0 || !(array[1] == "1"))
			{
				list.Add("best '" + text3 + "' (want best=- or " + CrewBot.MeName + ":<gain above 0>:1)");
			}
		}
		return string.Join("; ", list);
	}

	internal static string BestSelfTest()
	{
		CrewDeals d = new CrewDeals();
		List<string> steps = new List<string>();
		bool ok = true;
		T("bot1|v1", 3, "bot1:3");
		T("bot1|v2", 6, "bot1:6");
		T("bot1|v2", -8, "bot1:3");
		T("bot1|v2", 3, "bot1:3");
		T("bot2|v3", 3, "bot1:3");
		T("bot1|v1", 0, "bot1:3");
		T("bot1|v1", -4, "bot2:3");
		T("bot2|v3", -3, "bot1:1");
		T("bot1|v2", -1, "-");
		return (ok ? "ok: " : "FAIL: ") + string.Join(" ", steps);
		void T(string visit, int gain, string want)
		{
			string text = (visit.StartsWith("bot2", StringComparison.Ordinal) ? "bot2" : "bot1");
			d.Trade(visit, (!(text == "bot2")) ? 1 : 2, text, "Customer", gain);
			string text2 = ((d.Best == null) ? "-" : (d.Best.Name + ":" + d.Best.Gain.ToString(CultureInfo.InvariantCulture)));
			steps.Add(visit + ((gain >= 0) ? "+" : "") + gain.ToString(CultureInfo.InvariantCulture) + "=>" + text2);
			if (text2 != want)
			{
				ok = false;
				steps.Add("(want " + want + ")");
			}
		}
	}

	public static MsgCrewTally WideTable(int day)
	{
		MsgCrewTally msgCrewTally = new MsgCrewTally
		{
			day = day,
			seq = 900000,
			why = "lab",
			rows = new List<MsgCrewRow>()
		};
		for (int i = 1; i <= 8; i++)
		{
			msgCrewTally.rows.Add(new MsgCrewRow
			{
				p = 100 + i,
				n = "Wanderer " + i,
				on = (i <= 6),
				deals = i,
				bought = i,
				spent = 1000 * i,
				sold = i + 1,
				earned = 1500 * i,
				caught = i,
				repairs = i,
				uses = 2 * i,
				made = 3 * i
			});
		}
		msgCrewTally.best = new MsgCrewBest
		{
			p = 103,
			n = "Wanderer 3",
			gain = 120,
			items = 2,
			who = "Merchant"
		};
		return msgCrewTally;
	}
}
