using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.UI;

namespace ProbablyTogether.Systems;

/// Server to one player: choose again (trade or perks); an empty kind ends the offer.
public sealed class MsgRepickOffer
{
	public string kind;

	/// With an empty kind: what became of it (shown to the player).
	public string note;
}

/// The player confirms on the game's screen: this trade, or these perks.
public sealed class MsgRepickAsk
{
	public string kind;

	public string id;

	public string[] ids;
}

public sealed class MsgRepickCancel
{
	public string why;
}

/// /retrade and /reperk: the founder offers a player the game's trade or perk screen; nothing changes until Continue.
/// The offer lives only on the server and ends on confirm, back, or when the shop opens.
public static class Repick
{
	public const string Trade = "trade";

	public const string PerksKind = "perks";

	/// The first version whose players can be offered this.
	private static readonly Version since = new Version(0, 8, 4);

	private static readonly Dictionary<int, string> offers = new Dictionary<int, string>();

	internal static int Offers;

	internal static int Cancelled;

	internal static int Done;

	internal static string LastRefusal = "";

	/// What this copy's player may choose again now (empty for nothing).
	internal static string Mine = "";

	internal static string LastNote = "";

	[SystemInit]
	internal static void Init()
	{
		Net.On<MsgRepickOffer>("repick.offer", null, ClientOffer);
		Net.On<MsgRepickAsk>("repick.ask", ServerAsk, null);
		Net.On<MsgRepickCancel>("repick.cancel", ServerCancel, null);
		Net.Disconnected += delegate
		{
			Mine = "";
			offers.Clear();
		};
	}

	private static int PlayerOf(int seat)
	{
		foreach (KeyValuePair<int, PlayerInfo> player in Net.Players)
		{
			if (player.Value != null && player.Value.Seat == seat)
			{
				return player.Key;
			}
		}
		return 0;
	}

	private static bool TooOld(int player, out string has)
	{
		has = Mod.Version;
		if (Net.IsHost && player == Net.LocalPlayer)
		{
			return false;
		}
		has = Server.ByPlayer.TryGetValue(player, out var value) ? (value.ModVersion ?? "") : "";
		return !Version.TryParse(has, out var result) || result < since;
	}

	private static void Tell(int player, string text)
	{
		if (player > 0)
		{
			Net.ToPlayer(player, "chat.notice", new MsgChatNotice
			{
				text = text
			});
		}
	}

	private static string Refused(string text)
	{
		LastRefusal = text;
		Log.Info("repick: refused: " + text);
		return text;
	}

