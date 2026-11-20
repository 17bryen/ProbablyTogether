using System;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether.Bots.Scripts;

/// Calls ItemMouseDragHandler.EndDrag by reflection; Invoke does not fill optional parameters, so End passes them explicitly.
internal static class BotDrag
{
	internal static readonly MethodInfo EndDrag = AccessTools.Method(typeof(ItemMouseDragHandler), "EndDrag", (Type[])null, (Type[])null);

	/// Ends the current drag as a drop (not cancelled), whatever the game's signature.
	internal static object End(object handler)
	{
		if (EndDrag == null)
		{
			throw new InvalidOperationException("ItemMouseDragHandler.EndDrag not found");
		}
		ParameterInfo[] parameters = EndDrag.GetParameters();
		object[] array = new object[parameters.Length];
		for (int i = 0; i < parameters.Length; i++)
		{
			array[i] = (parameters[i].ParameterType == typeof(bool)) ? ((object)false) : (parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null);
		}
		return EndDrag.Invoke(handler, array);
	}
}
