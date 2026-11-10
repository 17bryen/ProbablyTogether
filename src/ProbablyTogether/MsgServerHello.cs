namespace ProbablyTogether;

public sealed class MsgServerHello
{
	public int protocol;

	public string mod;

	public string game;

	public string name;

	public string nonce;

	public string salt;

	public bool passworded;

	public int players;

	public int maxPlayers;
}
