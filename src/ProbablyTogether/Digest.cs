using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ProbablyTogether;

internal static class Digest
{
	private static long lastSendMs;

	private static int mismatches;

	private static long lastAskMs = -100000L;

	internal const int IntervalMs = 10000;

	private static readonly List<KeyValuePair<string, Func<string>>> partList = new List<KeyValuePair<string, Func<string>>>();

	private static readonly HashSet<string> partFailLogged = new HashSet<string>();

	private static string askedAreas;

	/// Reloads asked because the shop differed, plus differences sat out (night, someone out).
	internal static int ReloadsAsked;

	internal static int Deferred;

	private static int askedSame;

	private static readonly List<KeyValuePair<string, Func<string>>> explainList = new List<KeyValuePair<string, Func<string>>>();

	/// The readable content behind a part of the fingerprint: "a,b,c" or "k=v;k=v".
	public static void AddExplain(string name, Func<string> text)
	{
		if (!string.IsNullOrEmpty(name) && text != null)
		{
			explainList.RemoveAll((KeyValuePair<string, Func<string>> p) => p.Key == name);
			explainList.Add(new KeyValuePair<string, Func<string>>(name, text));
		}
	}

	internal static Dictionary<string, string> Explain()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (KeyValuePair<string, Func<string>> explain in explainList)
		{
			string text;
			try
			{
				text = explain.Value() ?? "";
			}
			catch (Exception ex)
			{
				text = "!threw " + ex.GetType().Name + ": " + ex.Message;
			}
			dictionary[explain.Key] = (text.Length > 40000) ? text.Substring(0, 40000) : text;
		}
		return dictionary;
	}

	/// A difference this copy does not reload for: the server still gets this side, at most once a minute.
	internal static void ReportSatOut(string what)
	{
		if (Net.IsClient && !Net.IsHost && Client.State == ClientState.Ready && Log.Allow("c|satout|report", 60000L, out var _))
		{
			Client.Send(Wire.Encode("sys.resync", new MsgResync
			{
				reason = Text.Clip(what, 180),
				hold = true,
				report = DesyncReport.Build(what)
			}));
		}
	}

	public static void AddPart(string name, Func<string> part)
	{
		if (string.IsNullOrEmpty(name) || part == null)
		{
			return;
		}
		for (int i = 0; i < partList.Count; i++)
		{
			if (partList[i].Key == name)
			{
				partList[i] = new KeyValuePair<string, Func<string>>(name, part);
				return;
			}
		}
		partList.Add(new KeyValuePair<string, Func<string>>(name, part));
	}

	internal static Dictionary<string, string> Parts()
	{
		if (partList.Count == 0)
		{
			return null;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (KeyValuePair<string, Func<string>> part in partList)
		{
			string value;
			try
			{
				value = part.Value() ?? "";
			}
			catch (Exception ex)
			{
				value = "!";
				if (partFailLogged.Add(part.Key))
				{
					Log.Warn("digest part " + part.Key + ": " + ex.Message);
				}
			}
			dictionary[part.Key] = value;
		}
		return dictionary;
	}

	internal static MsgDigest Compute()
	{
		MsgDigest msgDigest = new MsgDigest
		{
			hash = "",
			state = "",
			client = ""
		};
		if (!Game.ShopLoaded)
		{
			return msgDigest;
		}
		PlayerStore store = Game.Store;
		msgDigest.day = Game.Station?.dayCounter ?? 0;
		msgDigest.cash = store.playerCash;
		msgDigest.favor = store.wildFavor;
		msgDigest.state = store.storeState.ToString();
		try
		{
			StoreClientInstance currentClientInstance = store.currentClientInstance;
			msgDigest.client = ((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier) ?? "";
		}
		catch
		{
			msgDigest.client = "?";
		}
		try
		{
			msgDigest.queue = (store.storeClientManager?.clientStack?.Count).GetValueOrDefault();
		}
		catch
		{
			msgDigest.queue = -1;
		}
		ulong num = 0uL;
		int num2 = 0;
		List<GraphNodeStorage> list = Game.Roots();
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] == null)
			{
				continue;
			}
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(list[i], (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				num += ItemHash(item, i);
				num2++;
			}
		}
		msgDigest.items = num2;
		msgDigest.hash = num.ToString("x16");
		msgDigest.parts = Parts();
		return msgDigest;
	}

	private static ulong ItemHash(GameItem it, int root)
	{
		ulong h = 14695981039346656037uL;
		Mix(ref h, it.uniqueId);
		Mix(ref h, it.identifier);
		Mix(ref h, it.unitCount);
		Mix(ref h, root);
		GameItem val = null;
		try
		{
			GameInventory parentInventory = it.parentInventory;
			val = ((parentInventory != null) ? parentInventory.GetParentItem() : null);
		}
		catch
		{
		}
		Mix(ref h, (val != null) ? val.uniqueId : (-1));
		GridShape modifiedShape = it.modifiedShape;
		if (modifiedShape != null)
		{
			Mix(ref h, modifiedShape.minX);
			Mix(ref h, modifiedShape.minY);
			Mix(ref h, modifiedShape.orientation);
			Mix(ref h, modifiedShape.flipped ? 1 : 0);
		}
		return h;
	}

	private static void Mix(ref ulong h, long v)
	{
		for (int i = 0; i < 8; i++)
		{
			h ^= (byte)(v >> i * 8);
			h *= 1099511628211uL;
		}
	}

	private static void Mix(ref ulong h, string s)
	{
		if (s == null)
		{
			Mix(ref h, -7L);
			return;
		}
		foreach (char c in s)
		{
			h ^= c;
			h *= 1099511628211uL;
		}
		Mix(ref h, s.Length);
	}

	internal static string Diff(MsgDigest a, MsgDigest b)
	{
		if (a == null || b == null)
		{
			return "missing";
		}
		List<string> list = new List<string>();
		if (a.day != b.day)
		{
			list.Add("day " + a.day + "/" + b.day);
		}
		if (a.cash != b.cash)
		{
			list.Add("cash " + a.cash + "/" + b.cash);
		}
		if (a.favor != b.favor)
		{
			list.Add("favor " + a.favor + "/" + b.favor);
		}
		if (a.items != b.items)
		{
			list.Add("items " + a.items + "/" + b.items);
		}
		if (a.hash != b.hash)
		{
			list.Add("item hash");
		}
		if (a.state != b.state)
		{
			list.Add("state " + a.state + "/" + b.state);
		}
		if (a.client != b.client)
		{
			list.Add("counter customer " + a.client + "/" + b.client);
		}
		if (a.queue != b.queue)
		{
			list.Add("queue " + a.queue + "/" + b.queue);
		}
		if (a.parts != null || b.parts != null)
		{
			HashSet<string> hashSet = new HashSet<string>();
			if (a.parts != null)
			{
				hashSet.UnionWith(a.parts.Keys);
			}
			if (b.parts != null)
			{
				hashSet.UnionWith(b.parts.Keys);
			}
			foreach (string item in hashSet)
			{
				string value = null;
				string value2 = null;
				a.parts?.TryGetValue(item, out value);
				b.parts?.TryGetValue(item, out value2);
				if (value != value2)
				{
					list.Add(item);
				}
			}
		}
		if (list.Count != 0)
		{
			return string.Join(", ", list);
		}
		return "";
	}

	internal static string AreasOf(string diff)
	{
		if (string.IsNullOrEmpty(diff))
		{
			return "";
		}
		List<string> list = new List<string>();
		string[] array = diff.Split(new string[1] { ", " }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string text = Regex.Replace(array[i].Trim(), "\\s+\\S+/\\S+$", "");
			if (text.Length > 0 && !list.Contains(text))
			{
				list.Add(text);
			}
		}
		list.Sort(StringComparer.Ordinal);
		return string.Join(", ", list);
	}

	internal static void ServerTick()
	{
		if (!Server.ShopReady)
		{
			return;
		}
		long ms = Clock.Ms;
		if (ms - lastSendMs < 10000)
		{
			return;
		}
		lastSendMs = ms;
		bool flag = false;
		foreach (Server.Peer value in Server.ByPlayer.Values)
		{
			if (value.State == Server.PeerState.Ready)
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			return;
		}
		byte[] frame = Wire.Encode("sys.digest", Compute());
		foreach (Server.Peer value2 in Server.ByPlayer.Values)
		{
			if (value2.State == Server.PeerState.Ready)
			{
				value2.Conn.Send(frame);
			}
		}
	}

	internal static void Compare(MsgDigest server)
	{
		if (Systems.Outings.Private)
		{
			return;
		}
		if (server == null)
		{
			return;
		}
		MsgDigest b = Compute();
		string text = Diff(server, b);
		if (text.Length == 0)
		{
			if (mismatches > 0)
			{
				Log.Info("client: digest matches the server again");
			}
			mismatches = 0;
			askedAreas = null;
			askedSame = 0;
			return;
		}
		mismatches++;
		Log.Info("client: digest differs from the server (" + mismatches + " in a row): server/mine " + text);
		if (mismatches < 2)
		{
			return;
		}
		string text3 = null;
		if (Systems.Outings.AnyOut)
		{
			text3 = "somebody is out on a trip (their trip is settled when they are back)";
		}
		else if (Game.Store != null && (int)Game.Store.storeState == 0)
		{
			text3 = "it is night (the morning brings every copy back in line without replaying the day)";
		}
		if (text3 != null)
		{
			Deferred++;
			mismatches = 0;
			ReportSatOut("digest mismatch: " + text);
			if (Log.Allow("c|digest|deferred", 60000L, out var skipped))
			{
				Log.Info("client: no reload asked for (" + AreasOf(text) + "): " + text3 + Log.More(skipped));
			}
			return;
		}
		if (Locks.LocalHoldsAny())
		{
			Log.Info("client: resync waits: this player holds a lock");
			return;
		}
		long ms = Clock.Ms;
		if (ms - lastAskMs < 30000)
		{
			return;
		}
		string text2 = AreasOf(text);
		if (text2 == askedAreas)
		{
			askedSame++;
		}
		else
		{
			askedAreas = text2;
			askedSame = 1;
		}
		if (askedSame > 3)
		{
			if (askedSame == 4)
			{
				Log.Info("client: " + text2 + " still differ after " + 3 + " reloads; not asking again for them");
			}
			mismatches = 0;
		}
		else
		{
			lastAskMs = ms;
			mismatches = 0;
			ReloadsAsked++;
			Snapshot.Ask("digest mismatch: " + text);
		}
	}

	internal static void ResetClient()
	{
		mismatches = 0;
		askedAreas = null;
		askedSame = 0;
	}
}
