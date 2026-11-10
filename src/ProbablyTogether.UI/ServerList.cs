using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ProbablyTogether.Bots;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

public static class ServerList
{
	public sealed class Entry
	{
		public string Name = "";

		public string Address = "";

		public string State = "";

		public string Mod = "";

		public string Game = "";

		public string City = "";

		public string Why;

		public int Players;

		public int Max;

		public int Day;

		public int Protocol;

		public int Mods;

		public bool Password;
	}

	private sealed class Recent
	{
		public JoinMenu.SavedServer Saved;

		public Entry Live;
	}

	public const string WindowId = "humangenome_accomplice.servers";

	public const string NeedsMod = "needs a mod update";

	public const string NeedsGame = "needs a game update";

	private const int TimeoutSeconds = 8;

	private const int MaxBytes = 1048576;

	private const int MaxRows = 100;

	private const int NameMax = 40;

	private const float RowHeight = 50f;

	private const float RowGap = 4f;

	private const float HeadHeight = 30f;

	private const int RowsShown = 8;

	private static readonly List<Entry> rows = new List<Entry>();

	private static UnityWebRequest request;

	private static CustomUIWindow window;

	private static string lastProblem;

	private const float SectionHeight = 34f;

	private const float Width = 1000f;

	private const float CityW = 140f;

	private const float VerW = 88f;

	private const float ModsW = 64f;

	private const float PlayersW = 80f;

	private const float DayW = 110f;

	private const float ForgetW = 100f;

	private const float BoxHeight = 530f;

	private static bool showVer;

	private static bool rebuilding;

	private static RectTransform headRow;

	private static Sprite lockSprite;

	public static bool IsOpen
	{
		get
		{
			if ((Object)(object)CustomUIManager.Instance != (Object)null)
			{
				return CustomUIManager.Instance.IsOpen("humangenome_accomplice.servers");
			}
			return false;
		}
	}

	public static string State { get; private set; } = "";

	public static IReadOnlyList<Entry> Rows => rows;

	public static string Status { get; private set; } = "";

	public static int Fetches { get; private set; }

	public static int Shown { get; private set; }

	public static void OpenWindow()
	{
		if (!((Object)(object)CustomUIManager.Instance == (Object)null) && Ui.AtMenu)
		{
			Log.Info("ui: Server list pressed");
			Refresh();
		}
	}

	public static void Refresh()
	{
		Abort();
		rows.Clear();
		State = "loading";
		Status = "";
		Build();
		Fetch();
	}

	public static void Close()
	{
		CustomUIManager instance = CustomUIManager.Instance;
		if (instance != null)
		{
			instance.CloseWindow("humangenome_accomplice.servers");
		}
		Abort();
	}

	private static void Fetch()
	{
		try
		{
			string text = Args.Get("--br-list");
			if (text != null && text.Length > 0 && BotRunner.LabAllowed(out var _))
			{
				Fetches++;
				Done(File.Exists(text) ? File.ReadAllText(text) : null, File.Exists(text) ? null : "lab list file missing");
				return;
			}
		}
		catch (Exception ex)
		{
			Done(null, "lab list: " + Text.Clip(ex.Message, 80));
			return;
		}
		UnityWebRequest val = null;
		try
		{
			val = UnityWebRequest.Get("https://api.survivalservers.com/probably_together/v1/servers");
			val.timeout = 8;
			val.SetRequestHeader("User-Agent", "ProbablyTogether/0.2.8");
			val.SendWebRequest();
			request = val;
			Fetches++;
		}
		catch (Exception ex2)
		{
			try
			{
				if (val != null)
				{
					val.Dispose();
				}
			}
			catch
			{
			}
			request = null;
			Done(null, "could not ask (" + Text.Clip(ex2.Message, 120) + ")");
		}
	}

	private static void Abort()
	{
		UnityWebRequest val = request;
		request = null;
		if (val == null)
		{
			return;
		}
		try
		{
			val.Abort();
		}
		catch
		{
		}
		try
		{
			val.Dispose();
		}
		catch
		{
		}
	}

