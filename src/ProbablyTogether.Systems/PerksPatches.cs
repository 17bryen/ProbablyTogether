using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether.Systems;

/// A personal perk is answered for the player whose doing it is.
[HarmonyPatch(typeof(StartingPerk), "IsPerkActive", new Type[] { typeof(string) })]
internal static class PerksIsActivePatch
{
	private static bool Prefix(string perkId, ref bool __result)
	{
		try
		{
			if (Perks.Answer(perkId, out var result))
			{
				__result = result;
				return false;
			}
		}
		catch (Exception ex)
		{
			if (Log.Allow("perks|active", 30000L, out var skipped))
			{
				Log.Warn("perks: answering for '" + perkId + "' threw" + Log.More(skipped) + ": " + ex.Message);
			}
		}
		return true;
	}
}

/// The game's health code reads the shop's list directly for these five.
[HarmonyPatch]
internal static class PerksHealthAsksPatch
{
	private static readonly Dictionary<string, string> asks = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		{ "IsNicotineAddicted", "nicotine_addiction" },
		{ "IsAlcoholAddicted", "alcohol_addiction" },
		{ "IsNarcoticAddicted", "narcotic_addiction" },
		{ "IsGamblingAddicted", "gambling_addiction" },
		{ "IsSentinel", "sentinel" }
	};

	private static IEnumerable<MethodBase> TargetMethods()
	{
		foreach (string key in asks.Keys)
		{
			MethodInfo methodInfo = AccessTools.Method(typeof(HealthData), key, Type.EmptyTypes, (Type[])null);
			if (methodInfo != null)
			{
				yield return methodInfo;
			}
			else
			{
				Log.Warn("perks: HealthData." + key + " not found in this game build");
			}
		}
	}

	private static bool Prefix(MethodBase __originalMethod, ref bool __result)
	{
		try
		{
			string text = asks[__originalMethod.Name];
			bool result;
			if ((Perks.KindOf(text) == Perks.Personal) ? Perks.Answer(text, out result) : Perks.AnswerAddicted(text, out result))
			{
				__result = result;
				return false;
			}
		}
		catch (Exception ex)
		{
			if (Log.Allow("perks|health", 30000L, out var skipped))
			{
				Log.Warn("perks: answering a health question threw" + Log.More(skipped) + ": " + ex.Message);
			}
		}
		return true;
	}
}

/// A perk goes: from its holder's record first.
[HarmonyPatch(typeof(StartingPerk), "RemovePerk", new Type[] { typeof(string) })]
internal static class PerksRemovePatch
{
	private static bool Prefix(string perkId)
	{
		try
		{
			return Perks.OnRemove(perkId);
		}
		catch (Exception ex)
		{
			Log.Warn("perks: taking '" + perkId + "' away threw: " + ex.Message);
			return true;
		}
	}
}

[HarmonyPatch(typeof(MindReprogrammerHelper), "Apply", new Type[] { typeof(GameItem), typeof(StartingPerk) })]
internal static class PerksReprogramPatch
{
	private static void Postfix(StartingPerk definition)
	{
		try
		{
			if (definition != null)
			{
				Perks.AfterReprogram(definition.id);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("perks: after the Mind Reprogrammer: " + ex.Message);
		}
	}
}

/// The night: the shop's part once, every player's own clocks after it.
[HarmonyPatch(typeof(HealthData), "OnSleep")]
internal static class PerksNightPatch
{
	private static bool Prefix(HealthData __instance)
	{
		return Perks.Night(__instance);
	}
}

[HarmonyPatch]
internal static class PerksNightShopPartsPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		MethodInfo methodInfo = AccessTools.Method(typeof(LiquidLuckHelper), "OnSleep", (Type[])null, (Type[])null);
		if (methodInfo != null)
		{
			yield return methodInfo;
		}
		MethodInfo methodInfo2 = AccessTools.Method(typeof(LotteryHelper), "OnDrawWiningNumber", (Type[])null, (Type[])null);
		if (methodInfo2 != null)
		{
			yield return methodInfo2;
		}
	}

	private static bool Prefix()
	{
		return !Perks.PlayerPassOfNight;
	}
}

/// The shop as a whole is asked: the day's queue, how many customers, the shutter opening in the morning.
[HarmonyPatch]
internal static class PerksNobodyPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		MethodInfo methodInfo = AccessTools.Method(typeof(StoreClientManager), "GenerateClient", Type.EmptyTypes, (Type[])null);
		if (methodInfo != null)
		{
			yield return methodInfo;
		}
		MethodInfo methodInfo2 = AccessTools.Method(typeof(StoreClientManager), "GetNormalCustomerCount", Type.EmptyTypes, (Type[])null);
		if (methodInfo2 != null)
		{
			yield return methodInfo2;
		}
		MethodInfo methodInfo3 = AccessTools.Method(typeof(StoreShutterButton), "OnShutterDoorOpenMorning", Type.EmptyTypes, (Type[])null);
		if (methodInfo3 != null)
		{
			yield return methodInfo3;
		}
	}

	private static void Prefix(out bool __state)
	{
		__state = Perks.Enabled;
		if (__state)
		{
			Perks.NobodyEnter();
		}
	}

	private static Exception Finalizer(bool __state, Exception __exception)
	{
		if (__state)
		{
			Perks.NobodyLeave();
		}
		return __exception;
	}
}

