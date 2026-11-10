using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CoreOpsServer : BotScript
{
	[Serializable]
	[CompilerGenerated]
	private sealed class _003C_003Ec
	{
		public static readonly _003C_003Ec _003C_003E9 = new _003C_003Ec();

		public static Func<JToken, string> _003C_003E9__12_0;

		public static Application.LogCallback _003C_003E9__26_0;

		public static Func<bool> _003C_003E9__26_1;

		public static Func<string> _003C_003E9__26_2;

		public static Func<bool> _003C_003E9__26_3;

		public static Func<string> _003C_003E9__26_4;

		public static Func<BotStep, StepStatus> _003C_003E9__26_7;

		public static Func<bool> _003C_003E9__26_8;

		public static Func<PlayerInfo, string> _003C_003E9__26_54;

		public static Func<string> _003C_003E9__26_9;

		public static Func<JObject, bool> _003C_003E9__26_10;

		public static Func<string, bool> _003C_003E9__26_11;

		public static Func<string, bool> _003C_003E9__26_13;

		public static Func<string> _003C_003E9__26_15;

		public static Func<string, bool> _003C_003E9__26_17;

		public static Func<string, bool> _003C_003E9__26_18;

		public static Func<string, bool> _003C_003E9__26_19;

		public static Func<string, bool> _003C_003E9__26_20;

		public static Func<string, bool> _003C_003E9__26_21;

		public static Func<bool> _003C_003E9__26_22;

		public static Func<string> _003C_003E9__26_23;

		public static Func<JObject, bool> _003C_003E9__26_24;

		public static Func<bool> _003C_003E9__26_25;

		public static Func<string> _003C_003E9__26_26;

		public static Func<string, bool> _003C_003E9__26_27;

		public static Func<string> _003C_003E9__26_29;

		public static Func<string> _003C_003E9__26_31;

		public static Func<JObject, bool> _003C_003E9__26_32;

		public static Func<BotStep, StepStatus> _003C_003E9__26_33;

		public static Func<string, bool> _003C_003E9__26_34;

		public static Func<string, bool> _003C_003E9__26_35;

		public static Func<JObject, bool> _003C_003E9__26_36;

		public static Func<bool> _003C_003E9__26_37;

		public static Func<string> _003C_003E9__26_38;

		public static Func<JObject, bool> _003C_003E9__26_39;

		public static Func<bool> _003C_003E9__26_40;

		public static Func<string> _003C_003E9__26_41;

		public static Func<string, bool> _003C_003E9__26_42;

		public static Func<bool> _003C_003E9__26_43;

		public static Func<string> _003C_003E9__26_44;

		public static Func<JObject, bool> _003C_003E9__26_45;

		public static Func<bool> _003C_003E9__26_46;

		public static Func<string> _003C_003E9__26_47;

		public static Func<JObject, bool> _003C_003E9__26_48;

		public static Func<string> _003C_003E9__26_49;

		public static Func<JObject, bool> _003C_003E9__26_50;

		internal string _003CNames_003Eb__12_0(JToken x)
		{
			return ((object)x[(object)"id"])?.ToString() + ":" + (object)x[(object)"name"];
		}

		internal void _003CBuild_003Eb__26_0(string msg, string trace, LogType type)
		{
			if (msg != null && msg.StartsWith("[ProbablyTogether] server: chat.line #", StringComparison.Ordinal))
			{
				if (msg.Contains("night agreed"))
				{
					cueSeen.Add("night agreed");
				}
				if (msg.Contains("newrun agreed"))
				{
					cueSeen.Add("newrun agreed");
				}
			}
		}

		internal bool _003CBuild_003Eb__26_1()
		{
			return Net.IsServer;
		}

		internal string _003CBuild_003Eb__26_2()
		{
			return "role " + Net.Role;
		}

		internal bool _003CBuild_003Eb__26_3()
		{
			if (Game.ShopLoaded)
			{
				return ProbablyTogether.Server.ShopReady;
			}
			return false;
		}

		internal string _003CBuild_003Eb__26_4()
		{
			return "scene " + BotProbes.Scene;
		}

		internal StepStatus _003CBuild_003Eb__26_7(BotStep st)
		{
			_003C_003Ec__DisplayClass26_0 CS_0024_003C_003E8__locals10 = new _003C_003Ec__DisplayClass26_0
			{
				o = AskStatus(),
				f = Status()
			};
			st.Detail = "STATUS " + Short(CS_0024_003C_003E8__locals10.o);
			if (CS_0024_003C_003E8__locals10.o == null || CS_0024_003C_003E8__locals10.f == null)
			{
				return StepStatus.Fail;
			}
			string[] source = new string[11]
			{
				"state", "name", "players", "maxPlayers", "day", "cash", "scenario", "ending", "version", "protocol",
				"gameVersion"
			};
			List<string> list = source.Where((string k) => CS_0024_003C_003E8__locals10.o[k] == null).ToList();
			List<string> list2 = new string[2] { "data", "lastSave" }.Where((string k) => CS_0024_003C_003E8__locals10.o[k] != null).ToList();
			List<string> list3 = source.Where((string k) => CS_0024_003C_003E8__locals10.o[k] != null && CS_0024_003C_003E8__locals10.f[k] != null && CS_0024_003C_003E8__locals10.o[k].ToString((Formatting)0, Array.Empty<JsonConverter>()) != CS_0024_003C_003E8__locals10.f[k].ToString((Formatting)0, Array.Empty<JsonConverter>())).ToList();
			st.Detail += ((list.Count > 0) ? ("; MISSING " + string.Join(",", list)) : "");
			st.Detail += ((list2.Count > 0) ? ("; LEAKS " + string.Join(",", list2) + " (ruling 16)") : "");
			st.Detail += ((list3.Count > 0) ? ("; differs from status.json: " + string.Join(",", list3)) : "; same fields as status.json");
			if (list.Count != 0 || list2.Count != 0 || list3.Count != 0 || !(S(CS_0024_003C_003E8__locals10.o, "state") == "running"))
			{
				return StepStatus.Fail;
			}
			return StepStatus.Pass;
		}

		internal bool _003CBuild_003Eb__26_8()
		{
			if (ProbablyTogether.Server.ByPlayer.Count == 2)
			{
				return ProbablyTogether.Server.ByPlayer.Keys.All(Net.IsPlayerLive);
			}
			return false;
		}

		internal string _003CBuild_003Eb__26_9()
		{
			return "players " + string.Join(",", Net.Players.Values.Select((PlayerInfo p) => p.Name + "#" + p.Id));
		}

		internal string _003CBuild_003Eb__26_54(PlayerInfo p)
		{
			return p.Name + "#" + p.Id;
		}

		internal bool _003CBuild_003Eb__26_10(JObject o)
		{
			if (Count(o) == 2 && Names(o).Contains(":bot1") && Names(o).Contains(":bot2"))
			{
				return S(o, "state") == "running";
			}
			return false;
		}

		internal bool _003CBuild_003Eb__26_11(string r)
		{
			return r.StartsWith("sent to 2 players", StringComparison.Ordinal);
		}

		internal bool _003CBuild_003Eb__26_13(string r)
		{
			return r.StartsWith("saved slot", StringComparison.Ordinal);
		}

		internal string _003CBuild_003Eb__26_15()
		{
			return "slot file written " + File.GetLastWriteTimeUtc(Data(SaveFiles.Slot(Game.Store.saveSlotId))).ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " UTC, lastSave " + PanelLink.LastSaveUnix;
		}

		internal bool _003CBuild_003Eb__26_17(string r)
		{
			return r.StartsWith("refused: the run is still going", StringComparison.Ordinal);
		}

		internal bool _003CBuild_003Eb__26_18(string r)
		{
			return r == "unknown command";
		}

		internal bool _003CBuild_003Eb__26_19(string r)
		{
			return r.StartsWith("no such player", StringComparison.Ordinal);
		}

		internal bool _003CBuild_003Eb__26_20(string r)
		{
			return r.StartsWith("sent to 2 players", StringComparison.Ordinal);
		}

		internal bool _003CBuild_003Eb__26_21(string r)
		{
			return r.StartsWith("kicked bot2", StringComparison.Ordinal);
		}

		internal bool _003CBuild_003Eb__26_22()
		{
			return ProbablyTogether.Server.PlayerCount == 1;
		}

		internal string _003CBuild_003Eb__26_23()
		{
			return "players " + ProbablyTogether.Server.PlayerCount;
		}

		internal bool _003CBuild_003Eb__26_24(JObject o)
		{
			if (Count(o) == 1 && Names(o).Contains(":bot1"))
			{
				return !Names(o).Contains(":bot2");
			}
			return false;
		}

		internal bool _003CBuild_003Eb__26_25()
		{
			if (Game.ShopLoaded)
			{
				return (int)Game.Store.storeState == 1;
			}
			return false;
		}

		internal string _003CBuild_003Eb__26_26()
		{
			return "state " + (Game.Store?.storeState).ToString();
		}

		internal bool _003CBuild_003Eb__26_27(string r)
		{
			return r.StartsWith("queued", StringComparison.Ordinal);
		}

		internal string _003CBuild_003Eb__26_29()
		{
			return "day " + Game.Day + ", " + (Game.Store?.storeState).ToString();
		}

		internal string _003CBuild_003Eb__26_31()
		{
			ServerMode.SetRunEnded("labtest");
			return "SetRunEnded(labtest): RunEnded " + ServerMode.RunEnded + ", state " + ServerMode.StatusState;
		}

		internal bool _003CBuild_003Eb__26_32(JObject o)
		{
			if (S(o, "state") == "ended")
			{
				return S(o, "ending") == "labtest";
			}
			return false;
		}

		internal StepStatus _003CBuild_003Eb__26_33(BotStep st)
		{
			JObject o = AskStatus();
			st.Detail = "STATUS " + Short(o);
			if (!(S(o, "state") == "ended") || !(S(o, "ending") == "labtest"))
			{
				return StepStatus.Fail;
			}
			return StepStatus.Pass;
		}

		internal bool _003CBuild_003Eb__26_34(string r)
		{
			return r.StartsWith("refused: the run has ended", StringComparison.Ordinal);
		}

		internal bool _003CBuild_003Eb__26_35(string r)
		{
			return r.StartsWith("starting a new run", StringComparison.Ordinal);
		}

		internal bool _003CBuild_003Eb__26_36(JObject o)
		{
			return S(o, "state") == "loading";
		}

		internal bool _003CBuild_003Eb__26_37()
		{
			if (ServerMode.NewRunsStarted >= 1 && ServerMode.StatusState == "running")
			{
				return Game.ShopLoaded;
			}
			return false;
		}

		internal string _003CBuild_003Eb__26_38()
		{
			return "state " + ServerMode.StatusState + ", runs " + ServerMode.NewRunsStarted;
		}

		internal bool _003CBuild_003Eb__26_39(JObject o)
		{
			if (S(o, "state") == "running" && S(o, "ending") == "" && I(o, "day") == 1 && I(o, "cash") == Game.Store.playerCash)
			{
				return S(o, "scenario") != "";
			}
			return false;
		}

		internal bool _003CBuild_003Eb__26_40()
		{
			if (System.IO.Directory.GetFiles(Paths.Root, "ended_*_save_" + ProbablyTogether.Server.Config.saveSlot + ".es3").Length >= 1)
			{
				return File.Exists(Data(SaveFiles.Slot(ProbablyTogether.Server.Config.saveSlot)));
			}
			return false;
		}

		internal string _003CBuild_003Eb__26_41()
		{
			return "ended files: " + string.Join(", ", System.IO.Directory.GetFiles(Paths.Root, "ended_*").Select(Path.GetFileName)) + "; slot file " + File.Exists(Data(SaveFiles.Slot(ProbablyTogether.Server.Config.saveSlot)));
		}

		internal bool _003CBuild_003Eb__26_42(string r)
		{
			return r.StartsWith("starting a new run", StringComparison.Ordinal);
		}

		internal bool _003CBuild_003Eb__26_43()
		{
			if (ServerMode.NewRunsStarted >= 2 && ServerMode.StatusState == "running")
			{
				return Game.ShopLoaded;
			}
			return false;
		}

		internal string _003CBuild_003Eb__26_44()
		{
			return "state " + ServerMode.StatusState + ", runs " + ServerMode.NewRunsStarted;
		}

		internal bool _003CBuild_003Eb__26_45(JObject o)
		{
			if (S(o, "state") == "running")
			{
				return I(o, "day") == 1;
			}
			return false;
		}

		internal bool _003CBuild_003Eb__26_46()
		{
			return ProbablyTogether.Server.PlayerCount == 0;
		}

		internal string _003CBuild_003Eb__26_47()
		{
			return "players " + ProbablyTogether.Server.PlayerCount;
		}

		internal bool _003CBuild_003Eb__26_48(JObject o)
		{
			return Count(o) == 0;
		}

		internal string _003CBuild_003Eb__26_49()
		{
			File.WriteAllText(Data("stop"), "panel");
			return "wrote " + Data("stop");
		}

		internal bool _003CBuild_003Eb__26_50(JObject o)
		{
			return S(o, "state") == "stopping";
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass26_0
	{
		public JObject o;

		public JObject f;

		internal bool _003CBuild_003Eb__51(string k)
		{
			return o[k] == null;
		}

		internal bool _003CBuild_003Eb__52(string k)
		{
			return o[k] != null;
		}

		internal bool _003CBuild_003Eb__53(string k)
		{
			if (o[k] != null && f[k] != null)
			{
				return o[k].ToString((Formatting)0, Array.Empty<JsonConverter>()) != f[k].ToString((Formatting)0, Array.Empty<JsonConverter>());
			}
			return false;
		}
	}

	private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private static readonly HashSet<string> cueSeen = new HashSet<string>();

	private static bool cueHooked;

	private int day0;

	private long saveBefore;

	private DateTime slotTimeBefore;

	private string stamp;

	public override string Name => "coreops-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 900f;

	private static string Data(string f)
	{
		return Paths.Under(f);
	}

	private static JObject Status()
	{
		try
		{
			return JObject.Parse(File.ReadAllText(Data("status.json")));
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

	private static long L(JObject o, string k)
	{
		JToken val = ((o != null) ? o[k] : null);
		if (val == null || (int)val.Type != 6)
		{
			return -999L;
		}
		return (long)val;
	}

	private static string Names(JObject o)
	{
		JToken obj = ((o != null) ? o["players"] : null);
		JArray val = (JArray)(object)((obj is JArray) ? obj : null);
		if (val == null)
		{
			return "(none)";
		}
		return string.Join(",", ((IEnumerable<JToken>)val).Select((JToken x) => ((object)x[(object)"id"])?.ToString() + ":" + (object)x[(object)"name"]));
	}

	private static int Count(JObject o)
	{
		JToken obj = ((o != null) ? o["players"] : null);
		JArray val = (JArray)(object)((obj is JArray) ? obj : null);
		if (val == null)
		{
			return -1;
		}
		return ((JContainer)val).Count;
	}

	private static string Short(JObject o)
	{
		if (o != null)
		{
			return ((JToken)o).ToString((Formatting)0, Array.Empty<JsonConverter>());
		}
		return "status.json unreadable";
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
		catch (Exception ex)
		{
			Debug.Log((object)("[ProbablyTogether][bot] INFO coreops-server STATUS ask failed: " + ex.Message));
			return null;
		}
	}

	private BotStep Cmd(string name, string line, Func<string, bool> ok, float timeout = 15f)
	{
		return Raw(name, line + "\n", 1, ok, timeout);
	}

	private BotStep Raw(string name, string text, int lines, Func<string, bool> ok, float timeout = 15f, bool bom = false)
	{
		int before = 0;
		return Step(name, timeout, delegate(BotStep st)
		{
			if (PanelLink.CommandsRun < before + lines)
			{
				return StepStatus.Running;
			}
			string text2 = Data("commands.txt");
			long num = (File.Exists(text2) ? new FileInfo(text2).Length : (-1));
			bool flag = !File.Exists(text2 + ".run");
			st.Detail = "'" + PanelLink.LastCommand + "' -> " + PanelLink.LastResult + "; commands.txt " + ((num < 0) ? "missing" : (num + " bytes")) + (flag ? "" : ", .run still there");
			return (ok(PanelLink.LastResult) && num == 0 && flag) ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			before = PanelLink.CommandsRun;
			byte[] array = Utf8NoBom.GetBytes(text);
			if (bom)
			{
				array = new byte[3] { 239, 187, 191 }.Concat(array).ToArray();
			}
			using FileStream fileStream = new FileStream(Data("commands.txt"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
			fileStream.Write(array, 0, array.Length);
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
			st.Detail = Short(last) + " (write: " + PanelLink.LastStatusWhy + ", after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s)";
			return StepStatus.Pass;
		});
		botStep.TimeoutDetail = () => "status.json never matched: " + Short(last);
		return botStep;
	}

	private BotStep Cue(string name, string cue)
	{
		BotStep botStep = Step(name, 120f, (BotStep st) => cueSeen.Contains(cue) ? StepStatus.Pass : StepStatus.Running);
		botStep.Detail = "bot1 said '" + cue + "'";
		botStep.TimeoutDetail = () => "bot1 never said '" + cue + "'";
		return botStep;
	}

	protected override void Build()
	{
		stamp = DateTime.UtcNow.ToString("HHmmss", CultureInfo.InvariantCulture);
		if (!cueHooked)
		{
			cueHooked = true;
			object obj = _003C_003Ec._003C_003E9__26_0;
			if (obj == null)
			{
				Application.LogCallback val = delegate(string msg, string trace, LogType type)
				{
					if (msg != null && msg.StartsWith("[ProbablyTogether] server: chat.line #", StringComparison.Ordinal))
					{
						if (msg.Contains("night agreed"))
						{
							cueSeen.Add("night agreed");
						}
						if (msg.Contains("newrun agreed"))
						{
							cueSeen.Add("newrun agreed");
						}
					}
				};
				_003C_003Ec._003C_003E9__26_0 = val;
				obj = (object)val;
			}
			Application.logMessageReceived += (Application.LogCallback)obj;
		}
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop", 300f, () => Game.ShopLoaded && ProbablyTogether.Server.ShopReady, () => "scene " + BotProbes.Scene);
		Do("day0", delegate
		{
			day0 = Game.Day;
			return "day " + day0 + ", slot " + Game.Store.saveSlotId + ", data " + PanelLink.DataFolder;
		});
		StatusIs("status-start", 20f, (JObject o) => S(o, "state") == "running" && Count(o) == 0 && I(o, "day") == day0 && I(o, "cash") == Game.Store.playerCash && S(o, "scenario") == Game.Store.startType.ToString() && S(o, "ending") == "" && L(o, "lastSave") > 1700000000 && S(o, "version") == "0.2.8" && I(o, "protocol") == 2 && S(o, "gameVersion") == Application.version && S(o, "data") == PanelLink.DataFolder && System.IO.Directory.Exists(S(o, "data")) && I(o, "maxPlayers") == ProbablyTogether.Server.Config.maxPlayers && S(o, "name") == ProbablyTogether.Server.Config.name);
		Step("status-port", 10f, delegate(BotStep st)
		{
			JObject o = AskStatus();
			JObject f = Status();
			st.Detail = "STATUS " + Short(o);
			if (o == null || f == null)
			{
				return StepStatus.Fail;
			}
			string[] source = new string[11]
			{
				"state", "name", "players", "maxPlayers", "day", "cash", "scenario", "ending", "version", "protocol",
				"gameVersion"
			};
			List<string> list = source.Where((string k) => o[k] == null).ToList();
			List<string> list2 = new string[2] { "data", "lastSave" }.Where((string k) => o[k] != null).ToList();
			List<string> list3 = source.Where((string k) => o[k] != null && f[k] != null && o[k].ToString((Formatting)0, Array.Empty<JsonConverter>()) != f[k].ToString((Formatting)0, Array.Empty<JsonConverter>())).ToList();
			st.Detail += ((list.Count > 0) ? ("; MISSING " + string.Join(",", list)) : "");
			st.Detail += ((list2.Count > 0) ? ("; LEAKS " + string.Join(",", list2) + " (ruling 16)") : "");
			st.Detail += ((list3.Count > 0) ? ("; differs from status.json: " + string.Join(",", list3)) : "; same fields as status.json");
			return (list.Count == 0 && list2.Count == 0 && list3.Count == 0 && S(o, "state") == "running") ? StepStatus.Pass : StepStatus.Fail;
		});
		WaitFor("two-live", 300f, () => ProbablyTogether.Server.ByPlayer.Count == 2 && ProbablyTogether.Server.ByPlayer.Keys.All(Net.IsPlayerLive), () => "players " + string.Join(",", Net.Players.Values.Select((PlayerInfo p) => p.Name + "#" + p.Id)));
		StatusIs("status-join", 15f, (JObject o) => Count(o) == 2 && Names(o).Contains(":bot1") && Names(o).Contains(":bot2") && S(o, "state") == "running");
		Cmd("cmd-say", "say Hello from the panel " + stamp, (string r) => r.StartsWith("sent to 2 players", StringComparison.Ordinal));
		Do("save-before", delegate
		{
			saveBefore = PanelLink.LastSaveUnix;
			slotTimeBefore = File.GetLastWriteTimeUtc(Data(SaveFiles.Slot(Game.Store.saveSlotId)));
			return "lastSave " + saveBefore + ", slot file written " + slotTimeBefore.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " UTC";
		});
		Delay("tick", 1.2f);
		Cmd("cmd-save", "save", (string r) => r.StartsWith("saved slot", StringComparison.Ordinal));
		Check("save-wrote", () => File.GetLastWriteTimeUtc(Data(SaveFiles.Slot(Game.Store.saveSlotId))) > slotTimeBefore && PanelLink.LastSaveUnix >= saveBefore, () => "slot file written " + File.GetLastWriteTimeUtc(Data(SaveFiles.Slot(Game.Store.saveSlotId))).ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " UTC, lastSave " + PanelLink.LastSaveUnix);
		StatusIs("status-save", 15f, (JObject o) => L(o, "lastSave") == PanelLink.LastSaveUnix && L(o, "lastSave") >= saveBefore);
		Cmd("cmd-newrun-running", "newrun", (string r) => r.StartsWith("refused: the run is still going", StringComparison.Ordinal));
		Cmd("cmd-unknown", "frobnicate the shop", (string r) => r == "unknown command");
		Cmd("cmd-kick-nobody", "kick nobody-here", (string r) => r.StartsWith("no such player", StringComparison.Ordinal));
		Raw("cmd-bom-crlf", "say external line " + stamp + "\r\nsay second line\r\n", 2, (string r) => r.StartsWith("sent to 2 players", StringComparison.Ordinal), 15f, bom: true);
		Delay("clients-see-chat", 4f);
		Cmd("cmd-kick", "kick bot2", (string r) => r.StartsWith("kicked bot2", StringComparison.Ordinal));
		WaitFor("kicked-gone", 20f, () => ProbablyTogether.Server.PlayerCount == 1, () => "players " + ProbablyTogether.Server.PlayerCount);
		StatusIs("status-leave", 15f, (JObject o) => Count(o) == 1 && Names(o).Contains(":bot1") && !Names(o).Contains(":bot2"));
		WaitFor("day-open", 180f, () => Game.ShopLoaded && (int)Game.Store.storeState == 1, () => "state " + (Game.Store?.storeState).ToString());
		Cmd("cmd-save-day", "save", (string r) => r.StartsWith("queued", StringComparison.Ordinal));
		WaitFor("night", 240f, () => Game.ShopLoaded && Game.Day == day0 + 1 && (int)Game.Store.storeState == 2, () => "day " + Game.Day + ", " + (Game.Store?.storeState).ToString());
		StatusIs("status-day", 15f, (JObject o) => I(o, "day") == day0 + 1 && L(o, "lastSave") > saveBefore && I(o, "cash") == Game.Store.playerCash);
		Cue("bot1-saw-night", "night agreed");
		Do("run-ended", delegate
		{
			ServerMode.SetRunEnded("labtest");
			return "SetRunEnded(labtest): RunEnded " + ServerMode.RunEnded + ", state " + ServerMode.StatusState;
		});
		StatusIs("status-ended", 15f, (JObject o) => S(o, "state") == "ended" && S(o, "ending") == "labtest");
		Step("status-port-ended", 10f, delegate(BotStep st)
		{
			JObject o = AskStatus();
			st.Detail = "STATUS " + Short(o);
			return (S(o, "state") == "ended" && S(o, "ending") == "labtest") ? StepStatus.Pass : StepStatus.Fail;
		});
		Cmd("cmd-save-ended", "save", (string r) => r.StartsWith("refused: the run has ended", StringComparison.Ordinal));
		Cmd("cmd-newrun", "newrun", (string r) => r.StartsWith("starting a new run", StringComparison.Ordinal));
		StatusIs("status-loading", 10f, (JObject o) => S(o, "state") == "loading");
		WaitFor("newrun-up", 180f, () => ServerMode.NewRunsStarted >= 1 && ServerMode.StatusState == "running" && Game.ShopLoaded, () => "state " + ServerMode.StatusState + ", runs " + ServerMode.NewRunsStarted);
		StatusIs("status-newrun", 15f, (JObject o) => S(o, "state") == "running" && S(o, "ending") == "" && I(o, "day") == 1 && I(o, "cash") == Game.Store.playerCash && S(o, "scenario") != "");
		Check("old-run-aside", () => System.IO.Directory.GetFiles(Paths.Root, "ended_*_save_" + ProbablyTogether.Server.Config.saveSlot + ".es3").Length >= 1 && File.Exists(Data(SaveFiles.Slot(ProbablyTogether.Server.Config.saveSlot))), () => "ended files: " + string.Join(", ", System.IO.Directory.GetFiles(Paths.Root, "ended_*").Select(Path.GetFileName)) + "; slot file " + File.Exists(Data(SaveFiles.Slot(ProbablyTogether.Server.Config.saveSlot))));
		Cue("bot1-saw-newrun", "newrun agreed");
		Cmd("cmd-newrun-force", "newrun force", (string r) => r.StartsWith("starting a new run", StringComparison.Ordinal));
		WaitFor("newrun2-up", 180f, () => ServerMode.NewRunsStarted >= 2 && ServerMode.StatusState == "running" && Game.ShopLoaded, () => "state " + ServerMode.StatusState + ", runs " + ServerMode.NewRunsStarted);
		StatusIs("status-newrun2", 15f, (JObject o) => S(o, "state") == "running" && I(o, "day") == 1);
		WaitFor("bot1-done", 200f, () => ProbablyTogether.Server.PlayerCount == 0, () => "players " + ProbablyTogether.Server.PlayerCount);
		StatusIs("status-empty", 15f, (JObject o) => Count(o) == 0);
		Do("stop-file", delegate
		{
			File.WriteAllText(Data("stop"), "panel");
			return "wrote " + Data("stop");
		});
		StatusIs("status-stopping", 10f, (JObject o) => S(o, "state") == "stopping");
	}
}
