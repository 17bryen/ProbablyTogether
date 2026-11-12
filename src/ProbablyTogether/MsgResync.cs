namespace ProbablyTogether;

public sealed class MsgResync
{
	public string reason;

	/// This side of the difference (DesyncReport.Build): fingerprint, readable parts, the last log lines.
	public string report;

	/// True: a difference this copy sits out. The server keeps the report and sends no snapshot.
	public bool hold;
}
