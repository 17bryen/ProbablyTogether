using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class DnUiServer : BotScript
{
	[Serializable]
	[CompilerGenerated]
	private sealed class _003C_003Ec
	{
		public static readonly _003C_003Ec _003C_003E9 = new _003C_003Ec();

		public static Application.LogCallback _003C_003E9__19_0;

		public static Application.LogCallback _003C_003E9__19_1;

		public static Func<bool> _003C_003E9__19_2;

		public static Func<string> _003C_003E9__19_3;

		public static Func<bool> _003C_003E9__19_4;

		public static Func<string> _003C_003E9__19_5;

		public static Func<bool> _003C_003E9__19_7;

		public static Func<PlayerInfo, string> _003C_003E9__19_50;

		public static Func<string> _003C_003E9__19_8;

		public static Func<string, bool> _003C_003E9__19_9;

		public static Func<JObject, bool> _003C_003E9__19_10;

		public static Func<string> _003C_003E9__19_11;

		public static Func<bool> _003C_003E9__19_12;

		public static Func<string> _003C_003E9__19_13;

		public static Func<JObject, bool> _003C_003E9__19_14;

		public static Func<BotStep, StepStatus> _003C_003E9__19_15;

		public static Func<bool> _003C_003E9__19_16;

		public static Func<string> _003C_003E9__19_17;

		public static Func<string, bool> _003C_003E9__19_18;

		public static Func<bool> _003C_003E9__19_19;

		public static Func<string> _003C_003E9__19_20;

		public static Func<bool> _003C_003E9__19_21;

		public static Func<string> _003C_003E9__19_22;

		public static Func<bool> _003C_003E9__19_25;

		public static Func<string> _003C_003E9__19_26;

		public static Func<JObject, bool> _003C_003E9__19_27;

		public static Func<bool> _003C_003E9__19_28;

		public static Func<string> _003C_003E9__19_29;

		public static Func<bool> _003C_003E9__19_30;

		public static Func<string> _003C_003E9__19_31;

		public static Func<string, bool> _003C_003E9__19_32;

		public static Func<string, bool> _003C_003E9__19_33;

		public static Func<bool> _003C_003E9__19_36;

		public static Func<string> _003C_003E9__19_37;

		public static Func<JObject, bool> _003C_003E9__19_38;

		public static Func<bool> _003C_003E9__19_40;

		public static Func<string> _003C_003E9__19_41;

		public static Func<JObject, bool> _003C_003E9__19_44;

		public static Func<bool> _003C_003E9__19_46;

		public static Func<string> _003C_003E9__19_47;

		internal void _003CBuild_003Eb__19_0(string msg, string trace, LogType type)
		{
			if (msg != null && msg.StartsWith("[ProbablyTogether] server: chat.line #", StringComparison.Ordinal))
			{
				if (msg.Contains("voted run agreed"))
				{
					cues.Add("voted run agreed");
				}
				if (msg.Contains("b agreed on the voted run"))
				{
					cues.Add("b agreed on the voted run");
				}
				if (msg.Contains("force run agreed"))
				{
					cues.Add("force run agreed");
				}
				if (msg.Contains("night ending agreed"))
				{
					cues.Add("night ending agreed");
				}
			}
		}

		internal void _003CBuild_003Eb__19_1(string msg, string trace, LogType type)
		{
			if (msg != null && msg.StartsWith("[ProbablyTogether] server: a save of slot", StringComparison.Ordinal) && msg.Contains("was skipped"))
			{
				savesSkipped++;
			}
		}

		internal bool _003CBuild_003Eb__19_2()
		{
			return Net.IsServer;
		}

		internal string _003CBuild_003Eb__19_3()
		{
			return "role " + Net.Role;
		}

		internal bool _003CBuild_003Eb__19_4()
		{
			if (Game.ShopLoaded)
			{
				return ProbablyTogether.Server.ShopReady;
			}
			return false;
		}

		internal string _003CBuild_003Eb__19_5()
		{
			return "scene " + BotProbes.Scene;
		}

		internal bool _003CBuild_003Eb__19_7()
		{
			if (ProbablyTogether.Server.ByPlayer.Count == 2)
			{
				return ProbablyTogether.Server.ByPlayer.Keys.All(Net.IsPlayerLive);
			}
			return false;
		}

		internal string _003CBuild_003Eb__19_8()
		{
			return "players " + string.Join(",", Net.Players.Values.Select((PlayerInfo p) => p.Name + "#" + p.Id));
		}

		internal string _003CBuild_003Eb__19_50(PlayerInfo p)
		{
			return p.Name + "#" + p.Id;
		}

		internal bool _003CBuild_003Eb__19_9(string r)
		{
			return r.StartsWith("refused: the run is still going", StringComparison.Ordinal);
		}

		internal bool _003CBuild_003Eb__19_10(JObject o)
		{
			if (S(o, "state") == "running")
			{
				return S(o, "ending") == "";
			}
			return false;
		}

		internal string _003CBuild_003Eb__19_11()
		{
			Chat.AnnounceOwner("Owner line <b>lab</b>");
			return "Chat.AnnounceOwner('Owner line <b>lab</b>')";
		}

		internal bool _003CBuild_003Eb__19_12()
		{
			if (DayNightRun.RunOver && ServerMode.RunEnded)
			{
				return ServerMode.Ending == DayNightRun.Ending;
			}
			return false;
		}

		internal string _003CBuild_003Eb__19_13()
		{
			return "day-night run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), core RunEnded " + ServerMode.RunEnded + " (" + ServerMode.Ending + ")";
		}

		internal bool _003CBuild_003Eb__19_14(JObject o)
		{
			if (S(o, "state") == "ended" && S(o, "ending") == DayNightRun.Ending)
			{
				return S(o, "ending") == "rent";
			}
			return false;
		}

		internal StepStatus _003CBuild_003Eb__19_15(BotStep st)
		{
			JObject o = AskStatus();
			st.Detail = "STATUS " + Short(o);
			if (!(S(o, "state") == "ended") || !(S(o, "ending") == "rent"))
			{
				return StepStatus.Fail;
			}
			return StepStatus.Pass;
		}

		internal bool _003CBuild_003Eb__19_16()
		{
			if (GamePatches.SaveIsBlocked && Snapshot.SaveIsBlocked)
			{
				return DayNightRun.BlockSaves;
			}
			return false;
		}

		internal string _003CBuild_003Eb__19_17()
		{
			return "GamePatches.SaveIsBlocked " + GamePatches.SaveIsBlocked + ", Snapshot.SaveIsBlocked " + Snapshot.SaveIsBlocked + ", BlockSaves " + DayNightRun.BlockSaves;
		}

		internal bool _003CBuild_003Eb__19_18(string r)
		{
			return r.StartsWith("refused: the run has ended", StringComparison.Ordinal);
		}

		internal bool _003CBuild_003Eb__19_19()
		{
			return DayNight.ServerLastRefused == "test.gated";
		}

		internal string _003CBuild_003Eb__19_20()
		{
			return "last gate refusal '" + DayNight.ServerLastRefused + "'";
		}

		internal bool _003CBuild_003Eb__19_21()
		{
			return DnUi.GatedReached == 0;
		}

		internal string _003CBuild_003Eb__19_22()
		{
			return "test.gated reached its handler " + DnUi.GatedReached + " time(s)";
		}

		internal bool _003CBuild_003Eb__19_25()
		{
			if (ServerMode.LastNewRunWhy.StartsWith("everyone voted (2)", StringComparison.Ordinal))
			{
				return DayNightRun.LastNewRunWhy == ServerMode.LastNewRunWhy;
			}
			return false;
		}

		internal string _003CBuild_003Eb__19_26()
		{
			return "core why '" + ServerMode.LastNewRunWhy + "', day-night why '" + DayNightRun.LastNewRunWhy + "'";
		}

		internal bool _003CBuild_003Eb__19_27(JObject o)
		{
			if (S(o, "state") == "running" && S(o, "ending") == "")
			{
				return I(o, "day") == 1;
			}
			return false;
		}

		internal bool _003CBuild_003Eb__19_28()
		{
			if (System.IO.Directory.GetFiles(Paths.Root, "ended_*_save_" + ProbablyTogether.Server.Config.saveSlot + ".es3").Length >= 1 && File.Exists(Paths.Under(SaveFiles.Slot(ProbablyTogether.Server.Config.saveSlot))))
			{
				return !GamePatches.SaveIsBlocked;
			}
			return false;
		}

		internal string _003CBuild_003Eb__19_29()
		{
			return "ended files: " + string.Join(", ", System.IO.Directory.GetFiles(Paths.Root, "ended_*").Select(Path.GetFileName)) + "; slot file " + File.Exists(Paths.Under(SaveFiles.Slot(ProbablyTogether.Server.Config.saveSlot))) + "; blocked " + GamePatches.SaveIsBlocked;
		}

		internal bool _003CBuild_003Eb__19_30()
		{
			if (ProbablyTogether.Server.ByPlayer.Count == 2)
			{
				return ProbablyTogether.Server.ByPlayer.Keys.All(Net.IsPlayerLive);
			}
			return false;
		}

		internal string _003CBuild_003Eb__19_31()
		{
			return "live " + ProbablyTogether.Server.ByPlayer.Keys.Count(Net.IsPlayerLive);
		}

		internal bool _003CBuild_003Eb__19_32(string r)
		{
			return r.StartsWith("refused: the run is still going", StringComparison.Ordinal);
		}

		internal bool _003CBuild_003Eb__19_33(string r)
		{
			return r.StartsWith("starting a new run", StringComparison.Ordinal);
		}

		internal bool _003CBuild_003Eb__19_36()
		{
			if (ServerMode.LastNewRunWhy == "panel: newrun force")
			{
				return DayNightRun.LastNewRunWhy == "panel: newrun force";
			}
			return false;
		}

		internal string _003CBuild_003Eb__19_37()
		{
			return "core why '" + ServerMode.LastNewRunWhy + "', day-night why '" + DayNightRun.LastNewRunWhy + "'";
		}

		internal bool _003CBuild_003Eb__19_38(JObject o)
		{
			if (S(o, "state") == "running")
			{
				return I(o, "day") == 1;
			}
			return false;
		}

		internal bool _003CBuild_003Eb__19_40()
		{
			if (DayNightRun.RunOver && DayNightRun.Ending == "aug" && !DayNight.NightRunning)
			{
				return ServerMode.RunEnded;
			}
			return false;
		}

		internal string _003CBuild_003Eb__19_41()
		{
			return "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), night " + DayNight.NightRunning + ", day " + Game.Day;
		}

		internal bool _003CBuild_003Eb__19_44(JObject o)
		{
			if (S(o, "state") == "ended")
			{
				return S(o, "ending") == "aug";
			}
			return false;
		}

		internal bool _003CBuild_003Eb__19_46()
		{
			return ProbablyTogether.Server.PlayerCount == 1;
		}

		internal string _003CBuild_003Eb__19_47()
		{
			return "players " + ProbablyTogether.Server.PlayerCount;
		}
	}

	private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private static readonly HashSet<string> cues = new HashSet<string>();

	private static bool cueHooked;

	private int runs0;

	private static int savesSkipped;

	public override string Name => "dnui-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 900f;

	private static JObject Status()
	{
		try
		{
			return JObject.Parse(File.ReadAllText(Paths.Under("status.json")));
		}
		catch
		{
			return null;
		}
	}

	private static string S(JObject o, string k)
	{
		return ((o == null) ? null : ((object)o[k])?.ToString()) ?? "(none)";
	}

	private static int I(JObject o, string k)
	{
		JToken val = ((o != null) ? o[k] : null);
		if (val == null || (int)val.Type != 6)
		{
			return -999;
		}
		return (int)val;
	}

	private static string Short(JObject o)
	{
		if (o != null)
		{
			return ((JToken)o).ToString((Formatting)0, Array.Empty<JsonConverter>());
		}
		return "unreadable";
	}

	private static JObject AskStatus()
	{
		try
		{
			using TcpClient tcpClient = new TcpClient();
			tcpClient.ReceiveTimeout = 2000;
			tcpClient.SendTimeout = 2000;
			tcpClient.Connect("127.0.0.1", ProbablyTogether.Server.Config.port);
			NetworkStream stream = tcpClient.GetStream();
			byte[] bytes = Encoding.ASCII.GetBytes("STATUS\n");
			stream.Write(bytes, 0, bytes.Length);
			byte[] array = new byte[8192];
			int num = 0;
			int num2;
			while (num < array.Length && (num2 = stream.Read(array, num, array.Length - num)) > 0)
			{
				num += num2;
				if (Array.IndexOf(array, (byte)10, 0, num) >= 0)
				{
					break;
				}
			}
			return JObject.Parse(Encoding.UTF8.GetString(array, 0, num).Trim());
		}
		catch
		{
			return null;
		}
	}

	private BotStep Cmd(string name, string line, Func<string, bool> ok)
	{
		int before = 0;
		return Step(name, 20f, delegate(BotStep st)
		{
			if (PanelLink.CommandsRun < before + 1)
			{
				return StepStatus.Running;
			}
			st.Detail = "'" + PanelLink.LastCommand + "' -> " + PanelLink.LastResult;
			return ok(PanelLink.LastResult) ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			before = PanelLink.CommandsRun;
			byte[] bytes = Utf8NoBom.GetBytes(line + "\n");
			using FileStream fileStream = new FileStream(Paths.Under("commands.txt"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
			fileStream.Write(bytes, 0, bytes.Length);
		});
	}

	private BotStep StatusIs(string name, float timeout, Func<JObject, bool> cond)
	{
		JObject last = null;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			last = Status();
			bool flag;
			try
			{
				flag = last != null && cond(last);
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				return StepStatus.Running;
			}
			st.Detail = "status.json " + Short(last) + " (write: " + PanelLink.LastStatusWhy + ")";
			return StepStatus.Pass;
		});
		botStep.TimeoutDetail = () => "status.json never matched: " + Short(last);
		return botStep;
	}

	private BotStep Cue(string name, string cue, float timeout)
	{
		BotStep botStep = Step(name, timeout, (BotStep st) => cues.Contains(cue) ? StepStatus.Pass : StepStatus.Running);
		botStep.Detail = "bot1 said '" + cue + "'";
		botStep.TimeoutDetail = () => "bot1 never said '" + cue + "'";
		return botStep;
	}

	protected override void Build()
	{
		if (!cueHooked)
		{
			cueHooked = true;
			object obj = _003C_003Ec._003C_003E9__19_0;
			if (obj == null)
			{
				Application.LogCallback val = delegate(string msg, string trace, LogType type)
				{
					if (msg != null && msg.StartsWith("[ProbablyTogether] server: chat.line #", StringComparison.Ordinal))
					{
						if (msg.Contains("voted run agreed"))
						{
							cues.Add("voted run agreed");
						}
						if (msg.Contains("b agreed on the voted run"))
						{
							cues.Add("b agreed on the voted run");
						}
						if (msg.Contains("force run agreed"))
						{
							cues.Add("force run agreed");
						}
						if (msg.Contains("night ending agreed"))
						{
							cues.Add("night ending agreed");
						}
					}
				};
				_003C_003Ec._003C_003E9__19_0 = val;
				obj = (object)val;
			}
			Application.logMessageReceived += (Application.LogCallback)obj;
			object obj2 = _003C_003Ec._003C_003E9__19_1;
			if (obj2 == null)
			{
				Application.LogCallback val2 = delegate(string msg, string trace, LogType type)
				{
					if (msg != null && msg.StartsWith("[ProbablyTogether] server: a save of slot", StringComparison.Ordinal) && msg.Contains("was skipped"))
					{
						savesSkipped++;
					}
				};
				_003C_003Ec._003C_003E9__19_1 = val2;
				obj2 = (object)val2;
			}
			Application.logMessageReceived += (Application.LogCallback)obj2;
		}
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop", 300f, () => Game.ShopLoaded && ProbablyTogether.Server.ShopReady, () => "scene " + BotProbes.Scene);
		Do("setup", delegate
		{
			DnUi.RegisterGatedKind();
			DayNight.SetLabTimeout(60);
			runs0 = ServerMode.NewRunsStarted;
			return "vote/ready timeout " + DayNight.ReadyTimeoutSeconds + " s; gates " + Dispatch.GateCount + "; Snapshot.SaveBlocked set " + (Snapshot.SaveBlocked != null) + "; new runs so far " + runs0;
		});
		WaitFor("two-live", 480f, () => ProbablyTogether.Server.ByPlayer.Count == 2 && ProbablyTogether.Server.ByPlayer.Keys.All(Net.IsPlayerLive), () => "players " + string.Join(",", Net.Players.Values.Select((PlayerInfo p) => p.Name + "#" + p.Id)));
		Cmd("cmd-newrun-running", "newrun", (string r) => r.StartsWith("refused: the run is still going", StringComparison.Ordinal));
		StatusIs("status-running", 15f, (JObject o) => S(o, "state") == "running" && S(o, "ending") == "");
		Do("owner-line", delegate
		{
			Chat.AnnounceOwner("Owner line <b>lab</b>");
			return "Chat.AnnounceOwner('Owner line <b>lab</b>')";
		});
		WaitFor("ended", 300f, () => DayNightRun.RunOver && ServerMode.RunEnded && ServerMode.Ending == DayNightRun.Ending, () => "day-night run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), core RunEnded " + ServerMode.RunEnded + " (" + ServerMode.Ending + ")");
		StatusIs("status-ended", 15f, (JObject o) => S(o, "state") == "ended" && S(o, "ending") == DayNightRun.Ending && S(o, "ending") == "rent");
		Step("status-port-ended", 10f, delegate(BotStep st)
		{
			JObject o = AskStatus();
			st.Detail = "STATUS " + Short(o);
			return (S(o, "state") == "ended" && S(o, "ending") == "rent") ? StepStatus.Pass : StepStatus.Fail;
		});
		Check("slot-blocked", () => GamePatches.SaveIsBlocked && Snapshot.SaveIsBlocked && DayNightRun.BlockSaves, () => "GamePatches.SaveIsBlocked " + GamePatches.SaveIsBlocked + ", Snapshot.SaveIsBlocked " + Snapshot.SaveIsBlocked + ", BlockSaves " + DayNightRun.BlockSaves);
		Cmd("cmd-save-ended", "save", (string r) => r.StartsWith("refused: the run has ended", StringComparison.Ordinal));
		WaitFor("gated-dropped", 120f, () => DayNight.ServerLastRefused == "test.gated", () => "last gate refusal '" + DayNight.ServerLastRefused + "'");
		Check("gate-held", () => DnUi.GatedReached == 0, () => "test.gated reached its handler " + DnUi.GatedReached + " time(s)");
		WaitFor("new-run-by-vote", 240f, () => ServerMode.NewRunsStarted == runs0 + 1 && ServerMode.StatusState == "running" && Game.ShopLoaded && !DayNightRun.RunOver, () => "new runs " + (ServerMode.NewRunsStarted - runs0) + ", state " + ServerMode.StatusState + ", core why '" + ServerMode.LastNewRunWhy + "', run over " + DayNightRun.RunOver);
		Check("by-vote", () => ServerMode.LastNewRunWhy.StartsWith("everyone voted (2)", StringComparison.Ordinal) && DayNightRun.LastNewRunWhy == ServerMode.LastNewRunWhy, () => "core why '" + ServerMode.LastNewRunWhy + "', day-night why '" + DayNightRun.LastNewRunWhy + "'");
		StatusIs("status-new-run", 15f, (JObject o) => S(o, "state") == "running" && S(o, "ending") == "" && I(o, "day") == 1);
		WaitFor("old-run-aside", 30f, () => System.IO.Directory.GetFiles(Paths.Root, "ended_*_save_" + ProbablyTogether.Server.Config.saveSlot + ".es3").Length >= 1 && File.Exists(Paths.Under(SaveFiles.Slot(ProbablyTogether.Server.Config.saveSlot))) && !GamePatches.SaveIsBlocked, () => "ended files: " + string.Join(", ", System.IO.Directory.GetFiles(Paths.Root, "ended_*").Select(Path.GetFileName)) + "; slot file " + File.Exists(Paths.Under(SaveFiles.Slot(ProbablyTogether.Server.Config.saveSlot))) + "; blocked " + GamePatches.SaveIsBlocked);
		WaitFor("two-live-again", 120f, () => ProbablyTogether.Server.ByPlayer.Count == 2 && ProbablyTogether.Server.ByPlayer.Keys.All(Net.IsPlayerLive), () => "live " + ProbablyTogether.Server.ByPlayer.Keys.Count(Net.IsPlayerLive));
		Cue("a-agreed-vote", "voted run agreed", 120f);
		Cue("b-agreed-vote", "b agreed on the voted run", 120f);
		Cmd("cmd-newrun-running-2", "newrun", (string r) => r.StartsWith("refused: the run is still going", StringComparison.Ordinal));
		Cmd("cmd-newrun-force", "newrun force", (string r) => r.StartsWith("starting a new run", StringComparison.Ordinal));
		WaitFor("new-run-forced", 180f, () => ServerMode.NewRunsStarted == runs0 + 2 && ServerMode.StatusState == "running" && Game.ShopLoaded, () => "new runs " + (ServerMode.NewRunsStarted - runs0) + ", state " + ServerMode.StatusState);
		Check("by-panel", () => ServerMode.LastNewRunWhy == "panel: newrun force" && DayNightRun.LastNewRunWhy == "panel: newrun force", () => "core why '" + ServerMode.LastNewRunWhy + "', day-night why '" + DayNightRun.LastNewRunWhy + "'");
		StatusIs("status-forced", 15f, (JObject o) => S(o, "state") == "running" && I(o, "day") == 1);
		Cue("a-agreed-force", "force run agreed", 150f);
		Do("skips-before", delegate
		{
			Vars["sk"] = savesSkipped.ToString(CultureInfo.InvariantCulture);
			return savesSkipped + " skipped save(s) so far";
		});
		WaitFor("night-ending", 300f, () => DayNightRun.RunOver && DayNightRun.Ending == "aug" && !DayNight.NightRunning && ServerMode.RunEnded, () => "run over " + DayNightRun.RunOver + " (" + DayNightRun.Ending + "), night " + DayNight.NightRunning + ", day " + Game.Day);
		Check("night-save-skipped", () => savesSkipped > int.Parse(Vars["sk"], CultureInfo.InvariantCulture) && !File.Exists(Paths.Under(SaveFiles.Slot(ProbablyTogether.Server.Config.saveSlot))), () => "saves skipped " + (savesSkipped - int.Parse(Vars["sk"], CultureInfo.InvariantCulture)) + ", slot file " + File.Exists(Paths.Under(SaveFiles.Slot(ProbablyTogether.Server.Config.saveSlot))) + ", ended files " + string.Join(", ", System.IO.Directory.GetFiles(Paths.Root, "ended_*_save_1.es3").Select(Path.GetFileName)));
		StatusIs("status-night-ended", 15f, (JObject o) => S(o, "state") == "ended" && S(o, "ending") == "aug");
		Cue("a-agreed-night", "night ending agreed", 150f);
		Do("rate-table", delegate
		{
			Vars["rt"] = Chat.RecentCount.ToString(CultureInfo.InvariantCulture);
			Chat.AnnounceOwner("Owner line: bot2 may leave now");
			return Chat.RecentCount + " player(s) in the chat rate table; told bot2 to leave";
		});
		WaitFor("b-gone", 120f, () => ProbablyTogether.Server.PlayerCount == 1, () => "players " + ProbablyTogether.Server.PlayerCount);
		Check("rate-table-pruned", () => Vars["rt"] == "2" && Chat.RecentCount == 1, () => "before " + Vars["rt"] + ", after B left " + Chat.RecentCount);
	}
}
