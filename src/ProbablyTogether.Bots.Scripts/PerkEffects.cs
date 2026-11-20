using System;
using System.Linq;
using System.Reflection;
using ProbablyTogether.Systems;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

/// Checks what eight perks do through the game function each changes; each check restores what it touched, dice included.
internal static class PerkEffects
{
	private static readonly MethodInfo repPer1000 = AccessTools.Method(typeof(BargainUIManager), "ComputeRepPer1000Credits", (Type[])null, (Type[])null);

	private static readonly FieldInfo repAmount = AccessTools.Field(typeof(StoreReputation), "amount");

	private static PlayerStore S => Game.Store;

	private static T Keeping<T>(Func<T> f)
	{
		UnityEngine.Random.State state = UnityEngine.Random.state;
		try
		{
			using (Net.Apply())
			{
				return f();
			}
		}
		finally
		{
			UnityEngine.Random.state = state;
		}
	}

	/// Vigilant: the night's burglary check stops before anything (no day without insurance counted).
	internal static string Vigilant(out bool ok)
	{
		PlayerStore s = S;
		StoreService ins = StoreService.GetInsuranceService();
		StoreService cartel = StoreService.GetCartelProtectionService();
		bool unlocked = ins.unlocked;
		bool isUsing = ins.isUsing;
		bool skip = s.skipIntro;
		bool alarm = s.isSecurityAlarmActive;
		bool cartelUsing = cartel != null && cartel.isUsing;
		int days = s.dayWithoutInsurance;
		int after = Keeping(delegate
		{
			try
			{
				ins.unlocked = true;
				ins.isUsing = false;
				s.skipIntro = false;
				s.isSecurityAlarmActive = false;
				if (cartel != null)
				{
					cartel.isUsing = false;
				}
				s.HandleInsurance();
				return s.dayWithoutInsurance;
			}
			finally
			{
				ins.unlocked = unlocked;
				ins.isUsing = isUsing;
				s.skipIntro = skip;
				s.isSecurityAlarmActive = alarm;
				if (cartel != null)
				{
					cartel.isUsing = cartelUsing;
				}
				s.dayWithoutInsurance = days;
			}
		});
		ok = after == days;
		return "uninsured, no alarm, past the grace days: days without insurance " + days + " -> " + after + " (a burglary roll counts one)";
	}

	/// Sawyer's Crew: the Janitorial Service costs nothing while the shop is not vandalized.
	internal static string SawyerCrew(out bool ok)
	{
		StoreService jan = StoreService.GetJanitorialService();
		int cost = jan.cost;
		bool unlocked = jan.unlocked;
		bool vandal = S.isVandalized;
		int now = Keeping(delegate
		{
			try
			{
				S.isVandalized = false;
				jan.UpdateCost();
				return jan.cost;
			}
			finally
			{
				jan.cost = cost;
				jan.unlocked = unlocked;
				S.isVandalized = vandal;
			}
		});
		ok = now == 0;
		return "janitorial cost after UpdateCost " + now + " (without the perk: day / 10 + 3)";
	}

	/// Diplomatic: trade earns a third more reputation (1000 credits with Security: 1000 x 1.33 / 400).
	internal static string Diplomatic(out bool ok)
	{
		double num = (repPer1000 != null) ? ((double)repPer1000.Invoke(null, new object[2] { 1.0, "FACTION_SECURITY" })) : (-1.0);
		ok = Math.Abs(num - 3.325) < 0.0001;
		return "reputation per 1000 credits with Security " + num.ToString("0.####") + " (without the perk 2.5)";
	}

