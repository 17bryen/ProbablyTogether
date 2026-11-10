using System.Collections.Generic;

namespace ProbablyTogether;

public sealed class Tape
{
	public List<int> uids = new List<int>();

	public List<long> ids = new List<long>();

	public int after;
}
