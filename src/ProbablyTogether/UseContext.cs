namespace ProbablyTogether;

public sealed class UseContext
{
	internal int ConfirmCount;

	internal bool ConfirmCallback;

	internal UseOp OriginalOp;

	public UseOp Op { get; internal set; }

	public GameItem Source { get; internal set; }

	public GameItem Target { get; internal set; }

	public long SourceId { get; internal set; }

	public long TargetId { get; internal set; }

	public int Player { get; internal set; }

	public bool OnServer { get; internal set; }

	public bool Quiet { get; internal set; }

	public long Seq { get; internal set; }

	public bool Replayed { get; internal set; }

	public int Index { get; internal set; }

	public string Text { get; internal set; }

	public bool Yes { get; internal set; }

	public bool TouchesCounter
	{
		get
		{
			if (!ItemsUse.InCounter(Source))
			{
				return ItemsUse.InCounter(Target);
			}
			return true;
		}
	}

	public override string ToString()
	{
		return Op.ToString() + ((Op == UseOp.Confirm) ? ("(" + OriginalOp.ToString() + (Yes ? ", yes" : ", no") + ")") : "") + " " + ItemsUse.Describe(Source) + ((Target != null) ? (" -> " + ItemsUse.Describe(Target)) : "") + " by " + Net.PlayerName(Player);
	}
}
