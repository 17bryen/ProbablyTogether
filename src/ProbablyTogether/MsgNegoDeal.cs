namespace ProbablyTogether;

public sealed class MsgNegoDeal : NegoMsg
{
	public long[] ids;

	public bool single;

	public bool ignoreBudget;
}
