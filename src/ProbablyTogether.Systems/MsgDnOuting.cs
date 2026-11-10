using System.Collections.Generic;

namespace ProbablyTogether.Systems;

public sealed class MsgDnOuting
{
	public string what;

	public int day;

	public int by;

	public string locationId;

	public long backpackId;

	public Seed seed;

	public long rd;

	public Dictionary<string, long> parts;

	public int attempts;

	public int wound;
}
