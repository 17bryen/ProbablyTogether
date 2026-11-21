namespace ProbablyTogether;

public abstract class NegoMsg
{
	public NegoVisit v;

	public int player;

	public Seed seed;

	public NegoAfter after;

	/// Where bought items go: 1 into the shop's storage, 0 they stay on the counter, -1 the game decides.
	/// The buyer sends their own setting so every copy files the purchase the same way.
	public int inv = -1;
}
