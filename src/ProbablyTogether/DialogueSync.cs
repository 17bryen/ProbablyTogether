using System;

namespace ProbablyTogether;

public static class DialogueSync
{
	public static bool UiForOthers => DialogueSystem.UiForOthers;

	public static int RefusedCount => DialogueSystem.RefusedCount;

	public static string LastRefused => DialogueSystem.LastRefused;

	public static int MismatchCount => DialogueSystem.Mismatches;

	public static event Action<string, string, string> Refused
	{
		add
		{
			DialogueSystem.refused = (Action<string, string, string>)Delegate.Combine(DialogueSystem.refused, value);
		}
		remove
		{
			DialogueSystem.refused = (Action<string, string, string>)Delegate.Remove(DialogueSystem.refused, value);
		}
	}

	public static bool RequestShown(string ch, bool skip = false)
	{
		return DialogueSystem.SendShown(DialogueSystem.Chan(ch), skip);
	}

	public static bool RequestAdvance(string ch)
	{
		return DialogueSystem.SendLine(DialogueSystem.Chan(ch), "dialogue.advance", skip: false, -1);
	}

	public static bool RequestChoose(string ch, int index)
	{
		return DialogueSystem.SendLine(DialogueSystem.Chan(ch), "dialogue.choose", skip: false, index);
	}

	public static bool RequestCall(long number, bool quick = false)
	{
		return DialogueSystem.SendCall(number, quick);
	}

	public static bool RequestHangup()
	{
		return DialogueSystem.SendHangup();
	}

	public static bool IsLocalPacer(string ch)
	{
		return DialogueSystem.IsPacer(DialogueSystem.Chan(ch));
	}

	public static void RegisterRoot(string name, Func<Dialogue> factory)
	{
		DialogueRefs.Register(name, factory);
	}

	public static Dialogue BuildRoot(string name)
	{
		return DialogueRefs.BuildNamed(name);
	}

	public static string Digest()
	{
		return DialogueSystem.Digest();
	}

	public static string At(string ch)
	{
		return DialogueSystem.Chan(ch)?.At() ?? "";
	}

	public static string AtHistory(string ch)
	{
		return DialogueSystem.Chan(ch)?.HistoryText() ?? "";
	}

	public static bool AtMatches(string mine, string serverHistory, out bool pending, out string detail)
	{
		return DialogueSystem.AtMatches(mine, serverHistory, out pending, out detail);
	}

	public static string CurrentFp(string ch)
	{
		return DialogueRefs.Fp(DialogueSystem.Chan(ch)?.Current);
	}

	public static int Seq(string ch)
	{
		return DialogueSystem.Chan(ch)?.SeqNow ?? (-1);
	}
}
