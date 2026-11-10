using System.Collections.Generic;

namespace ProbablyTogether;

public sealed class MsgEconCommissaryDone
{
	public string op;

	public string item;

	public int by;

	public Seed seed;

	public int credit;

	public int cash;

	public long d;

	public Dictionary<string, string> p;
}
