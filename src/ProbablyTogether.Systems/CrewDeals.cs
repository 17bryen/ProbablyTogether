using System;
using System.Collections.Generic;

namespace ProbablyTogether.Systems;

internal sealed class CrewDeals
{
	internal sealed class Deal
	{
		public int Player;

		public string Name;

		public string Who;

		public int Gain;

		public int Items;

		public long Stamp;
	}

	private readonly Dictionary<string, Deal> deals = new Dictionary<string, Deal>(StringComparer.Ordinal);

	private long stamp;

	public Deal Best { get; private set; }

	public int Count => deals.Count;

	public void Trade(string key, int player, string name, string who, int gain)
	{
		if (!deals.TryGetValue(key, out var value))
		{
			Dictionary<string, Deal> dictionary = deals;
			Deal obj = new Deal
			{
				Player = player,
				Name = name,
				Who = who
			};
			value = obj;
			dictionary[key] = obj;
		}
		int gain2 = value.Gain;
		value.Gain = Crew.Cap((long)value.Gain + (long)gain);
		value.Items++;
		if (value.Gain != gain2 || value.Stamp == 0L)
		{
			value.Stamp = ++stamp;
		}
		Deal deal = null;
		foreach (Deal value2 in deals.Values)
		{
			if (value2.Gain > 0 && (deal == null || value2.Gain > deal.Gain || (value2.Gain == deal.Gain && value2.Stamp < deal.Stamp)))
			{
				deal = value2;
			}
		}
		Best = deal;
	}

	public void Clear()
	{
		deals.Clear();
		Best = null;
		stamp = 0L;
	}
}
