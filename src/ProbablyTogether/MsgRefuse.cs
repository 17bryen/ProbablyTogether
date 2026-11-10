using Newtonsoft.Json.Linq;

namespace ProbablyTogether;

public sealed class MsgRefuse
{
	public string code;

	public string reason;

	public JToken mods;
}
