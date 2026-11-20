using System.Collections.Generic;

namespace ProbablyTogether;

public sealed class MsgDlgInline
{
	public string fp;

	public List<MsgDlgLine> lines;

	/// Text only: a copy that cannot match the line shows the server's text and goes on instead of asking for a resync.
	public bool plain;

	/// Made on the server alone, so every copy shows the text at once.
	public bool own;
}
