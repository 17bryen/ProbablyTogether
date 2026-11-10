namespace ProbablyTogether.Systems;

public sealed class MsgDnState
{
	public MsgDnReadyState ready;

	public bool runOver;

	public string ending;

	public MsgDnVotes votes;

	public bool mapOpen;
}
