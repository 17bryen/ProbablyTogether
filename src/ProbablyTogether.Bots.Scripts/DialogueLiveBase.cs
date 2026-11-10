namespace ProbablyTogether.Bots.Scripts;

public abstract class DialogueLiveBase : DialogueBotBase
{
	protected const string Board = "appraisalBoardIntro";

	protected const long Wild = 8376L;

	public override float TimeoutSeconds => 480f;

	protected static bool FutureHasBoard2()
	{
		if (Game.ShopLoaded && Game.Store.futurStoreClientIdQueue != null)
		{
			return Game.Store.futurStoreClientIdQueue.Contains("appraisalBoardIntro2");
		}
		return false;
	}
}
