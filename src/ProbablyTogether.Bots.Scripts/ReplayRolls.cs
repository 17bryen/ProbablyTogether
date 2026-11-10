namespace ProbablyTogether.Bots.Scripts;

public sealed class ReplayRolls
{
	public int a;

	public int b;

	public int c;

	public int d;

	public int uid;

	public int placed;

	public int cash;

	public string faction = "";

	public string pick = "";

	public override string ToString()
	{
		return "a=" + a + " b=" + b + " c=" + c + " d=" + d + " faction=" + faction + " pick=" + pick + " uid=" + uid + " placed=" + placed + " cash=" + cash;
	}
}
