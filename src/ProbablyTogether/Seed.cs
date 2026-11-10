namespace ProbablyTogether;

public sealed class Seed
{
	public string RandomState = "";

	public int SysSeed;

	public int UidBase;

	public int UidAfter;

	public long After;

	public int Actor = -1;

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
