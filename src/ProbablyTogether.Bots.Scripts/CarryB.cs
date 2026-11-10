using System;
using System.Globalization;
using System.Linq;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class CarryB : CarryBotBase
{
	private long want;

	private long bagId;

	private string wantCount = "";

	public override string Name => "carry-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 600f;

	private void Want(string text)
	{
		string[] array = text.Split(':');
		want = long.Parse(array[0], CultureInfo.InvariantCulture);
		int num = ((array.Length > 1) ? int.Parse(array[1], CultureInfo.InvariantCulture) : 0);
		wantCount = ((num > 1) ? ("x" + num) : "");
	}

	private static Presence.Remote A()
	{
		if (!Presence.Remotes.TryGetValue(CarryBotBase.Other(), out var value))
		{
			return null;
		}
		return value;
	}

	private bool Shows()
	{
		int player = CarryBotBase.Other();
		Presence.Remote remote = A();
		Sprite val = Carry.SpriteOf(CarryBotBase.I(want));
		if (remote != null && remote.Visible && remote.Drag == want && Carry.ShownItem(player) == want && Carry.IconOnScreen(player) && (Object)(object)val != (Object)null && (Object)(object)Carry.ShownSprite(player) == (Object)(object)val && Carry.ShownCount(player) == wantCount)
		{
			return remote.LabelText.Contains("holding ");
		}
		return false;
	}

	private static bool Cleared()
	{
		Presence.Remote remote = A();
		int player = CarryBotBase.Other();
		if (remote != null && remote.Drag == 0L && Carry.ShownItem(player) == 0L)
		{
			return !Carry.IconOnScreen(player);
		}
		return false;
	}

	private string Describe()
	{
		int player = CarryBotBase.Other();
		Presence.Remote remote = A();
		if (remote != null)
		{
			return "A's cursor visible " + remote.Visible + " view '" + remote.View + "' (mine '" + Presence.LocalView + "') d=" + remote.Drag + " n=" + remote.Units + " g=" + remote.Group + "; icon item " + Carry.ShownItem(player) + " on screen " + Carry.IconOnScreen(player) + " sprite " + (((Object)(object)Carry.ShownSprite(player) != (Object)null) ? ((Object)Carry.ShownSprite(player)).name : "none") + " count '" + Carry.ShownCount(player) + "' (want #" + want + " '" + wantCount + "'); tag '" + remote.LabelText + "'";
		}
		return "no cursor for player " + player + " yet";
	}

	private void Phase(string k, string clearMark, string clearedMark, Func<bool> extra = null, float holdClear = 0f)
	{
		WaitMark("drag" + k + "-told", 60f, "a-drag" + k + ":", Want);
		WaitFor("drag" + k + "-shown", 20f, Shows, Describe);
		Check("drag" + k + "-click-through", () => ((Component)Overlay.Root).GetComponentsInChildren<Graphic>(true).All((Graphic g) => !g.raycastTarget), () => "every overlay graphic is click-through");
		Mark("saw" + k + "-mark", () => "b-saw" + k);
		WaitMark(clearMark + "-told", 60f, clearMark + ":");
		WaitFor("drag" + k + "-cleared", 20f, () => Cleared() && (extra == null || extra()), Describe);
		if (holdClear > 0f)
		{
			Step("drag" + k + "-stays-cleared", holdClear + 5f, delegate(BotStep st)
			{
				if (!Cleared())
				{
					st.Detail = "came back: " + Describe();
					return StepStatus.Fail;
				}
				if (st.Elapsed < holdClear)
				{
					return StepStatus.Running;
				}
				st.Detail = "still cleared after " + CarryBotBase.F(st.Elapsed) + " s while A still holds it (A's refresh frames are hidden by the server)";
				return StepStatus.Pass;
			});
		}
		Mark(clearedMark + "-mark", () => clearedMark);
	}

	protected override void Build()
	{
		JoinAndMeet();
		WaitFor("bag", 60f, () => CarryBotBase.Bag != null, () => "no BR_CARRY_BAG yet");
		Do("bag-id", delegate
		{
			bagId = Ids.Of(CarryBotBase.Bag);
			return "bag #" + bagId;
		});
		Phase("1", "a-drop1", "b-clear1", delegate
		{
			GameItem obj = CarryBotBase.I(want);
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				GameInventory parentInventory = obj.parentInventory;
				obj2 = ((parentInventory != null) ? parentInventory.GetParentItem() : null);
			}
			return obj2 == CarryBotBase.I(bagId);
		});
		Phase("2", "a-refused2", "b-clear2", null, 4f);
		Phase("3", "a-cancel3", "b-clear3");
		Phase("4", "a-abort4", "b-clear4");
		WaitMark("drag5-told", 60f, "a-drag5:", Want);
		WaitFor("drag5-shown", 20f, Shows, Describe);
		Mark("saw5-mark", () => "b-saw5");
		WaitMark("fewer5-told", 60f, "a-fewer5:", Want);
		WaitFor("drag5-count", 20f, Shows, Describe);
		Mark("fewer5-mark", () => "b-fewer5");
		WaitMark("end5-told", 60f, "a-end5:");
		WaitFor("drag5-cleared", 20f, Cleared, Describe);
		Mark("clear5-mark", () => "b-clear5");
		WaitMark("a-done", 60f, "a-done");
		Check("nothing-left", Cleared, Describe);
		Do("done", delegate
		{
			Presence.FakePointer = null;
			return "watched 5 drags";
		});
		Mark("done-mark", () => "b-done");
	}
}
