using System.Collections.Generic;

namespace ProbablyTogether;

public sealed class MsgAppraisalBlock
{
	public string typeId;

	public bool allowed;

	public int player;

	public List<string> blacklist;
}
