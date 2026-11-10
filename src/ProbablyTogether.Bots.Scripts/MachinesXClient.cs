using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class MachinesXClient : BotScript
{
	private string k = "0";

	private float lastAsk;

	private float lastNote;

	private int askId = -1;

	private int asks;

	private string fxDetail = "no reply yet";

	private int mism0;

	private int repairs0;

	private int rejects0;

	private int sent0;

	private int ticks0;

	private int loaded0;

	private int energy0;

	private int nq0;

	private int ns0;

	private int used0;

	private int flood0;

	private IReadOnlyDictionary<string, string> srv = new Dictionary<string, string>();

	public override string Name => "machines-x";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 400f;

	private bool A => k == "0";

	private GameItem X(string n)
	{
		return MachinesX.Item("x." + n);
	}

	private static string Mine(string p)
	{
		return BotProbes.Read(p);
	}

	private string Theirs(string p)
	{
		if (!srv.TryGetValue(p, out var value))
		{
			return "?";
		}
		return value;
	}

	private BotStep Converge(string name, float timeout, params string[] probes)
	{
		int id = -1;
		string diff = "no reply yet";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (id >= 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = -1;
					srv = dictionary;
					List<string> list = new List<string>();
					string[] array = probes;
					foreach (string text in array)
					{
						string text2 = Mine(text);
						string text3 = Theirs(text);
						if (text2 != text3 || text2 == "")
						{
							list.Add(text + " here " + text2 + " server " + text3);
						}
					}
					diff = string.Join("; ", list);
					if (list.Count == 0)
					{
						st.Detail = string.Join(" ", probes.Select((string p) => p + "=" + Mine(p))) + " (same as the server) after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
						return StepStatus.Pass;
					}
				}
			}
			if (id < 0 && Time.realtimeSinceStartup - lastAsk > 1f)
			{
				lastAsk = Time.realtimeSinceStartup;
				id = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "still different: " + diff;
		return botStep;
	}

	private BotStep UntilServer(string name, float timeout, Func<IReadOnlyDictionary<string, string>, bool> ok, Func<IReadOnlyDictionary<string, string>, string> show)
	{
		int id = -1;
		return Step(name, timeout, delegate(BotStep st)
		{
			if (id >= 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = -1;
					srv = dictionary;
					st.Detail = show(dictionary);
					if (ok(dictionary))
					{
						return StepStatus.Pass;
					}
				}
			}
			if (id < 0 && Time.realtimeSinceStartup - lastAsk > 1f)
			{
				lastAsk = Time.realtimeSinceStartup;
				id = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
	}

	private BotStep WaitMark(string name, string mark)
	{
		string value;
		return UntilServer(name, 60f, (IReadOnlyDictionary<string, string> r) => r.TryGetValue("machx.marks", out value) && ("," + value).Contains("," + mark + "="), (IReadOnlyDictionary<string, string> r) => "marks " + (r.TryGetValue("machx.marks", out value) ? value : "?"));
	}

	private static int Int(string s)
	{
		if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return -999;
		}
		return result;
	}

	protected override void Build()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "player " + Net.LocalPlayer);
		WaitFor("shop-loaded", 180f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene + ", live " + Net.IsLive);
		Do("quiet-dialogue", delegate
		{
			EmporiumOption instance = EmporiumOption.Instance;
			if ((Object)(object)instance != (Object)null)
			{
				instance.autoNextDialog = false;
			}
			return "autoNextDialog off";
		});
		string value;
		Step("fixtures", 150f, delegate(BotStep st)
		{
			if (askId >= 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(askId);
				if (dictionary != null)
				{
					askId = -1;
					dictionary.TryGetValue("machx.ready", out value);
					dictionary.TryGetValue("machx.fx", out var value2);
					fxDetail = "server ready=" + (value ?? "?");
					if (value == "1" && !string.IsNullOrEmpty(value2))
					{
						MachinesX.ParseFx(value2);
						List<int> list = Net.Players.Keys.OrderBy((int i) => i).ToList();
						k = (Math.Max(0, list.IndexOf(Net.LocalPlayer)) % 2).ToString(CultureInfo.InvariantCulture);
						List<string> list2 = MachinesX.Fx.Keys.Where((string n) => MachinesX.Item(n) == null && n != "x.cp").ToList();
						fxDetail = (A ? "client A" : "client B") + ", " + MachinesX.Fx.Count + " fixture ids" + ((list2.Count > 0) ? ("; not here: " + string.Join(",", list2)) : "");
						st.Detail = fxDetail;
						if (list2.Count == 0 && MachinesX.Cassette() != null)
						{
							return StepStatus.Pass;
						}
					}
				}
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (askId < 0 && realtimeSinceStartup - lastAsk > 2f)
			{
				lastAsk = realtimeSinceStartup;
				asks++;
				askId = BotProbes.AskServer();
			}
			if (realtimeSinceStartup - lastNote > 10f)
			{
				lastNote = realtimeSinceStartup;
				BotLog.Info(Name, "fixtures: " + asks + " asks, " + fxDetail);
			}
			return StepStatus.Running;
		}).TimeoutDetail = () => asks + " asks; " + fxDetail;
		WaitFor("two-players", 90f, () => Net.Players.Count >= 2, () => Net.Players.Count + " player(s)");
		Do("mark-live", delegate
		{
			MachinesX.Mark(A ? "a-live" : "b-live");
			return "told the server";
		});
		UntilServer("both-live", 90f, (IReadOnlyDictionary<string, string> r) => r.TryGetValue("machx.marks", out value) && value.Contains("a-live") && value.Contains("b-live"), (IReadOnlyDictionary<string, string> r) => "marks " + (r.TryGetValue("machx.marks", out value) ? value : "?"));
		Do("drift", delegate
		{
			if (A)
			{
				return "client A: nothing to drift";
			}
			repairs0 = Machines.RepairsAsked;
			using (Net.Apply())
			{
				X("box").ModifyTag("BR_LAB_DRIFT", (Action<TagState>)delegate(TagState st)
				{
					st.Enable().SetInt(7);
				}, false);
			}
			return "BR_LAB_DRIFT=7 written on this copy of the printer only; digest here " + MachinesX.Dg("box");
		});
		Do("cassette-play", delegate
		{
			if (!A)
			{
				return "client B waits for A's press";
			}
			MachineryButton val = MachinesX.ButtonCalling(MachinesX.Cassette(), AccessTools.Method(typeof(MachineCassettePlayer), "OnPlayButtonPressed", (Type[])null, (Type[])null));
			if (val == null)
			{
				throw new InvalidOperationException("no play button on the cassette player (" + Machines.Widgets(MachinesX.Cassette()).Count + " widgets)");
			}
			sent0 = Machines.SentCount;
			MachinesX.Press(val);
			return "Play pressed (widget " + Machines.Widgets(MachinesX.Cassette()).IndexOf(val) + ", " + MachinesX.Tracks() + " tracks in the deck); requests " + sent0 + " -> " + Machines.SentCount + "; here still " + MachinesX.Cp();
		});
		WaitFor("cassette-playing", 30f, () => MachinesX.CpState() == 1, () => "cassette " + MachinesX.Cp() + "; last on it: " + Machines.LastOpOn(-103L));
		Check("cassette-not-quiet", () => Machines.LastRunOn(-103L).StartsWith("loud", StringComparison.Ordinal), () => (A ? "presser" : "other player") + ": the press ran " + Machines.LastRunOn(-103L) + " here (the music is the shop's: never quiet)");
		Converge("cassette-same", 15f, "machx.cp", "machx.dg.cp");
		Do("cassette-note-1", delegate
		{
			Vars["i0"] = MachinesX.CpIndex().ToString(CultureInfo.InvariantCulture);
			if (!A)
			{
				MachinesX.Mark("c1");
			}
			return "index here " + Vars["i0"];
		});
		WaitMark("cassette-wait-1", "c1");
		Do("cassette-next", delegate
		{
			if (!A)
			{
				return "client B waits for A's press (index here " + Vars["i0"] + ")";
			}
			MachinesX.Press(MachinesX.ButtonCalling(MachinesX.Cassette(), AccessTools.Method(typeof(MachineCassettePlayer), "OnNextButtonPressed", (Type[])null, (Type[])null)) ?? throw new InvalidOperationException("no next button on the cassette player"));
			return "Next pressed at index " + Vars["i0"] + "; here still " + MachinesX.Cp();
		});
		WaitFor("cassette-next-applied", 20f, () => MachinesX.CpIndex() == (Int(Vars["i0"]) + 1) % Math.Max(1, MachinesX.Tracks()) && MachinesX.CpState() == 1, () => "cassette " + MachinesX.Cp() + " (was index " + Vars["i0"] + ")");
		Converge("cassette-same-next", 15f, "machx.cp", "machx.dg.cp");
		Do("cassette-note-2", delegate
		{
			Vars["i1"] = MachinesX.CpIndex().ToString(CultureInfo.InvariantCulture);
			Vars["t1"] = MachinesX.Cassette().GetTagReadonly("CURRENT_PLAYING_TRACK_ID_TAG").GetString() ?? "";
			if (A)
			{
				MachinesX.Mark("c2");
			}
			return "index here " + Vars["i1"] + " (" + Vars["t1"] + ")";
		});
		WaitMark("cassette-wait-2", "c2");
		Do("track-end", delegate
		{
			if (A)
			{
				return "client A waits for B's report (index here " + Vars["i1"] + ")";
			}
			sent0 = Machines.SentCount;
			AudioManager.OnTrackDone();
			return "AudioManager.OnTrackDone() on B at index " + Vars["i1"] + ": requests " + sent0 + " -> " + Machines.SentCount + " (" + Machines.LastSentKind + "); here still " + MachinesX.Cp();
		});
		WaitFor("track-advanced", 20f, () => MachinesX.CpIndex() == (Int(Vars["i1"]) + 1) % Math.Max(1, MachinesX.Tracks()) && MachinesX.CpState() == 1, () => "cassette " + MachinesX.Cp() + " (was index " + Vars["i1"] + "); last on it: " + Machines.LastOpOn(-103L));
		Check("track-by-b", () => Machines.LastOpOn(-103L).StartsWith("track by ", StringComparison.Ordinal), () => "last on the cassette player: " + Machines.LastOpOn(-103L));
		Do("track-late", delegate
		{
			if (!A)
			{
				return "client B: A sends the late report";
			}
			Net.ToServer("machine.track", new MachineReq
			{
				q = 999,
				m = -103L,
				w = Int(Vars["i1"]),
				x = Vars["t1"]
			});
			return "sent a track-end report for index " + Vars["i1"] + " (" + Vars["t1"] + "); everyone plays " + MachinesX.Cp();
		});
		UntilServer("track-late-ignored", 20f, (IReadOnlyDictionary<string, string> r) => Int(r["machx.trackstale"]) >= 1 && r["machx.cp"] == Mine("machx.cp"), (IReadOnlyDictionary<string, string> r) => "server stale reports " + r["machx.trackstale"] + ", cassette there " + r["machx.cp"] + ", here " + Mine("machx.cp"));
		Converge("cassette-same-2", 10f, "machx.cp", "machx.dg.cp");
		Do("cassette-stop", delegate
		{
			if (!A)
			{
				return "client B waits for A's press";
			}
			MachinesX.Press(MachinesX.ButtonCalling(MachinesX.Cassette(), AccessTools.Method(typeof(MachineCassettePlayer), "OnStopButtonPressed", (Type[])null, (Type[])null)) ?? throw new InvalidOperationException("no stop button on the cassette player"));
			return "Stop pressed";
		});
		WaitFor("cassette-stopped", 20f, () => MachinesX.CpState() == 0, () => "cassette " + MachinesX.Cp());
		Do("butcher-now", delegate
		{
			if (!A)
			{
				return "client B waits for A's press; " + MachinesX.Rats();
			}
			MachineryButton b = MachinesX.ButtonText(X("butch"), "Butcher now") ?? throw new InvalidOperationException("no 'Butcher now' button: " + string.Join(",", from x in Machines.Widgets(X("butch")).OfType<MachineryButton>()
				select x.text));
			Vars["rats0"] = MachinesX.Rats();
			sent0 = Machines.SentCount;
			MachinesX.Press(b);
			return "'Butcher now' pressed; machine requests " + sent0 + " -> " + Machines.SentCount + "; here still " + MachinesX.Rats();
		});
		WaitFor("butcher-now-applied", 25f, () => MachinesX.LiveRats() == 0, () => MachinesX.Rats() + "; last on it: " + Machines.LastOpOn(X("butch").uniqueId));
		Converge("butcher-now-same", 15f, "machx.rats", "machx.dg.butch", "machx.dg.cage");
		Do("butcher-content", delegate
		{
			if (A)
			{
				return "client A waits for B's press; " + MachinesX.Rats();
			}
			MachineryButton b = MachinesX.ButtonText(X("butch"), "Butcher content") ?? throw new InvalidOperationException("no 'Butcher content' button");
			Vars["tray0"] = MachinesX.Rats();
			MachinesX.Press(b);
			return "'Butcher content' pressed by B; here still " + MachinesX.Rats();
		});
		WaitFor("butcher-content-applied", 25f, () => MachinesX.DeadInTray() == 0, () => MachinesX.Rats() + "; last on it: " + Machines.LastOpOn(X("butch").uniqueId));
		Converge("butcher-content-same", 15f, "machx.rats", "machx.dg.butch", "machx.dg.cage");
		Do("coord-open", delegate
		{
			if (!A)
			{
				return "client B waits";
			}
			MachineStorageCoordinator.OpenWindow(X("coord"));
			return "coordinator window opened on A";
		});
		WaitFor("coord-lock", 20f, () => Int(Mine("machx.lock")) >= 1, () => "lock holder " + Mine("machx.lock") + " (me " + Net.LocalPlayer + ")");
		Do("coord-save", delegate
		{
			if (!A)
			{
				return "client B waits";
			}
			MachineStorageCoordinator.SavePresets(X("coord"), new List<MachineStorageCoordinator.Preset>
			{
				new MachineStorageCoordinator.Preset
				{
					name = "lab-A",
					keyCode = 49
				}
			});
			return "preset lab-A saved here: " + MachinesX.Presets();
		});
		WaitFor("coord-saved", 20f, () => MachinesX.Presets() == "1:lab-A", () => "presets here " + MachinesX.Presets());
		Converge("coord-same", 15f, "machx.presets", "machx.dg.coord");
		Do("coord-other", delegate
		{
			if (A)
			{
				return "client A holds the window";
			}
			rejects0 = Machines.RejectCount;
			MachineStorageCoordinator.SavePresets(X("coord"), new List<MachineStorageCoordinator.Preset>
			{
				new MachineStorageCoordinator.Preset
				{
					name = "lab-B"
				}
			});
			return "B saved preset lab-B while A holds the window (lock " + Mine("machx.lock") + "): here " + MachinesX.Presets();
		});
		WaitFor("coord-other-refused", 20f, () => A || (Machines.RejectCount > rejects0 && Machines.LastReject.Contains("locked") && MachinesX.Presets() == "1:lab-A"), () => "rejects " + Machines.RejectCount + ", last " + Machines.LastReject + ", presets here " + MachinesX.Presets());
		UntilServer("coord-refusal-seen", 30f, (IReadOnlyDictionary<string, string> r) => Int(r["machx.refused"]) >= 1, (IReadOnlyDictionary<string, string> r) => "server refusals " + r["machx.refused"]);
		Do("coord-close", delegate
		{
			if (!A)
			{
				return "client B waits";
			}
			CustomUIManager instance = CustomUIManager.Instance;
			if (instance != null)
			{
				instance.CloseWindow("storage_coordinator");
			}
			return "window closed";
		});
		WaitFor("coord-free", 20f, () => Mine("machx.lock") == "-1", () => "lock holder " + Mine("machx.lock"));
		Converge("coord-same-2", 15f, "machx.presets", "machx.dg.coord");
		Do("fridge-start", delegate
		{
			ticks0 = Machines.TicksApplied;
			return "bottle here " + MachinesX.Temp() + " C (25 C at spawn), 1 s functions skipped here " + Machines.SuppressedTicks;
		});
		WaitFor("fridge-cold", 60f, () => MachinesX.Temp() <= 23 && MachinesX.Temp2() <= 24, () => "freeze-zone bottle here " + MachinesX.Temp() + " C, chill-zone bottle " + MachinesX.Temp2() + " C, ticks applied " + Machines.TicksApplied + ", 1 s functions skipped here " + Machines.SuppressedTicks + "; " + MachinesX.FridgeDiag());
		AskServer("ask-fridge");
		Step("fridge-close", 0f, delegate(BotStep st)
		{
			int num = MachinesX.Temp();
			int num2 = Int(base.Server["machx.temp"]);
			int num3 = MachinesX.Temp2();
			int num4 = Int(base.Server["machx.temp2"]);
			st.Detail = "freeze-zone bottle here " + num + " C, server " + num2 + " C; chill-zone bottle here " + num3 + " C, server " + num4 + " C; 1 s functions skipped here " + Machines.SuppressedTicks + " (the server pushes every 3 s); server: " + (base.Server.TryGetValue("machx.fridge", out value) ? value : "?");
			return (Math.Abs(num - num2) <= 6 && Math.Abs(num3 - num4) <= 6 && Machines.SuppressedTicks > 0) ? StepStatus.Pass : StepStatus.Fail;
		});
		Converge("water-label-same", 25f, "machx.chlor");
		Check("water-label-moved", () => Mine("machx.chlor").Split(':').ElementAtOrDefault(1) == MachinesX.PureId && Machines.WaterLabelsApplied > 0, () => "label here " + Mine("machx.chlor") + " (spawned h:<base>:...:9990: the reaction set the label condition to " + MachinesX.PureId + " on the server); water labels from machine.tick here " + Machines.WaterLabelsApplied);
		Do("repair-print", delegate
		{
			mism0 = Machines.MismatchCount;
			if (!A)
			{
				return "client B waits for A's print (drifted: " + X("box").GetTagReadonly("BR_LAB_DRIFT").GetInt() + ")";
			}
			MachineryButton val = MachinesX.ButtonText(X("box"), "Print Small Bottle") ?? Machines.Widgets(X("box")).OfType<MachineryButton>().FirstOrDefault();
			MachinesX.Press(val);
			return "'" + val.text + "' pressed by A";
		});
		WaitFor("repair-printed", 20f, () => MachinesX.BoxOut() == 1, () => "printer output here " + MachinesX.BoxOut());
		WaitFor("repair-asked", 15f, () => A || Machines.RepairsAsked > repairs0, () => "repairs asked here " + (Machines.RepairsAsked - repairs0) + ", mismatches " + (Machines.MismatchCount - mism0));
		WaitFor("repair-whole", 20f, () => A || X("box").GetTagReadonly("BR_LAB_DRIFT").GetInt() == 0, delegate
		{
			GameItem obj = X("box");
			return "drift tag here " + ((obj != null) ? obj.GetTagReadonly("BR_LAB_DRIFT").GetInt() : (-1));
		});
		Converge("repair-same", 15f, "machx.boxout", "machx.dg.box");
		Check("repair-a-clean", () => !A || Machines.MismatchCount == mism0, () => (A ? "A: mismatches during the print " : "B: mismatches ") + (Machines.MismatchCount - mism0) + ", repairs asked " + Machines.RepairsAsked);
		UntilServer("repair-sent", 15f, (IReadOnlyDictionary<string, string> r) => Int(r["machx.repairs"]) >= 1, (IReadOnlyDictionary<string, string> r) => "server repairs sent " + r["machx.repairs"]);
		Do("repair-flood", delegate
		{
			flood0 = Int(Theirs("machx.repairs"));
			if (!A)
			{
				return "client B: A floods (server repairs so far " + flood0 + ")";
			}
			string text = "flood " + new string('x', 150) + "\n[ProbablyTogether] server: run ended (forged)\r\n" + new string('y', 150);
			string mine = new string('z', 600) + "\n" + new string('w', 600);
			int num = 0;
			string[] array = new string[6] { "box", "deal", "coord", "cage", "butch", "fridge" };
			foreach (string n in array)
			{
				GameItem val = X(n);
				if (val != null)
				{
					Net.ToServer("machine.repair", new MachineRepairReq
					{
						m = val.uniqueId,
						why = text,
						mine = mine
					});
					num++;
				}
			}
			return num + " machine.repair requests for " + num + " machines in one frame (reason " + text.Length + " chars with newlines; server repairs so far " + flood0 + ")";
		});
		UntilServer("repair-flood-throttled", 20f, delegate(IReadOnlyDictionary<string, string> r)
		{
			if (!A)
			{
				return Int(r["machx.repthrottled"]) >= 5;
			}
			return Int(r["machx.repairs"]) >= flood0 + 1 && Int(r["machx.repthrottled"]) >= 5 && r["machx.repwhy"].EndsWith(":clean", StringComparison.Ordinal) && Int(r["machx.repwhy"].Split(':')[0]) <= 83;
		}, (IReadOnlyDictionary<string, string> r) => "server repairs sent " + flood0 + " -> " + r["machx.repairs"] + ", dropped by the per-player limit " + r["machx.repthrottled"] + ", logged reason " + r["machx.repwhy"] + " (chars:control or clean)");
		Do("notice-note", delegate
		{
			nq0 = ItemsUse.QuietNoticesBlocked;
			ns0 = ItemsUse.NoticesShown;
			used0 = ItemsUse.AppliedCount;
			if (!A)
			{
				MachinesX.Mark("n1");
			}
			return "notices kept off here " + nq0 + ", shown " + ns0 + "; butcher 2: " + Mine("machx.butch2");
		});
		WaitMark("notice-wait", "n1");
		Do("notice-drop", () => (!A) ? "client B waits for A's drop" : ("A dropped the live rat on butcher 2 (tray full): request sent " + ItemsUse.RequestTarget(X("rat"), X("butch2")) + "; here still " + Mine("machx.butch2")));
		WaitFor("notice-applied", 20f, () => ItemsUse.AppliedCount > used0, () => "uses applied here " + used0 + " -> " + ItemsUse.AppliedCount + ", last refusal '" + ItemsUse.LastRefusal + "'");
		Check("notice-actor-only", delegate
		{
			if (!A)
			{
				if (ItemsUse.QuietNoticesBlocked > nq0)
				{
					return ItemsUse.NoticesShown == ns0;
				}
				return false;
			}
			return ItemsUse.NoticesShown > ns0 && ItemsUse.QuietNoticesBlocked == nq0;
		}, () => (A ? "presser" : "other player") + ": notices shown " + (ItemsUse.NoticesShown - ns0) + ", kept off " + (ItemsUse.QuietNoticesBlocked - nq0) + "; butcher 2: " + Mine("machx.butch2"));
		Converge("notice-same", 15f, "machx.butch2");
		Do("open-shutter", delegate
		{
			if (!A)
			{
				return "client B waits for A's shutter";
			}
			EmporiumOption instance = EmporiumOption.Instance;
			if ((Object)(object)instance != (Object)null)
			{
				instance.autoCallNext = false;
			}
			StoreShutterButton.Instance.OnClick();
			return "autoCallNext off; clicked the shutter (state " + Mine("cust.state") + ")";
		});
		WaitFor("shop-open", 40f, () => Mine("cust.state") == "OPEN", () => "state " + Mine("cust.state") + "; last day-night refusal '" + DayNight.LastRefusedWhy + "'");
		WaitFor("customer-queued", 40f, () => Mine("cust.head") == "scavGeneral" || Mine("cust.current") == "scavGeneral", () => "head " + Mine("cust.head") + ", at the counter " + Mine("cust.current"));
		Do("call-customer", delegate
		{
			if (!A)
			{
				return "client B waits for A's customer";
			}
			Customers.RequestCall();
			return "call sent; at the counter here " + Mine("cust.current");
		});
		WaitFor("customer-here", 40f, () => Mine("cust.current") != "none" && Mine("cust.current") != "" && Mine("cust.arrived") == "1" && Int(Mine("cust.holder")) >= 1, () => "customer " + Mine("cust.current") + ", arrived " + Mine("cust.arrived") + ", counter " + Mine("cust.holder"));
		Do("deal-start", delegate
		{
			energy0 = MachinesX.DealEnergy();
			if (!A)
			{
				return "client B waits for A's press (deal maker here " + MachinesX.DealOn() + ")";
			}
			MachinesX.Press(MachinesX.Widget0(X("deal")));
			return "deal maker pressed by A, the counter holder (energy " + energy0 + ")";
		});
		WaitFor("deal-on", 20f, () => MachinesX.DealOn() == "1", () => "deal maker on here " + MachinesX.DealOn() + ", energy " + MachinesX.DealEnergy());
		Check("deal-sound", () => (!A) ? Machines.LastRunOn(X("deal").uniqueId).StartsWith("quiet", StringComparison.Ordinal) : Machines.LastRunOn(X("deal").uniqueId).StartsWith("loud", StringComparison.Ordinal), () => (A ? "presser: " : "other player: ") + Machines.LastRunOn(X("deal").uniqueId) + " (quiet scope: ItemsUse.Quiet)");
		Converge("deal-same", 15f, "machx.dealon", "machx.dealenergy", "machx.dg.deal");
		Do("deal-other", delegate
		{
			if (A)
			{
				return "client A holds the counter";
			}
			rejects0 = Machines.RejectCount;
			MachinesX.Press(MachinesX.Widget0(X("deal")));
			return "B pressed the deal maker while A holds the counter";
		});
		WaitFor("deal-other-refused", 20f, () => A || (Machines.RejectCount > rejects0 && Machines.LastReject.Contains("counter")), () => "rejects " + Machines.RejectCount + ", last " + Machines.LastReject);
		UntilServer("deal-refusal-seen", 30f, (IReadOnlyDictionary<string, string> r) => Int(r["machx.refused"]) >= 2, (IReadOnlyDictionary<string, string> r) => "server refusals " + r["machx.refused"]);
		Do("dismiss", delegate
		{
			if (!A)
			{
				return "client B waits";
			}
			Customers.RequestDismiss(callNext: false);
			return "dismiss sent";
		});
		WaitFor("customer-gone", 30f, () => Mine("cust.current") == "none", () => "customer " + Mine("cust.current"));
		Do("resync", delegate
		{
			if (A)
			{
				return "client A: B resyncs";
			}
			loaded0 = Machines.LoadedCount;
			mism0 = Machines.MismatchCount;
			Snapshot.RequestResync();
			return "resync asked (machine actions applied so far " + Machines.AppliedCount + ")";
		});
		WaitFor("resync-loaded", 90f, () => A || (Machines.LoadedCount > loaded0 && Net.IsLive && !DayLog.Replaying), () => "loaded " + Machines.LoadedCount + ", live " + Net.IsLive + ", state " + Net.ClientState);
		Delay("resync-settle", 4f);
		Converge("replayed-same", 20f, new string[5] { "machx.cp", "machx.rats", "machx.presets", "machx.boxout", "machx.dealenergy" }.Concat(MachinesX.DgNames.Select((string n) => "machx.dg." + n)).ToArray());
		Check("replayed-no-mismatch", () => A || Machines.MismatchCount == mism0, () => "machine mismatches during the replay " + (Machines.MismatchCount - mism0) + ", repairs asked " + Machines.RepairsAsked);
		Check("summary", () => true, () => "quiet runs " + Machines.QuietRuns + ", loud runs " + Machines.LoudRuns + ", sounds kept off " + (ItemsUse.QuietSoundsBlocked + ItemsUse.QuietSoundsStopped) + ", notices kept off " + ItemsUse.QuietNoticesBlocked + ", ticks applied " + Machines.TicksApplied + ", water labels applied " + Machines.WaterLabelsApplied + ", mismatches " + Machines.MismatchCount + ", repairs asked " + Machines.RepairsAsked);
	}
}
