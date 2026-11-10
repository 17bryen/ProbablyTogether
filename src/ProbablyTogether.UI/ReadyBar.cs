using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

public static class ReadyBar
{
	public sealed class View
	{
		public bool Active;

		public IList<int> Ready = new List<int>();

		public int Needed;

		public float SecondsLeft = -1f;

		public bool LocalReady;

		public bool CanPress = true;

		public string Note;
	}

	private static Func<View> read;

	private static Action<bool> press;

	private static View current;

	private static float readAt;

	private static RectTransform plate;

	private static TextMeshProUGUI title;

	private static TextMeshProUGUI names;

	private static TextMeshProUGUI note;

	private static TextMeshProUGUI buttonLabel;

	private static Button button;

	public static bool Bound => read != null;

	public static string BoundBy => read?.Method.DeclaringType?.FullName ?? "";

	public static bool Visible
	{
		get
		{
			if (Bound && current != null && current.Active)
			{
				return Ui.ShopOnline;
			}
			return false;
		}
	}

	public static View Current => current;

	public static string Text
	{
		get
		{
			if (!((Object)(object)title != (Object)null))
			{
				return "";
			}
			return ((TMP_Text)title).text + (((Object)(object)names != (Object)null) ? (" | " + ((TMP_Text)names).text) : "");
		}
	}

	public static void Bind(Func<View> readView, Action<bool> onPress)
	{
		read = readView;
		press = onPress;
		Refresh();
		Log.Info("ui: ready indicator bound to " + (readView?.Method.DeclaringType?.Name ?? "nothing"));
	}

	internal static KeyValuePair<Func<View>, Action<bool>> Saved()
	{
		return new KeyValuePair<Func<View>, Action<bool>>(read, press);
	}

	internal static void Restore(KeyValuePair<Func<View>, Action<bool>> b)
	{
		read = b.Key;
		press = b.Value;
		Refresh();
		Log.Info("ui: ready indicator binding restored (" + ((BoundBy.Length > 0) ? BoundBy : "nothing") + ")");
	}

	public static bool IsReady(int player)
	{
		if (current != null && current.Active && current.Ready != null)
		{
			return current.Ready.Contains(player);
		}
		return false;
	}

	public static void Press(bool ready)
	{
		if (press == null)
		{
			Log.Warn("ui: ready pressed but day-night has not bound the indicator");
			return;
		}
		try
		{
			press(ready);
		}
		catch (Exception ex)
		{
			Log.Error("ui: ready press: " + ex);
		}
		readAt = -10f;
	}

	public static void Refresh()
	{
		readAt = -10f;
	}

	internal static void OnOverlayBuilt()
	{
		plate = Kit.Plate((Transform)(object)Overlay.ReadyArea, "ready", out var content, 0.92f, 10, 3f);
		Kit.Pin(plate, new Vector2(0.5f, 1f), Vector2.zero);
		title = Kit.Label((Transform)(object)content, "title", 18f, Kit.Text, 0f, (TextAlignmentOptions)4097);
		names = Kit.Label((Transform)(object)content, "names", 15f, Kit.Dim, 420f, (TextAlignmentOptions)4097);
		note = Kit.Label((Transform)(object)content, "note", 14f, Kit.Warn, 420f, (TextAlignmentOptions)4097);
		button = Kit.TextButton((Transform)(object)content, "button", Lang.Tr("I'm ready"), 16f, delegate
		{
			Press(!(current?.LocalReady ?? false));
		}, out buttonLabel);
		((Object)((Component)button).gameObject).name = "humangenome_accomplice_ready_button";
	}

	internal static void Tick()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (read == null || !Ui.ShopOnline)
		{
			current = null;
		}
		else
		{
			if (realtimeSinceStartup - readAt < 0.25f)
			{
				return;
			}
			readAt = realtimeSinceStartup;
			try
			{
				current = read();
			}
			catch (Exception e)
			{
				UiErrors.Report("ready view", e);
				current = null;
			}
			if (current == null || !current.Active || (Object)(object)plate == (Object)null)
			{
				return;
			}
			int num = ((current.Needed > 0) ? current.Needed : Net.Players.Count);
			int num2 = current.Ready?.Count ?? 0;
			string text = ((current.SecondsLeft >= 0f) ? ("  " + Countdown(current.SecondsLeft)) : "");
			Kit.SetText(title, Lang.Trf("Ready for the night: {0}/{1}", num2.ToString(CultureInfo.InvariantCulture), num.ToString(CultureInfo.InvariantCulture)) + text);
			List<string> list = new List<string>();
			List<int> list2 = new List<int>(Net.Players.Keys);
			list2.Sort();
			foreach (int item in list2)
			{
				bool flag = current.Ready != null && current.Ready.Contains(item);
				list.Add("<color=#" + Kit.Hex(Kit.PlayerColor(item)) + ">" + Kit.Name(Net.PlayerName(item)) + "</color> " + (flag ? ("<color=#" + Kit.Hex(Kit.Good) + ">" + Lang.Tr("ready") + "</color>") : ("<color=#" + Kit.Hex(Kit.Dim) + ">" + Lang.Tr("not yet") + "</color>")));
			}
			Kit.SetText(names, string.Join("   ", list));
			Kit.SetText(note, Kit.Safe(Lang.Tr(current.Note ?? "")));
			((Component)note).gameObject.SetActive(!string.IsNullOrEmpty(current.Note));
			Kit.SetText(buttonLabel, Lang.Tr(current.LocalReady ? "Not ready" : "I'm ready"));
			((Selectable)button).interactable = current.CanPress || current.LocalReady;
		}
	}

	private static string Countdown(float s)
	{
		int num = Mathf.Max(0, Mathf.CeilToInt(s));
		return Lang.Trf("{0} left", (num / 60).ToString(CultureInfo.InvariantCulture) + ":" + (num % 60).ToString("00", CultureInfo.InvariantCulture));
	}
}