	/// Intimidating (x0.67) or Blabbermouth (x1.33): evidence of a crime.
	internal static string Evidence(double factor, out bool ok)
	{
		SecData sec = S.secData;
		int got = Keeping(delegate
		{
			try
			{
				sec.CommitCrime("lab_probe", 100);
				int num = sec.crimeTypes.IndexOf("lab_probe");
				return (num >= 0) ? sec.crimeAmount[num] : (-1);
			}
			finally
			{
				int num2;
				while ((num2 = sec.crimeTypes.IndexOf("lab_probe")) >= 0)
				{
					sec.crimeTypes.RemoveAt(num2);
					sec.crimeAmount.RemoveAt(num2);
					sec.crimeAmountTotal.RemoveAt(num2);
				}
			}
		});
		ok = Math.Abs((double)got - 100.0 * factor) <= 1.0;
		return "a crime of 100 leaves " + got + " evidence (expected about " + (100.0 * factor).ToString("0") + ")";
	}

	/// Blabbermouth: half the reputation gained with the Black Market.
	internal static string BlabbermouthRep(out bool ok)
	{
		StoreReputation r = S.storeReputations.First((StoreReputation x) => x != null && x.factionId == "FACTION_BLACK_MARKET");
		double amount = r.GetReputationExact();
		double delta = Keeping(delegate
		{
			try
			{
				r.ModReputation(10.0);
				return r.GetReputationExact() - amount;
			}
			finally
			{
				repAmount.SetValue(r, amount);
			}
		});
		double num = 5.0 * (double)Difficulty.Rules.reputationGainMultiplier;
		ok = Math.Abs(delta - num) < 0.0001 || (amount + 10.0 > 200.0);
		return "+10 with the Black Market gives +" + delta.ToString("0.###") + " (expected +" + num.ToString("0.###") + ")";
	}

	/// The player whose seat this is, for asking the game as them.
	private static int PlayerOf(int seat)
	{
		foreach (int item in Net.Players.Keys)
		{
			if (Members.SeatOf(item) == seat)
			{
				return item;
			}
		}
		return -1;
	}

	/// Runs f on the server as that player, as during their request.
	internal static T AsSeat<T>(int seat, Func<T> f)
	{
		Perks.Enter(PlayerOf(seat));
		try
		{
			return f();
		}
		finally
		{
			Perks.Leave();
		}
	}

	/// Illiterate and Tinkerer, as the server answers during each player's own request; Osteoarthritis: the
	/// holder's dump searches a week later.
	internal static string Personal(int holder, int other, out bool ok)
	{
		bool flag = AsSeat(holder, () => !HealthData.CanRead());
		bool flag2 = AsSeat(other, () => HealthData.CanRead());
		bool flag3 = AsSeat(holder, () => StartingPerk.IsTinkerer());
		bool flag4 = AsSeat(other, () => !StartingPerk.IsTinkerer());
		StoreStation st = StoreStation.Instance;
		int day = st.dayCounter;
		int[] array = AsSeat(holder, delegate
		{
			try
			{
				st.dayCounter = 1;
				int num = ScavHelper.GetMaxScavAttempts();
				st.dayCounter = 15;
				return new int[2]
				{
					num,
					ScavHelper.GetMaxScavAttempts()
				};
			}
			finally
			{
				st.dayCounter = day;
			}
		});
		int[] array2 = AsSeat(other, delegate
		{
			try
			{
				st.dayCounter = 1;
				int num = ScavHelper.GetMaxScavAttempts();
				st.dayCounter = 15;
				return new int[2]
				{
					num,
					ScavHelper.GetMaxScavAttempts()
				};
			}
			finally
			{
				st.dayCounter = day;
			}
		});
		bool flag5 = (array[1] == array[0] - 2 || (array[0] - 2 < 3 && array[1] == 0)) && array2[1] == array2[0];
		ok = flag && flag2 && flag3 && flag4 && flag5;
		return "illiterate: holder can't read " + flag + ", other can " + flag2 + "; tinkerer for the holder " + flag3 + ", not the other " + flag4 + "; dump searches day 1 -> day 15: holder " + array[0] + " -> " + array[1] + ", other " + array2[0] + " -> " + array2[1];
	}
}
