namespace ProbablyTogether;

internal static class Inbox
{
	internal static readonly InboxQueue Queue = new InboxQueue();

	internal static void Post(InboxItem x)
	{
		Queue.Enqueue(x);
	}
}
