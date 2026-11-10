using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public abstract class ItemsBotBase : BotScript
{
	protected static EmporiumEntry E => EmporiumEntry.Instance;

	protected static string S(object o)
	{
		return Convert.ToString(o, CultureInfo.InvariantCulture);
	}

	protected static GameItem Tagged(string tag)
	{
		return ((IEnumerable<GameItem>)Items.All()).FirstOrDefault((Func<GameItem, bool>)((GameItem i) => i.IsTag(tag)));
	}

	protected static bool IsTest(GameItem it)
	{
		if (!it.IsTag("BR_BOT_BAG") && !it.IsTag("BR_BOT_BOX") && !it.IsTag("BR_BOT_SHARD"))
		{
			return it.IsTag("BR_BOT_INNER");
		}
		return true;
	}

	protected static List<GameItem> Movables(GameInventory inv, Func<GameItem, bool> more = null)
	{
		return inv.childItems.Where((GameItem i) => !Items.IsGone(i) && !IsTest(i) && GeneralHelper.IsItemOwned(i) && i.MayRemove() && i.MaxNumRemove() >= 1 && i.contentWindow == null && !ItemPinHelper.IsPinned(i) && (more == null || more(i))).ToList();
	}

	protected static bool Spot(GameInventory inv, GameItem item, int n, int rot, out int x, out int y)
	{
		x = (y = 0);
		int num = 30;
		int num2 = 30;
		GameGridInventory val = (GameGridInventory)(object)((inv is GameGridInventory) ? inv : null);
		if (val != null && val.inventoryShape != null)
		{
			num = val.inventoryShape.width;
			num2 = val.inventoryShape.height;
		}
		for (int i = 0; i < num2; i++)
		{
			for (int j = 0; j < num; j++)
			{
				if (item.parentInventory != inv || j != item.modifiedShape.minX || i != item.modifiedShape.minY || rot != item.modifiedShape.orientation)
				{
					SlotMarker val2 = inv.TryInventorySlot(item, n, Vector2.negativeInfinity, Items.Shape(item, j, i, item.modifiedShape.flipped, rot), item.modifiedState);
					if (val2 != null && val2.numTransfer >= 1)
					{
						x = j;
						y = i;
						return true;
					}
				}
			}
		}
		return false;
	}

	protected void Cmd(string name, string cmd)
	{
		Do(name, delegate
		{
			Net.ToServer("items.botcmd", new ItemsBotCmd
			{
				cmd = cmd
			});
			return "sent " + cmd;
		});
	}

	protected BotStep Act(string name, float timeout, Func<string> prepare, Func<int> send, Func<bool> done, Func<string> detail = null)
	{
		int q = 0;
		string prep = null;
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (prep != null && prep.StartsWith("SKIP", StringComparison.Ordinal))
			{
				st.Detail = prep;
				return StepStatus.Fail;
			}
			if (q == 0)
			{
				st.Detail = "request not sent (" + prep + ")";
				return StepStatus.Fail;
			}
			string text = ItemsMove.Outcome(q);
			if (text.StartsWith("rejected", StringComparison.Ordinal))
			{
				st.Detail = prep + "; server " + text;
				return StepStatus.Fail;
			}
			if (text == "applied-mismatch")
			{
				st.Detail = prep + "; applied but this copy differed (" + ItemsMove.ApplyMismatches + " mismatches)";
				return StepStatus.Fail;
			}
			if (text != "applied" || !done())
			{
				return StepStatus.Running;
			}
			st.Detail = prep + "; applied in " + st.Elapsed.ToString("F2", CultureInfo.InvariantCulture) + " s" + ((detail != null) ? ("; " + detail()) : "");
			return StepStatus.Pass;
		}, delegate
		{
			prep = ((prepare != null) ? prepare() : "");
			if (prep == null || !prep.StartsWith("SKIP", StringComparison.Ordinal))
			{
				q = send();
			}
		});
		botStep.TimeoutDetail = () => "request " + q + " outcome '" + ItemsMove.Outcome(q) + "' after " + timeout + " s; " + prep + ((detail != null) ? ("; " + detail()) : "");
		return botStep;
	}

	protected BotStep ActRejected(string name, float timeout, Func<string> prepare, Func<int> send, string why)
	{
		int q = 0;
		string prep = null;
		return Step(name, timeout, delegate(BotStep st)
		{
			if (q == 0)
			{
				st.Detail = "request not sent (" + prep + ")";
				return StepStatus.Fail;
			}
			string text = ItemsMove.Outcome(q);
			if (text == "pending")
			{
				return StepStatus.Running;
			}
			st.Detail = prep + "; server " + text;
			return (text == "rejected:" + why) ? StepStatus.Pass : StepStatus.Fail;
		}, delegate
		{
			prep = ((prepare != null) ? prepare() : "");
			q = send();
		});
	}

	protected BotStep DigestSame(string name, float timeout = 20f)
	{
		int id = 0;
		int tries = 0;
		float askedAt = 0f;
		string last = "";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (id == 0)
			{
				if (st.Elapsed - askedAt < 0.5f)
				{
					return StepStatus.Running;
				}
				id = BotProbes.AskServer();
				tries++;
				askedAt = st.Elapsed;
				return StepStatus.Running;
			}
			Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
			if (dictionary == null)
			{
				return StepStatus.Running;
			}
			id = 0;
			string text = ItemsMove.Digest();
			dictionary.TryGetValue("items.digest", out var value);
			last = "here " + text + ", server " + value;
			if (!string.IsNullOrEmpty(text) && text == value)
			{
				st.Detail = last + " (ask " + tries + ")";
				return StepStatus.Pass;
			}
			return (tries >= 6) ? Fail(st, last + " after " + tries + " asks") : StepStatus.Running;
		});
		botStep.TimeoutDetail = () => last + " (" + tries + " asks)";
		return botStep;
	}

	private static StepStatus Fail(BotStep st, string d)
	{
		st.Detail = d;
		return StepStatus.Fail;
	}

	protected void JoinSteps()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("live", 240f, () => Net.IsLive && Items.Ready, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		Delay("settle", 2f);
	}
}
