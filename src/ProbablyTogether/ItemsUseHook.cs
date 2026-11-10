using System.Reflection;

namespace ProbablyTogether;

internal static class ItemsUseHook
{
	internal static bool Found(MethodBase m, string name)
	{
		if (m != null)
		{
			return true;
		}
		Log.Warn("items-use: " + name + " not found in this game build: that hook is off");
		return false;
	}
}
