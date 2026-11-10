namespace ProbablyTogether.Systems;

internal static class CustomersGate
{
	internal static bool Allow()
	{
		if (Net.IsRemote)
		{
			return Net.Applying;
		}
		return true;
	}
}
