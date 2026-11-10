using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Bots.Scripts;

public sealed class FenceGroupA : ItemsBotBase
{
	private static readonly FieldInfo fDigestMismatches = AccessTools.Field(typeof(Digest), "mismatches");

	private static readonly (string id, int nth)[] Order = new(string, int)[7]
	{
		("heavy_pistol_ammo", 0),
		("heavy_pistol_ammo", 1),
		("med_keycard", 0),
		("eng_keycard", 0),
		("sec_keycard", 0),
		("dream_dust", 0),
		("dream_dust", 1)
	};

	private long caseId;

	private long pistolId;

	private long medId;

	private long engId;

	private readonly List<long> group = new List<long>();

	private readonly List<int> groupQ = new List<int>();

	private int qPistol;

	private int qMed;

	private int qEng;

	private int worstDigest;

	public override string Name => "fence-group-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	private static GameItem I(long id)
	{
		return Items.Find(id);
	}

	private static GameItem ByName(string ident, int nth)
	{
		return (from i in Items.All()
			where i.identifier == ident
			select i).OrderBy(Ids.Of).Skip(nth).FirstOrDefault();
	}

	private static int DigestMismatches()
	{
		try
		{
			return (int)fDigestMismatches.GetValue(null);
		}
		catch
		{
			return -1;
		}
	}

	private static string Where(GameItem it)
	{
		if (it == null || Items.IsGone(it))
		{
			return "gone";
		}
		GridShape modifiedShape = it.modifiedShape;
		string text = ((modifiedShape == null) ? "?" : (modifiedShape.minX + "," + modifiedShape.minY + ((modifiedShape.orientation != 0) ? (" r" + modifiedShape.orientation) : "") + (modifiedShape.flipped ? " f" : "") + " " + modifiedShape.globalWidth + "x" + modifiedShape.globalHeight));
		return it.identifier + "#" + Ids.Of(it) + " x" + it.unitCount + " " + (Items.RefOf((GraphNodeStorage)(object)it.parentInventory)?.ToString() ?? "-") + "@" + text;
	}

	private string Layout()
	{
		List<string> list = new List<string>();
		GameItem val = I(caseId);
		if (val != null)
		{
			PixelWindow contentWindow = val.contentWindow;
			GraphNodeStorage obj = ((contentWindow != null) ? contentWindow.child : null);
			GameGridInventory val2 = (GameGridInventory)(object)((obj is GameGridInventory) ? obj : null);
			list.Add("case " + Where(val) + ((((val2 != null) ? val2.inventoryShape : null) != null) ? (" grid " + val2.inventoryShape.width + "x" + val2.inventoryShape.height) : "") + " holds [" + string.Join("; ", (((val2 != null) ? ((GameInventory)val2).childItems : null) ?? new List<GameItem>()).Select(Where)) + "]");
		}
		list.Add("pistol " + Where(I(pistolId)));
		list.Add("group " + string.Join("; ", group.Select((long id) => Where(I(id)))));
		return string.Join(" | ", list);
	}

	private string Outcomes()
	{
		List<string> list = new List<string>();
		for (int i = 0; i < group.Count; i++)
		{
			GameItem obj = I(group[i]);
			list.Add((((obj != null) ? obj.identifier : null) ?? ("#" + group[i])) + "=" + ItemsMove.Outcome(groupQ[i]));
		}
		list.Add("late med=" + ItemsMove.Outcome(qMed) + " \"" + ItemsMove.RejectTextOf(qMed) + "\"");
		list.Add("late eng=" + ItemsMove.Outcome(qEng) + " \"" + ItemsMove.RejectTextOf(qEng) + "\"");
		return string.Join(", ", list);
	}

