using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

/// Covers the screen in the game's transition colour while a shop is joined, loaded or started, with the game's own
/// lettering and cancel button; falls back to the mod's plate when the game's fonts are missing.
public static class LoadingScreen
{
	private static TextMeshProUGUI title;

	private static TextMeshProUGUI detail;

	private static GameObject body;

	/// 0: nothing built yet; 1: the mod's plate; 2: the game's lettering.
	private static int built;

	private static ClientState shownFor;

	private static float since;

	private static bool wasLiveThisSession;

	public static bool Visible { get; private set; }

	internal static bool GameStyle => built == 2;

	public static string Text
	{
		get
		{
			if (!((Object)(object)title != (Object)null))
			{
				return "";
			}
			return ((TMP_Text)title).text;
		}
	}

	internal static void OnOverlayBuilt()
	{
		RectTransform loadingRoot = Overlay.LoadingRoot;
		// Opaque, so nothing of the menu or the half-loaded shop shows through.
		Kit.Stretch(((Graphic)Kit.Box((Transform)(object)loadingRoot, "cover", new Color(Kit.PlateColor.r, Kit.PlateColor.g, Kit.PlateColor.b, 1f), raycast: true)).rectTransform);
		built = 0;
		body = null;
		title = null;
		detail = null;
		Build();
	}

	private static void Cancel()
	{
		Log.Info("ui: join cancelled on the loading screen");
		Net.Disconnect();
	}

