using System.Globalization;

namespace ProbablyTogether.UI;

public static class Brand
{
	public const string Name = "ProbablyTogether";

	public const string WorkshopId = "3832445441";

	public const string Host = "Survival Servers";

	public const string RentUrl = "https://www.survivalservers.com/services/game_servers/probably_stolen/";

	public const string RentUrlFromList = "https://www.survivalservers.com/services/game_servers/probably_stolen/";

	public const string ServerListUrl = "https://api.survivalservers.com/probably_together/v1/servers";

	public static bool HasWorkshopPage
	{
		get
		{
			if ("3832445441".Length > 0 && ulong.TryParse("3832445441", NumberStyles.None, CultureInfo.InvariantCulture, out var result))
			{
				return result != 0;
			}
			return false;
		}
	}

	public static string WorkshopUrl => "https://steamcommunity.com/sharedfiles/filedetails/?id=3832445441";

	public static string[] AboutParagraphs(string chatKey)
	{
		return new string[4]
		{
			Lang.Trf("Online co-op for Probably Stolen, made by {0}. No server yet? Press Rent a server to get one from {0}, ready to join.", "Survival Servers"),
			Lang.Tr("Type the server's address and press Join (the server's panel shows it), or press Server list to pick one."),
			Lang.Trf("One player serves the counter at a time while the others work the back room. The night starts when everyone is ready. Press {0} to chat.", string.IsNullOrEmpty(chatKey) ? "T" : chatKey),
			Lang.Tr("A free, unofficial mod. Not made by or affiliated with Questing Goose Studio or tinyBuild.")
		};
	}
}