	/// Handles /retrade and /reperk; returns the line the asker reads.
	internal static string ChatCommand(int from, string kind, string who)
	{
		bool trade = kind == Trade;
		string text = trade ? "/retrade" : "/reperk";
		if (!Bays.Enabled)
		{
			return "This shop does not use personal trades.";
		}
		if (!trade && !Perks.Enabled)
		{
			return "This shop does not use personal perks.";
		}
		if (!Game.ShopLoaded || Game.Store == null)
		{
			return "The shop is not loaded.";
		}
		if (Members.SeatOf(from) != 1)
		{
			return "Only " + Members.NameOf(1) + ", who founded the shop, can do that.";
		}
		// The founder's trade is the shop's own; their perks are theirs like anybody's.
		List<Member> list = Members.State.list.Where((Member m) => !trade || m.s != 1).ToList();
		string text2 = string.Join(", ", list.Select((Member m) => m.n + (trade ? ((Leaving.TradesOf(m.s).Count > 0) ? (" (" + string.Join(", ", Leaving.TradesOf(m.s).Select(Specs.Label)) + ")") : " (no trade)") : "")));
		if (string.IsNullOrWhiteSpace(who))
		{
			return (trade ? "/retrade <player>: the player chooses another trade on the trade screen. Nothing changes until they confirm; if they go back they keep the trade they have. On confirming, the old trade and its machines go, their storage bay and perks stay. " : "/reperk <player>: the player chooses their perks again on the perk screen. Nothing changes until they confirm; if they go back they keep what they have. ") + ((list.Count > 0) ? ("Players: " + text2 + ".") : "Nobody else has a place in this shop.");
		}
		string name = who.Trim();
		if (trade && string.Equals(Members.NameOf(1), name, StringComparison.OrdinalIgnoreCase))
		{
			return "The shop itself is " + Members.NameOf(1) + "'s: the founder's trade stays.";
		}
		List<Member> list2 = list.Where((Member m) => string.Equals(m.n, name, StringComparison.OrdinalIgnoreCase)).ToList();
		if (list2.Count == 0)
		{
			list2 = list.Where((Member m) => (m.n ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
		}
		if (list2.Count != 1)
		{
			return ((list2.Count == 0) ? ("Nobody called '" + name + "' has a place in this shop. ") : ("'" + name + "' fits several players. ")) + "Players: " + text2 + ".";
		}
		Member member = list2[0];
		int num = PlayerOf(member.s);
		if (num <= 0 || !Members.Online(member.s))
		{
			return Refused(member.n + " is not in the shop right now. They choose for themselves: ask again when they are here.");
		}
		string text3 = trade ? Leaving.Check(member.s, whole: false) : (Leaving.Busy() ?? (Perks.Picked(member.s) ? null : (member.n + " has not picked perks yet: the perk screen is already waiting for them.")));
		if (text3 != null)
		{
			return Refused(text3);
		}
		if (TooOld(num, out var has))
		{
			return Refused(member.n + " needs Probably Together " + since + " or newer for that (they have " + ((has.Length > 0) ? has : "an older version") + ").");
		}
		if (!trade)
		{
			// Everybody replays a changed pick: an older copy would replay it as a first pick.
			List<string> list3 = (from p in Net.Players.Keys
				where TooOld(p, out var _)
				select Net.PlayerName(p)).ToList();
			if (list3.Count > 0)
			{
				return Refused("Everyone in the shop needs Probably Together " + since + " or newer for that: " + string.Join(", ", list3) + " " + ((list3.Count == 1) ? "has" : "have") + " an older version.");
			}
		}
		LastRefusal = "";
		offers[member.s] = kind;
		Offers++;
		Net.ToPlayer(num, "repick.offer", new MsgRepickOffer
		{
			kind = kind
		});
		Log.Info("repick: " + text + " " + member.n + " (seat " + member.s + "): offered; nothing changes until they confirm");
		return (member.s == 1) ? "Your perk screen is open. Nothing changes until you confirm; go back to keep what you have." : (member.n + (trade ? " is choosing another trade." : " is choosing their perks again.") + " Nothing changes until they confirm; they can also go back and keep what they have.");
	}

	private static void End(int seat, int player, string note)
	{
		offers.Remove(seat);
		Net.ToPlayer(player, "repick.offer", new MsgRepickOffer
		{
			kind = "",
			note = note
		});
	}

	private static void ServerAsk(MsgRepickAsk m, int from)
	{
		if (m == null || !Game.ShopLoaded)
		{
			return;
		}
		int num = Members.SeatOf(from);
		bool flag = m.kind == Trade;
		if (num <= 0 || !offers.TryGetValue(num, out var value) || value != m.kind)
		{
			string text = "Nobody offered you that: " + Members.NameOf(1) + " decides who chooses again.";
			if (flag)
			{
				Specs.RefuseTo(from, text);
			}
			else
			{
				Perks.RefuseTo(from, text);
			}
			return;
		}
		string text2 = Members.NameOf(num);
		int player = PlayerOf(1);
		if (flag)
		{
			string id = (m.id ?? "").Trim().ToUpperInvariant();
			int num2 = Specs.TakenBy(id);
			string text3 = (!ServerMode.PlayableScenarioList().Any((KeyValuePair<string, string> s) => string.Equals(s.Key, id, StringComparison.OrdinalIgnoreCase)) || id == "ROCK_BOTTOM") ? "That is not a specialization this shop offers." : ((num2 == num) ? "That is your trade already." : ((num2 != 0) ? (Members.NameOf(num2) + " already has that specialization.") : ((Bays.BayOf(num) == null) ? "Your storage bay is not here yet. Try again in a moment." : Leaving.Check(num, whole: false))));
			if (text3 != null)
			{
				LastRefusal = text3;
				Specs.RefuseTo(from, text3);
				return;
			}
			string text4 = string.Join(", ", Leaving.TradesOf(num).Select(Specs.Label));
			Leaving.GiveUp(num);
			Specs.PickFor(from, id);
			bool flag2 = Specs.Of(num) == id;
			Done++;
			Log.Info("repick: " + text2 + " (seat " + num + ") gave up " + text4 + " for " + id + (flag2 ? "" : ": THE NEW TRADE WAS REFUSED (" + Specs.LastRefusal + "); they pick again on the trade screen"));
			End(num, from, flag2 ? ("You are now the shop's " + Specs.Label(id) + ".") : "");
			if (player != from)
			{
				Tell(player, flag2 ? (text2 + " gave up " + text4 + " and is now the shop's " + Specs.Label(id) + ".") : (text2 + " gave up " + text4 + " and is picking a new trade."));
			}
			return;
		}
		List<string> list = (m.ids ?? new string[0]).Where((string i) => !string.IsNullOrEmpty(i)).Select((string i) => i.Trim()).ToList();
		string text5 = Perks.Refusal(num, list, again: true) ?? Leaving.Busy();
		if (text5 != null)
		{
			LastRefusal = text5;
			Perks.RefuseTo(from, text5);
			return;
		}
		List<string> ids = Perks.Of(num).Ids;
		if (ids.Count == list.Count && !list.Except(ids).Any())
		{
			Cancelled++;
			Log.Info("repick: " + text2 + " (seat " + num + ") confirmed the perks they already had: nothing to do");
			End(num, from, "You keep your perks.");
			if (player != from)
			{
				Tell(player, text2 + " keeps their perks.");
			}
			return;
		}
		Perks.ServerSwap(num, list);
		Done++;
		string text6 = (list.Count > 0) ? string.Join(", ", list.Select(Perks.NameOfPerk)) : "none";
		End(num, from, "Your perks now: " + text6 + ".");
		if (player != from)
		{
			Tell(player, text2 + "'s perks now: " + text6 + ".");
		}
	}

	private static void ServerCancel(MsgRepickCancel m, int from)
	{
		int num = Members.SeatOf(from);
		if (num <= 0 || !offers.TryGetValue(num, out var value))
		{
			return;
		}
		offers.Remove(num);
		Cancelled++;
		Log.Info("repick: " + Members.NameOf(num) + " (seat " + num + ") keeps their " + ((value == Trade) ? "trade" : "perks") + " (" + (m?.why ?? "") + ")");
		int num2 = PlayerOf(1);
		if (num2 != from)
		{
			Tell(num2, Members.NameOf(num) + " keeps their " + ((value == Trade) ? "trade" : "perks") + ".");
		}
	}

	private static void ClientOffer(MsgRepickOffer m)
	{
		string text = m?.kind ?? "";
		if (text != Trade && text != PerksKind)
		{
			text = "";
		}
		Mine = text;
		LastNote = m?.note ?? "";
		PerkScreens.OfferChanged();
		if (text.Length > 0)
		{
			Log.Info("repick: " + Members.NameOf(1) + " lets this player choose their " + ((text == Trade) ? "trade" : "perks") + " again");
			if (Members.LocalSeat != 1)
			{
				Notices.Show((text == Trade) ? Lang.Trf("{0} lets you choose another trade. Go back to keep yours.", Kit.Name(Members.NameOf(1))) : Lang.Trf("{0} lets you choose your perks again. Go back to keep them.", Kit.Name(Members.NameOf(1))), (text == Trade) ? "spec" : "perks");
			}
		}
		else if (LastNote.Length > 0)
		{
			Notices.Show(Lang.Tr(LastNote), "perks");
		}
	}

	internal static void Confirm(string kind, string id, IEnumerable<string> ids)
	{
		if (Net.IsOnline)
		{
			Net.ToServer("repick.ask", new MsgRepickAsk
			{
				kind = kind,
				id = id,
				ids = (ids ?? new string[0]).ToArray()
			});
		}
	}

	internal static void Cancel(string why)
	{
		if (Mine.Length == 0)
		{
			return;
		}
		Log.Info("repick: this player keeps their " + ((Mine == Trade) ? "trade" : "perks") + " (" + why + ")");
		Mine = "";
		if (Net.IsOnline)
		{
			Net.ToServer("repick.cancel", new MsgRepickCancel
			{
				why = why
			});
		}
	}
}