	internal static void Tick()
	{
		UnityWebRequest val = request;
		if (val == null)
		{
			return;
		}
		if (!val.isDone)
		{
			if (val.downloadedBytes > 1048576)
			{
				request = null;
				try
				{
					val.Abort();
					val.Dispose();
				}
				catch
				{
				}
				Done(null, "the answer was too big");
			}
			return;
		}
		request = null;
		string text = null;
		string problem = null;
		try
		{
			if ((int)val.result == 1)
			{
				DownloadHandler downloadHandler = val.downloadHandler;
				text = ((downloadHandler != null) ? downloadHandler.text : null);
			}
			else
			{
				problem = Text.Clip(val.error ?? ((object)val.result).ToString(), 120) + ((val.responseCode > 0) ? (" (HTTP " + val.responseCode + ")") : "");
			}
		}
		catch (Exception ex)
		{
			problem = Text.Clip(ex.Message, 120);
		}
		finally
		{
			try
			{
				val.Dispose();
			}
			catch
			{
			}
		}
		Done(text, problem);
	}

	private static void Done(string text, string problem)
	{
		rows.Clear();
		if (problem == null && !Parse(text, rows, out problem))
		{
			rows.Clear();
		}
		State = ((problem != null) ? "unreachable" : ((rows.Count == 0) ? "empty" : "listed"));
		int num = 0;
		foreach (Entry row in rows)
		{
			if (row.Why == null)
			{
				num++;
			}
		}
		string m = ((problem != null) ? ("ui: server list unreachable: " + problem) : ("ui: server list: " + rows.Count + " server(s), " + num + " joinable with this mod and game version"));
		if (problem == null || !string.Equals(problem, lastProblem, StringComparison.Ordinal))
		{
			Log.Info(m);
		}
		lastProblem = problem;
		if (window != null && window.IsAlive)
		{
			Build();
		}
	}

