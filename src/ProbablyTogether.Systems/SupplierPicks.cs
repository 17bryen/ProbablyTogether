using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Systems;

/// The game rolls the day's supplier picks on first ask and keeps them until the opening;
/// the server's picks travel with the opening so every copy opens with the same ones.
internal static class SupplierPicks
{
	// A literal type: the game's mod scanner refuses reflection on computed types.
	private static readonly FieldInfo field = Find();

	internal static int Adopted;

	private static FieldInfo Find()
	{
		try
		{
			return AccessTools.Field(typeof(ShowcaseHelper), "pendingSupplierPicks");
		}
		catch
		{
			return null;
		}
	}

	private static Dictionary<string, string> Live()
	{
		try
		{
			return field?.GetValue(null) as Dictionary<string, string>;
		}
		catch
		{
			return null;
		}
	}

	/// Server, before opening: what this copy already picked. Null when unreadable: every copy forgets its own.
	internal static Dictionary<string, string> ForOpening()
	{
		Dictionary<string, string> dictionary = Live();
		if (dictionary == null)
		{
			ShowcaseHelper.ClearPendingSupplierPicks();
			return null;
		}
		return new Dictionary<string, string>(dictionary);
	}

	/// A player's copy, before it replays the opening: the server's picks instead of its own.
	internal static void Adopt(Dictionary<string, string> fromServer)
	{
		try
		{
			ShowcaseHelper.ClearPendingSupplierPicks();
			Dictionary<string, string> dictionary = Live();
			if (fromServer == null || dictionary == null)
			{
				return;
			}
			foreach (KeyValuePair<string, string> item in fromServer)
			{
				dictionary[item.Key] = item.Value;
			}
			Adopted++;
		}
		catch (Exception ex)
		{
			Log.Warn("dayNight: taking over the server's supplier picks threw: " + ex.Message);
		}
	}
}
