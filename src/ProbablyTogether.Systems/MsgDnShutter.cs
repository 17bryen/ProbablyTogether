using System.Collections.Generic;

namespace ProbablyTogether.Systems;

public sealed class MsgDnShutter
{
	public int day;

	public int by;

	public bool paper;

	public Seed seed;

	public long rd;

	public Dictionary<string, long> parts;

	public int xc;
}
