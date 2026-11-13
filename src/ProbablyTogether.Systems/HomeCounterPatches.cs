using System;
using HarmonyLib;

namespace ProbablyTogether.Systems;

/// While the player is at Dr. Jackson's, what the game puts "on the counter" for the shop at home goes to the
/// shop's counter (HostTrip.HomeCounter).
[HarmonyPatch(typeof(GraphUtils), "EmporiumTryAdd", new Type[] { typeof(GraphNodeStorage), typeof(GameItem) })]
internal static class HomeCounterEmporiumTryAddPatch
{
	private static void Prefix(ref GraphNodeStorage currentNode)
	{
		currentNode = HostTrip.HomeCounter(currentNode);
	}
}

[HarmonyPatch(typeof(GraphUtils), "TryAcceptAllOrDestroy", new Type[] { typeof(GraphNodeStorage), typeof(GameItem), typeof(string) })]
internal static class HomeCounterTryAcceptAllOrDestroyPatch
{
	private static void Prefix(ref GraphNodeStorage currentNode)
	{
		currentNode = HostTrip.HomeCounter(currentNode);
	}
}

[HarmonyPatch(typeof(GraphUtils), "TryAccept", new Type[] { typeof(GraphNodeStorage), typeof(GameItem) })]
internal static class HomeCounterTryAcceptPatch
{
	private static void Prefix(ref GraphNodeStorage currentNode)
	{
		currentNode = HostTrip.HomeCounter(currentNode);
	}
}

[HarmonyPatch(typeof(GraphUtils), "TryAcceptAll", new Type[] { typeof(GraphNodeStorage), typeof(GameItem), typeof(int) })]
internal static class HomeCounterTryAcceptAllPatch
{
	private static void Prefix(ref GraphNodeStorage currentNode)
	{
		currentNode = HostTrip.HomeCounter(currentNode);
	}
}

[HarmonyPatch(typeof(GraphUtils), "TryAcceptAllMid", new Type[] { typeof(GameGridInventory), typeof(GameItem), typeof(int) })]
internal static class HomeCounterTryAcceptAllMidPatch
{
	private static void Prefix(ref GameGridInventory currentNode)
	{
		GraphNodeStorage val = HostTrip.HomeCounter((GraphNodeStorage)(object)currentNode);
		if (val is GameGridInventory val2)
		{
			currentNode = val2;
		}
	}
}
