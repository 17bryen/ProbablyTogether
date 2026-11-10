namespace ProbablyTogether;

public enum ClientState
{
	Idle,
	Connecting,
	Handshake,
	AwaitSnapshot,
	Loading,
	Ready
}
