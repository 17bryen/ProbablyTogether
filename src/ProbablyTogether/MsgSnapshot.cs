using System.Collections.Generic;

namespace ProbablyTogether;

public sealed class MsgSnapshot
{
	public int seq;

	public string reason;

	public int day;

	public int slot;

	public string save;

	public string saveB64;

	public int uidCounter;

	public int items;

	public int logCount;

	public Dictionary<string, int> locks;
}
