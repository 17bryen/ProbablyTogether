using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PicsPvLead : PicsBase2
{
	public override string Name => "pics-pv-lead";

	protected override void Build()
	{
		JoinN(4);
		WaitMark("w-pv-in", "pics-pv-in", 120f);
		OpenShop();
		Delay("queue-fills", 4f);
		float called = -100f;
		int calls = 0;
		S("call", 120f, delegate(BotStep st)
		{
			if (Customers.SomeoneAtCounter)
			{
				st.Detail = "a customer is at the counter after " + calls + " call(s)";
				return StepStatus.Pass;
			}
			if (Time.realtimeSinceStartup - called >= 6f)
			{
				called = Time.realtimeSinceStartup;
				calls++;
				Customers.RequestCall();
			}
			st.Detail = calls + " call(s), queue " + PicsBase.Ps.storeClientManager.clientStack.Count;
			return StepStatus.Running;
		});
		Do("cursor", delegate
		{
			Presence.FakePointer = PicsBase2.CursorArg(new Vector2(0.52f, 0.5f));
			Vector2? fakePointer = Presence.FakePointer;
			return "my pointer at " + fakePointer.ToString();
		});
		WaitMark("w-pv-done", "pics-pv-done", 300f);
	}
}
