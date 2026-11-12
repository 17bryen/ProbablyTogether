namespace ProbablyTogether;

/// A player on a private trip redeems stamps or tickets kept at home: which ones to the server, what was redeemed to everyone.
public sealed class MsgEconRedeemHome
{
	public string op;

	public long[] ids;

	public int by;

	public Seed seed;
}
