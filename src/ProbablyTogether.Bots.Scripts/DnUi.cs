using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

internal static class DnUi
{
	internal const string OwnerText = "Owner line <b>lab</b>";

	internal const string LeaveText = "Owner line: bot2 may leave now";

	internal const string CleanIn = "B\u200bob\u202e <i>x</i> \ud83d\ude00 \ud800end\ufeff";

	internal const string WireIn = "B\u200bob\u202e <i>x</i> \ud83d\ude00 end\ufeff";

	internal const string CleanOut = "Bob ‹i>x‹/i> \ud83d\ude00 end";

	internal const string CueVoted = "voted run agreed";

	internal const string CueVotedB = "b agreed on the voted run";

	internal const string CueForce = "force run agreed";

	internal const string CueNight = "night ending agreed";

	internal const string KGated = "test.gated";

	internal static int GatedReached;

	private static bool gatedRegistered;

	internal static void RegisterGatedKind()
	{
		if (!gatedRegistered && Net.IsServer)
		{
			gatedRegistered = true;
			Net.On("test.gated", delegate(MsgDnEmpty m, int p)
			{
				GatedReached++;
				Log.Warn("lab: test.gated from " + Net.PlayerName(p) + " reached its handler (no gate stopped it)");
			}, null);
		}
	}
}
