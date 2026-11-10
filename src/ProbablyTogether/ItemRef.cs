namespace ProbablyTogether;

public sealed class ItemRef
{
	public string r;

	public long o;

	public int n;

	public long i;

	public override string ToString()
	{
		string text = r;
		if (text == null)
		{
			if (i == 0L)
			{
				return "item#" + o + "/" + n;
			}
			text = "item#" + i;
		}
		return text;
	}

	public bool Same(ItemRef b)
	{
		if (b != null && r == b.r && o == b.o && n == b.n)
		{
			return i == b.i;
		}
		return false;
	}
}
