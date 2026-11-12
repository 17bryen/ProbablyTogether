namespace ProbablyTogether;

public sealed class Seed
{
	public string RandomState = "";

	public int SysSeed;

	public int UidBase;

	public int UidAfter;

	public long After;

	public int Actor = -1;

	/// The player whose action this is (0: nobody); replays run with that player's wound (Systems.Wounds).
	public int P;

	public bool IsEmpty
	{
		get
		{
			if (string.IsNullOrEmpty(RandomState) && SysSeed == 0)
			{
				return UidBase == 0;
			}
			return false;
		}
	}
}
