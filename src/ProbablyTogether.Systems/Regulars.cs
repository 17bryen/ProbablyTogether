using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ProbablyTogether.Systems;

/// A rare customer who knows a player: a remark, an ask, or an offer for that player alone, by their standing with the buyer's faction.
/// Every copy decides the same each morning from the ledger, the customer's name and the day; nothing random.
public static class Regulars
{
	public const int None = 0;

	public const int Remark = 1;

	public const int Asks = 2;

	public const int Offer = 3;

	public static int RemarkAt = 8;

	public static int AsksAt = 16;

	public static int OfferAt = 30;

	public static int Gap = 3;

	/// Chance in a hundred that such a customer comes on a day one could.
	public static int Chance = 50;

	private sealed class Tag
	{
		public StoreClient Client;

		public string Id = "";

		public int Seat;

		public int Kind;

		public string Lines = "";

		public Dialogue Head;

		public Dialogue Original;

		public int Cash;

		public double RepFactor;
	}

	private static Tag today;

	private const string Key = "regular";

	/// Customers re-labelled, and such lines begun at the counter.
	internal static int Tagged;

	internal static int Spoken;

	internal static string LastTagged = "";

	/// The day the shop's last such customer was queued (saved, compared between copies).
	public static string Raw
	{
		get
		{
			try
			{
				return ModHelper.GetModData("bellun_probably_together", Key, (string)null) ?? "";
			}
			catch
			{
				return "";
			}
		}
	}

