using System;
using System.Collections.Generic;
using System.Globalization;
using ProbablyTogether.Systems;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

public static class NewRun
{
	public const string ButtonName = "humangenome_accomplice_newrun_button";

	private static RectTransform plate;

	private static TextMeshProUGUI title;

	private static TextMeshProUGUI names;

	private static TextMeshProUGUI note;

	private static TextMeshProUGUI buttonLabel;

	private static Button button;

	private static float readAt;

	public static bool Visible { get; private set; }

	public static string Text
	{
		get
		{
			if (!((Object)(object)title == (Object)null))
			{
				return ((TMP_Text)title).text + " | " + Kit.Plain(((TMP_Text)names).text) + " | " + ((TMP_Text)note).text + " | " + ((TMP_Text)buttonLabel).text;
			}
			return "";
		}
	}

	public static bool LocalAsked { get; private set; }

	public static string LastRefusal { get; private set; } = "";

	public static bool Starting { get; private set; }

	public static bool VotesKnown => true;

	public static bool CanTakeBack => true;

	public static bool LocalVoted => DayNightRun.LocalVoted;

	public static List<int> Votes()
	{
		return new List<int>(DayNightRun.NewRunVotes);
	}

	internal static void ClientInit()
	{
		DayNightRun.RunEnded += delegate(string ending, bool final)
		{
			if (final)
			{
				LocalAsked = false;
				LastRefusal = "";
				Starting = false;
			}
		};
		DayNightRun.NewRunStarting += delegate
		{
			Starting = true;
		};
		DayNight.Refused += delegate(string what, string why)
		{
			if (!(what != "newRun"))
			{
				LastRefusal = Kit.Safe(why);
				LocalAsked = false;
				Log.Info("ui: new run refused: " + LastRefusal);
			}
		};
		Snapshot.Loaded += delegate
		{
			if (!DayNightRun.RunOver)
			{
				Reset();
			}
		};
	}

	internal static void Reset()
	{
		LocalAsked = false;
		LastRefusal = "";
		Starting = false;
	}

	public static void Press()
	{
		if (!Net.IsClient || !DayNightRun.RunOver)
		{
			return;
		}
		if (LocalVoted)
		{
			LocalAsked = false;
			Log.Info("ui: new-run vote taken back");
			try
			{
				DayNightRun.CancelNewRunVote();
			}
			catch (Exception ex)
			{
				Log.Error("ui: CancelNewRunVote: " + ex);
			}
			readAt = -10f;
			return;
		}
		LocalAsked = true;
		LastRefusal = "";
		Log.Info("ui: Start a new run pressed");
		try
		{
			DayNightRun.RequestNewRun();
		}
		catch (Exception ex2)
		{
			Log.Error("ui: RequestNewRun: " + ex2);
			LocalAsked = false;
		}
		readAt = -10f;
	}

	internal static void OnOverlayBuilt()
	{
		plate = Kit.Plate((Transform)(object)Overlay.NewRunArea, "newrun", out var content, 0.95f, 12, 4f);
		Kit.Pin(plate, new Vector2(1f, 0f), Vector2.zero);
		title = Kit.Label((Transform)(object)content, "title", 19f, Kit.Text, 460f, (TextAlignmentOptions)4097);
		names = Kit.Label((Transform)(object)content, "names", 15f, Kit.Dim, 460f, (TextAlignmentOptions)4097);
		note = Kit.Label((Transform)(object)content, "note", 14f, Kit.Warn, 460f, (TextAlignmentOptions)4097);
		button = Kit.TextButton((Transform)(object)content, "button", Lang.Tr("Start a new run"), 18f, Press, out buttonLabel);
		((Object)((Component)button).gameObject).name = "humangenome_accomplice_newrun_button";
	}

	private static bool ShouldShow()
	{
		if (Ui.ShopOnline && SafeRunOver())
		{
			return !Starting;
		}
		return false;
	}

	private static bool SafeRunOver()
	{
		return DayNightRun.RunOver;
	}

	internal static void Tick()
	{
		bool flag = (Object)(object)plate != (Object)null && ShouldShow();
		if (flag != Visible)
		{
			Visible = flag;
			if (flag)
			{
				readAt = -10f;
				Log.Info("ui: new-run plate up (ending " + SafeEnding() + ")");
			}
		}
		if (flag)
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (!(realtimeSinceStartup - readAt < 0.25f))
			{
				readAt = realtimeSinceStartup;
				Draw();
			}
		}
	}

	private static string SafeEnding()
	{
		return DayNightRun.Ending;
	}

	private static void Draw()
	{
		List<int> list = new List<int>(Net.Players.Keys);
		list.Sort();
		List<int> list2 = Votes();
		bool localVoted = LocalVoted;
		int num = DayNightRun.NewRunNeeded;
		if (num <= 0)
		{
			num = list.Count;
		}
		int newRunSecondsLeft = DayNightRun.NewRunSecondsLeft;
		Kit.SetText(title, Lang.Trf("Start a new run: {0}/{1} voted", list2.Count.ToString(CultureInfo.InvariantCulture), num.ToString(CultureInfo.InvariantCulture)) + ((newRunSecondsLeft >= 0) ? ("  " + Countdown(newRunSecondsLeft)) : ""));
		List<string> list3 = new List<string>();
		foreach (int item in list)
		{
			bool flag = list2.Contains(item);
			list3.Add("<color=#" + Kit.Hex(Kit.PlayerColor(item)) + ">" + Kit.Name(Net.PlayerName(item)) + "</color> " + (flag ? ("<color=#" + Kit.Hex(Kit.Good) + ">" + Lang.Tr("voted") + "</color>") : ("<color=#" + Kit.Hex(Kit.Dim) + ">" + Lang.Tr("not yet") + "</color>")));
		}
		Kit.SetText(names, string.Join("   ", list3));
		string text = ((LastRefusal.Length > 0) ? LastRefusal : ((localVoted && list2.Count >= num) ? "Starting the new run..." : (localVoted ? "Waiting for the others..." : "The run has ended. A new run starts when everyone here presses Start a new run, or when the server owner starts one.")));
		Kit.SetText(note, Lang.Tr(text));
		((Component)note).gameObject.SetActive(text.Length > 0);
		Kit.SetText(buttonLabel, Lang.Tr(localVoted ? "Take my vote back" : "Start a new run"));
		((Selectable)button).interactable = true;
	}

	private static string Countdown(int s)
	{
		int num = Mathf.Max(0, s);
		return Lang.Trf("{0} left", (num / 60).ToString(CultureInfo.InvariantCulture) + ":" + (num % 60).ToString("00", CultureInfo.InvariantCulture));
	}
}
