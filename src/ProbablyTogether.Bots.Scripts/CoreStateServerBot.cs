using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CoreStateServerBot : BotScript
{
	private string winner = "";

	private string loser = "";

	private string bName = "";

	private int bSyncingId = -1;

	private float orderAt;

	private int repliesBefore;

	public override string Name => "core-state-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 400f;

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop-loaded", 300f, () => Net.IsLive && BotProbes.InShop, () => "scene " + BotProbes.Scene);
		Do("fixture", delegate
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			using (Net.Apply())
			{
				GameItem val = WaterPremadeHelper.BaseWaterAsHQWater("bottled_water");
				if (GraphUtils.TryAcceptAll((GraphNodeStorage)(object)instance.hirelingInv, val, -1) != 0 || val.parentInventory == null)
				{
					throw new InvalidOperationException("the bottle did not fit in the hireling inventory");
				}
			}
			bool flag = Snapshot.NewMorning("core-state fixture");
			if (ProbablyTogether.Server.PlayerCount > 0)
			{
				Snapshot.SendToAll("core-state fixture");
			}
			return "water in the hireling inventory: " + CstLab.Water() + "; " + CstLab.Features() + "; new morning snapshot " + (flag ? "taken" : "FAILED");
		});
		Check("actor-server", delegate
		{
			int actor = Net.Actor;
			int actor2;
			int actor3;
			int actor4;
			using (Net.Apply(5))
			{
				actor2 = Net.Actor;
				using (Net.Apply())
				{
					actor3 = Net.Actor;
				}
				using (Net.Apply(-1))
				{
					actor4 = Net.Actor;
				}
			}
			int actor5 = Net.Actor;
			Seed seed;
			int actor6;
			using (Replay.Server(out seed, 7))
			{
				actor6 = Net.Actor;
			}
			Vars["actor"] = "before " + actor + ", Apply(5) " + actor2 + ", nested Apply() " + actor3 + ", Apply(-1) " + actor4 + ", after " + actor5 + ", Replay.Server(actor 7) " + actor6 + " seed.Actor " + seed.Actor + ", after " + Net.Actor;
			return actor == -1 && actor2 == 5 && actor3 == 5 && actor4 == -1 && actor5 == -1 && actor6 == 7 && seed.Actor == 7 && Net.Actor == -1;
		}, () => Vars["actor"]);
		WaitFor("both-live", 200f, () => CstLab.roles.ContainsKey("a") && CstLab.roles.ContainsKey("b") && Net.IsPlayerLive(CstLab.roles["a"]) && Net.IsPlayerLive(CstLab.roles["b"]), () => string.Join(", ", CstLab.roles.Select((KeyValuePair<string, int> kv) => kv.Key + "=" + Net.PlayerName(kv.Value) + "#" + kv.Value)));
		Do("ask-a-early", delegate
		{
			CstLab.Order("a", "report", 0f, "early");
			return "asked a for a first report (its snapshot load count)";
		});
		WaitFor("a-early", 15f, () => CstLab.reports.ContainsKey("a.early"), () => "a: loads " + CstLab.reports["a.early"].loads + ", digest " + CstLab.reports["a.early"].digest);
		Do("actor-broadcasts", delegate
		{
			int num2 = CstLab.roles["a"];
			int num3 = CstLab.roles["b"];
			bName = CstLab.roleNames["b"];
			CstActor cstActor = new CstActor
			{
				n = 1,
				expect = num2
			};
			using (Replay.Server(out cstActor.seed, num2))
			{
			}
			Net.ToAll("cst.actor", cstActor);
			CstActor cstActor2 = new CstActor
			{
				n = 2,
				expect = num3
			};
			using (Net.Apply(num3))
			{
				using (Replay.Server(out cstActor2.seed))
				{
				}
			}
			Net.ToAll("cst.actor", cstActor2);
			CstActor cstActor3 = new CstActor
			{
				n = 3,
				expect = num2,
				overrideActor = num2
			};
			using (Replay.Server(out cstActor3.seed))
			{
			}
			Net.ToAll("cst.actor", cstActor3);
			return "#1 seed.Actor " + cstActor.seed.Actor + " (a=" + num2 + "), #2 inherited " + cstActor2.seed.Actor + " (b=" + num3 + "), #3 none " + cstActor3.seed.Actor + " + client override " + num2 + "; b is '" + bName + "'";
		});
		Step("contend", 20f, delegate(BotStep st)
		{
			if (orderAt == 0f)
			{
				repliesBefore = CstLab.serverReplies.Count;
				CstLab.Order("a", "take");
				CstLab.Order("b", "take");
				orderAt = Time.realtimeSinceStartup;
				return StepStatus.Running;
			}
			List<KeyValuePair<int, CstLockReply>> list = (from r in CstLab.serverReplies.Skip(repliesBefore)
				where r.Value.op == "take"
				select r).ToList();
			if (list.Count < 2)
			{
				return StepStatus.Running;
			}
			int num2 = CstLab.roles["a"];
			_ = CstLab.roles["b"];
			List<KeyValuePair<int, CstLockReply>> list2 = list.Where((KeyValuePair<int, CstLockReply> r) => r.Value.ok).ToList();
			st.Detail = "both asked in the same frame: " + string.Join(", ", list.Select((KeyValuePair<int, CstLockReply> r) => Net.PlayerName(r.Key) + (r.Value.ok ? " WON" : (" refused (holder " + Net.PlayerName(r.Value.holder) + ")")))) + "; holder now " + Net.PlayerName(Locks.Holder("cst.test"));
			if (list2.Count != 1)
			{
				return StepStatus.Fail;
			}
			winner = ((list2[0].Key == num2) ? "a" : "b");
			loser = ((winner == "a") ? "b" : "a");
			return (list.First((KeyValuePair<int, CstLockReply> r) => !r.Value.ok).Value.holder == list2[0].Key && Locks.Holder("cst.test") == list2[0].Key) ? StepStatus.Pass : StepStatus.Fail;
		});
		Do("loser-retries", delegate
		{
			CstLab.lastFreeAt = -1f;
			CstLab.Order(loser, "retry", 2f);
			return loser + " asks every 2 s; " + winner + " holds and stays idle";
		});
		WaitFor("idle-timeout", 80f, () => CstLab.lastFreeAt > 0f, () => "lock freed " + (CstLab.lastFreeAt - CstLab.takenAt[CstLab.roles[winner]]).ToString("F1", CultureInfo.InvariantCulture) + " s after " + winner + " took it (60 s idle rule; swept once a second)");
		Check("idle-60s", delegate
		{
			float num2 = CstLab.lastFreeAt - CstLab.takenAt[CstLab.roles[winner]];
			return num2 >= 59.5f && num2 <= 62.5f;
		}, () => "idle " + (CstLab.lastFreeAt - CstLab.takenAt[CstLab.roles[winner]]).ToString("F1", CultureInfo.InvariantCulture) + " s");
		WaitFor("idle-pass", 6f, () => Locks.Holder("cst.test") == CstLab.roles[loser], () => loser + " (" + Net.PlayerName(CstLab.roles[loser]) + ") holds it " + (Time.realtimeSinceStartup - CstLab.lastFreeAt).ToString("F1", CultureInfo.InvariantCulture) + " s after it was freed; events " + string.Join(" ", CstLab.lockEvents));
		Do("release", delegate
		{
			CstLab.Order(loser, "release");
			return "told " + loser + " to release";
		});
		WaitFor("released", 10f, () => Locks.Holder("cst.test") == -1, () => "free; events " + string.Join(" ", CstLab.lockEvents));
		Do("b-takes", delegate
		{
			CstLab.Order("b", "take");
			return "told b to take it";
		});
		WaitFor("b-holds", 10f, () => Locks.Holder("cst.test") == CstLab.roles["b"], () => "holder " + Net.PlayerName(Locks.Holder("cst.test")));
		Do("a-asks", delegate
		{
			repliesBefore = CstLab.serverReplies.Count;
			CstLab.Order("a", "take");
			return "told a to take it while b holds it";
		});
		Step("a-refused", 10f, delegate(BotStep st)
		{
			KeyValuePair<int, CstLockReply> keyValuePair = CstLab.serverReplies.Skip(repliesBefore).FirstOrDefault((KeyValuePair<int, CstLockReply> x) => x.Key == CstLab.roles["a"] && x.Value.op == "take");
			if (keyValuePair.Value == null)
			{
				return StepStatus.Running;
			}
			st.Detail = "a's take " + (keyValuePair.Value.ok ? "WON (wrong)" : ("refused, holder " + Net.PlayerName(keyValuePair.Value.holder)));
			return (!keyValuePair.Value.ok && keyValuePair.Value.holder == CstLab.roles["b"]) ? StepStatus.Pass : StepStatus.Fail;
		});
		Do("b-leaves", delegate
		{
			CstLab.TrackStates();
			string password = ProbablyTogether.Server.Config.password;
			if (string.IsNullOrEmpty(password))
			{
				ProbablyTogether.Server.Config.password = CstLab.LabPassword;
			}
			CstLab.Order("a", "retry", 1f);
			CstLab.Order("b", "leave");
			return "a asks every 1 s; b told to disconnect while holding the lock; server password " + (string.IsNullOrEmpty(password) ? "turned on for the refusal test" : "already set");
		});
		WaitFor("disconnect-pass", 20f, delegate
		{
			CstLab.TrackStates();
			int key = CstLab.roles["b"];
			return CstLab.leftAt.ContainsKey(key) && Locks.Holder("cst.test") == CstLab.roles["a"];
		}, delegate
		{
			int key = CstLab.roles["b"];
			return "b left (" + CstLab.leftWhile[key] + "); a holds the lock " + (Time.realtimeSinceStartup - CstLab.leftAt[key]).ToString("F1", CultureInfo.InvariantCulture) + " s later; events " + string.Join(" ", CstLab.lockEvents);
		});
		string[] array = new string[2] { "early", "late" };
		foreach (string text in array)
		{
			string d = text;
			Step("b-loading-" + d, 90f, delegate(BotStep st)
			{
				CstLab.TrackStates();
				Server.Peer peer = CstLab.PeerNamed(bName);
				if (peer == null || peer.State != ProbablyTogether.Server.PeerState.Syncing || peer.Id == bSyncingId)
				{
					return StepStatus.Running;
				}
				bSyncingId = peer.Id;
				st.Detail = bName + " joined again as player " + peer.Id + " and is loading snapshot #" + peer.SnapshotSeq + " (" + d + " drop coming)";
				return StepStatus.Pass;
			});
			Step("mid-load-cleanup-" + d, 90f, delegate(BotStep st)
			{
				CstLab.TrackStates();
				if (!CstLab.leftAt.ContainsKey(bSyncingId))
				{
					return StepStatus.Running;
				}
				int num2 = CstLab.roles["a"];
				bool flag = !ProbablyTogether.Server.ByPlayer.ContainsKey(bSyncingId) && !Net.Players.ContainsKey(bSyncingId) && ProbablyTogether.Server.PlayerCount == 1 && ProbablyTogether.Server.ByConn.Values.All((Server.Peer c) => c.Info == null || c.Info.Id != bSyncingId) && Net.IsPlayerLive(num2) && Locks.Holder("cst.test") == num2;
				st.Detail = "player " + bSyncingId + " left while " + CstLab.leftWhile[bSyncingId] + "; players now " + ProbablyTogether.Server.PlayerCount + " (" + string.Join(", ", ProbablyTogether.Server.ByPlayer.Values.Select((Server.Peer x) => x.Info.Name + "=" + x.State)) + "), connections " + ProbablyTogether.Server.ByConn.Count + ", a live " + Net.IsPlayerLive(num2) + ", lock holder " + Net.PlayerName(Locks.Holder("cst.test"));
				return (flag && CstLab.leftWhile[bSyncingId] == "Syncing") ? StepStatus.Pass : StepStatus.Fail;
			});
		}
		Do("ask-a", delegate
		{
			CstLab.reports.Remove("a.asked");
			CstLab.Order("a", "report");
			return "asked a for a report";
		});
		Step("a-unaffected", 15f, delegate(BotStep st)
		{
			if (!CstLab.reports.TryGetValue("a.asked", out var value))
			{
				return StepStatus.Running;
			}
			string text2 = CstLab.Hex(Replay.Digest());
			st.Detail = "a: digest " + value.digest + " (server " + text2 + "), snapshot loads " + value.loads + ", lock view " + value.lockView + ", " + value.detail;
			int loads = CstLab.reports["a.early"].loads;
			st.Detail = st.Detail + "; loads at the start " + loads;
			return (value.digest == text2 && value.loads == loads && value.lockView == CstLab.roles["a"]) ? StepStatus.Pass : StepStatus.Fail;
		});
		Step("b-rejoined", 120f, delegate(BotStep st)
		{
			Server.Peer peer = CstLab.PeerNamed(bName);
			if (peer == null || peer.State != ProbablyTogether.Server.PeerState.Ready || peer.Id == bSyncingId || !CstLab.roles.TryGetValue("b", out var value) || value != peer.Id)
			{
				return StepStatus.Running;
			}
			st.Detail = bName + " is live again as player " + peer.Id;
			return StepStatus.Pass;
		});
		Do("ask-b", delegate
		{
			CstLab.reports.Remove("b.asked");
			CstLab.Order("b", "report");
			return "asked b for a report";
		});
		Step("b-matches", 15f, delegate(BotStep st)
		{
			if (!CstLab.reports.TryGetValue("b.asked", out var value))
			{
				return StepStatus.Running;
			}
			string text2 = CstLab.Hex(Replay.Digest());
			string text3 = CstLab.Features();
			st.Detail = "b: digest " + value.digest + " (server " + text2 + "), features " + value.features + " (server " + text3 + "), lock view " + Net.PlayerName(value.lockView) + ", " + value.detail;
			return (value.digest == text2 && value.features == text3 && value.lockView == CstLab.roles["a"]) ? StepStatus.Pass : StepStatus.Fail;
		});
		Do("a-releases", delegate
		{
			CstLab.Order("a", "release");
			return "told a to release";
		});
		WaitFor("free-at-end", 10f, () => Locks.Holder("cst.test") == -1, () => "events " + string.Join(" ", CstLab.lockEvents));
		Check("gate-held", () => CstLab.gateHandlerRan == 0, () => "gated requests that reached their handler: " + CstLab.gateHandlerRan);
		Check("ids-same-frame", delegate
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			Ids.Find(-999999L);
			List<string> list = new List<string>();
			bool flag = true;
			using (Net.Apply())
			{
				KeyValuePair<string, GameInventory>[] array2 = new KeyValuePair<string, GameInventory>[2]
				{
					new KeyValuePair<string, GameInventory>("swap buffer", (GameInventory)(object)instance.swapBufferElement),
					new KeyValuePair<string, GameInventory>("bazaar grid", (GameInventory)(object)instance.bazarLeftinvElement)
				};
				for (int i = 0; i < array2.Length; i++)
				{
					KeyValuePair<string, GameInventory> keyValuePair = array2[i];
					GameItem val = DirectoryMaster.Item("newspaper", true);
					int num2 = GraphUtils.TryAcceptAll((GraphNodeStorage)(object)keyValuePair.Value, val, -1);
					GameItem val2 = Ids.Find(val.uniqueId);
					bool flag2 = num2 == 0 && val2 == val;
					flag = flag && flag2;
					list.Add(keyValuePair.Key + ": uid " + val.uniqueId + ((num2 == 0) ? " placed" : " NOT placed") + ", Find " + (flag2 ? "found it" : ((val2 == null) ? "missed" : "found another item")));
				}
			}
			Vars["ids"] = string.Join("; ", list) + "; frame " + Time.frameCount;
			return flag;
		}, () => Vars["ids"]);
		Do("done", delegate
		{
			CstLab.Order("a", "done");
			CstLab.Order("b", "done");
			return "told both clients they are done; lock events " + string.Join(" ", CstLab.lockEvents);
		});
	}
}
