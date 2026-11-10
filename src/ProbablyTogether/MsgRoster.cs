using System.Collections.Generic;

namespace ProbablyTogether;

public sealed class MsgRoster
{
	public List<PlayerInfo> players;

	public int joined = -1;

	public int left = -1;

	public string why;
}