	private static int LastDay => int.TryParse(Raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : (-1000);

	public static int KindOf(string identifier)
	{
		return (today != null && !string.IsNullOrEmpty(identifier) && today.Id == identifier) ? today.Kind : 0;
	}

	public static int SeatOf(string identifier)
	{
		return (today != null && !string.IsNullOrEmpty(identifier) && today.Id == identifier) ? today.Seat : 0;
	}

	public static string LinesOf(string identifier)
	{
		return (today != null && !string.IsNullOrEmpty(identifier) && today.Id == identifier) ? today.Lines : "";
	}

	private static StoreClient Current()
	{
		try
		{
			return Game.Store?.currentClientInstance?.GetClientBlueprint();
		}
		catch
		{
			return null;
		}
	}

	/// The seat the customer at the counter deals with and nobody else (0: anybody). Not while that player is not
	/// in the game: an offer nobody can take would only block the counter.
	public static int OnlyNow()
	{
		if (today == null || today.Kind != Offer || Current() != today.Client)
		{
			return 0;
		}
		return (Members.BySeat(today.Seat) != null && Members.Online(today.Seat)) ? today.Seat : 0;
	}

	/// Server: why this player cannot close a deal with the customer at the counter (null: they can).
	internal static string Refusal(int player)
	{
		int num = OnlyNow();
		if (num <= 0 || Members.SeatOf(player) == num)
		{
			return null;
		}
		return "This customer will only deal with " + Members.NameOf(num) + ".";
	}

	private static int Hash(string identifier, int day, int salt)
	{
		uint num = (uint)(day * 7919 + salt * 104729 + 17);
		foreach (char c in identifier ?? "")
		{
			num = num * 31 + c;
		}
		num ^= num >> 15;
		num *= 2246822519u;
		num ^= num >> 13;
		return (int)(num % 100);
	}

	// {0} the player, {1} the goods.
	private static readonly string[][] remarks = new string[3][]
	{
		new string[1] { "Give {0} my regards. The {1} from this shop gets talked about where I live." },
		new string[1] { "I hear {0} is the one to thank for the {1} here." },
		new string[1] { "Tell {0} the {1} did right by us." }
	};

	private static readonly string[][] asks = new string[3][]
	{
		new string[2] { "Is {0} in?", "My people say the {1} here is their doing." },
		new string[2] { "I'm looking for {0}.", "A friend said to ask them about the {1}." },
		new string[2] { "Which one of you is {0}?", "I was told they are the one to see for the {1}." }
	};

	private static readonly string[][] offers = new string[2][]
	{
		new string[2] { "I'm here for {0}, and only {0}.", "My people want more of that {1}, and they sent me with money." },
		new string[2] { "This one is between me and {0}.", "It's their {1} we are after. Nobody else's." }
	};

	private static string GoodsName(string id)
	{
		try
		{
			string text = string.IsNullOrEmpty(id) ? null : GeneralHelper.GetDisplayNameFromID(id);
			if (!string.IsNullOrEmpty(text) && !text.StartsWith("[", StringComparison.Ordinal))
			{
				return text;
			}
		}
		catch
		{
		}
		return UI.Lang.Tr("goods");
	}

	/// Undoes the last look-over for customers not yet at the counter.
	private static void Undo()
	{
		Tag tag = today;
		today = null;
		if (tag?.Client == null)
		{
			return;
		}
		try
		{
			if (tag.Client.mainDialogue == tag.Head)
			{
				tag.Client.mainDialogue = tag.Original;
			}
			if (tag.Kind == Offer)
			{
				tag.Client.clientCash = tag.Cash;
				tag.Client.tradeReputationMultiplier = tag.RepFactor;
			}
		}
		catch (Exception ex)
		{
			Log.Warn("regulars: taking back a customer's lines threw: " + ex.Message);
		}
	}

	/// Every copy, right after the game made up the day's queue.
	internal static void AfterQueue(StoreClientManager mgr)
	{
		Undo();
		if (!Standing.Active || mgr?.clientStack == null)
		{
			return;
		}
		List<Member> list = Members.State.list.OrderBy((Member m) => m.s).ToList();
		if (list.Count < 2)
		{
			return;
		}
		int day = Game.Day;
		int lastDay = LastDay;
		if (lastDay != day && (day - lastDay < Gap || Hash("shop", day, 3) >= Chance))
		{
			return;
		}
		// The buyer whose faction knows one player best, among the buyers who came for what that player sells.
		StoreClient who = null;
		int seat = 0;
		KeyValuePair<string, Standing.Good> best = default(KeyValuePair<string, Standing.Good>);
		foreach (StoreClient item in mgr.clientStack.ToList())
		{
			if (item == null || string.IsNullOrEmpty(item.identifier) || string.IsNullOrEmpty(item.clientFaction) || item.mainDialogue == null || !item.IsClientBuying() || item.isSecurity || TradeVisits.SeatFor(item.identifier) != 0)
			{
				continue;
			}
			foreach (Member item2 in list)
			{
				KeyValuePair<string, Standing.Good>? keyValuePair = Standing.BestFor(item2.s, item);
				if (keyValuePair.HasValue && keyValuePair.Value.Value.N >= RemarkAt && (who == null || keyValuePair.Value.Value.N > best.Value.N))
				{
					who = item;
					seat = item2.s;
					best = keyValuePair.Value;
				}
			}
		}
		if (who == null)
		{
			return;
		}
		int num = (best.Value.N >= OfferAt) ? Offer : ((best.Value.N >= AsksAt) ? Asks : Remark);
		string[][] array = (num == Offer) ? offers : ((num == Asks) ? asks : remarks);
		string[] array2 = array[Hash(who.identifier, day, 2) % array.Length];
		string name = Members.NameOf(seat);
		string goods = GoodsName(best.Value.Last);
		List<string> list2 = array2.Select((string s) => UI.Lang.Trf(s, name, goods)).ToList();
		Tag tag = new Tag
		{
			Client = who,
			Id = who.identifier,
			Seat = seat,
			Kind = num,
			Lines = string.Join(" / ", list2),
			Original = who.mainDialogue,
			Cash = who.clientCash,
			RepFactor = who.tradeReputationMultiplier
		};
		tag.Head = Prepend(who, list2, tag);
		if (num == Offer)
		{
			// They brought more money, and the deal weighs more with their faction.
			who.clientCash = (int)Math.Min(int.MaxValue, (long)who.clientCash * 5 / 4);
			who.tradeReputationMultiplier *= 1.5;
		}
		today = tag;
		if (lastDay != day)
		{
			using (Net.Apply())
			{
				ModHelper.SetModData("bellun_probably_together", Key, day.ToString(CultureInfo.InvariantCulture));
			}
		}
		Tagged++;
		LastTagged = who.identifier + " " + Word(num) + " seat " + seat;
		if (!DayLog.Replaying)
		{
			Log.Info("regulars: day " + day + ": " + who.identifier + " (" + Standing.FactionName(who.clientFaction) + ") " + Word(num) + " " + name + ": " + best.Value.N + " sale(s) of " + best.Key + ", last " + best.Value.Last + "; the shop's last such customer was on day " + lastDay);
		}
	}

	internal static string Word(int kind)
	{
		switch (kind)
		{
		case Remark:
			return "has a word about";
		case Asks:
			return "asks for";
		case Offer:
			return "brings an offer only for";
		default:
			return "-";
		}
	}

	/// Adds the customer's own lines before their first, once; later returns go straight to their first line.
	private static Dialogue Prepend(StoreClient c, List<string> lines, Tag tag)
	{
		Dialogue mainDialogue = c.mainDialogue;
		Dialogue dialogue = null;
		Dialogue dialogue2 = null;
		foreach (string line in lines)
		{
			Dialogue dialogue3 = new Dialogue();
			dialogue3.SetText(mainDialogue.title ?? c.displayName, line);
			dialogue3.portrait = mainDialogue.portrait;
			dialogue3.priority = mainDialogue.priority;
			if (dialogue == null)
			{
				dialogue = dialogue3;
			}
			else
			{
				dialogue2.SetNextDialogue(dialogue3);
			}
			dialogue2 = dialogue3;
		}
		dialogue2.SetNextDialogue(mainDialogue);
		Dialogue head = dialogue;
		head.startAction = delegate
		{
			if (c.mainDialogue == head)
			{
				c.mainDialogue = mainDialogue;
			}
			Spoken++;
			if (!DayLog.Replaying)
			{
				Log.Info("regulars: " + c.identifier + " begins \"" + tag.Lines + "\" (" + Spoken + " such line(s) begun since the game started, day " + Game.Day + ")");
			}
		};
		head.isMainDialog = mainDialogue.isMainDialog;
		c.mainDialogue = head;
		return head;
	}

	/// Every copy looks over the queue as it stands now (after the lab added a customer).
	internal static void LabRetag()
	{
		if (!Net.IsServer || !Bots.BotRunner.Enabled)
		{
			return;
		}
		ApplyLab(null);
		Net.ToAll("regulars.lab", new MsgRegularsLab());
	}

	private static void ApplyLab(MsgRegularsLab m)
	{
		if (Bots.BotRunner.Enabled && Game.ShopLoaded)
		{
			AfterQueue(Game.Store?.storeClientManager);
		}
	}

	[SystemInit]
	internal static void Init()
	{
		Net.On<MsgRegularsLab>("regulars.lab", null, ApplyLab);
		Digest.AddExplain("standing.today", () => ((today == null) ? "" : (today.Id + "=" + today.Kind + "/" + today.Seat)) + "; last day " + Raw);
		Snapshot.Loaded += delegate
		{
			// A scene loaded in the morning has no queue yet; one loaded during the day gets its queue from the replay
			// of the opening, which runs AfterQueue again. Either way the customers of the old scene are gone.
			today = null;
		};
	}
}

public sealed class MsgRegularsLab
{
	public int n;
}

/// The reputation panel: under a faction's standing, whose doing it was.
[HarmonyPatch(typeof(ReputationElement), "Init")]
internal static class RegularsReputationPanelPatch
{
	private static void Postfix(ReputationElement __instance, StoreReputation storeReputation)
	{
		try
		{
			if (!Standing.Active || storeReputation == null || (UnityEngine.Object)(object)__instance.reputationStatus == (UnityEngine.Object)null)
			{
				return;
			}
			List<string> list = new List<string>();
			foreach (Member item in Members.State.list.OrderBy((Member m) => m.s))
			{
				double num = Standing.RepOf(item.s, storeReputation.factionId);
				if (Math.Abs(num) >= 0.05)
				{
					list.Add(UI.Kit.Name(item.n) + " " + ((num > 0.0) ? "+" : "") + num.ToString("0.0", CultureInfo.InvariantCulture));
				}
			}
			if (list.Count > 0)
			{
				((TMP_Text)__instance.reputationStatus).text = ((TMP_Text)__instance.reputationStatus).text + "\n<size=65%>" + string.Join("\n", list) + "</size>";
			}
		}
		catch (Exception ex)
		{
			if (Log.Allow("regulars|panel", 30000L, out var skipped))
			{
				Log.Warn("regulars: the reputation panel line threw" + Log.More(skipped) + ": " + ex.Message);
			}
		}
	}
}
