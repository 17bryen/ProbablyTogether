using System.Collections.Generic;

namespace ProbablyTogether.Bots.Scripts;

public static class SecurityLabItems
{
	private static HashSet<string> realIds;

	public static bool Real(string id)
	{
		if (realIds == null || realIds.Count == 0)
		{
			realIds = new HashSet<string>(DirectoryMaster.GetIdentifierList<GameItem>((string)null));
		}
		if (id != null)
		{
			return realIds.Contains(id);
		}
		return false;
	}
}