	protected override void Build()
	{
		JoinSteps();
		DigestSame("join-digest");
		Step("find", 30f, delegate(BotStep st)
		{
			GameItem pistol = ByName("heavy_handmade_pistol", 0);
			if (pistol == null)
			{
				return StepStatus.Running;
			}
			GameItem val = I(12L);
			if (val == null || val.contentWindow == null || !val.MayHaveValidInventorySlot(pistol))
			{
				val = ((IEnumerable<GameItem>)Items.All()).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => i.contentWindow != null && i != pistol && GeneralHelper.MayPlayerInsertInto(i) && i.MayHaveValidInventorySlot(pistol) && i.identifier.Contains("case")));
			}
			if (val == null)
			{
				st.Detail = "no case that takes the pistol; items: " + string.Join(", ", from i in Items.All()
					select i.identifier + "#" + Ids.Of(i));
				return StepStatus.Fail;
			}
			caseId = Ids.Of(val);
			pistolId = Ids.Of(pistol);
			group.Clear();
			(string, int)[] order = Order;
			for (int num = 0; num < order.Length; num++)
			{
				(string, int) tuple = order[num];
				string item = tuple.Item1;
				int item2 = tuple.Item2;
				GameItem val2 = ByName(item, item2);
				if (val2 == null)
				{
					st.Detail = "no " + item + " #" + item2 + " in this run";
					return StepStatus.Fail;
				}
				group.Add(Ids.Of(val2));
			}
			medId = Ids.Of(ByName("med_keycard", 0));
			engId = Ids.Of(ByName("eng_keycard", 0));
			st.Detail = Layout();
			return StepStatus.Pass;
		});
		Act("pistol-in", 20f, () => Where(I(pistolId)), () => qPistol = ItemsMove.InsertInto(I(pistolId), I(caseId), -1, chain: true, "bot-drag"), delegate
		{
			GameItem obj = I(pistolId);
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				GameInventory parentInventory = obj.parentInventory;
				obj2 = ((parentInventory != null) ? parentInventory.GetParentItem() : null);
			}
			return obj2 == I(caseId);
		}, () => Where(I(pistolId)));
		DigestSame("pistol-digest");
		Step("group-in", 30f, delegate(BotStep st)
		{
			if (groupQ.Count != group.Count)
			{
				st.Detail = "sent " + groupQ.Count + " of " + group.Count + " group request(s)";
				return StepStatus.Fail;
			}
			if (groupQ.Concat(new int[2] { qMed, qEng }).Any((int num2) => num2 == 0 || ItemsMove.Outcome(num2) == "pending"))
			{
				return StepStatus.Running;
			}
			st.Detail = Outcomes() + " | " + Layout();
			string text = (((Args.Get("--br-fg-oldserver") ?? "").Trim() == "1") ? "Someone else moved that item first." : "Your last move already put that item somewhere else.");
			int[] array = new int[2] { qMed, qEng };
			foreach (int q in array)
			{
				if (ItemsMove.Outcome(q) != "rejected:moved" || ItemsMove.RejectTextOf(q) != text)
				{
					return StepStatus.Fail;
				}
			}
			return StepStatus.Pass;
		}, delegate
		{
			GameItem val = I(caseId);
			GameItem val2 = I(medId);
			GameItem val3 = I(engId);
			GridShape modifiedShape = val2.modifiedShape;
			GridShape modifiedShape2 = val3.modifiedShape;
			GameInventory parentInventory = val2.parentInventory;
			ItemsCapture.Ctx c;
			bool entered = ItemsCapture.Enter("group", out c);
			try
			{
				foreach (long item3 in group)
				{
					GameItem val4 = I(item3);
					int sentCount = ItemsMove.SentCount;
					if (val4 == null || val4.parentInventory == null || !val4.MayRemove() || val4.MaxNumRemove() < 1)
					{
						groupQ.Add(0);
					}
					else
					{
						if (GeneralHelper.MayPlayerInsertInto(val) && val.MayHaveValidInventorySlot(val4))
						{
							GraphUtils.TryAcceptAll((GraphNodeStorage)(object)val, val4, -1);
						}
						groupQ.Add((ItemsMove.SentCount > sentCount) ? ItemsMove.LastSentQ : 0);
					}
				}
			}
			finally
			{
				ItemsCapture.Exit(entered);
			}
			qMed = ItemsMove.Move(val2, (GraphNodeStorage)(object)parentInventory, modifiedShape.minX, modifiedShape.minY, modifiedShape.flipped, modifiedShape.orientation, -1, rep: false, "bot-late");
			PixelWindow contentWindow = val.contentWindow;
			GraphNodeStorage obj = ((contentWindow != null) ? contentWindow.child : null);
			GameInventory val5 = (GameInventory)(object)((obj is GameInventory) ? obj : null);
			qEng = ((val5 != null) ? ItemsMove.Move(val3, (GraphNodeStorage)(object)val5, 0, 0, modifiedShape2.flipped, modifiedShape2.orientation, -1, rep: false, "bot-late") : 0);
		});
		DigestSame("group-digest");
		Step("watch", 40f, delegate(BotStep st)
		{
			int num = DigestMismatches();
			if (num > worstDigest)
			{
				worstDigest = num;
			}
			if (ItemsMove.ApplyMismatches > 0 || Snapshot.IsLoading || num > 0)
			{
				st.Detail = "after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s: apply mismatches " + ItemsMove.ApplyMismatches + ", digest mismatches in a row " + num + ", reloading " + Snapshot.IsLoading + " | " + Layout();
				return StepStatus.Fail;
			}
			if (st.Elapsed < 30f)
			{
				return StepStatus.Running;
			}
			st.Detail = "30 s with no apply or digest mismatch and no reload; tape mismatches " + Items.TapeMismatches + ", random " + ItemsMove.RandomMismatches;
			return StepStatus.Pass;
		});
		Do("final", () => Layout());
		long driftId = 0L;
		string driftFrom = "";
		Step("drift", 10f, delegate(BotStep st)
		{
			GameItem val = I(driftId);
			if (val == null)
			{
				st.Detail = "no item to drift";
				return StepStatus.Fail;
			}
			string text = Where(val);
			if (text == driftFrom)
			{
				st.Detail = "the local move did nothing: " + text;
				return StepStatus.Fail;
			}
			st.Detail = "moved here only: " + driftFrom + " > " + text;
			return StepStatus.Pass;
		}, delegate
		{
			GameItem val = ByName("dream_dust", 1);
			if (val != null)
			{
				driftId = Ids.Of(val);
				driftFrom = Where(val);
				GraphUtils.TryAcceptOnce((GraphNodeStorage)(object)ItemsBotBase.E.invElement, val, -1);
			}
		});
		WaitFor("drift-reload", 120f, () => Snapshot.LastReloadReport != null && Snapshot.LastReloadReport.Contains("#" + driftId + " "), () => "report " + (Snapshot.LastReloadReport ?? "none") + "; now " + Where(I(driftId)));
		DigestSame("drift-digest");
		Do("ask-with-reason", delegate
		{
			Snapshot.RequestResync("lab: fence-group-a asks with its own reason");
			return "asked";
		});
		WaitFor("reason-reload", 60f, () => Snapshot.LastReloadReport != null && Snapshot.LastReloadReport.StartsWith("(lab: fence-group-a", StringComparison.Ordinal), () => "report " + (Snapshot.LastReloadReport ?? "none"));
		Cmd("tell-done", "done");
	}
}