	/// Uses the game's pieces when available (retried at every showing), else the mod's plate.
	private static void Build()
	{
		RectTransform loadingRoot = Overlay.LoadingRoot;
		if ((Object)(object)loadingRoot == (Object)null || built == 2)
		{
			return;
		}
		GameObject gameObject = null;
		try
		{
			CustomUIManager instance = CustomUIManager.Instance;
			NewGamePanelFrame newGamePanelFrame = ((Object)(object)instance != (Object)null && (Object)(object)instance.newGameDifficultyPrefab != (Object)null) ? instance.newGameDifficultyPrefab.GetComponent<NewGamePanelFrame>() : null;
			if ((Object)(object)newGamePanelFrame != (Object)null && (Object)(object)newGamePanelFrame.fontLubrifont != (Object)null && (Object)(object)newGamePanelFrame.fontNotoMedium != (Object)null)
			{
				RectTransform rectTransform = Kit.Rect("game", (Transform)(object)loadingRoot);
				gameObject = ((Component)rectTransform).gameObject;
				Kit.Pin(rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
				rectTransform.sizeDelta = new Vector2(1500f, 250f);
				TextMeshProUGUI textMeshProUGUI = NewGameUIElements.NewText(newGamePanelFrame, (Transform)(object)rectTransform, "title", 0f, 0f, 1500f, 76f, FontLoader.FontRole.Lubrifont, 54f, NewGameStyle.BUTTON_BORDER_COLOR, (TextAlignmentOptions)514, wrap: false);
				TextMeshProUGUI textMeshProUGUI2 = NewGameUIElements.NewText(newGamePanelFrame, (Transform)(object)rectTransform, "detail", 0f, 86f, 1500f, 34f, FontLoader.FontRole.Noto_Sans, 22f, NewGameStyle.DESCRIPTION, (TextAlignmentOptions)514, wrap: false);
				NewGameCommonButton.Build(newGamePanelFrame, (Transform)(object)rectTransform, "bellun_probably_together_loading_cancel", 620f, 176f, 260f, 48f, Lang.Tr("Cancel"), Cancel);
				if ((Object)(object)body != (Object)null)
				{
					Object.Destroy((Object)(object)body);
				}
				body = gameObject;
				title = textMeshProUGUI;
				detail = textMeshProUGUI2;
				built = 2;
				Log.Info("ui: the loading screen uses the game's lettering");
				return;
			}
		}
		catch (Exception ex)
		{
			if ((Object)(object)gameObject != (Object)null)
			{
				Object.Destroy((Object)(object)gameObject);
			}
			if (Log.Allow("loading|game", 60000L, out var _))
			{
				Log.Warn("ui: the loading screen could not be made from the game's pieces: " + ex.Message);
			}
		}
		if (built != 0)
		{
			return;
		}
		RectTransform rectTransform2 = Kit.Plate((Transform)(object)loadingRoot, "panel", out var content, 1f, 18, 8f);
		Kit.Pin(rectTransform2, new Vector2(0.5f, 0.5f), Vector2.zero);
		body = ((Component)rectTransform2).gameObject;
		title = Kit.Label((Transform)(object)content, "title", 26f, Kit.Text, 640f, (TextAlignmentOptions)514);
		detail = Kit.Label((Transform)(object)content, "detail", 17f, Kit.Dim, 640f, (TextAlignmentOptions)514);
		HorizontalLayoutGroup val = Kit.Row((Transform)(object)content, "buttons");
		((Component)val).gameObject.AddComponent<LayoutElement>().preferredWidth = 640f;
		((Component)Kit.Rect("left", ((Component)val).transform)).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
		((Object)((Component)Kit.TextButton(((Component)val).transform, "cancel", Lang.Tr("Cancel"), 17f, Cancel, out var _)).gameObject).name = "bellun_probably_together_loading_cancel";
		((Component)Kit.Rect("right", ((Component)val).transform)).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
		built = 1;
	}

	/// Host mode: set when "Host" is pressed, cleared when the shop is up or hosting stops.
	internal static bool HostStarting;

	internal static void Tick()
	{
		if ((Object)(object)Overlay.LoadingRoot == (Object)null)
		{
			return;
		}
		ClientState clientState = Net.ClientState;
		if (clientState == ClientState.Ready)
		{
			wasLiveThisSession = true;
		}
		if (clientState == ClientState.Idle)
		{
			wasLiveThisSession = false;
		}
		int num;
		switch (clientState)
		{
		case ClientState.Connecting:
		case ClientState.Handshake:
			num = 1;
			break;
		default:
			num = 0;
			break;
		case ClientState.AwaitSnapshot:
		case ClientState.Loading:
			num = 1;
			break;
		}
		bool hosting = Net.IsHost && HostStarting;
		bool flag = hosting || (byte)num != 0;
		if (flag != Visible)
		{
			Visible = flag;
			if (flag)
			{
				Build();
			}
			((Component)Overlay.LoadingRoot).gameObject.SetActive(flag);
			if (flag)
			{
				Log.Info("ui: loading screen up (" + (hosting ? "starting a shop" : clientState.ToString()) + (GameStyle ? "" : ", the mod's plate") + ")");
			}
		}
		if (!flag)
		{
			shownFor = ClientState.Idle;
			return;
		}
		if (clientState != shownFor)
		{
			shownFor = clientState;
			since = Time.realtimeSinceStartup;
		}
		if ((Object)(object)title == (Object)null)
		{
			return;
		}
		string n = (string.IsNullOrEmpty(Net.ServerName) ? Net.ServerAddress : Net.ServerName);
		string text2 = hosting ? Lang.Trf("Starting {0}...", Kit.Name(n)) : clientState switch
		{
			ClientState.Connecting => Lang.Trf("Connecting to {0}...", Kit.Name(Net.ServerAddress)),
			ClientState.Handshake => Lang.Trf("Joining {0}...", Kit.Name(n)),
			ClientState.AwaitSnapshot => Lang.Trf("Joined {0}. Getting the shop...", Kit.Name(n)),
			_ => wasLiveThisSession ? Lang.Tr("Catching up with the shop...") : Lang.Trf("Loading {0}...", Kit.Name(n)),
		};
		if (built != 2)
		{
			Kit.SetText(title, text2);
		}
		else if (((TMP_Text)title).text != text2)
		{
			// Set directly: Kit.SetText would replace the game's font with the mod's.
			((TMP_Text)title).text = text2;
		}
		int num2 = Mathf.FloorToInt(Time.realtimeSinceStartup - since);
		string text = ((num2 >= 3) ? (num2.ToString(CultureInfo.InvariantCulture) + " s") : "");
		if (((TMP_Text)detail).text != text)
		{
			((TMP_Text)detail).text = text;
		}
	}
}
