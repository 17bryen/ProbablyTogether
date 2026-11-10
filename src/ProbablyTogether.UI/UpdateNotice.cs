using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using ProbablyTogether.Bots;
using Steamworks;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

public static class UpdateNotice
{
	public const string WindowId = "bellun_probably_together.update";

	private const float PollSeconds = 5f;

	private const uint Slack = 60u;

	private static bool started;

	private static bool show;

	private static bool lab;

	private static string folder;

	private static PublishedFileId_t item;

	private static uint latest;

	private static float nextPoll;

	private static CallResult<SteamUGCQueryCompleted_t> query;

	private static readonly Regex VersionTag = new Regex("<ModVersion>\\s*([0-9A-Za-z.\\-]{1,24})\\s*</ModVersion>", RegexOptions.CultureInvariant);

	public static string State { get; private set; } = "";

	public static string Line { get; private set; } = "";

	public static bool IsOpen
	{
		get
		{
			if ((Object)(object)CustomUIManager.Instance != (Object)null)
			{
				return CustomUIManager.Instance.IsOpen("bellun_probably_together.update");
			}
			return false;
		}
	}

	internal static void Tick()
	{
		if (!Ui.AtMenu)
		{
			return;
		}
		if (!started)
		{
			Start();
		}
		if (State == "downloading" && !lab && Time.unscaledTime >= nextPoll)
		{
			nextPoll = Time.unscaledTime + 5f;
			Check();
		}
		if (show && !JoinMenu.DialogOpen && Net.ClientState == ClientState.Idle)
		{
			CustomUIManager instance = CustomUIManager.Instance;
			if (instance != null && !instance.IsOpen("bellun_probably_together.notice"))
			{
				Show();
			}
		}
	}

	private static void Start()
	{
		started = true;
		if (Lab())
		{
			return;
		}
		ModList.Info own = ModList.Own;
		if (own == null || own.Ws == 0L || string.IsNullOrEmpty(own.Folder))
		{
			State = "local";
			Log.Info("update: this copy is not the Workshop one (local mod folder): no update check");
			return;
		}
		folder = own.Folder;
		item = new PublishedFileId_t(own.Ws);
		if (Ready())
		{
			return;
		}
		if (!SteamManager.Initialized)
		{
			State = "no-steam";
			Log.Info("update: Steam is not running: no update check");
			return;
		}
		try
		{
			UGCQueryHandle_t val = SteamUGC.CreateQueryUGCDetailsRequest((PublishedFileId_t[])(object)new PublishedFileId_t[1] { item }, 1u);
			SteamUGC.SetAllowCachedResponse(val, 0u);
			SteamAPICall_t val2 = SteamUGC.SendQueryUGCRequest(val);
			query = CallResult<SteamUGCQueryCompleted_t>.Create((CallResult<SteamUGCQueryCompleted_t>.APIDispatchDelegate)OnQuery);
			query.Set(val2, (CallResult<SteamUGCQueryCompleted_t>.APIDispatchDelegate)null);
			State = "asking";
		}
		catch (Exception ex)
		{
			State = "failed";
			Log.Warn("update: could not ask Steam (" + ex.Message + ")");
		}
	}

	private unsafe static void OnQuery(SteamUGCQueryCompleted_t r, bool ioFailure)
	{
		try
		{
			SteamUGCDetails_t val = default(SteamUGCDetails_t);
			if (ioFailure || (int)r.m_eResult != 1 || r.m_unNumResultsReturned < 1)
			{
				State = "failed";
				Log.Info("update: Steam did not answer the details request (" + (ioFailure ? "IO failure" : ((object)(*(EResult*)(&r.m_eResult))).ToString()) + "): no notice");
			}
			else if (SteamUGC.GetQueryUGCResult(r.m_handle, 0u, out val))
			{
				latest = val.m_rtimeUpdated;
			}
		}
		catch (Exception ex)
		{
			State = "failed";
			Log.Warn("update: reading Steam's answer: " + ex.Message);
		}
		finally
		{
			try
			{
				SteamUGC.ReleaseQueryUGCRequest(r.m_handle);
			}
			catch
			{
			}
		}
		if (State != "failed")
		{
			Check();
		}
	}

