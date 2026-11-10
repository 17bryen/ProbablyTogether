using System.Collections.Generic;

namespace ProbablyTogether;

public sealed class MsgReplay
{
	public int count;

	public Dictionary<string, int> locks;
}
