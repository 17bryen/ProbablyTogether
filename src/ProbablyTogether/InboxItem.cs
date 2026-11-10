using Newtonsoft.Json.Linq;

namespace ProbablyTogether;

internal struct InboxItem
{
	public InboxEvent Event;

	public Conn Conn;

	public string Kind;

	public JToken Data;

	public string Text;

	internal int Cost;
}
