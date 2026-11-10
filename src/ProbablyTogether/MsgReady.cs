using System.Collections.Generic;

namespace ProbablyTogether;

public sealed class MsgReady
{
	public int seq;

	public MsgDigest digest;

	public Dictionary<string, string> econ;
}
