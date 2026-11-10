using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PicsWatch : PicsBase
{
	public override string Name => "pics-watch";

	protected override void Build()
	{
		Join();
		Pic("pic-shop", () => "two players in the shop; roster: " + string.Join(" / ", Presence.RosterTexts.Values));
		Mark("m-shop", "pics-shop-seen");
		WaitFor("lead-on-phone", 60f, () => Locks.HeldBy("phone") == UiCoopBase.Other() && UiCoopBase.Row(UiCoopBase.Other()).Contains("on the phone"), () => "row '" + UiCoopBase.Row(UiCoopBase.Other()) + "', labels " + UiCoopBase.Labels());
		Delay("phone-settle", 1f);
		Pic("pic-phone", () => "row '" + UiCoopBase.Row(UiCoopBase.Other()) + "', labels " + UiCoopBase.Labels());
		Do("phone-object", delegate
		{
			StorePhoneObject val = Object.FindObjectOfType<StorePhoneObject>();
			if ((Object)(object)val == (Object)null)
			{
				return "no StorePhoneObject";
			}
			Camera cam = Camera.main;
			GameObject gameObject = ((Component)val).gameObject;
			string text = string.Join(",", from c in gameObject.GetComponents<Component>()
				select ((object)c).GetType().Name);
			string text2 = (((Object)(object)cam != (Object)null) ? ((object)cam.WorldToScreenPoint(gameObject.transform.position)).ToString() : "no camera");
			Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(true);
			string text3 = ((componentsInChildren.Length == 0) ? "no renderer" : string.Join(" / ", componentsInChildren.Take(3).Select(delegate(Renderer r)
			{
				string name = ((object)r).GetType().Name;
				object obj;
				if (!((Object)(object)cam != (Object)null))
				{
					obj = "";
				}
				else
				{
					Camera obj2 = cam;
					Bounds bounds = r.bounds;
					string? text4 = ((object)obj2.WorldToScreenPoint(bounds.center)).ToString();
					Camera obj3 = cam;
					bounds = r.bounds;
					obj = text4 + " max " + ((object)obj3.WorldToScreenPoint(bounds.max)).ToString();
				}
				return name + " " + (string?)obj;
			})));
			return "phone '" + ((Object)gameObject).name + "' parent '" + (((Object)(object)gameObject.transform.parent != (Object)null) ? ((Object)gameObject.transform.parent).name : "-") + "' components " + text + "; transform on screen " + text2 + "; renderers " + text3 + "; screen " + Screen.width + "x" + Screen.height;
		});
		Mark("m-phone", "pics-phone-seen");
		WaitFor("lead-aiming", 60f, () => Presence.Aiming == UiCoopBase.Other() && UiCoopBase.Row(UiCoopBase.Other()).Contains("aiming"), () => "row '" + UiCoopBase.Row(UiCoopBase.Other()) + "', labels " + UiCoopBase.Labels());
		Delay("aim-settle", 1f);
		Pic("pic-aim", () => "row '" + UiCoopBase.Row(UiCoopBase.Other()) + "', labels " + UiCoopBase.Labels());
		Mark("m-aim", "pics-aim-seen");
		WaitFor("lead-chat", 60f, () => Chat.Lines.Any((Chat.Line l) => l.Player == UiCoopBase.Other() && l.Text == "Shutter goes up soon. I've got the phone."), () => Chat.Lines.Count + " line(s)");
		Do("reply", () => (!Chat.Send("On it. I'll watch the back room.")) ? "not sent" : "sent 'On it. I'll watch the back room.'");
		WaitFor("reply-back", 20f, () => Chat.Lines.Any((Chat.Line l) => l.Player == Net.LocalPlayer && l.Text == "On it. I'll watch the back room."), () => Chat.Lines.Count + " line(s)");
		Pic("pic-chat", () => Chat.Lines.Count + " chat line(s)");
		Mark("m-chat", "pics-chat-seen");
		WaitFor("lead-opened", 60f, () => PicsBase.State == "OPEN", () => "state " + PicsBase.State);
		Delay("open-settle", 6f);
		Pic("pic-open", () => "state " + PicsBase.State + ", counter " + Locks.HeldBy("counter") + ", roster: " + string.Join(" / ", Presence.RosterTexts.Values) + ", labels " + UiCoopBase.Labels());
		Mark("m-open", "pics-open-seen");
		WaitFor("lead-ready", 120f, () => ReadyBar.Visible && ReadyBar.IsReady(UiCoopBase.Other()), () => "ready bar " + ReadyBar.Visible + " '" + ReadyBar.Text + "', state " + PicsBase.State);
		Delay("ready-settle", 1f);
		Pic("pic-ready", () => "ready bar '" + ReadyBar.Text + "'");
		Mark("m-ready", "pics-ready-seen");
		WaitFor("plate", 60f, () => DayNightRun.RunOver && NewRun.Visible, () => "run over " + DayNightRun.RunOver + ", plate " + NewRun.Visible);
		Delay("plate-settle", 2f);
		Pic("pic-plate", () => "plate '" + NewRun.Text + "'");
		WaitFor("lead-voted", 40f, () => NewRun.Votes().Contains(UiCoopBase.Other()), () => "plate '" + NewRun.Text + "'");
		Delay("vote-settle", 1f);
		Pic("pic-vote", () => "plate '" + NewRun.Text + "'");
	}
}
