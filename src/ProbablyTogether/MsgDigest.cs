using System.Collections.Generic;
using System.Linq;

namespace ProbablyTogether;

public sealed class MsgDigest
{
	public int day;

	public int cash;

	public int favor;

	public int items;

	public string hash;

	public string state;

	public string client;

	public int queue;

	public Dictionary<string, string> parts;

	public string Describe()
	{
		return "day=" + day + " cash=" + cash + " favor=" + favor + " items=" + items + " hash=" + hash + " state=" + state + " client=" + (string.IsNullOrEmpty(client) ? "-" : client) + " queue=" + queue + ((parts == null || parts.Count == 0) ? "" : (" parts=" + string.Join(",", parts.Select((KeyValuePair<string, string> kv) => kv.Key + ":" + kv.Value))));
	}
}
