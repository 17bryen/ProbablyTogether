using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PicsPvCursor : PicsBase2
{
	public override string Name => "pics-pv-cursor";

	protected override void Build()
	{
		JoinN(4);
		Do("cursor", delegate
		{
			Presence.FakePointer = PicsBase2.CursorArg(new Vector2(0.45f, 0.18f));
			Vector2? fakePointer = Presence.FakePointer;
			return "my pointer at " + fakePointer.ToString();
		});
		WaitMark("w-pv-done", "pics-pv-done", 420f);
	}
}
