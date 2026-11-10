using Newtonsoft.Json.Linq;

namespace ProbablyTogether;

public sealed class MsgHello
{
	public int protocol;

	public string mod;

	public string game;

	public string name;

	public string steamId;

	public string auth;

	public JToken mods;

	public string build;

	public string beta;
}
