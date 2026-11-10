using System.Collections.Generic;

namespace ProbablyTogether;

public sealed class MachineDone
{
	public long seq;

	public int actor;

	public int q;

	public string op;

	public long m;

	public int w = -1;

	public string t;

	public float v;

	public float v2;

	public bool on;

	public List<MachineTag> tags;

	public Seed seed;

	public Tape tape;

	public long dg;

	public long rd;
}
