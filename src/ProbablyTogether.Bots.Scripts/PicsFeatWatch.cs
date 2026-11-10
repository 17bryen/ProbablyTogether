using System;
using System.Linq;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PicsFeatWatch : PicsFeatBase
{
	public override string Name => "pics-feat-watch";

	protected override void Build()
	{
		Join();
		WaitFor("hint", 30f, () => Chat.Lines.Any((Chat.Line l) => l.Text.Contains("to talk to the shop.")), () => Chat.Lines.Count + " chat line(s)");
		Do("cursor-away", delegate
		{
			Presence.FakePointer = new Vector2(0.98f, 0.02f);
			return "this copy's pointer parked in a corner";
		});
		Pic("pic-fshop", () => "roster: " + string.Join(" / ", Presence.RosterTexts.Values) + "; last chat '" + Chat.Lines.Last().Text + "'");
		Mark("m-fshop", "pics-fshop-seen");
		WaitFor("lead-talking", 40f, () => UiCoopBase.Row(UiCoopBase.Other()).Contains("talking"), () => "row '" + UiCoopBase.Row(UiCoopBase.Other()) + "'");
		Delay("talk-settle", 1f);
		Pic("pic-talk", () => "row '" + UiCoopBase.Row(UiCoopBase.Other()) + "'");
		Mark("m-talk", "pics-talk-seen");
		WaitFor("lead-quiet", 20f, () => !UiCoopBase.Row(UiCoopBase.Other()).Contains("talking"), () => "row '" + UiCoopBase.Row(UiCoopBase.Other()) + "'");
		Do("options-open", delegate
		{
			GameOptionUIManager.Instance.OpenUI();
			return "the game's Options window opened";
		});
		Delay("options-layout", 1.5f);
		Do("options-scroll", delegate
		{
			CustomUIManager instance = CustomUIManager.Instance;
			CustomUIWindow val = ((instance != null) ? instance.GetWindow("game_options") : null);
			ScrollRect? obj = ((val != null && (Object)(object)val.root != (Object)null) ? val.root.GetComponentsInChildren<ScrollRect>(true).FirstOrDefault() : null);
			if ((Object)(object)obj == (Object)null)
			{
				throw new Exception("no scroll area in the Options window");
			}
			obj.verticalNormalizedPosition = 0f;
			return "scrolled to the end (our Co-op voice section is after the game's)";
		});
		Delay("options-settle", 1f);
		Pic("pic-options", () => "Options open " + GameOptionUIManager.Instance.IsOpen);
		Do("options-close", delegate
		{
			GameOptionUIManager.Instance.CloseUI();
			return "closed";
		});
		Delay("options-closed", 1f);
		Mark("m-options", "pics-options-seen");
		WaitFor("item-marker", 30f, () => PointMarks.Marks.TryGetValue(UiCoopBase.Other(), out var value) && value.ItemId != 0L && value.Visible, () => (!PointMarks.Marks.TryGetValue(UiCoopBase.Other(), out var value)) ? "no marker from the leader" : value.Describe());
		Delay("item-settle", 0.5f);
		Do("pic-ping-item", () => "PIC pic-ping-item: " + PointMarks.Marks[UiCoopBase.Other()].Describe() + "; chat '" + Chat.Lines.Last().Text + "'");
		Delay("pic-ping-item-hold", 3f);
		Mark("m-ping-item", "pics-ping-item-seen");
		WaitFor("spot-marker", 30f, () => PointMarks.Marks.TryGetValue(UiCoopBase.Other(), out var value) && value.ItemId == 0L && value.Visible, () => (!PointMarks.Marks.TryGetValue(UiCoopBase.Other(), out var value)) ? "no marker from the leader" : value.Describe());
		Delay("spot-settle", 0.5f);
		Do("pic-ping-spot", () => "PIC pic-ping-spot: " + PointMarks.Marks[UiCoopBase.Other()].Describe());
		Delay("pic-ping-spot-hold", 3f);
		Mark("m-ping-spot", "pics-ping-spot-seen");
		WaitFor("carry-icon", 30f, () => Carry.IconOnScreen(UiCoopBase.Other()), () => "shown item " + Carry.ShownItem(UiCoopBase.Other()));
		Delay("carry-settle", 1f);
		Pic("pic-carry", () => "icon of #" + Carry.ShownItem(UiCoopBase.Other()) + " '" + Carry.ShownCount(UiCoopBase.Other()) + "'; " + string.Join(" / ", Presence.Remotes.Values.Select((Presence.Remote r) => "p" + r.Id + " '" + r.LabelText + "'")));
		Mark("m-carry", "pics-carry-seen");
		WaitFor("carry-gone", 20f, () => !Carry.IconOnScreen(UiCoopBase.Other()), () => "shown item " + Carry.ShownItem(UiCoopBase.Other()));
	}
}
