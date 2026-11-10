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

	private static ClientState shownFor;

	private static float since;

	private static bool wasLiveThisSession;

	public static bool Visible { get; private set; }

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
		Kit.Stretch(((Graphic)Kit.Box((Transform)(object)loadingRoot, "dim", new Color(Kit.PlateColor.r, Kit.PlateColor.g, Kit.PlateColor.b, 0.94f), raycast: true)).rectTransform);
		Kit.Pin(Kit.Plate((Transform)(object)loadingRoot, "panel", out var content, 1f, 18, 8f), new Vector2(0.5f, 0.5f), Vector2.zero);
		title = Kit.Label((Transform)(object)content, "title", 26f, Kit.Text, 640f, (TextAlignmentOptions)514);
		detail = Kit.Label((Transform)(object)content, "detail", 17f, Kit.Dim, 640f, (TextAlignmentOptions)514);
		HorizontalLayoutGroup val = Kit.Row((Transform)(object)content, "buttons");
		((Component)val).gameObject.AddComponent<LayoutElement>().preferredWidth = 640f;
		((Component)Kit.Rect("left", ((Component)val).transform)).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
		((Object)((Component)Kit.TextButton(((Component)val).transform, "cancel", Lang.Tr("Cancel"), 17f, delegate
		{
			Log.Info("ui: join cancelled on the loading screen");
			Net.Disconnect();
		}, out var _)).gameObject).name = "humangenome_accomplice_loading_cancel";
		((Component)Kit.Rect("right", ((Component)val).transform)).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
	}

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
			num = ((!JoinMenu.DialogOpen) ? 1 : 0);
			break;
		default:
			num = 0;
			break;
		case ClientState.AwaitSnapshot:
		case ClientState.Loading:
			num = 1;
			break;
		}
		bool flag = (byte)num != 0;
		if (flag != Visible)
		{
			Visible = flag;
			((Component)Overlay.LoadingRoot).gameObject.SetActive(flag);
			if (flag)
			{
				Log.Info("ui: loading screen up (" + clientState.ToString() + ")");
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
		string n = (string.IsNullOrEmpty(Net.ServerName) ? Net.ServerAddress : Net.ServerName);
		Kit.SetText(text: clientState switch
		{
			ClientState.Connecting => Lang.Trf("Connecting to {0}...", Kit.Name(Net.ServerAddress)), 
			ClientState.Handshake => Lang.Trf("Joining {0}...", Kit.Name(n)), 
			ClientState.AwaitSnapshot => Lang.Trf("Joined {0}. Getting the shop...", Kit.Name(n)), 
			_ => wasLiveThisSession ? Lang.Tr("Catching up with the shop...") : Lang.Trf("Loading {0}'s shop...", Kit.Name(n)), 
		}, t: title);
		int num2 = Mathf.FloorToInt(Time.realtimeSinceStartup - since);
		string text = ((num2 >= 3) ? (num2.ToString(CultureInfo.InvariantCulture) + " s") : "");
		if (((TMP_Text)detail).text != text)
		{
			((TMP_Text)detail).text = text;
		}
	}
}
