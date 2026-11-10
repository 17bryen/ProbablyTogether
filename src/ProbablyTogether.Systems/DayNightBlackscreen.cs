using System;

namespace ProbablyTogether.Systems;

internal sealed class DayNightBlackscreen : IDisposable
{
	[ThreadStatic]
	private static DayNightBlackscreen armed;

	internal Func<Action, Action> WrapWith;

	internal bool Visual;

	internal bool Consumed;

	internal static DayNightBlackscreen Instant(bool visual)
	{
		DayNightBlackscreen result = new DayNightBlackscreen
		{
			Visual = visual
		};
		armed = result;
		return result;
	}

	internal static DayNightBlackscreen Wrap(Func<Action, Action> wrap)
	{
		DayNightBlackscreen result = new DayNightBlackscreen
		{
			WrapWith = wrap
		};
		armed = result;
		return result;
	}

	internal static DayNightBlackscreen Take()
	{
		DayNightBlackscreen dayNightBlackscreen = armed;
		if (dayNightBlackscreen == null)
		{
			return null;
		}
		armed = null;
		dayNightBlackscreen.Consumed = true;
		return dayNightBlackscreen;
	}

	public void Dispose()
	{
		if (armed == this)
		{
			armed = null;
		}
	}
}
