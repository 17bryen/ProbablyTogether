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

	public const int Dislike = 4;

	public static double RemarkAt = 0.5;

	public static double AsksAt = 1.5;

	public static double OfferAt = 4.0;

	public static double DislikeAt = -0.5;

	public static int Chance = 35;

	private sealed class Tag
	{
		public int Seat;

		public int Kind;

		public string Lines = "";
	}

	private static readonly Dictionary<string, Tag> today = new Dictionary<string, Tag>(StringComparer.Ordinal);

	private static int forDay = -1;

	internal static int Tagged;

	internal static string LastTagged = "";

	public static int KindOf(string identifier)
	{
		Tag value;
		return (!string.IsNullOrEmpty(identifier) && today.TryGetValue(identifier, out value)) ? value.Kind : 0;
	}

	public static int SeatOf(string identifier)
	{
		Tag value;
		return (!string.IsNullOrEmpty(identifier) && today.TryGetValue(identifier, out value)) ? value.Seat : 0;
	}

	public static string LinesOf(string identifier)
	{
		Tag value;
		return (!string.IsNullOrEmpty(identifier) && today.TryGetValue(identifier, out value)) ? value.Lines : "";
	}

	private static string CurrentId()
	{
		try
		{
			PlayerStore store = Game.Store;
			if (store == null || store.currentClientInstance == null)
			{
				return null;
			}
			return store.currentClientInstance.GetClientBlueprint()?.identifier;
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
		string text = CurrentId();
		if (text == null || KindOf(text) != Offer)
		{
			return 0;
		}
		int num = SeatOf(text);
		return (Members.BySeat(num) != null && Members.Online(num)) ? num : 0;
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
		new string[1] { "Give {0} my regards. People down my way speak well of this shop because of them." },
		new string[1] { "I hear {0} is the one to thank for how this place treats us." },
		new string[1] { "Tell {0} we remember who does right by us." }
	};

	private static readonly string[][] asks = new string[3][]
	{
		new string[2] { "Is {0} in?", "I'd rather deal with somebody my people know." },
		new string[2] { "I'm looking for {0}.", "A friend said to ask for them." },
		new string[2] { "Which one of you is {0}?", "I was told they are the one to see here." }
	};

	private static readonly string[][] offers = new string[2][]
	{
		new string[2] { "I'm here for {0}, and only {0}.", "What I've brought is for them. A thank-you, call it." },
		new string[2] { "This one is between me and {0}.", "My people asked me to see them right. Nobody else." }
	};

	private static readonly string[][] dislikes = new string[3][]
	{
		new string[1] { "{0} works here, right? We haven't forgotten." },
		new string[1] { "I know about {0}. Just so that is said." },
		new string[1] { "My people have a long memory for what {0} sold us." }
	};

	/// Every copy, right after the game made up the day's queue.
	internal static void AfterQueue(StoreClientManager mgr)
	{
		today.Clear();
		forDay = Game.Day;
		if (!Standing.Active || mgr?.clientStack == null)
		{
			return;
		}
		List<Member> list = Members.State.list.OrderBy((Member m) => m.s).ToList();
		if (list.Count < 2)
		{
			return;
		}
		int num = (list.Count + 1) / 2;
		HashSet<int> hashSet = new HashSet<int>();
		int day = Game.Day;
		foreach (StoreClient item in mgr.clientStack.ToList())
		{
			if (hashSet.Count >= num)
			{
				break;
			}
			if (item == null || string.IsNullOrEmpty(item.identifier) || string.IsNullOrEmpty(item.clientFaction) || item.mainDialogue == null || today.ContainsKey(item.identifier) || TradeVisits.SeatFor(item.identifier) != 0)
			{
				continue;
			}
			int seat = 0;
			double num2 = 0.0;
			int seat2 = 0;
			double num3 = 0.0;
			foreach (Member item2 in list)
			{
				double num4 = Standing.RepOf(item2.s, item.clientFaction);
				if (num4 > num2)
				{
					num2 = num4;
					seat = item2.s;
				}
				if (num4 < num3)
				{
					num3 = num4;
					seat2 = item2.s;
				}
			}
			int num5 = 0;
			int num6 = 0;
			bool flag = item.clientIntent == StoreClient.ClientIntent.BUY || item.clientIntent == StoreClient.ClientIntent.SELL || item.clientIntent == StoreClient.ClientIntent.SELLNBUY || item.clientIntent == StoreClient.ClientIntent.BARTER;
			if (seat > 0 && num2 >= RemarkAt && !hashSet.Contains(seat))
			{
				num6 = seat;
				num5 = (num2 >= OfferAt && flag) ? Offer : ((num2 >= AsksAt && flag) ? Asks : Remark);
			}
			else if (seat2 > 0 && num3 <= DislikeAt && !hashSet.Contains(seat2))
			{
				num6 = seat2;
				num5 = Dislike;
			}
			if (num5 == 0 || Hash(item.identifier, day, 1) >= Chance)
			{
				continue;
			}
			string[][] array = (num5 == Offer) ? offers : ((num5 == Asks) ? asks : ((num5 == Remark) ? remarks : dislikes));
			string[] array2 = array[Hash(item.identifier, day, 2) % array.Length];
			string name = Members.NameOf(num6);
			List<string> list2 = array2.Select((string s) => UI.Lang.Trf(s, name)).ToList();
			Prepend(item, list2);
			if (num5 == Offer)
			{
				item.buyPriceModifier = Math.Min(item.buyPriceModifier, -15);
				item.tradeReputationMultiplier *= 1.5;
			}
			today[item.identifier] = new Tag
			{
				Seat = num6,
				Kind = num5,
				Lines = string.Join(" / ", list2)
			};
			hashSet.Add(num6);
			Tagged++;
			LastTagged = item.identifier + " " + Word(num5) + " seat " + num6;
			if (!DayLog.Replaying)
			{
				Log.Info("regulars: day " + day + ": " + item.identifier + " (" + Standing.FactionName(item.clientFaction) + ") " + Word(num5) + " " + name + " (their own standing there " + ((num5 == Dislike) ? num3 : num2).ToString("0.00", CultureInfo.InvariantCulture) + ")");
			}
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
		case Dislike:
			return "has not forgotten";
		default:
			return "-";
		}
	}

	internal static void Prepend(StoreClient c, List<string> lines)
	{
		Dialogue mainDialogue = c.mainDialogue;
		if (mainDialogue == null || lines == null || lines.Count == 0)
		{
			return;
		}
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
		Action startAction = mainDialogue.startAction;
		mainDialogue.startAction = delegate
		{
			if (c.mainDialogue == dialogue)
			{
				c.mainDialogue = mainDialogue;
			}
			startAction?.Invoke();
		};
		dialogue.isMainDialog = mainDialogue.isMainDialog;
		c.mainDialogue = dialogue;
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
		Digest.AddExplain("standing.today", () => string.Join(";", today.OrderBy((KeyValuePair<string, Tag> p) => p.Key).Select((KeyValuePair<string, Tag> p) => p.Key + "=" + p.Value.Kind + "/" + p.Value.Seat)));
		Snapshot.Loaded += delegate
		{
			if (Game.ShopLoaded && Game.Day != forDay)
			{
				today.Clear();
			}
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
