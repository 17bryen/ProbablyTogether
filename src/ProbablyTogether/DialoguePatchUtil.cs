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
}
