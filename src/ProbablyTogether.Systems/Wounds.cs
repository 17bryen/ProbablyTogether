using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ProbablyTogether.UI;
using HarmonyLib;

namespace ProbablyTogether.Systems;

/// Each player has their own wound record; the game's wound fields hold the record of whoever's doing is being run.
/// Only on when everyone goes out alone; group outings keep the game's single wound.
public static class Wounds
{
	public struct W : IEquatable<W>
	{
		public int State;

		public bool Stable;

		public bool Fresh;

		public static W Healthy => new W
		{
			State = 0,
			Stable = true,
			Fresh = false
		};

		public bool Equals(W o)
		{
			return State == o.State && Stable == o.Stable && Fresh == o.Fresh;
		}

		public override string ToString()
		{
			return State.ToString(CultureInfo.InvariantCulture) + (Stable ? "s" : "u") + (Fresh ? "f" : "");
		}
	}

	private const string Key = "wounds";

	private static string cachedRaw;

	private static Dictionary<int, W> cached = new Dictionary<int, W>();

	private static readonly Stack<int> seats = new Stack<int>();

	private static int scopeSeat;

	private static bool nightly;

	private static float nextLook;

	internal static int Swaps;

	internal static int Nights;

	public static bool Active => Net.IsOnline && Outings.Individual && Game.ShopLoaded && Game.Store != null && Game.Store.healthData != null;

	/// The whole record as "seat=wound;...".
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

	private static bool TripViewLive => HostTrip.Out && !HostTrip.HomeView;

	private static int LocalSeat => Net.HasLocalPlayer ? Members.LocalSeat : 0;

	/// The seat whose wound the game's fields hold now (0: nobody's in particular).
	private static int ViewSeat => (seats.Count > 0 && scopeSeat > 0) ? scopeSeat : (TripViewLive ? 0 : LocalSeat);

	private static W Fields()
	{
		HealthData healthData = Game.Store.healthData;
		return new W
		{
			State = healthData.woundState,
			Stable = healthData.isWoundStable,
			Fresh = healthData.isWoundedFresh
		};
	}

	private static void SetFields(W w)
	{
		HealthData healthData = Game.Store.healthData;
		healthData.woundState = w.State;
		healthData.isWoundStable = w.Stable;
		healthData.isWoundedFresh = w.Fresh;
	}

	private static Dictionary<int, W> Parse(string raw)
	{
		Dictionary<int, W> dictionary = new Dictionary<int, W>();
		string[] array = (raw ?? "").Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			int num = text.IndexOf('=');
			if (num <= 0 || !int.TryParse(text.Substring(0, num), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				continue;
			}
			string text2 = text.Substring(num + 1);
			int i;
			for (i = 0; i < text2.Length && (char.IsDigit(text2[i]) || text2[i] == '-'); i++)
			{
			}
			int.TryParse(text2.Substring(0, i), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2);
			dictionary[result] = new W
			{
				State = result2,
				Stable = text2.IndexOf('s', i) >= 0,
				Fresh = text2.IndexOf('f', i) >= 0
			};
		}
		return dictionary;
	}

	private static void Write(Dictionary<int, W> table)
	{
		string text = string.Join(";", from p in table
			orderby p.Key
			select p.Key.ToString(CultureInfo.InvariantCulture) + "=" + p.Value.ToString());
		using (Net.Apply())
		{
			ModHelper.SetModData("bellun_probably_together", Key, text);
		}
		cachedRaw = text;
		cached = table;
	}

	private static Dictionary<int, W> Table()
	{
		string text = null;
		try
		{
			text = ModHelper.GetModData("bellun_probably_together", Key, (string)null);
		}
		catch
		{
		}
		if (text == null)
		{
			// Older or new shop: everyone starts from the shop's one wound (same save, so the same on every copy).
			W value = Fields();
			Dictionary<int, W> dictionary = new Dictionary<int, W>();
			foreach (Member item in Members.State.list)
			{
				dictionary[item.s] = value;
			}
			Write(dictionary);
			Log.Info("wounds: every player now has a wound of their own; " + dictionary.Count + " record(s) start from the shop's (" + value.ToString() + ")");
			return cached;
		}
		if ((object)text != cachedRaw && text != cachedRaw)
		{
			cachedRaw = text;
			cached = Parse(text);
		}
		return cached;
	}

	public static W Of(int seat)
	{
		W value;
		return (seat > 0 && Table().TryGetValue(seat, out value)) ? value : W.Healthy;
	}

	private static void Put(int seat, W w)
	{
		if (seat <= 0)
		{
			return;
		}
		Dictionary<int, W> dictionary = Table();
		if (dictionary.TryGetValue(seat, out var value) ? value.Equals(w) : w.Equals(W.Healthy))
		{
			return;
		}
		Write(new Dictionary<int, W>(dictionary) { [seat] = w });
	}

	/// Sets a wound from outside the player's doing (a trip handed over), shown at once if on view.
	private static void Set(int seat, W w)
	{
		Put(seat, w);
		if (seat == ViewSeat)
		{
			SetFields(w);
		}
	}

	/// Until Leave, the game's wound fields hold this player's wound.
	internal static void Enter(int player)
	{
		int num = 0;
		try
		{
			if (Active && player > 0 && !TripViewLive && !nightly)
			{
				num = Members.SeatOf(player);
			}
			if (num > 0)
			{
				if (seats.Count > 0 && scopeSeat > 0)
				{
					Put(scopeSeat, Fields());
				}
				SetFields(Of(num));
				scopeSeat = num;
				Swaps++;
			}
		}
		catch (Exception ex)
		{
			num = 0;
			Log.Warn("wounds: turning to player " + player + "'s wound threw: " + ex.Message);
		}
		seats.Push(num);
	}

