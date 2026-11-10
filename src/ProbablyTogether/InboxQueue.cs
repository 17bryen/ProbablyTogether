using System.Collections.Generic;

namespace ProbablyTogether;

internal sealed class InboxQueue
{
	private readonly object gate = new object();

	private readonly Queue<InboxItem> loose = new Queue<InboxItem>();

	private readonly Queue<Conn> turns = new Queue<Conn>();

	private int count;

	public int Count
	{
		get
		{
			lock (gate)
			{
				return count;
			}
		}
	}

	public bool IsEmpty => Count == 0;

	public void Enqueue(InboxItem x)
	{
		lock (gate)
		{
			count++;
			Conn conn = x.Conn;
			if (conn == null)
			{
				loose.Enqueue(x);
				return;
			}
			conn.InboxPending.Enqueue(x);
			if (x.Event == InboxEvent.Frame)
			{
				conn.PendingFrames++;
				conn.PendingBytes += x.Cost;
			}
			if (!conn.InTurns)
			{
				conn.InTurns = true;
				turns.Enqueue(conn);
			}
		}
	}

	public bool TryDequeue(out InboxItem x)
	{
		lock (gate)
		{
			if (loose.Count > 0)
			{
				count--;
				x = loose.Dequeue();
				return true;
			}
			while (turns.Count > 0)
			{
				Conn conn = turns.Dequeue();
				Queue<InboxItem> inboxPending = conn.InboxPending;
				while (inboxPending.Count > 0)
				{
					InboxItem inboxItem = inboxPending.Dequeue();
					count--;
					if (inboxItem.Event == InboxEvent.Frame)
					{
						conn.PendingFrames--;
						conn.PendingBytes -= inboxItem.Cost;
						if (conn.Abusive)
						{
							Conn.Stats.DroppedAfterAbuse++;
							continue;
						}
					}
					if (inboxPending.Count > 0)
					{
						turns.Enqueue(conn);
					}
					else
					{
						conn.InTurns = false;
					}
					x = inboxItem;
					return true;
				}
				conn.InTurns = false;
			}
			x = default(InboxItem);
			return false;
		}
	}
}
