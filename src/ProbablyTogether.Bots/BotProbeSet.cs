using System;
using System.Globalization;

namespace ProbablyTogether.Bots;

public abstract class BotProbeSet
{
	public abstract void Register();

	protected static string Str(object o)
	{
		if (o != null)
		{
			return Convert.ToString(o, CultureInfo.InvariantCulture);
		}
		return "";
	}
}
