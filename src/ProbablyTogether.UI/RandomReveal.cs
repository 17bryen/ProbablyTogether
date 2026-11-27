using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

/// The window that shows a player what was dealt to them at random, once they are in the shop.
internal static partial class HostPanels
{
	internal const string RevealWindow = "bellun_probably_together.random_reveal";

	private static float GuessIn(string text, float size, float width)
	{
		float num = width / (size * 0.56f) * 0.92f;
		int num2 = 0;
		foreach (string item in (text ?? "").Split('\n'))
		{
			num2 += Mathf.Max(1, Mathf.CeilToInt((float)item.Length / num));
		}
		return Mathf.Ceil((float)num2 * size * 1.36f);
	}

	private static Sprite TradeIcon(StartingScenario scenario)
	{
		Sprite sprite = null;
		try
		{
			sprite = ((Object)(object)IconProvider.Instance != (Object)null) ? IconProvider.Instance.GetIcon(scenario.id) : null;
			CustomUIManager instance = CustomUIManager.Instance;
			NewGameSpecializationFrame newGameSpecializationFrame = ((Object)(object)instance != (Object)null && (Object)(object)instance.newGameSpecializationPrefab != (Object)null) ? instance.newGameSpecializationPrefab.GetComponent<NewGameSpecializationFrame>() : null;
			if ((Object)(object)sprite == (Object)null && (Object)(object)newGameSpecializationFrame != (Object)null && !string.IsNullOrEmpty(scenario.iconKey))
			{
				sprite = newGameSpecializationFrame.FindIcon(scenario.iconKey);
			}
			if ((Object)(object)sprite == (Object)null && (Object)(object)instance != (Object)null && !string.IsNullOrEmpty(scenario.iconKey))
			{
				sprite = instance.GetSprite(scenario.iconKey);
			}
			if ((Object)(object)sprite == (Object)null && (Object)(object)newGameSpecializationFrame != (Object)null)
			{
				sprite = newGameSpecializationFrame.FindIcon("icons_" + scenario.id);
			}
			if ((Object)(object)sprite == (Object)null && (Object)(object)newGameSpecializationFrame != (Object)null)
			{
				sprite = newGameSpecializationFrame.FindIcon("icons_apprentice");
			}
			if ((Object)(object)sprite == (Object)null)
			{
				Log.Warn("random: no icon for " + scenario.id);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("random: the trade's icon threw: " + ex.Message);
		}
		return sprite;
	}

	private static string CostLabel(int cost)
	{
		string text = LocHelper.GetLocalizedPerkTable("perk_points");
		return ((cost > 0) ? ("-" + cost) : ((cost < 0) ? ("+" + -cost) : "0")) + " " + text;
	}

	private static TextMeshProUGUI Text(Panel p, Transform parent, string name, float x, float y, float width, float height, string text, float size, Color color, bool wrap, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontLoader.FontRole role = FontLoader.FontRole.Noto_Sans)
	{
		TextMeshProUGUI textMeshProUGUI = NewGameUIElements.NewText(p.Frame, parent, name, x, y, width, height, role, size, color, align, wrap);
		textMeshProUGUI.text = text ?? "";
		return textMeshProUGUI;
	}

	private const float RowsTop = 130f;

	private const float BarRoom = 30f;

	private static ScrollRect revealScroll;

	/// Lab: scrolls the reveal (1 top, 0 bottom).
	internal static void LabScrollReveal(float position)
	{
		if ((Object)(object)revealScroll != (Object)null)
		{
			revealScroll.verticalNormalizedPosition = position;
		}
	}

	/// A thin track and a framed handle, in the colours of the game's credits scroll bar.
	private static Scrollbar RevealBar(Panel p, float height)
	{
		float num = Left + Width - BarRoom + 12f;
		NewGameUIElements.NewImage(p.Body, "rows_track", num + 6f, RowsTop, 2f, height, null, NewGameStyle.CREDITS_SCROLL_TRACK);
		RectTransform rectTransform = NewGameUIElements.NewRect(p.Body, "rows_bar", num, RowsTop, 14f, height);
		RectTransform rectTransform2 = NewGameUIElements.NewRect(rectTransform, "area", 0f, 0f, 14f, height);
		rectTransform2.anchorMin = Vector2.zero;
		rectTransform2.anchorMax = Vector2.one;
		rectTransform2.offsetMin = Vector2.zero;
		rectTransform2.offsetMax = Vector2.zero;
		RectTransform rectTransform3 = NewGameUIElements.NewRect(rectTransform2, "handle", 0f, 0f, 14f, 40f);
		rectTransform3.anchorMin = Vector2.zero;
		rectTransform3.anchorMax = Vector2.one;
		rectTransform3.offsetMin = Vector2.zero;
		rectTransform3.offsetMax = Vector2.zero;
		Image image = rectTransform3.gameObject.AddComponent<Image>();
		image.color = NewGameStyle.CREDITS_SCROLL_HANDLE_BG;
		Image image2 = NewGameUIElements.NewImage(rectTransform3, "border", 0f, 0f, 14f, 40f, p.Frame.frame2Square, NewGameStyle.CREDITS_SCROLL_HANDLE_BORDER);
		image2.type = Image.Type.Sliced;
		image2.rectTransform.anchorMin = Vector2.zero;
		image2.rectTransform.anchorMax = Vector2.one;
		image2.rectTransform.offsetMin = Vector2.zero;
		image2.rectTransform.offsetMax = Vector2.zero;
		Scrollbar scrollbar = rectTransform.gameObject.AddComponent<Scrollbar>();
		scrollbar.handleRect = rectTransform3;
		scrollbar.targetGraphic = image;
		scrollbar.direction = Scrollbar.Direction.BottomToTop;
		return scrollbar;
	}

	/// The trade's and the perks' rows from the top of `rows`, `width` wide; returns their height.
	private static float BuildRows(Panel p, RectTransform rows, string trade, IList<string> perks, List<StartingPerk> list, float width)
	{
		const float Icon = 64f;
		const float Gap = 20f;
		const float num = 18f;
		float textX = Left + Icon + Gap;
		float textW = width - Icon - Gap;
		float y = 10f;
		StartingScenario startingScenario = null;
		if (Enum.TryParse<NewGameData.StartType>(trade ?? "", ignoreCase: true, out var result))
		{
			startingScenario = StartingScenario.GetByStartType(result);
		}
		if (startingScenario != null)
		{
			Text(p, rows, "tradehead", Left, y, width, 26f, Lang.Tr("Your trade"), 20f, NewGameStyle.DIFF_OPTION_LABEL, wrap: false, TextAlignmentOptions.MidlineLeft);
			y += 42f;
			Sprite sprite = TradeIcon(startingScenario);
			if ((Object)(object)sprite != (Object)null)
			{
				// The trade icons are dark: on the slot the game's trade strip draws them on.
				CustomUIManager instance = CustomUIManager.Instance;
				NewGameSpecializationFrame newGameSpecializationFrame = ((Object)(object)instance != (Object)null && (Object)(object)instance.newGameSpecializationPrefab != (Object)null) ? instance.newGameSpecializationPrefab.GetComponent<NewGameSpecializationFrame>() : null;
				if ((Object)(object)newGameSpecializationFrame != (Object)null && (Object)(object)newGameSpecializationFrame.slotSelected != (Object)null)
				{
					NewGameUIElements.NewImage(rows, "tradeslot", Left, y, Icon, Icon, newGameSpecializationFrame.slotSelected, Color.white);
				}
				Image image = NewGameUIElements.NewImage(rows, "tradeicon", Left, y, Icon, Icon, sprite, Color.white);
				NewGameUIElements.FitPixelArt(image, Left, y, Icon, 38f);
			}
			Text(p, rows, "tradename", textX, y, textW, 34f, startingScenario.GetTitle(), 28f, NewGameStyle.TITLE, wrap: false, TextAlignmentOptions.MidlineLeft);
			string tag = startingScenario.GetTag();
			if (!string.IsNullOrEmpty(tag))
			{
				Text(p, rows, "tradetag", textX, y + 34f, textW, 26f, tag, 18f, NewGameStyle.TagColor(startingScenario.tagTone), wrap: false, TextAlignmentOptions.MidlineLeft);
			}
			string description = startingScenario.GetDescription() ?? "";
			float num2 = GuessIn(description, num, textW);
			Text(p, rows, "tradedesc", textX, y + 64f, textW, num2, description, num, NewGameStyle.DESCRIPTION, wrap: true);
			y += Mathf.Max(Icon, 64f + num2) + 28f;
		}
		else if (trade != null)
		{
			Text(p, rows, "tradename", Left, y, width, 34f, Lang.Trf("Your trade: {0}", Specs.Label(trade)), 24f, NewGameStyle.TITLE, wrap: false, TextAlignmentOptions.MidlineLeft);
			y += 50f;
		}
		if (perks != null)
		{
			if (startingScenario != null || trade != null)
			{
				NewGameUIElements.NewImage(rows, "rule", Left, y - 14f, width, 2f, null, NewGameStyle.SEPARATOR);
			}
			Text(p, rows, "perkshead", Left, y, width, 26f, (list.Count > 0) ? Lang.Trf("Your perks ({0})", CostLabel(Perks.Points(list.Select((StartingPerk d) => d.id)))) : Lang.Tr("Your perks"), 20f, NewGameStyle.DIFF_OPTION_LABEL, wrap: false, TextAlignmentOptions.MidlineLeft);
			y += 42f;
			if (list.Count == 0)
			{
				Text(p, rows, "noperks", Left, y, width, 30f, Lang.Tr("None: the dice dealt you no perks."), 20f, NewGameStyle.DESCRIPTION, wrap: false, TextAlignmentOptions.MidlineLeft);
				y += 40f;
			}
			for (int i = 0; i < list.Count; i++)
			{
				StartingPerk startingPerk = list[i];
				Sprite icon = StartingPerkIconLoader.GetIcon(startingPerk);
				if ((Object)(object)icon != (Object)null)
				{
					Image image2 = NewGameUIElements.NewImage(rows, "perkicon" + i, Left, y, Icon, Icon, icon, NewGameStyle.PerkColor(startingPerk.type));
					NewGameUIElements.FitPixelArt(image2, Left, y, Icon, Icon);
				}
				Text(p, rows, "perkname" + i, textX, y, textW - 220f, 28f, startingPerk.GetLocalizedDisplayName(), 22f, NewGameStyle.PERK_TOOLTIP_NAME, wrap: false, TextAlignmentOptions.MidlineLeft);
				Text(p, rows, "perkcost" + i, textX + textW - 220f, y, 220f, 28f, CostLabel(startingPerk.cost), 18f, NewGameStyle.PERK_TOOLTIP_COST, wrap: false, TextAlignmentOptions.MidlineRight);
				string text = string.Join(" ", (startingPerk.GetLocalizedDescription() ?? "").Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select((string s) => s.Trim()));
				float num3 = GuessIn(text, num, textW);
				Text(p, rows, "perkdesc" + i, textX, y + 30f, textW, num3, text, num, NewGameStyle.DESCRIPTION, wrap: true);
				y += Mathf.Max(Icon, 30f + num3) + 14f;
			}
		}
		return y;
	}

	/// Only what was dealt: the trade, the perks, or both. One OK button.
	internal static void OpenReveal(string trade, IList<string> perks, Action onClosed, string layer)
	{
		CloseMessage(RevealWindow);
		Panel p = Open(RevealWindow, Lang.Tr("Dealt at Random"), delegate
		{
			CloseMessage(RevealWindow);
		}, layer);
		messages[RevealWindow] = p;
		List<StartingPerk> list = (perks ?? new string[0]).Select(Perks.Def).Where((StartingPerk d) => d != null).ToList();
		// The rows scroll when they are longer than the space over the line above OK.
		float num5 = Mathf.Max(200f, ContinueRect(p).y - 34f - RowsTop);
		RectTransform rectTransform = NewGameUIElements.NewRect(p.Body, "rows_view", 0f, RowsTop, p.Body.rect.width, num5);
		Image image0 = rectTransform.gameObject.AddComponent<Image>();
		image0.color = new Color(0f, 0f, 0f, 0f);
		image0.raycastTarget = true;
		rectTransform.gameObject.AddComponent<RectMask2D>();
		RectTransform rows = NewGameUIElements.NewRect(rectTransform, "rows", 0f, 0f, p.Body.rect.width, num5);
		float y = BuildRows(p, rows, trade, perks, list, Width);
		if (y + 6f > num5)
		{
			// Scrolls: the rows make room for the bar, inside the panel's frame.
			for (int i = rows.childCount - 1; i >= 0; i--)
			{
				Object.DestroyImmediate(rows.GetChild(i).gameObject);
			}
			y = BuildRows(p, rows, trade, perks, list, Width - BarRoom);
		}
		float num6 = y + 6f;
		rows.sizeDelta = new Vector2(rows.sizeDelta.x, Mathf.Max(num5, num6));
		ScrollRect scrollRect = rectTransform.gameObject.AddComponent<ScrollRect>();
		scrollRect.viewport = rectTransform;
		scrollRect.content = rows;
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		scrollRect.inertia = false;
		scrollRect.scrollSensitivity = 40f;
		revealScroll = scrollRect;
		if (num6 > num5)
		{
			scrollRect.verticalScrollbar = RevealBar(p, num5);
			scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
		}
		scrollRect.verticalNormalizedPosition = 1f;
		Buttons(p, RevealWindow, new Choice[1] { new Choice(Lang.Tr("OK")) }, 0);
		p.Window.onClose = delegate
		{
			if (messages.TryGetValue(RevealWindow, out var value) && value == p)
			{
				messages.Remove(RevealWindow);
			}
			onClosed?.Invoke();
		};
		p.Window.Show();
	}
}

/// Opens the reveal once this copy's player is in the shop with nothing else in front of it.
internal static class RandomReveal
{
	private static float nextLook;

