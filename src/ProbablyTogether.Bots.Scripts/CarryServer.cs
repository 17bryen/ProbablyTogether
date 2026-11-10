using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CarryServer : BotScript
{
	private const int Stand = 9999;

	public override string Name => "carry-server";

	public override BotRole Role => BotRole.Server;

	public override float TimeoutSeconds => 600f;

	private static MsgCursor Run(long d, int n, int g, int c)
	{
		MsgCursor msgCursor = new MsgCursor
		{
			p = 9999,
			x = 0.5f,
			y = 0.5f,
			v = "store"
		};
		Carry.ServerCursor(9999, new MsgCursor
		{
			d = d,
			n = n,
			g = g,
			c = c
		}, msgCursor);
		return msgCursor;
	}

	protected override void Build()
	{
		WaitFor("role", 60f, () => Net.IsServer, () => "role " + Net.Role);
		WaitFor("shop", 300f, () => BotProbes.InShop && Net.IsLive && Items.Ready, () => "scene " + BotProbes.Scene);
		Do("rules", delegate
		{
			GameItem val = (from i in Items.All()
				where Ids.Of(i) > 0
				select i).OrderBy(Ids.Of).FirstOrDefault();
			if (val == null)
			{
				throw new Exception("no item with an id in the shop");
			}
			long num = Ids.Of(val);
			int val2 = Math.Max(1, val.unitCount);
			List<string> list = new List<string>();
			int serverInvalid = Carry.ServerInvalid;
			int serverCleared = Carry.ServerCleared;
			int serverHidden = Carry.ServerHidden;
			MsgCursor msgCursor = Run(987654321L, 3, 0, 1);
			if (msgCursor.d != 0L || Carry.ServerInvalid != serverInvalid + 1)
			{
				list.Add("unknown item relayed as " + msgCursor.d + " (invalid " + (Carry.ServerInvalid - serverInvalid) + ")");
			}
			msgCursor = Run(num, 500, 1000, 7);
			if (msgCursor.d != num || msgCursor.n != Math.Min(500, val2) || msgCursor.g != 20 || msgCursor.c != 7)
			{
				list.Add("clamp: d " + msgCursor.d + " n " + msgCursor.n + " g " + msgCursor.g + " c " + msgCursor.c);
			}
			Carry.ServerRefused(9999, "items.move", new ItemsReq
			{
				i = num + 7777
			}, "gone");
			if (Carry.ServerCleared != serverCleared)
			{
				list.Add("a refusal about another item cleared the carry");
			}
			Carry.ServerRefused(9999, "items.move", new ItemsReq
			{
				i = num
			}, "moved");
			if (Carry.ServerCleared != serverCleared + 1)
			{
				list.Add("the refusal about the carried item cleared nothing");
			}
			msgCursor = Run(num, 1, 0, 7);
			if (msgCursor.d != 0L || Carry.ServerHidden != serverHidden + 1)
			{
				list.Add("the refused drag (same serial) relayed as " + msgCursor.d);
			}
			msgCursor = Run(num, 1, 0, 8);
			if (msgCursor.d != num)
			{
				list.Add("a new drag (serial 8) after the refusal relayed as " + msgCursor.d);
			}
			Carry.ServerRefused(9999, "items.groupMove", new ItemsReq
			{
				ids = new long[2]
				{
					num + 1,
					num
				}
			}, "noroom");
			if (Carry.ServerCleared != serverCleared + 2)
			{
				list.Add("a refused group move with the carried item cleared nothing");
			}
			msgCursor = Run(0L, 0, 0, 0);
			msgCursor = Run(num, 1, 0, 8);
			if (msgCursor.d != num)
			{
				list.Add("after 'not carrying' the same drag stayed hidden (" + msgCursor.d + ")");
			}
			Carry.ServerForget(9999);
			if (list.Count > 0)
			{
				throw new Exception(string.Join("; ", list));
			}
			return "item#" + num + " (" + val2 + " unit(s)): unknown id -> 0, n 500 -> " + Math.Min(500, val2) + ", g 1000 -> " + 20 + ", refusal about another item ignored, refused drag hidden until a new serial or none, group refusal clears";
		});
		Do("miss-per-drag", delegate
		{
			GameItem val = (from i in Items.All()
				where Ids.Of(i) > 0
				select i).OrderBy(Ids.Of).FirstOrDefault();
			if (val == null)
			{
				throw new Exception("no item with an id in the shop");
			}
			long num = Ids.Of(val);
			int uniqueId = val.uniqueId;
			List<string> list = new List<string>();
			int serverInvalid = Carry.ServerInvalid;
			val.uniqueId = -424242;
			MsgCursor msgCursor;
			try
			{
				Ids.Rebuild();
				msgCursor = Run(num, 1, 0, 21);
				if (msgCursor.d != 0L || Carry.ServerInvalid != serverInvalid + 1)
				{
					list.Add("a missing id relayed as " + msgCursor.d + " (misses " + (Carry.ServerInvalid - serverInvalid) + ")");
				}
				msgCursor = Run(num, 1, 0, 21);
				if (msgCursor.d != 0L || Carry.ServerInvalid != serverInvalid + 1)
				{
					list.Add("the same drag was looked up again (misses " + (Carry.ServerInvalid - serverInvalid) + ")");
				}
			}
			finally
			{
				val.uniqueId = uniqueId;
				Ids.Rebuild();
			}
			if (Ids.Find(num) != val)
			{
				list.Add("item#" + num + " not found again after the restore");
			}
			msgCursor = Run(num, 1, 0, 21);
			if (msgCursor.d != 0L || Carry.ServerInvalid != serverInvalid + 1)
			{
				list.Add("the drag that missed was looked up again (relayed " + msgCursor.d + ")");
			}
			msgCursor = Run(num, 1, 0, 22);
			if (msgCursor.d != num || msgCursor.c != 22)
			{
				list.Add("the next drag of the same id (serial 22) relayed as " + msgCursor.d + ": still hidden by the old miss");
			}
			Carry.ServerForget(9999);
			msgCursor = Run(987654321L, 1, 0, 23);
			int serverInvalid2 = Carry.ServerInvalid;
			string text;
			if (Net.Players.Keys.Any((int k) => k != 9999))
			{
				text = "new-run forget not checked here (" + Net.Players.Count + " player(s) already joined: clearing would touch their carries)";
			}
			else
			{
				Carry.ServerNewRun();
				msgCursor = Run(987654321L, 1, 0, 23);
				if (Carry.ServerInvalid != serverInvalid2 + 1)
				{
					list.Add("a new run did not forget the remembered miss (misses " + (Carry.ServerInvalid - serverInvalid2) + ")");
				}
				text = "a new run forgets the remembered miss";
			}
			Carry.ServerForget(9999);
			if (list.Count > 0)
			{
				throw new Exception(string.Join("; ", list));
			}
			return "item#" + num + ": missing -> 0 and one lookup for that drag (serial 21, also after the item came back); the next drag of the same id (serial 22) -> shown; " + text;
		});
		WaitFor("clients-done", 540f, () => Has("a-done") && Has("b-done"), () => "marks " + string.Join(",", CarryProbes.Marks));
		Check("counters", () => Carry.ServerCleared >= 3 && Carry.ServerInvalid >= 1 && Carry.ServerHidden >= 1, () => "cleared " + Carry.ServerCleared + " (2 rules + the clients' refusal), hidden frames " + Carry.ServerHidden + ", unknown ids " + Carry.ServerInvalid);
	}

	private static bool Has(string mark)
	{
		return CarryProbes.Marks.Any((string m) => m.EndsWith(":" + mark, StringComparison.Ordinal));
	}
}