	private static bool Parse(string text, List<Entry> into, out string problem)
	{
		problem = null;
		if (string.IsNullOrEmpty(text))
		{
			problem = "empty answer";
			return false;
		}
		if (text.Length > 1048576)
		{
			problem = "the answer was too big";
			return false;
		}
		JObject val2;
		try
		{
			using StringReader stringReader = new StringReader(text);
			JsonTextReader val = new JsonTextReader((TextReader)stringReader)
			{
				MaxDepth = 16,
				DateParseHandling = (DateParseHandling)0
			};
			try
			{
				JToken obj = JToken.ReadFrom((JsonReader)(object)val);
				val2 = (JObject)(object)((obj is JObject) ? obj : null);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch (Exception ex)
		{
			problem = "not a server list (" + Text.Clip(ex.Message, 80) + ")";
			return false;
		}
		JToken obj2 = ((val2 != null) ? val2["servers"] : null);
		JArray val3 = (JArray)(object)((obj2 is JArray) ? obj2 : null);
		if (val3 == null)
		{
			problem = "not a server list (no servers)";
			return false;
		}
		List<Entry> list = new List<Entry>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		int num = 0;
		foreach (JToken item in val3)
		{
			if (++num > 200 || into.Count + list.Count >= 100)
			{
				break;
			}
			JObject val4 = (JObject)(object)((item is JObject) ? item : null);
			if (val4 != null)
			{
				Entry entry = Read(val4);
				if (entry != null && hashSet.Add(entry.Address))
				{
					((entry.Why == null) ? into : list).Add(entry);
				}
			}
		}
		into.AddRange(list);
		return true;
	}

	private static Entry Read(JObject o)
	{
		string text = Str(o, "address", 80).Trim();
		if (!JoinMenu.ValidAddress(text) || text.IndexOf(':') < 0)
		{
			return null;
		}
		Entry entry = new Entry
		{
			Address = text,
			Players = Int(o, "players"),
			Max = Int(o, "max"),
			Day = Int(o, "day"),
			Protocol = Int(o, "protocol"),
			Password = Bool(o, "password"),
			State = Str(o, "state", 16),
			Mod = Str(o, "mod", 24),
			Game = Str(o, "game", 24).Trim(),
			Mods = Math.Min(Int(o, "mods"), 99)
		};
		string text2 = Text.Clip(Chat.Clean(Str(o, "city", 80)), 24).Trim();
		entry.City = ((text2.Length > 0) ? Kit.Name(text2) : "");
		string text3 = Text.Clip(Chat.Clean(Str(o, "name", 200)), 40).Trim();
		entry.Name = Kit.Name((text3.Length > 0) ? text3 : text);
		if (entry.Protocol > 0 && entry.Protocol != 2)
		{
			entry.Why = "needs a mod update";
		}
		else if (entry.Game.Length > 0 && !string.Equals(entry.Game, Application.version, StringComparison.Ordinal))
		{
			entry.Why = "needs a game update";
		}
		return entry;
	}

	private static string Str(JObject o, string key, int max)
	{
		JToken obj = o[key];
		JValue val = (JValue)(object)((obj is JValue) ? obj : null);
		if (val == null || val.Value == null || (int)((JToken)val).Type == 1 || (int)((JToken)val).Type == 2)
		{
			return "";
		}
		return Text.Clip(Convert.ToString(val.Value, CultureInfo.InvariantCulture) ?? "", max);
	}

	private static int Int(JObject o, string key)
	{
		JToken obj = o[key];
		JValue val = (JValue)(object)((obj is JValue) ? obj : null);
		if (val == null)
		{
			return 0;
		}
		try
		{
			double num;
			if ((int)((JToken)val).Type == 6 || (int)((JToken)val).Type == 7)
			{
				num = Convert.ToDouble(val.Value, CultureInfo.InvariantCulture);
			}
			else
			{
				if ((int)((JToken)val).Type != 8 || !double.TryParse((string)val.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
				{
					return 0;
				}
				num = result;
			}
			if (double.IsNaN(num) || num < 0.0)
			{
				return 0;
			}
			return (num > 99999.0) ? 99999 : ((int)num);
		}
		catch
		{
			return 0;
		}
	}

	private static bool Bool(JObject o, string key)
	{
		JToken obj = o[key];
		JValue val = (JValue)(object)((obj is JValue) ? obj : null);
		if (val != null && (int)((JToken)val).Type == 9)
		{
			return (bool)val.Value;
		}
		return false;
	}

	private static List<Recent> RecentRows()
	{
		List<Recent> list = new List<Recent>();
		foreach (JoinMenu.SavedServer s in JoinMenu.LoadServers())
		{
			if (s != null && JoinMenu.ValidAddress(s.address ?? ""))
			{
				Entry live = rows.Find((Entry e) => string.Equals(e.Address, s.address, StringComparison.OrdinalIgnoreCase));
				list.Add(new Recent
				{
					Saved = s,
					Live = live
				});
			}
		}
		return list;
	}

	private static void Build()
	{
		CustomUIManager instance = CustomUIManager.Instance;
		if ((Object)(object)instance == (Object)null || !Ui.AtMenu)
		{
			return;
		}
		rebuilding = true;
		List<Recent> recent = RecentRows();
		List<Entry> list = rows.FindAll((Entry e) => !recent.Exists((Recent r) => r.Live == e));
		CustomUIBuilder val = instance.CreateWindow("humangenome_accomplice.servers", Lang.Tr("Server list"), "menu", (GameObject)null, (CustomUIManager.Style)0).SetCloseOnEscape(true).SetSize(1000f, 820f);
		CustomUIWindow handle = val.Handle;
		val.AddLabel(Lang.Tr((recent.Count + rows.Count > 0) ? "Click a server to join it." : "Servers you join show up here too."), "info");
		List<RectTransform> list2 = new List<RectTransform>();
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = recent.Count > 0;
		foreach (Entry row in rows)
		{
			if (row.City.Length > 0)
			{
				flag = true;
			}
			if (row.Mods > 0)
			{
				flag2 = true;
			}
			if (row.Mod.Length > 0)
			{
				flag3 = true;
			}
		}
		showVer = flag3;
		val.BeginScroll(530f);
		CustomUIBuilder val2 = Row(val, 8f, out headRow);
		val2.AddSpace(14f).AddLabel(Lang.Tr("Server"), "h_name");
		if (flag)
		{
			val2.AddLabel(Lang.Tr("Where"), "h_city");
		}
		if (flag3)
		{
			val2.AddLabel(Lang.Tr("Version"), "h_ver");
		}
		if (flag2)
		{
			val2.AddLabel(Lang.Tr("Mods"), "h_mods");
		}
		val2.AddLabel(Lang.Tr("Players"), "h_pl").AddLabel(Lang.Tr("Day"), "h_day");
		if (flag4)
		{
			val2.AddSpace(100f);
		}
		if (flag4)
		{
			val.AddLabel(Lang.Tr("Your recent servers"), "sec_recent");
			for (int num = 0; num < recent.Count; num++)
			{
				AddRecentRow(val, recent[num], num, list2, flag, flag2);
			}
		}
		val.AddLabel((State == "loading" || list.Count == 0) ? Lang.Tr("Public servers") : Lang.Trf("Public servers ({0})", Num(list.Count)), "sec_public");
		if (list.Count == 0)
		{
			val.AddLabel(Lang.Tr((State == "loading") ? "Looking for public servers..." : "No public servers right now."), "public_none");
		}
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			AddRow(val, list[num2], num2, list2, flag, flag2, flag4);
		}
		val.End();
		val.AddWarning("", "status");
		RectTransform rect;
		CustomUIBuilder val3 = Row(val, 8f, out rect).AddButton(Lang.Tr("Refresh"), (Action)Refresh, "refresh");
		if (State == "empty" || State == "unreachable")
		{
			val3.AddButton(Lang.Tr("Rent a server"), (Action)delegate
			{
				JoinMenu.OpenRentPage(fromList: true, SetStatus);
			}, "rent");
		}
		val3.AddFlexibleSpace().AddButton(Lang.Tr("Back"), (Action)Close, "back");
		val.OnClose((Action)delegate
		{
			OnClosed(handle);
		}).Center();
		window = val.Show();
		rebuilding = false;
		JoinMenu.HideForChild();
		foreach (RectTransform item in list2)
		{
			Dim(item);
		}
		FitHeader(flag, flag2);
		string[] array = new string[2] { "sec_recent", "sec_public" };
		foreach (string text in array)
		{
			Section(window.Get(text));
		}
		CustomUIElement obj = window.Get("public_none");
		if (obj != null)
		{
			obj.SetTextColor(Kit.Dim);
		}
		for (int num4 = 0; num4 < recent.Count; num4++)
		{
			FitCells(num4, recent: true);
		}
		for (int num5 = 0; num5 < list.Count; num5++)
		{
			FitCells(num5, recent: false);
		}
		JoinMenu.FitScroll(window, 530f, 530f);
		JoinMenu.FitButtonRow(window, rect, new string[2] { "refresh", "back" }, new string[1] { "rent" });
		JoinMenu.FitHeight(window, 300f, 860f, 30f);
		SetStatus(Status);
		Shown = recent.Count + list.Count;
	}

	private static void Section(CustomUIElement el)
	{
		if (!((Object)(object)el?.text == (Object)null))
		{
			((TMP_Text)el.text).enableWordWrapping = false;
			((TMP_Text)el.text).margin = new Vector4(2f, 6f, 0f, 0f);
			LayoutElement obj = el.root.GetComponent<LayoutElement>() ?? el.root.AddComponent<LayoutElement>();
			obj.minHeight = 34f;
			obj.preferredHeight = 34f;
		}
	}

	private static void AddRow(CustomUIBuilder b, Entry e, int i, List<RectTransform> dimRows, bool anyCity, bool anyMods, bool forgetColumn)
	{
		RectTransform rect;
		CustomUIBuilder val = Row(b, 8f, out rect);
		if (e.Password)
		{
			val.AddImage(Lock(), 14f, 18f, "lock_" + Num(i));
		}
		else
		{
			val.AddSpace(14f);
		}
		Entry entry = e;
		val.AddButton(e.Name, (Action)delegate
		{
			Pick(entry);
		}, "srv_" + Num(i));
		if (anyCity)
		{
			val.AddLabel(e.City, "city_" + Num(i));
		}
		if (showVer)
		{
			val.AddLabel(VerText(e), "ver_" + Num(i));
		}
		if (e.Why == null)
		{
			if (anyMods)
			{
				val.AddLabel((e.Mods > 0) ? Num(e.Mods) : "", "mods_" + Num(i));
			}
			val.AddLabel(PlayersText(e), "pl_" + Num(i));
			val.AddLabel(DayText(e), "day_" + Num(i));
		}
		else
		{
			val.AddLabel(Lang.Tr(e.Why), "why_" + Num(i));
			dimRows.Add(rect);
		}
		if (forgetColumn)
		{
			val.AddSpace(100f);
		}
	}

	private static void AddRecentRow(CustomUIBuilder b, Recent r, int i, List<RectTransform> dimRows, bool anyCity, bool anyMods)
	{
		RectTransform rect;
		CustomUIBuilder val = Row(b, 8f, out rect);
		Entry live = r.Live;
		if (live?.Password ?? (!string.IsNullOrEmpty(r.Saved.password)))
		{
			val.AddImage(Lock(), 14f, 18f, "rlock_" + Num(i));
		}
		else
		{
			val.AddSpace(14f);
		}
		Recent rr = r;
		val.AddButton(RecentLabel(r), (Action)delegate
		{
			PickRecent(rr);
		}, "rec_" + Num(i));
		if (live == null)
		{
			val.AddLabel(Kit.Name(r.Saved.address), "raddr_" + Num(i));
		}
		else
		{
			if (anyCity)
			{
				val.AddLabel(live.City, "rcity_" + Num(i));
			}
			if (showVer)
			{
				val.AddLabel(VerText(live), "rver_" + Num(i));
			}
			if (live.Why != null)
			{
				val.AddLabel(Lang.Tr(live.Why), "rwhy_" + Num(i));
				dimRows.Add(rect);
			}
			else
			{
				if (anyMods)
				{
					val.AddLabel((live.Mods > 0) ? Num(live.Mods) : "", "rmods_" + Num(i));
				}
				val.AddLabel(PlayersText(live), "rpl_" + Num(i));
				val.AddLabel(DayText(live), "rday_" + Num(i));
			}
		}
		val.AddButton(Lang.Tr("Forget"), (Action)delegate
		{
			Forget(rr);
		}, "fgt_" + Num(i));
	}

	private static string PlayersText(Entry e)
	{
		if (e.Max <= 0)
		{
			return Num(e.Players);
		}
		return e.Players + "/" + e.Max;
	}

	private static string VerText(Entry e)
	{
		if (e.Mod.Length <= 0)
		{
			return "";
		}
		return "v" + e.Mod;
	}

	private static string DayText(Entry e)
	{
		if (!(e.State == "loading"))
		{
			if (e.Day <= 0)
			{
				return "";
			}
			return Lang.Trf("Day {0}", Num(e.Day));
		}
		return Lang.Tr("Starting");
	}

	private static string RecentLabel(Recent r)
	{
		if (r.Live == null)
		{
			return RecentName(r);
		}
		return r.Live.Name;
	}

	private static string RecentName(Recent r)
	{
		string text = Text.Clip(Chat.Clean(r.Saved.name ?? ""), 40).Trim();
		return Kit.Name((text.Length > 0) ? text : r.Saved.address);
	}

	private static void FitHeader(bool anyCity, bool anyMods)
	{
		CustomUIWindow val = window;
		if (val == null || !val.IsAlive)
		{
			return;
		}
		CustomUIElement val2 = val.Get("h_name");
		if ((Object)(object)val2?.root != (Object)null)
		{
			LayoutElement obj = val2.root.GetComponent<LayoutElement>() ?? val2.root.AddComponent<LayoutElement>();
			obj.minWidth = 60f;
			obj.preferredWidth = 60f;
			obj.flexibleWidth = 1f;
			if ((Object)(object)val2.text != (Object)null)
			{
				((TMP_Text)val2.text).alignment = (TextAlignmentOptions)4097;
				((TMP_Text)val2.text).margin = new Vector4(28f, 0f, 0f, 0f);
			}
		}
		if (anyCity)
		{
			Column(val.Get("h_city"), 140f, (TextAlignmentOptions)4097);
		}
		Column(val.Get("h_ver"), 88f, (TextAlignmentOptions)4097);
		if (anyMods)
		{
			Column(val.Get("h_mods"), 64f, (TextAlignmentOptions)4098);
		}
		Column(val.Get("h_pl"), 80f, (TextAlignmentOptions)4100);
		Column(val.Get("h_day"), 110f, (TextAlignmentOptions)4097, 16f);
		string[] array = new string[6] { "h_name", "h_city", "h_ver", "h_mods", "h_pl", "h_day" };
		foreach (string text in array)
		{
			CustomUIElement obj2 = val.Get(text);
			if (obj2 != null)
			{
				obj2.SetTextColor(Kit.Dim);
			}
		}
	}

	private static void FitCells(int i, bool recent)
	{
		CustomUIWindow val = window;
		if (val == null || !val.IsAlive)
		{
			return;
		}
		string text = (recent ? "r" : "");
		string text2 = Num(i);
		CustomUIElement val2 = val.Get((recent ? "rec_" : "srv_") + text2);
		if ((Object)(object)val2?.root != (Object)null)
		{
			LayoutElement obj = val2.root.GetComponent<LayoutElement>() ?? val2.root.AddComponent<LayoutElement>();
			obj.minWidth = 60f;
			obj.preferredWidth = 60f;
			obj.flexibleWidth = 1f;
			TextMeshProUGUI val3 = JoinMenu.OneLine(val2);
			HorizontalOrVerticalLayoutGroup component = val2.root.GetComponent<HorizontalOrVerticalLayoutGroup>();
			if ((Object)(object)component != (Object)null)
			{
				((LayoutGroup)component).childAlignment = (TextAnchor)3;
				component.childControlWidth = true;
				component.childForceExpandWidth = true;
			}
			if ((Object)(object)val3 != (Object)null)
			{
				(((Component)val3).GetComponent<LayoutElement>() ?? ((Component)val3).gameObject.AddComponent<LayoutElement>()).flexibleWidth = 1f;
				RectTransform rectTransform = ((TMP_Text)val3).rectTransform;
				rectTransform.anchorMin = Vector2.zero;
				rectTransform.anchorMax = Vector2.one;
				rectTransform.offsetMin = Vector2.zero;
				rectTransform.offsetMax = Vector2.zero;
				ContentSizeFitter component2 = ((Component)val3).GetComponent<ContentSizeFitter>();
				if ((Object)(object)component2 != (Object)null)
				{
					((Behaviour)component2).enabled = false;
				}
				((TMP_Text)val3).alignment = (TextAlignmentOptions)4097;
				((TMP_Text)val3).margin = new Vector4(12f, 0f, 8f, 0f);
				Kit.FitFont(val3);
			}
		}
		Column(val.Get(text + "city_" + text2), 140f, (TextAlignmentOptions)4097);
		CustomUIElement val4 = val.Get(text + "ver_" + text2);
		if (val4 != null)
		{
			Column(val4, 88f, (TextAlignmentOptions)4097);
			val4.SetTextColor(Kit.Dim);
			if ((Object)(object)val4.text != (Object)null)
			{
				((TMP_Text)val4.text).fontSize = Mathf.Round(((TMP_Text)val4.text).fontSize * 0.85f);
			}
		}
		Column(val.Get(text + "mods_" + text2), 64f, (TextAlignmentOptions)4098);
		Column(val.Get(text + "pl_" + text2), 80f, (TextAlignmentOptions)4100);
		Column(val.Get(text + "day_" + text2), 110f, (TextAlignmentOptions)4097, 16f);
		Column(val.Get(text + "why_" + text2), ((val.Get("h_mods") != null) ? 72f : 0f) + 80f + 8f + 110f, (TextAlignmentOptions)4097);
		CustomUIElement val5 = (recent ? val.Get("raddr_" + text2) : null);
		if (val5 != null)
		{
			Column(val5, ((val.Get("h_city") != null) ? 148f : 0f) + ((val.Get("h_ver") != null) ? 96f : 0f) + ((val.Get("h_mods") != null) ? 72f : 0f) + 80f + 8f + 110f, (TextAlignmentOptions)4097);
			val5.SetTextColor(Kit.Dim);
		}
		CustomUIElement obj2 = val.Get((recent ? "rlock_" : "lock_") + text2);
		if (obj2 != null)
		{
			obj2.SetImageColor(Kit.Text);
		}
		CustomUIElement val6 = val.Get("fgt_" + text2);
		if (recent && (Object)(object)val6?.root != (Object)null)
		{
			val6.SetFixedWidth(100f);
			JoinMenu.OneLine(val6);
		}
	}

	private static void Column(CustomUIElement el, float width, TextAlignmentOptions align, float left = 0f)
	{
		if ((Object)(object)el?.root == (Object)null)
		{
			return;
		}
		el.SetFixedWidth(width);
		TextMeshProUGUI text = el.text;
		if (!((Object)(object)text == (Object)null))
		{
			((TMP_Text)text).enableWordWrapping = false;
			((TMP_Text)text).overflowMode = (TextOverflowModes)1;
			((TMP_Text)text).alignment = align;
			if (left > 0f)
			{
				((TMP_Text)text).margin = new Vector4(left, 0f, 0f, 0f);
			}
		}
	}

	private static void Dim(RectTransform row)
	{
		if (!((Object)(object)row == (Object)null))
		{
			(((Component)row).GetComponent<CanvasGroup>() ?? ((Component)row).gameObject.AddComponent<CanvasGroup>()).alpha = 0.5f;
		}
	}

	private static void Pick(Entry e)
	{
		if (e != null)
		{
			if (e.Why != null)
			{
				SetStatus(JoinMenu.Refusal((e.Why == "needs a mod update") ? "protocol" : "game", null));
				Log.Info("ui: server list: " + e.Address + " picked but it " + e.Why);
			}
			else
			{
				Log.Info("ui: server list: picked " + e.Address + (e.Password ? " (password)" : ""));
				Close();
				JoinMenu.JoinListed(e.Address, e.Name, e.Password);
			}
		}
	}

	private static void PickRecent(Recent r)
	{
		if (r?.Saved != null)
		{
			if (r.Live != null)
			{
				Pick(r.Live);
				return;
			}
			Log.Info("ui: server list: picked recent " + r.Saved.address + " (not public)");
			Close();
			JoinMenu.JoinListed(r.Saved.address, RecentName(r), null);
		}
	}

	private static void Forget(Recent r)
	{
		if (r?.Saved != null)
		{
			JoinMenu.Forget(r.Saved);
			Log.Info("ui: server list: forgot " + r.Saved.address);
			Build();
		}
	}

	private static void SetStatus(string s)
	{
		Status = Kit.Safe(s ?? "");
		if (window != null && window.IsAlive)
		{
			CustomUIElement obj = window.Get("status");
			if (obj != null)
			{
				obj.SetText(Status);
			}
		}
	}

	private static void OnClosed(CustomUIWindow w)
	{
		if (!rebuilding && w == window)
		{
			window = null;
			Abort();
			JoinMenu.ShowAfterChild();
		}
	}

	private static string Num(int i)
	{
		return i.ToString(CultureInfo.InvariantCulture);
	}

	private static CustomUIBuilder Row(CustomUIBuilder b, float spacing, out RectTransform rect)
	{
		HorizontalLayoutGroup val = Kit.Row((Transform)(object)b.CurrentContainer, "row", spacing);
		((LayoutGroup)val).childAlignment = (TextAnchor)3;
		rect = (RectTransform)((Component)val).transform;
		return new CustomUIBuilder(CustomUIManager.Instance, b.Handle, rect);
	}

	private static Sprite Lock()
	{
		if ((Object)(object)lockSprite != (Object)null)
		{
			return lockSprite;
		}
		Texture2D val = new Texture2D(14, 18, (TextureFormat)4, false)
		{
			filterMode = (FilterMode)1,
			wrapMode = (TextureWrapMode)1
		};
		Color32[] array = (Color32[])(object)new Color32[252];
		for (int i = 0; i < 18; i++)
		{
			for (int j = 0; j < 14; j++)
			{
				int num = 0;
				for (int k = 0; k < 4; k++)
				{
					for (int l = 0; l < 4; l++)
					{
						if (InLock((float)j + ((float)l + 0.5f) / 4f, (float)i + ((float)k + 0.5f) / 4f))
						{
							num++;
						}
					}
				}
				byte b = (byte)(255 * num / 16);
				array[(17 - i) * 14 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, b);
			}
		}
		val.SetPixels32(array);
		val.Apply();
		lockSprite = Sprite.Create(val, new Rect(0f, 0f, 14f, 18f), new Vector2(0.5f, 0.5f), 100f);
		return lockSprite;
	}

	private static bool InLock(float x, float y)
	{
		if (x >= 0.6f && x <= 13.4f && y >= 7.6f && y <= 17.4f)
		{
			float num = x - 7f;
			float num2 = y - 11.6f;
			return !(num * num + num2 * num2 <= 2.89f) && (!(Mathf.Abs(num) <= 0.8f) || !(y >= 11.6f) || !(y <= 14.8f));
		}
		float num3 = x - 7f;
		float num4 = y - 6.5f;
		float num5 = Mathf.Sqrt(num3 * num3 + num4 * num4);
		if (y <= 6.5f)
		{
			if (num5 <= 5.2f)
			{
				return num5 >= 2.9f;
			}
			return false;
		}
		float num6 = 2.2999997f;
		if (y <= 8f)
		{
			if (!(x >= 1.8000002f) || !(x <= 1.8000002f + num6))
			{
				if (x <= 12.2f)
				{
					return x >= 12.2f - num6;
				}
				return false;
			}
			return true;
		}
		return false;
	}
}
