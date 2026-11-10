using ProbablyTogether.Bots;
using ProbablyTogether.Systems;

namespace ProbablyTogether;

internal static class Hooks
{
	private static void BotStart(string script)
	{
		BotRunner.Start(script);
	}

	internal static void StartBot(string script)
	{
		BotStart(script);
	}

	private static void CounterVisitSeq(ref int seq)
	{
		seq = Customers.VisitSeq();
	}

	internal static int VisitSeq()
	{
		int seq = 0;
		CounterVisitSeq(ref seq);
		if (seq == 0)
		{
			try
			{
				seq = Customers.Visit;
			}
			catch
			{
				seq = 0;
			}
		}
		return seq;
	}
}
