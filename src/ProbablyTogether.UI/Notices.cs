using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

public static class Notices
{
	private const float RepeatWindow = 4f;

	private static string lastText = "";

	private static float lastAt = -100f;

	private static int dialogueSeen;

	public static string Last { get; private set; } = "";

	public static string LastSource { get; private set; } = "";

	public static int Shown { get; private set; }

	public static int Repeats { get; private set; }

	public static string LastSecurity { get; private set; } = "";

	public static int DeniedShown { get; private set; }

	public static void Show(string text, string source = "")
	{
		if (Ui.Headless || !Net.HasLocalPlayer || string.IsNullOrWhiteSpace(text) || DayLog.Replaying)
		{
			return;
		}
		text = Kit.Safe(text.Trim());
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (text == lastText && realtimeSinceStartup - lastAt < 4f)
		{
			Repeats++;
			lastAt = realtimeSinceStartup;
			return;
		}
		lastText = text;
		lastAt = realtimeSinceStartup;
		Last = text;
		LastSource = source ?? "";
		Shown++;
		Log.Info("ui: notice" + ((LastSource.Length > 0) ? (" (" + LastSource + ")") : "") + ": " + text);
		try
		{
			if ((Object)(object)StoreUIManager.Instance != (Object)null)
			{
				StoreUIManager.Instance.Notify(Lang.Tr(text), "default");
			}
		}
		catch (Exception e)
		{
			UiErrors.Report("notice", e);
		}
	}

	internal static void ClientInit()
	{
		dialogueSeen = SafeDialogueCount();
		Net.Denied += delegate(string kind, string reason)
		{
			int shown = Shown;
			Show(string.IsNullOrWhiteSpace(reason) ? "The server refused that." : reason, "denied " + kind);
			if (Shown > shown)
			{
				DeniedShown++;
			}
		};
		try
		{
			Security.Refused += delegate(string what, string reason)
			{
				LastSecurity = what + ": " + reason;
			};
		}
		catch (Exception ex)
		{
			Log.Warn("ui: security refusals not hooked: " + ex.Message);
		}
	}

	internal static void Tick()
	{
		int num = SafeDialogueCount();
		if (num == dialogueSeen)
		{
			return;
		}
		bool flag = num > dialogueSeen;
		dialogueSeen = num;
		if (flag)
		{
			string text = SafeDialogueLast();
			int num2 = text.IndexOf(": ", StringComparison.Ordinal);
			string text2 = ((num2 >= 0) ? text.Substring(num2 + 2) : text);
			string text3 = ((num2 >= 0) ? text.Substring(0, num2) : "");
			if (Quiet(text3, text2))
			{
				Log.Info("ui: dialogue refusal not shown (stale or automatic): " + text);
			}
			else
			{
				Show(text2, "dialogue " + text3);
			}
		}
	}

	private static bool Quiet(string what, string why)
	{
		if (!what.EndsWith("shown", StringComparison.Ordinal) && !what.EndsWith("hangup", StringComparison.Ordinal) && !(why == "that line is over") && !(why == "nobody is talking"))
		{
			return why == "no conversation here";
		}
		return true;
	}

	private static int SafeDialogueCount()
	{
		try
		{
			return DialogueSync.RefusedCount;
		}
		catch
		{
			return 0;
		}
	}

	private static string SafeDialogueLast()
	{
		try
		{
			return DialogueSync.LastRefused ?? "";
		}
		catch
		{
			return "";
		}
	}

	internal static void ClientReset()
	{
		lastText = "";
		lastAt = -100f;
		dialogueSeen = SafeDialogueCount();
	}
}
