namespace ProbablyTogether;

public sealed class PlayerInfo
{
	public int Id;

	public string Name = "";

	public ulong SteamId;

	/// The player's permanent seat in this shop (Systems.Members); 0 for a guest.
	public int Seat;
}
