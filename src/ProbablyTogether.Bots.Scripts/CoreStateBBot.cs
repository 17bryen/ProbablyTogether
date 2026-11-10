using System.Globalization;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CoreStateBBot : CoreStateClientBase
{
	private string address;

	private float loadingSince = -1f;

	public override string Name => "core-state-b";

	protected override string R => "b";

	protected override void Build()
	{
		Common();
		Step("until-leave", 200f, delegate(BotStep st)
		{
			CstLab.ClientTick();
			st.Detail = "told to leave; lock view " + Net.PlayerName(Locks.HeldBy("cst.test"));
			return CstLab.leaveOrdered ? StepStatus.Pass : StepStatus.Running;
		});
		Check("holding", () => Locks.HeldBy("cst.test") == Net.LocalPlayer, () => "lock view " + Locks.HeldBy("cst.test") + ", me " + Net.LocalPlayer);
		Do("leave", delegate
		{
			address = (string.IsNullOrEmpty(Args.Join) ? Net.ServerAddress : Args.Join);
			Net.Disconnect();
			return "left " + address + " holding the lock";
		});
		WaitFor("menu", 60f, () => Net.ClientState == ClientState.Idle && BotProbes.Scene == "EmporiumMenu", () => "scene " + BotProbes.Scene);
		Do("wrong-password", delegate
		{
			Net.Connect(address, CstLab.LabPassword + "-wrong", Args.Name);
			return "joining with a wrong password";
		});
		WaitFor("refused-as-data", 30f, () => Net.ClientState == ClientState.Idle && Net.LastRefuseCode != null, () => "Net.LastRefuseCode " + Net.LastRefuseCode + ", reason '" + Net.LastRefuseReason + "'");
		Check("refuse-code", () => Net.LastRefuseCode == "password" && Net.LastRefuseReason == "Wrong password.", () => "code " + Net.LastRefuseCode + ", reason '" + Net.LastRefuseReason + "'");
		Delay("pause", 2f);
		string[] array = new string[2] { "early", "late" };
		foreach (string text in array)
		{
			string d = text;
			Do("join-to-drop-" + d, delegate
			{
				loadingSince = -1f;
				Net.Connect(address, CstLab.LabPassword, Args.Name);
				return "joining; will drop the connection " + ((d == "early") ? "as soon as the snapshot arrives" : "1 s or more into the snapshot load");
			});
			Step("drop-" + d, 60f, delegate(BotStep st)
			{
				ClientState clientState = Net.ClientState;
				if (clientState == ClientState.Ready)
				{
					st.Detail = "the load finished before the drop";
					return StepStatus.Fail;
				}
				bool num2;
				if (!(d == "early"))
				{
					num2 = clientState == ClientState.Loading;
				}
				else
				{
					if (clientState == ClientState.AwaitSnapshot)
					{
						goto IL_003e;
					}
					num2 = clientState == ClientState.Loading;
				}
				if (!num2)
				{
					return StepStatus.Running;
				}
				goto IL_003e;
				IL_003e:
				if (loadingSince < 0f)
				{
					loadingSince = Time.realtimeSinceStartup;
				}
				if (d == "late" && Time.realtimeSinceStartup - loadingSince < 1f)
				{
					return StepStatus.Running;
				}
				string scene = BotProbes.Scene;
				Net.Disconnect();
				st.Detail = "dropped the connection " + (Time.realtimeSinceStartup - loadingSince).ToString("F1", CultureInfo.InvariantCulture) + " s after the load began (state " + clientState.ToString() + ", scene " + scene + ")";
				return StepStatus.Pass;
			});
			WaitFor("back-at-menu-" + d, 90f, () => Net.ClientState == ClientState.Idle && BotProbes.Scene == "EmporiumMenu" && !Game.ShopLoaded, () => "scene " + BotProbes.Scene);
			Delay("settle-" + d, 3f);
		}
		Do("rejoin", delegate
		{
			Net.Connect(address, CstLab.LabPassword, Args.Name);
			return "joining again";
		});
		WaitFor("live-again", 150f, () => Net.IsLive && BotProbes.InShop && Net.ClientState == ClientState.Ready, () => "player " + Net.LocalPlayer + "; " + DayLog.Last);
		Do("hi-again", delegate
		{
			Net.ToServer("cst.hi", new CstHi
			{
				role = "b"
			});
			return "I am b again (player " + Net.LocalPlayer + ")";
		});
		Check("replayed-actors", () => CstLab.ActorsOk(3, replayed: true), () => "seen " + string.Join(" ", CstLab.actorSeen) + "; " + DayLog.Last);
		Check("sees-a-lock", () => Locks.HeldBy("cst.test") > 0 && Locks.HeldBy("cst.test") != Net.LocalPlayer, () => "lock view " + Net.PlayerName(Locks.HeldBy("cst.test")));
		AskServer("ask-2");
		SameAsServer("same-features-2", "cst.features");
		SameAsServer("same-digest-2", "cst.digest");
		Tick();
	}
}
