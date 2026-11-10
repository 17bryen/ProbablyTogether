using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PicsPvWatch : PicsBase2
{
	public override string Name => "pics-pv-watch";

	protected override void Build()
	{
		JoinN(4);
		Do("cursor-away", delegate
		{
			Presence.FakePointer = new Vector2(0.98f, 0.98f);
			return "this copy's own pointer parked in a corner (others see it there)";
		});
		Mark("m-pv-in", "pics-pv-in");
		WaitFor("customer", 150f, () => Customers.SomeoneAtCounter, () => "state " + PicsBase.State + ", someone at the counter " + Customers.SomeoneAtCounter);
		WaitFor("cursors", 60f, () => Presence.Remotes.Values.Count((Presence.Remote r) => r.Visible) >= 3, () => string.Join(" / ", Presence.Remotes.Values.Select((Presence.Remote r) => "p" + r.Id + " visible " + r.Visible + " at (" + r.X.ToString("F2") + "," + r.Y.ToString("F2") + ") view " + r.View)));
		Delay("settle", 4f);
		Pic("pic-preview-roster", () => string.Join(" / ", Presence.Remotes.Values.Select((Presence.Remote r) => "p" + r.Id + " (" + r.X.ToString("F2") + "," + r.Y.ToString("F2") + ") '" + r.LabelText + "'")) + "; labels " + UiCoopBase.Labels());
		Do("hide-corner", delegate
		{
			RectTransform corner = Overlay.Corner;
			if (corner != null)
			{
				((Component)corner).gameObject.SetActive(false);
			}
			RectTransform chatArea = Overlay.ChatArea;
			if (chatArea != null)
			{
				((Component)chatArea).gameObject.SetActive(false);
			}
			return "roster and chat log hidden for the capture";
		});
		Delay("hide-settle", 1f);
		Pic("pic-preview", delegate
		{
			StoreClientInstance currentClientInstance = PicsBase.Ps.currentClientInstance;
			return "customer " + (((currentClientInstance == null) ? null : currentClientInstance.GetClientBlueprint()?.identifier) ?? "nobody") + "; labels " + UiCoopBase.Labels();
		});
		Pic("pic-preview-2", () => string.Join(" / ", Presence.Remotes.Values.Select((Presence.Remote r) => "p" + r.Id + " visible " + r.Visible)));
		Do("show-corner", delegate
		{
			RectTransform corner = Overlay.Corner;
			if (corner != null)
			{
				((Component)corner).gameObject.SetActive(true);
			}
			RectTransform chatArea = Overlay.ChatArea;
			if (chatArea != null)
			{
				((Component)chatArea).gameObject.SetActive(true);
			}
			return "roster and chat log back";
		});
		Mark("m-pv-done", "pics-pv-done");
	}
}
