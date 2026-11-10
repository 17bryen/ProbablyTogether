using System.Collections.Generic;

namespace ProbablyTogether;

public sealed class ItemsOps
{
	public int seq;

	public int actor;

	public string kind;

	public int q;

	public ItemRef at;

	public Seed seed;

	public Tape tape;

	public List<ItemsOp> ops;

	public long dg;

	public long ra;

	public bool nego;

	public long negoId;
}
