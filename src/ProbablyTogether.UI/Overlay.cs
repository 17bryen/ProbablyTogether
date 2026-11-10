using ProbablyTogether.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

internal static class Overlay
{
	internal const string RootName = "humangenome_accomplice_presence";

	internal static RectTransform Root;

	internal static RectTransform Cursors;

	internal static RectTransform Labels;

	internal static RectTransform Corner;

	internal static RectTransform ChatArea;

	internal static RectTransform ReadyArea;

	internal static RectTransform NewRunArea;

	internal static RectTransform InputRoot;

	internal static RectTransform LoadingRoot;

	private static bool warnedNoManager;

	internal static bool Ensure()
	{
		if ((Object)(object)Root != (Object)null)
		{
			return true;
		}
		CustomUIManager instance = CustomUIManager.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			if (!warnedNoManager)
			{
				warnedNoManager = true;
				Log.Warn("ui: no CustomUIManager yet; the overlay waits for it");
			}
			return false;
		}
		RectTransform layerRoot = instance.GetLayerRoot("overlay");
		if ((Object)(object)layerRoot == (Object)null)
		{
			return false;
		}
		Transform val = ((Transform)layerRoot).Find("humangenome_accomplice_presence");
		if ((Object)(object)val != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)val).gameObject);
		}
		Root = Kit.Rect("humangenome_accomplice_presence", (Transform)(object)layerRoot);
		Kit.Stretch(Root);
		((Transform)Root).SetAsFirstSibling();
		Cursors = Kit.Rect("cursors", (Transform)(object)Root);
		Kit.Stretch(Cursors);
		Labels = Kit.Rect("locks", (Transform)(object)Root);
		Kit.Stretch(Labels);
		Corner = Kit.Rect("roster_corner", (Transform)(object)Root);
		Kit.Pin(Corner, new Vector2(1f, 1f), new Vector2(-16f, -16f));
		Corner.sizeDelta = new Vector2(360f, 10f);
		ChatArea = Kit.Rect("chat_area", (Transform)(object)Root);
		Kit.Pin(ChatArea, new Vector2(0f, 0f), new Vector2(16f, 16f));
		ChatArea.sizeDelta = new Vector2(540f, 360f);
		RectTransform val2 = Kit.Rect("humangenome_accomplice_presence_ready", (Transform)(object)layerRoot);
		Kit.Stretch(val2);
		((Transform)val2).SetSiblingIndex(1);
		ReadyArea = Kit.Rect("ready_area", (Transform)(object)val2);
		Kit.Pin(ReadyArea, new Vector2(0.5f, 1f), new Vector2(0f, -16f));
		ReadyArea.sizeDelta = new Vector2(10f, 10f);
		RectTransform val3 = Kit.Rect("humangenome_accomplice_presence_newrun", (Transform)(object)layerRoot);
		Kit.Stretch(val3);
		((Transform)val3).SetSiblingIndex(2);
		NewRunArea = Kit.Rect("newrun_area", (Transform)(object)val3);
		Kit.Pin(NewRunArea, new Vector2(1f, 0f), new Vector2(-16f, 16f));
		NewRunArea.sizeDelta = new Vector2(10f, 10f);
		InputRoot = Kit.Rect("humangenome_accomplice_presence_chatinput", (Transform)(object)layerRoot);
		Kit.Pin(InputRoot, new Vector2(0f, 0f), new Vector2(16f, 16f));
		InputRoot.sizeDelta = new Vector2(540f, 44f);
		((Transform)InputRoot).SetSiblingIndex(3);
		LoadingRoot = Kit.Rect("humangenome_accomplice_presence_loading", (Transform)(object)layerRoot);
		Kit.Stretch(LoadingRoot);
		((Transform)LoadingRoot).SetAsLastSibling();
		((Component)LoadingRoot).gameObject.SetActive(false);
		((Component)Root).gameObject.SetActive(false);
		((Component)val2).gameObject.SetActive(false);
		((Component)val3).gameObject.SetActive(false);
		((Component)InputRoot).gameObject.SetActive(false);
		Presence.OnOverlayBuilt();
		Chat.OnOverlayBuilt();
		ReadyBar.OnOverlayBuilt();
		NewRun.OnOverlayBuilt();
		LoadingScreen.OnOverlayBuilt();
		Log.Info("ui: overlay built under " + ((Object)layerRoot).name);
		return true;
	}

	internal static void Tick()
	{
		if (Ensure())
		{
			bool shopOnline = Ui.ShopOnline;
			if (((Component)Root).gameObject.activeSelf != shopOnline)
			{
				((Component)Root).gameObject.SetActive(shopOnline);
			}
			GameObject gameObject = ((Component)((Transform)ReadyArea).parent).gameObject;
			bool flag = shopOnline && ReadyBar.Visible && !NewRun.Visible;
			if (gameObject.activeSelf != flag)
			{
				gameObject.SetActive(flag);
			}
			float num = ((flag && DayNightOuting.MapOpen) ? (-84f) : (-16f));
			if (Mathf.Abs(ReadyArea.anchoredPosition.y - num) > 0.5f)
			{
				ReadyArea.anchoredPosition = new Vector2(0f, num);
			}
			GameObject gameObject2 = ((Component)((Transform)NewRunArea).parent).gameObject;
			bool flag2 = shopOnline && NewRun.Visible;
			if (gameObject2.activeSelf != flag2)
			{
				gameObject2.SetActive(flag2);
			}
			bool flag3 = shopOnline && Chat.IsOpen;
			if (((Component)InputRoot).gameObject.activeSelf != flag3)
			{
				((Component)InputRoot).gameObject.SetActive(flag3);
			}
			if (((Transform)Root).GetSiblingIndex() != 0)
			{
				((Transform)Root).SetAsFirstSibling();
			}
			if (((Transform)LoadingRoot).GetSiblingIndex() != ((Transform)LoadingRoot).parent.childCount - 1)
			{
				((Transform)LoadingRoot).SetAsLastSibling();
			}
		}
	}
}