	private static void Check()
	{
		if (Ready())
		{
			return;
		}
		uint num = Installed();
		if (latest != 0 && num != 0 && latest > num + 60)
		{
			if (State != "downloading")
			{
				State = "downloading";
				Say("A new version of ProbablyTogether is out. Steam is downloading it. This window says when it is ready.");
				Log.Info("update: the Workshop has a newer ProbablyTogether (updated " + Stamp(latest) + ", this copy " + Stamp(num) + "); waiting for Steam to download it");
			}
		}
		else if (State == "downloading")
		{
			State = "ready";
			Say("ProbablyTogether is updated. Restart the game to use it.");
			Log.Info("update: Steam downloaded the newer upload (" + Stamp(num) + "), same version number");
		}
		else
		{
			State = "current";
			Log.Info("update: this copy is the newest on the Workshop (" + ((num != 0) ? ("updated " + Stamp(num)) : "install time unknown") + ((latest != 0) ? "" : ", Steam gave no time") + ")");
		}
	}

	private static bool Ready()
	{
		string text = DiskVersion();
		if (text.Length == 0 || text == Mod.Version)
		{
			return false;
		}
		if (State == "ready")
		{
			return true;
		}
		State = "ready";
		Say("ProbablyTogether " + text + " is downloaded. Restart the game to use it.");
		Log.Info("update: " + Mod.Name + " " + text + " is on disk, " + Mod.Version + " is running: restart needed");
		return true;
	}

	private static string DiskVersion()
	{
		try
		{
			string text = Path.Combine(folder ?? "", "manifest.xml");
			if (folder == null || !File.Exists(text) || new FileInfo(text).Length > 65536)
			{
				return "";
			}
			Match match = VersionTag.Match(File.ReadAllText(text));
			return match.Success ? match.Groups[1].Value : "";
		}
		catch
		{
			return "";
		}
	}

	private static uint Installed()
	{
		try
		{
			ulong num = default(ulong);
			string text = default(string);
			uint num2 = default(uint);
			return SteamUGC.GetItemInstallInfo(item, out num, out text, 1024u, out num2) ? num2 : 0u;
		}
		catch
		{
			return 0u;
		}
	}

	private static string Stamp(uint t)
	{
		return DateTimeOffset.FromUnixTimeSeconds(t).UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
	}

	private static void Say(string line)
	{
		Line = line;
		show = true;
		if (IsOpen)
		{
			Show();
		}
	}

	private static void Show()
	{
		show = false;
		CustomUIManager instance = CustomUIManager.Instance;
		if ((Object)(object)instance == (Object)null || Line.Length == 0)
		{
			return;
		}
		if (IsOpen)
		{
			instance.CloseWindow("bellun_probably_together.update");
		}
		CustomUIBuilder val = instance.CreateWindow("bellun_probably_together.update", Lang.Tr("ProbablyTogether update"), "menu", (GameObject)null, (CustomUIManager.Style)0).SetCloseOnEscape(true).SetSize(560f, 260f);
		val.AddLabel(Lang.Tr(Line), "line");
		HorizontalLayoutGroup val2 = Kit.Row((Transform)(object)val.CurrentContainer, "row", 8f);
		new CustomUIBuilder(instance, val.Handle, (RectTransform)((Component)val2).transform).AddFlexibleSpace().AddButton(Lang.Tr("OK"), (Action)delegate
		{
			CustomUIManager instance2 = CustomUIManager.Instance;
			if (instance2 != null)
			{
				instance2.CloseWindow("bellun_probably_together.update");
			}
		}, "ok");
		CustomUIWindow obj = val.Center().Show();
		TextMeshProUGUI val3 = ((obj == null) ? null : obj.Get("line")?.text);
		if ((Object)(object)val3 != (Object)null)
		{
			((TMP_Text)val3).enableWordWrapping = true;
			((TMP_Text)val3).overflowMode = (TextOverflowModes)0;
			Kit.FitFont(val3);
		}
		JoinMenu.FitHeight(obj, 160f, 320f, 10f);
		Log.Info("ui: update notice shown: " + Line);
	}

	private static bool Lab()
	{
		try
		{
			string text = Args.Get("--br-update");
			if (text == null || text.Length == 0 || !BotRunner.LabAllowed(out var _))
			{
				return false;
			}
			if (text == "downloading")
			{
				State = "downloading";
				Say("A new version of ProbablyTogether is out. Steam is downloading it. This window says when it is ready.");
			}
			else
			{
				if (!text.StartsWith("ready:", StringComparison.Ordinal) || !Regex.IsMatch(text.Substring(6), "^[0-9.]{1,12}$"))
				{
					return false;
				}
				State = "ready";
				Say("ProbablyTogether " + text.Substring(6) + " is downloaded. Restart the game to use it.");
			}
			lab = true;
			Log.Info("update: lab state " + text);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
