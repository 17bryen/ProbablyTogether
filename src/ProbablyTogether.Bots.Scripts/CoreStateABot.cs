using System.Globalization;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CoreStateABot : CoreStateClientBase
{
	public override string Name => "core-state-a";

	protected override string R => "a";

	protected override void Build()
	{
		Common();
		Do("note-loads", delegate
		{
			Vars["loads"] = CstLab.loads.ToString(CultureInfo.InvariantCulture);
			return CstLab.loads + " snapshot load(s) so far";
		});
		Tick();
		Check("no-reload", () => CstLab.loads.ToString(CultureInfo.InvariantCulture) == Vars["loads"], () => CstLab.loads + " snapshot load(s) at the end, " + Vars["loads"] + " before the lock tests (b's leave, failed join and drop did not touch this copy)");
	}
}
