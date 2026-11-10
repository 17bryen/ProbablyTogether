namespace ProbablyTogether.Bots;

internal static class BotStartGuard
{
	private static bool done;

	[SystemTick]
	private static void Tick()
	{
		if (!done)
		{
			if (string.IsNullOrEmpty(Args.Bot) || BotRunner.WasStarted)
			{
				done = true;
			}
			else if (Net.IsServer && Server.ShopReady)
			{
				done = true;
				Log.Warn("bot: the menu jobs never started the bot runner (the server's shop load won the 2 s race); starting it now");
				BotRunner.Start(Args.Bot);
			}
		}
	}
}
