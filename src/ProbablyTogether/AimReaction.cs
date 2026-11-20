using System.Runtime.CompilerServices;
using HarmonyLib;

namespace ProbablyTogether;

/// The server's reaction pick is the counter's; a player's copy leaves its own pick out when matching the server's line.
[HarmonyPatch(typeof(AugHelper), "GetClientReactionUnholster")]
internal static class AimReaction
{
	private static readonly ConditionalWeakTable<Dialogue, object> serverOnly = new ConditionalWeakTable<Dialogue, object>();

	private static readonly ConditionalWeakTable<Dialogue, object> localOnly = new ConditionalWeakTable<Dialogue, object>();

	private static readonly object mark = new object();

	/// Local picks left out of the matching.
	internal static int LocalDropped;

	private static void Postfix(Dialogue __result)
	{
		if (__result == null || !Net.IsOnline)
		{
			return;
		}
		if (Net.IsServer)
		{
			serverOnly.Remove(__result);
			serverOnly.Add(__result, mark);
		}
		else if (!Net.Applying)
		{
			localOnly.Remove(__result);
			localOnly.Add(__result, mark);
		}
	}

	internal static bool IsServerOnly(Dialogue d)
	{
		return d != null && serverOnly.TryGetValue(d, out var _);
	}

	internal static bool IsLocalOnly(Dialogue d)
	{
		return d != null && localOnly.TryGetValue(d, out var _);
	}
}