/// Side Street and Prime Real Estate: a customer less, or more, for every holder (the game counts one).
[HarmonyPatch(typeof(StoreClientManager), "GetNormalCustomerCount")]
internal static class PerksCustomerCountPatch
{
	private static void Postfix(ref int __result)
	{
		try
		{
			if (!Perks.Enabled)
			{
				return;
			}
			int num = Math.Max(0, Perks.Count("prime_real_estate") - 1) - Math.Max(0, Perks.Count("side_street") - 1);
			if (num != 0)
			{
				__result = Mathf.Clamp(__result + num, 0, 8);
			}
		}
		catch (Exception ex)
		{
			if (Log.Allow("perks|count", 30000L, out var skipped))
			{
				Log.Warn("perks: counting the day's customers threw" + Log.More(skipped) + ": " + ex.Message);
			}
		}
	}
}

/// Awful Negotiator: five in a hundred for every holder.
[HarmonyPatch]
internal static class PerksAwfulNegotiatorPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		foreach (string name in new string[2] { "AwfulNegotiatorBuyPenalty", "AwfulNegotiatorSellPenalty" })
		{
			MethodInfo methodInfo = AccessTools.Method(typeof(ItemFeatureList), name, Type.EmptyTypes, (Type[])null);
			if (methodInfo != null)
			{
				yield return methodInfo;
			}
		}
	}

	private static void Postfix(MethodBase __originalMethod, ItemFeature __result)
	{
		try
		{
			int num = Perks.Enabled ? Perks.Count("awful_negotiator") : 1;
			if (__result != null && num > 1)
			{
				__result.SetPreExposedValueMod((__originalMethod.Name == "AwfulNegotiatorBuyPenalty") ? (5 * num) : (-5 * num));
			}
		}
		catch
		{
		}
	}
}

/// Withdrawal: eight in a hundred for every player in it, per substance.
[HarmonyPatch(typeof(HealthData), "GetTotalAddictionPenalty")]
internal static class PerksAddictionPenaltyPatch
{
	private static void Postfix(ref int __result)
	{
		try
		{
			if (Perks.Enabled)
			{
				__result = Perks.AddictionPenalty();
			}
		}
		catch
		{
		}
	}
}

/// The welfare program's Monday food stamps: one for every holder.
[HarmonyPatch(typeof(StartingPerkContent), "HandleWelfareProgram")]
internal static class PerksWelfareStampsPatch
{
	private static bool busy;

	private static void Prefix()
	{
		if (busy || !Perks.Enabled)
		{
			return;
		}
		int num = Perks.Count("welfare_program");
		busy = true;
		try
		{
			for (int i = 1; i < num; i++)
			{
				StartingPerkContent.HandleWelfareProgram();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("perks: another holder's food stamps threw: " + ex.Message);
		}
		finally
		{
			busy = false;
		}
	}
}

/// The secret admirer: once for every player who has one.
[HarmonyPatch(typeof(StartingPerkContent), "HandleSecretAdmirerOnOpenShutter")]
internal static class PerksSecretAdmirerPatch
{
	private static bool Prefix()
	{
		return Perks.SecretAdmirer();
	}
}

/// The Commissary is open to a player, not to the shop.
[HarmonyPatch(typeof(CommissaryLocation), "IsDiscovered")]
internal static class PerksCommissaryPatch
{
	private static void Postfix(ref bool __result)
	{
		try
		{
			if (Perks.Enabled)
			{
				__result = Perks.CommissaryOpenFor(Perks.Actor);
			}
		}
		catch
		{
		}
	}
}

/// Agoraphobia rests from the player's own last outing.
[HarmonyPatch(typeof(StartingPerkContent), "GetAgoraphobiaReadyDay")]
internal static class PerksAgoraphobiaPatch
{
	private static void Postfix(ref int __result)
	{
		try
		{
			if (Perks.Enabled)
			{
				int num = Perks.LastOuting(Perks.Actor);
				__result = (num <= 0) ? 0 : (num + 3);
			}
		}
		catch
		{
		}
	}
}

/// A gambling addict buys the customer's tickets only when the addict has the counter.
[HarmonyPatch(typeof(StartingPerkContent), "HandleGamblingAddictionItemAdded")]
internal static class PerksGamblingPatch
{
	private static void Prefix(out int __state)
	{
		__state = int.MinValue;
		try
		{
			if (Perks.Enabled)
			{
				int num = Members.SeatOf(Customers.CounterHolder);
				__state = Perks.TurnTo((num > 0) ? num : (-1));
			}
		}
		catch
		{
		}
	}

	private static Exception Finalizer(int __state, Exception __exception)
	{
		if (__state != int.MinValue)
		{
			Perks.TurnTo(__state);
		}
		return __exception;
	}
}

/// The blackmailer exposed: what the shop loses goes in the holder's row of the ledger.
[HarmonyPatch(typeof(StoreClientListSpec), "TriggerBlackmailerCapture")]
internal static class PerksBlackmailPatch
{
	private static void Prefix(out KeyValuePair<int, Dictionary<string, double>> __state)
	{
		__state = default(KeyValuePair<int, Dictionary<string, double>>);
		try
		{
			if (Perks.Enabled)
			{
				Dictionary<string, double> dictionary = new Dictionary<string, double>(StringComparer.Ordinal);
				foreach (StoreReputation storeReputation in Game.Store.storeReputations)
				{
					if (storeReputation != null && !string.IsNullOrEmpty(storeReputation.factionId))
					{
						dictionary[storeReputation.factionId] = Standing.RepNow(storeReputation.factionId);
					}
				}
				__state = new KeyValuePair<int, Dictionary<string, double>>(Perks.HolderOf("blackmail"), dictionary);
			}
		}
		catch
		{
		}
	}

	private static void Postfix(KeyValuePair<int, Dictionary<string, double>> __state)
	{
		try
		{
			if (__state.Key > 0)
			{
				Perks.CreditRep(__state.Key, __state.Value);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("perks: putting the blackmailer's damage in a row threw: " + ex.Message);
		}
	}
}
