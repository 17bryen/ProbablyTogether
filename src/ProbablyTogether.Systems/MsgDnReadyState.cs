namespace ProbablyTogether.Systems;

public sealed class MsgDnReadyState
{
	public int[] ready;

	public int[] waiting;

	public int secondsLeft;

	public int by;

	public string why;
}