	internal static void Leave()
	{
		if (seats.Count == 0)
		{
			return;
		}
		int num = seats.Pop();
		if (num <= 0)
		{
			return;
		}
		try
		{
			if (Game.Store?.healthData == null)
			{
				scopeSeat = 0;
				return;
			}
			Put(num, Fields());
			scopeSeat = 0;
			foreach (int seat in seats)
			{
				if (seat > 0)
				{
					scopeSeat = seat;
					break;
				}
			}
			if (scopeSeat > 0)
			{
				SetFields(Of(scopeSeat));
			}
			else
			{
				ShowLocal();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("wounds: turning back from seat " + num + "'s wound threw: " + ex.Message);
		}
	}

	private static void ShowLocal()
	{
		if (TripViewLive)
		{
			return;
		}
		int localSeat = LocalSeat;
		if (localSeat > 0)
		{
			W w = Of(localSeat);
			if (!Fields().Equals(w))
			{
				SetFields(w);
			}
		}
	}

	/// Every copy: a returning player's wound is what their own game reports.
	internal static void Returned(int player, int state, bool stable, bool fresh)
	{
		int num = Members.SeatOf(player);
		if (num <= 0)
		{
			return;
		}
		W w = Of(num);
		W w2 = new W
		{
			State = Math.Max(0, state),
			Stable = stable,
			Fresh = fresh
		};
		Set(num, w2);
		if (!w.Equals(w2))
		{
			Log.Info("wounds: " + Members.NameOf(num) + " came home with wound " + w2.ToString() + " (was " + w.ToString() + ")");
			if (w2.State > w.State && !DayLog.Replaying && player != Net.LocalPlayer && Net.HasLocalPlayer)
			{
				Notices.Show(Lang.Trf((w2.State > 5) ? "{0} came back badly hurt." : "{0} came back hurt.", Kit.Name(Members.NameOf(num))), "wound");
			}
		}
	}

	/// Runs the game's nightly heal or worsen once per player, in seat order.
	internal static bool Nightly()
	{
		if (nightly || !Active)
		{
			return true;
		}
		PlayerStore store = Game.Store;
		if (seats.Count > 0 && scopeSeat > 0)
		{
			Put(scopeSeat, Fields());
		}
		List<Member> list = Members.State.list.OrderBy((Member m) => m.s).ToList();
		nightly = true;
		try
		{
			foreach (Member item in list)
			{
				SetFields(Of(item.s));
				int num = (store.nightLogs != null) ? store.nightLogs.Count : 0;
				HealthData.HandleNightlyWound();
				Put(item.s, Fields());
				// Name whose wound the morning report means.
				if (list.Count > 1 && store.nightLogs != null)
				{
					for (int i = num; i < store.nightLogs.Count; i++)
					{
						string text = store.nightLogs[i] ?? "";
						int num2 = text.StartsWith("<color=", StringComparison.Ordinal) ? (text.IndexOf('>') + 1) : 0;
						store.nightLogs[i] = text.Substring(0, num2) + item.n + ": " + text.Substring(num2);
					}
				}
			}
			Nights++;
		}
		catch (Exception ex)
		{
			Log.Error("wounds: the night's healing threw: " + ex);
		}
		finally
		{
			nightly = false;
			if (seats.Count > 0 && scopeSeat > 0)
			{
				SetFields(Of(scopeSeat));
			}
			else
			{
				ShowLocal();
			}
		}
		return false;
	}

	[SystemInit]
	internal static void Init()
	{
		Digest.AddPart("wounds", () => Active ? Raw : "");
		Digest.AddExplain("wounds", () => Active ? string.Join(";", from m in Members.State.list
			orderby m.s
			select m.n.Replace(';', ' ').Replace('=', ' ') + "=" + Of(m.s).ToString()) : "");
		Snapshot.Loaded += delegate
		{
			cachedRaw = null;
			seats.Clear();
			scopeSeat = 0;
			nextLook = 0f;
		};
	}

	[SystemTick]
	private static void Tick()
	{
		if (!Active || seats.Count > 0 || nightly || Snapshot.IsLoading || Dispatch.InRequest)
		{
			return;
		}
		float realtimeSinceStartup = UnityEngine.Time.realtimeSinceStartup;
		if (realtimeSinceStartup < nextLook)
		{
			return;
		}
		nextLook = realtimeSinceStartup + 1f;
		try
		{
			Table();
			ShowLocal();
		}
		catch (Exception ex)
		{
			if (Log.Allow("wounds|tick", 30000L, out var skipped))
			{
				Log.Warn("wounds: showing this player's own wound threw" + Log.More(skipped) + ": " + ex.Message);
			}
		}
	}
}

[HarmonyPatch]
internal static class WoundsNightlyPatch
{
	private static readonly MethodBase target = Find();

	private static MethodBase Find()
	{
		try
		{
			return AccessTools.Method(typeof(HealthData), "HandleNightlyWound", Type.EmptyTypes, (Type[])null);
		}
		catch
		{
			return null;
		}
	}

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static bool Prefix()
	{
		return Wounds.Nightly();
	}
}
