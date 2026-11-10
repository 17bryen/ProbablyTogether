using System.Collections.Generic;

namespace ProbablyTogether;

public sealed class MsgEconBought
{
	public string id;

	public int by;

	public Seed seed;

	public int cost;

	public int favor;

	public int cash;

	public long d;

	public Dictionary<string, string> p;
}