	internal static int Opened;

	internal static string LastShown = "";

	[SystemTick]
	private static void Tick()
	{
		if ((RandomStarts.ShowTrade == null && RandomStarts.ShowPerks == null) || (Ui.Headless && !PerkScreens.LabOn))
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (realtimeSinceStartup < nextLook)
		{
			return;
		}
		nextLook = realtimeSinceStartup + 0.5f;
		string showTrade = RandomStarts.ShowTrade;
		List<string> showPerks = RandomStarts.ShowPerks;
		try
		{
			if (!Net.IsOnline || !Net.IsLive || !Game.ShopLoaded || Snapshot.IsLoading || PerkScreens.Covering || Repick.Mine.Length > 0 || !HostPanels.Available || HostPanels.MessageOpen(HostPanels.RevealWindow) || LoadingScreen.Visible)
			{
				return;
			}
			RandomStarts.Showed();
			HostPanels.OpenReveal(showTrade, showPerks, null, "meta");
			Opened++;
			LastShown = (showTrade ?? "-") + "; " + ((showPerks != null) ? ("[" + string.Join(", ", showPerks) + "]") : "-");
			Log.Info("random: the reveal is up: " + LastShown);
		}
		catch (Exception ex)
		{
			RandomStarts.Showed();
			Log.Warn("random: the reveal could not open: " + ex);
			if (showTrade != null)
			{
				Notices.Show(Lang.Trf("You were dealt a trade: {0}.", Specs.Label(showTrade)), "spec");
			}
			if (showPerks != null)
			{
				Notices.Show(Lang.Trf("You were dealt these perks: {0}.", (showPerks.Count > 0) ? string.Join(", ", showPerks.Select(Perks.NameOfPerk)) : Lang.Tr("none")), "perks");
			}
		}
	}
}
