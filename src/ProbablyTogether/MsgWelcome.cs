using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ProbablyTogether;

public sealed class MsgWelcome
{
	public int you;

	public string server;

	public int maxPlayers;

	public List<PlayerInfo> players;

	public JToken mods;

	public JToken modInfo;

	public string build;

	/// The server's counter takeover setting (seconds; negative or missing = strict lock).
	public double takeover = -1.0;

	public bool outings;
}
