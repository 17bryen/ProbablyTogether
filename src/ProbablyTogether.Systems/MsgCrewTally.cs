using System.Collections.Generic;

namespace ProbablyTogether.Systems;

public sealed class MsgCrewTally
{
	public int day;

	public int seq;

	public string why;

	public List<MsgCrewRow> rows;

	public MsgCrewBest best;
}
