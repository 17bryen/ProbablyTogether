using System;

namespace ProbablyTogether;

internal static class DialoguePatchUtil
{
	internal static bool ClientBlocks
	{
		get
		{
			if (Net.IsClient)
			{
				return !Net.Applying;
			}
			return false;
		}
	}

	internal static Exception Close(Exception e, DialogueSystem.Step s)
	{
		s?.Dispose();
		return e;
	}

	// The phone stands in the shop: while this copy's player is on a trip, every phone hook runs in the home view.
	private static readonly System.Collections.Generic.Stack<IDisposable> homeScopes = new System.Collections.Generic.Stack<IDisposable>();

	internal static int PhoneAtHome;

	internal static void EnterHome()
	{
		IDisposable disposable = Systems.HostTrip.AtHome();
		if (disposable != null)
		{
			PhoneAtHome++;
		}
		homeScopes.Push(disposable);
	}

	internal static void LeaveHome()
	{
		if (homeScopes.Count > 0)
		{
			homeScopes.Pop()?.Dispose();
		}
	}

	internal static Exception ClosePhone(Exception e, DialogueSystem.Step s)
	{
		try
		{
			s?.Dispose();
		}
		finally
		{
			LeaveHome();
		}
		return e;
	}
}
