using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ModSyncJoin : BotScript
{
	private string address;

	private string expect;

	private string why;

	private string ws;

	private string warn;

	private string note;

	private bool viaWindow;

	private int tries;

	private int askId;

	private float retryAt;

	private string outcome = "";

	public override string Name => "modsync-join";

	public override float TimeoutSeconds => 420f;

	protected override void Build()
	{
		address = Args.Get("--br-ms-join");
		viaWindow = !string.IsNullOrEmpty(address);
		expect = (Args.Get("--br-ms-expect") ?? "").Trim().ToLowerInvariant();
		if (expect == "accepted")
		{
			expect = "accept";
		}
		why = (Args.Get("--br-ms-why") ?? "").Trim().ToLowerInvariant();
		ws = (Args.Get("--br-ms-ws") ?? "").Trim();
		warn = (Args.Get("--br-ms-warn") ?? "").Trim();
		note = (Args.Get("--br-ms-note") ?? "").Trim();
		Check("switches", delegate
		{
			if (!viaWindow)
			{
				return Args.Join != null;
			}
			return JoinMenu.ValidAddress(address) && Args.Join == null;
		}, () => (viaWindow ? ("Join window, --br-ms-join=" + address) : ("core join, --br-join=" + (Args.Join ?? "absent"))) + "; expect " + ((expect.Length > 0) ? expect : "(record only)") + ((why.Length > 0) ? (", why " + why) : "") + ((ws.Length > 0) ? (", ws " + ws) : "") + ((warn.Length > 0) ? (", warning " + warn) : "") + ((note.Length > 0) ? (", copy note " + note) : ""));
		Do("my-mods", delegate
		{
			List<ModList.Info> list = ModSyncBot.Mine();
			return list.Count + " mod(s) besides ours: " + ModSyncBot.Describe(list);
		});
		if (viaWindow)
		{
			WaitFor("menu", 90f, () => Ui.AtMenu && (Object)(object)MainMenuUIController.Instance != (Object)null, () => "scene " + BotProbes.Scene);
			WaitFor("button", 30f, () => (Object)(object)JoinMenu.Button != (Object)null, () => "fallback " + JoinMenu.UsingFallbackButton);
			Do("open", delegate
			{
				((UnityEvent)JoinMenu.Button.GetComponentInChildren<Button>(true).onClick).Invoke();
				return "clicked Join server";
			});
			WaitFor("window", 10f, () => JoinMenu.DialogOpen);
			Do("fill", delegate
			{
				CustomUIWindow window = CustomUIManager.Instance.GetWindow("humangenome_accomplice.join");
				window.Get("address").SetString(address, true);
				window.Get("name").SetString("botm", true);
				window.Get("password").SetString(Args.Get("--br-ms-pw") ?? "", true);
				return "address " + address + ", name botm";
			});
			Do("join", delegate
			{
				tries = 1;
				JoinMenu.Join();
				return "pressed Join";
			});
		}
		else
		{
			tries = 1;
		}
		Step("outcome", 300f, Outcome).TimeoutDetail = () => "neither let in nor refused: state " + Net.ClientState.ToString() + ", refuse code " + (Net.LastRefuseCode ?? "-") + ", disconnect '" + Net.LastDisconnectReason + "', window status '" + JoinMenu.Status + "'";
		Step("refusal", 5f, Refusal);
		Step("mods-window", 15f, ModsWindow);
		Step("let-in", 30f, LetIn);
		Step("copy-note", 30f, CopyNote);
		Step("same-mods", 30f, SameMods, delegate
		{
			if (outcome == "accepted")
			{
				askId = BotProbes.AskServer();
			}
		});
		Step("verdict", 5f, Verdict);
	}

	private StepStatus Outcome(BotStep st)
	{
		if (retryAt > 0f)
		{
			if (Time.realtimeSinceStartup < retryAt || Net.ClientState != ClientState.Idle)
			{
				return StepStatus.Running;
			}
			retryAt = 0f;
			tries++;
			if (viaWindow)
			{
				JoinMenu.Join();
			}
			else
			{
				Net.Connect(Args.Join, Args.Password ?? "", Args.Name);
			}
			BotLog.Info(Name, "the server was still loading its shop; join asked again (try " + tries + ")");
			return StepStatus.Running;
		}
		string lastRefuseCode = Net.LastRefuseCode;
		if (lastRefuseCode != null && Net.ClientState == ClientState.Idle)
		{
			if (lastRefuseCode == "starting" && tries < 12)
			{
				retryAt = Time.realtimeSinceStartup + 10f;
				return StepStatus.Running;
			}
			outcome = lastRefuseCode;
			st.Detail = "refused, code " + lastRefuseCode + " after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s (try " + tries + "): " + Net.LastRefuseReason;
			return StepStatus.Pass;
		}
		if (Ui.ShopOnline)
		{
			outcome = "accepted";
			st.Detail = "let in as player " + Net.LocalPlayer + " after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s (try " + tries + "); server lists mods " + ((Client.ServerMods == null) ? "(none: an older server)" : ((Client.ServerMods.Count == 0) ? "-" : string.Join(", ", Client.ServerMods)));
			return StepStatus.Pass;
		}
		return StepStatus.Running;
	}

	private StepStatus Refusal(BotStep st)
	{
		if (outcome == "accepted")
		{
			st.Detail = "not refused";
			return StepStatus.Pass;
		}
		st.Detail = "code " + outcome + "; reason '" + Net.LastRefuseReason + "'; Net.LastRefuseMods: " + ModSyncBot.Describe(Net.LastRefuseMods);
		return StepStatus.Pass;
	}

	private StepStatus ModsWindow(BotStep st)
	{
		if (viaWindow && (outcome == "mods" || outcome == "extramods") && Net.LastRefuseMods != null && Net.LastRefuseMods.Count > 0 && !JoinMenu.ModsOpen)
		{
			if (st.Elapsed < 10f)
			{
				return StepStatus.Running;
			}
			st.Detail = "the mods window did not open (Join window status '" + JoinMenu.Status + "')";
			return StepStatus.Fail;
		}
		CustomUIManager instance = CustomUIManager.Instance;
		CustomUIWindow val = ((instance != null) ? instance.GetWindow("humangenome_accomplice.mods") : null);
		List<string> list = new List<string>();
		if (val != null)
		{
			for (int i = 0; i < 64; i++)
			{
				CustomUIElement val2 = val.Get("mod_" + i);
				if (val2 == null)
				{
					break;
				}
				list.Add("'" + val2.GetText() + "'" + ((val.Get("ws_" + i) != null) ? " +Workshop page button" : ""));
			}
		}
		Vars["window"] = (JoinMenu.ModsOpen ? "open" : "closed");
		string text = JoinMenu.ModsOpen.ToString();
		object obj;
		if (val == null)
		{
			obj = "";
		}
		else
		{
			string[] obj2 = new string[7] { ": reason '", null, null, null, null, null, null };
			CustomUIElement obj3 = val.Get("reason");
			obj2[1] = ((obj3 != null) ? obj3.GetText() : null);
			obj2[2] = "'; rows ";
			obj2[3] = string.Join(" | ", list);
			obj2[4] = "; ModsStatus '";
			obj2[5] = JoinMenu.ModsStatus;
			obj2[6] = "'";
			obj = string.Concat(obj2);
		}
		st.Detail = "ModsOpen " + text + (string?)obj + (viaWindow ? ("; Join window status '" + JoinMenu.Status + "'") : " (core join: the window opens only from the Join window)");
		return StepStatus.Pass;
	}

	private StepStatus LetIn(BotStep st)
	{
		if (outcome != "accepted")
		{
			st.Detail = "not let in";
			return StepStatus.Pass;
		}
		Chat.Line line = Chat.Lines.FirstOrDefault((Chat.Line l) => l.N == 0 && l.Text.Contains("not on this server"));
		bool flag = warn == "1" || (warn != "0" && ModList.ExtraModsLine(Client.ServerMods) != null);
		if (flag && line == null && st.Elapsed < 15f)
		{
			return StepStatus.Running;
		}
		if (!flag && st.Elapsed < 5f)
		{
			return StepStatus.Running;
		}
		Vars["warned"] = ((line != null) ? "1" : "0");
		st.Detail = ((line != null) ? ("warning line: '" + line.Text + "'") : ("no warning line (" + Chat.Lines.Count + " chat line(s))"));
		return StepStatus.Pass;
	}

	private StepStatus CopyNote(BotStep st)
	{
		if (outcome != "accepted")
		{
			st.Detail = "not let in";
			return StepStatus.Pass;
		}
		Chat.Line line = Chat.Lines.FirstOrDefault((Chat.Line l) => l.N == 0 && l.Text.Contains("not the same as the server's"));
		if (note == "1" && line == null && st.Elapsed < 20f)
		{
			return StepStatus.Running;
		}
		if (note != "1" && line == null && st.Elapsed < 5f)
		{
			return StepStatus.Running;
		}
		Vars["noted"] = ((line != null) ? "1" : "0");
		st.Detail = ((line != null) ? ("copy note: '" + line.Text + "'") : ("no copy note (" + Chat.Lines.Count + " chat line(s); last notice '" + Chat.LastNotice + "')"));
		return StepStatus.Pass;
	}

	private StepStatus SameMods(BotStep st)
	{
		if (outcome != "accepted")
		{
			st.Detail = "not let in";
			return StepStatus.Pass;
		}
		Dictionary<string, string> dictionary = BotProbes.TakeReply(askId);
		if (dictionary == null)
		{
			if (st.Elapsed < 25f)
			{
				return StepStatus.Running;
			}
			st.Detail = "no probe reply (does the server run a --br-bot script?)";
			return StepStatus.Pass;
		}
		string mine = ModSyncBot.Set();
		string value;
		string text = (dictionary.TryGetValue("mods.set", out value) ? value : "(no mods.set probe)");
		List<string> list = new HashSet<string>(from s in text.Split(',')
			where s.Contains("@")
			select s, StringComparer.Ordinal).Where((string s) => !mine.Split(',').Contains(s)).ToList();
		Vars["samemods"] = ((list.Count == 0) ? "1" : "0");
		st.Detail = "server " + text + " / here " + mine + ((list.Count == 0) ? ": every server mod is the same copy here" : (": NOT the same here: " + string.Join(",", list))) + ((note == "1") ? " (another copy expected: --br-ms-note=1)" : "");
		if (note == "1")
		{
			if (list.Count <= 0)
			{
				return StepStatus.Fail;
			}
			return StepStatus.Pass;
		}
		if (list.Count != 0)
		{
			return StepStatus.Fail;
		}
		return StepStatus.Pass;
	}

	private StepStatus Verdict(BotStep st)
	{
		List<string> list = new List<string>();
		if (expect.Length > 0 && expect != ((outcome == "accepted") ? "accept" : outcome))
		{
			list.Add("expected " + expect + ", got " + outcome);
		}
		List<ModList.Info> lastRefuseMods = Net.LastRefuseMods;
		if (why.Length > 0)
		{
			if (lastRefuseMods == null || lastRefuseMods.Count == 0)
			{
				list.Add("expected why " + why + " but no list came");
			}
			else if (lastRefuseMods.Any((ModList.Info m) => m.Why != why))
			{
				list.Add("expected every why " + why + ", got " + string.Join(",", lastRefuseMods.Select((ModList.Info m) => (m.Why.Length <= 0) ? "-" : m.Why)));
			}
		}
		if (ws.Length > 0 && (lastRefuseMods == null || !lastRefuseMods.Any((ModList.Info m) => m.Ws.ToString(CultureInfo.InvariantCulture) == ws)))
		{
			list.Add("no listed mod carries Workshop " + ws);
		}
		if (outcome == "accepted" && warn.Length > 0 && Vars.TryGetValue("warned", out var value) && value != warn)
		{
			list.Add("warning line " + ((value == "1") ? "shown" : "missing") + ", expected " + ((warn == "1") ? "shown" : "none"));
		}
		if (outcome == "accepted" && note.Length > 0 && Vars.TryGetValue("noted", out var value2) && value2 != note)
		{
			list.Add("copy note " + ((value2 == "1") ? "shown" : "missing") + ", expected " + ((note == "1") ? "shown" : "none"));
		}
		if (viaWindow && (outcome == "mods" || outcome == "extramods") && lastRefuseMods != null && lastRefuseMods.Count > 0 && Vars.TryGetValue("window", out var value3) && value3 != "open")
		{
			list.Add("mods window not open");
		}
		st.Detail = ((list.Count == 0) ? "as expected: " : "") + "outcome " + outcome + ((list.Count > 0) ? ("; " + string.Join("; ", list)) : "");
		if (list.Count != 0)
		{
			return StepStatus.Fail;
		}
		return StepStatus.Pass;
	}
}
