using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

/// B holds an item; A sees it taken, and moving another item in the same inventory keeps B's item in B's hand.
public abstract class HoldBotBase : DayNightBotBase
{
	protected static readonly FieldInfo currentItemField = AccessTools.Field(typeof(ItemMouseDragHandler), "currentItem");

	protected static readonly FieldInfo isDraggingField = AccessTools.Field(typeof(ItemMouseDragHandler), "isDragging");

	protected static EmporiumEntry E => EmporiumEntry.Instance;

	protected static List<GameItem> Loose()
	{
		return (from i in ItemsBotBase.Movables((GameInventory)(object)E.invElement)
			where i.contentWindow == null
			orderby Ids.Of(i)
			select i).ToList();
	}

	protected static bool InHand(GameItem item)
	{
		ItemMouseDragHandler current = ItemMouseDragHandler.current;
		GameItemElement val = (GameItemElement)(object)((item is GameItemElement) ? item : null);
		return current != null && current.IsDraggingItem && current.GetCurrentItem() == item && val != null && val.fakeBackground.image.enabled;
	}
}

public sealed class HoldB : HoldBotBase
{
	private long jId;

	public override string Name => "hold-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 300f;

	protected override void Build()
	{
		JoinSteps();
		WaitFor("a-is-here", 120f, () => Net.Players.Count == 2, () => Net.Players.Count + " connected");
		Delay("settle", 3f);
		Do("pick-up", delegate
		{
			// Matches the game's drag start: the item stays in its slot, its picture in the hand.
			GameItem val = Loose().LastOrDefault();
			GameItemElement val2 = (GameItemElement)(object)((val is GameItemElement) ? val : null);
			jId = Ids.Of(val);
			ItemMouseDragHandler current = ItemMouseDragHandler.current;
			currentItemField.SetValue(current, val2);
			isDraggingField.SetValue(current, true);
			val2.UpdateFakeReal(val.unitCount);
			val2.ToggleFakeBackground(true, UISettings.current.dragOpacity, 1f);
			Carry.OnLocalDragStart(val, group: false);
			return val.identifier + "#" + jId + " is in my hand";
		});
		Check("in-my-hand", () => InHand(Ids.Find(jId)), () => "holding #" + jId + ": " + InHand(Ids.Find(jId)));
		WaitFor("a-moved-something-and-i-still-hold-mine", 60f, () => DragKeeper.Restored >= 1 && InHand(Ids.Find(jId)), () => "my hand was put back " + DragKeeper.Restored + " time(s); still holding: " + InHand(Ids.Find(jId)));
		Delay("more-changes-may-come", 3f);
		Check("still-in-my-hand", () => InHand(Ids.Find(jId)) && (object)Ids.Find(jId).parentInventory == E.invElement, () => "holding #" + jId + ": " + InHand(Ids.Find(jId)) + " (put back " + DragKeeper.Restored + " time(s))");
		Do("let-go", delegate
		{
			ItemMouseDragHandler.current.Cancel();
			Carry.OnLocalDragEnd("bot");
			return "dropped where it was";
		});
		Agree("same-as-server", 30f, "core.digest", "items.digest");
	}
}

public sealed class HoldA : HoldBotBase
{
	private long sId;

	private long theirs;

	private int q;

	public override string Name => "hold-a";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 300f;

	private static int Other => Net.Players.Keys.FirstOrDefault((int p) => p != Net.LocalPlayer);

	protected override void Build()
	{
		JoinSteps();
		WaitFor("b-is-here", 120f, () => Net.Players.Count == 2, () => Net.Players.Count + " connected");
		WaitFor("b-holds-something", 60f, delegate
		{
			if (Presence.Remotes.TryGetValue(Other, out var value) && value.Drag != 0L)
			{
				theirs = value.Drag;
				return true;
			}
			return false;
		}, () => "the other player holds #" + theirs);
		Check("i-cannot-grab-what-they-hold", () => HoldGuardPatch.HeldBy(Ids.Find(theirs)) == Other, () => "item #" + theirs + " is held by player " + HoldGuardPatch.HeldBy(Ids.Find(theirs)) + " (the other player is " + Other + ")");
		Check("i-can-grab-anything-else", () => Loose().Any((GameItem i) => Ids.Of(i) != theirs && HoldGuardPatch.HeldBy(i) == 0), () => "other items are free");
		Step("move-another-item-in-the-same-inventory", 25f, delegate(BotStep st)
		{
			if (q == 0)
			{
				GameItem val = Loose().First((GameItem i) => Ids.Of(i) != theirs);
				sId = Ids.Of(val);
				if (!ItemsBotBase.Spot((GameInventory)(object)E.invElement, val, val.unitCount, val.modifiedShape.orientation, out var x, out var y))
				{
					st.Detail = "no free spot in the shop's storage";
					return StepStatus.Fail;
				}
				q = ItemsMove.Move(val, (GraphNodeStorage)(object)E.invElement, x, y, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "bot");
				st.Detail = val.identifier + "#" + sId + " -> " + x + "," + y + " while the other player holds #" + theirs;
				return StepStatus.Running;
			}
			string text = ItemsMove.Outcome(q);
			if (text.StartsWith("rejected", StringComparison.Ordinal))
			{
				st.Detail = st.Detail + "; server " + text;
				return StepStatus.Fail;
			}
			return (text == "applied") ? StepStatus.Pass : StepStatus.Running;
		});
		WaitFor("b-let-go", 90f, () => Presence.Remotes.TryGetValue(Other, out var value) && value.Drag == 0L, () => "the other player still holds something");
		Check("free-again", () => HoldGuardPatch.HeldBy(Ids.Find(theirs)) == 0, () => "item #" + theirs + " can be picked up again");
		Agree("same-as-server", 30f, "core.digest", "items.digest");
	}
}
