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

	private static TextMeshProUGUI Text(Panel p, string name, float x, float y, float width, float height, string text, float size, Color color, bool wrap, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontLoader.FontRole role = FontLoader.FontRole.Noto_Sans)
	{
		TextMeshProUGUI textMeshProUGUI = NewGameUIElements.NewText(p.Frame, p.Body, name, x, y, width, height, role, size, color, align, wrap);
		textMeshProUGUI.text = text ?? "";
		return textMeshProUGUI;
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
		const float Icon = 64f;
		const float Gap = 20f;
		float textX = Left + Icon + Gap;
		float textW = Width - Icon - Gap;
		List<StartingPerk> list = (perks ?? new string[0]).Select(Perks.Def).Where((StartingPerk d) => d != null).ToList();
		// Many perks: smaller descriptions so the window holds them all.
		float num = (list.Count > 4 || (trade != null && list.Count > 3)) ? 16f : 18f;
		float y = 140f;
		StartingScenario startingScenario = null;
		if (Enum.TryParse<NewGameData.StartType>(trade ?? "", ignoreCase: true, out var result))
		{
			startingScenario = StartingScenario.GetByStartType(result);
		}
		if (startingScenario != null)
		{
			Text(p, "tradehead", Left, y, Width, 26f, Lang.Tr("Your trade"), 20f, NewGameStyle.DIFF_OPTION_LABEL, wrap: false, TextAlignmentOptions.MidlineLeft);
			y += 42f;
			Sprite sprite = TradeIcon(startingScenario);
			if ((Object)(object)sprite != (Object)null)
			{
				// The trade icons are dark: on the slot the game's trade strip draws them on.
				CustomUIManager instance = CustomUIManager.Instance;
				NewGameSpecializationFrame newGameSpecializationFrame = ((Object)(object)instance != (Object)null && (Object)(object)instance.newGameSpecializationPrefab != (Object)null) ? instance.newGameSpecializationPrefab.GetComponent<NewGameSpecializationFrame>() : null;
				if ((Object)(object)newGameSpecializationFrame != (Object)null && (Object)(object)newGameSpecializationFrame.slotSelected != (Object)null)
				{
					NewGameUIElements.NewImage(p.Body, "tradeslot", Left, y, Icon, Icon, newGameSpecializationFrame.slotSelected, Color.white);
				}
				Image image = NewGameUIElements.NewImage(p.Body, "tradeicon", Left, y, Icon, Icon, sprite, Color.white);
				NewGameUIElements.FitPixelArt(image, Left, y, Icon, 38f);
			}
			Text(p, "tradename", textX, y, textW, 34f, startingScenario.GetTitle(), 28f, NewGameStyle.TITLE, wrap: false, TextAlignmentOptions.MidlineLeft);
			string tag = startingScenario.GetTag();
			if (!string.IsNullOrEmpty(tag))
			{
				Text(p, "tradetag", textX, y + 34f, textW, 26f, tag, 18f, NewGameStyle.TagColor(startingScenario.tagTone), wrap: false, TextAlignmentOptions.MidlineLeft);
			}
			string description = startingScenario.GetDescription() ?? "";
			float num2 = GuessIn(description, num, textW);
			Text(p, "tradedesc", textX, y + 64f, textW, num2, description, num, NewGameStyle.DESCRIPTION, wrap: true);
			y += Mathf.Max(Icon, 64f + num2) + 28f;
		}
		else if (trade != null)
		{
			Text(p, "tradename", Left, y, Width, 34f, Lang.Trf("Your trade: {0}", Specs.Label(trade)), 24f, NewGameStyle.TITLE, wrap: false, TextAlignmentOptions.MidlineLeft);
			y += 50f;
		}
		if (perks != null)
		{
			if (startingScenario != null || trade != null)
			{
				NewGameUIElements.NewImage(p.Body, "rule", Left, y - 14f, Width, 2f, null, NewGameStyle.SEPARATOR);
			}
			Text(p, "perkshead", Left, y, Width, 26f, (list.Count > 0) ? Lang.Trf("Your perks ({0})", CostLabel(Perks.Points(list.Select((StartingPerk d) => d.id)))) : Lang.Tr("Your perks"), 20f, NewGameStyle.DIFF_OPTION_LABEL, wrap: false, TextAlignmentOptions.MidlineLeft);
			y += 42f;
			if (list.Count == 0)
			{
				Text(p, "noperks", Left, y, Width, 30f, Lang.Tr("None: the dice dealt you no perks."), 20f, NewGameStyle.DESCRIPTION, wrap: false, TextAlignmentOptions.MidlineLeft);
				y += 40f;
			}
			for (int i = 0; i < list.Count; i++)
			{
				StartingPerk startingPerk = list[i];
				Sprite icon = StartingPerkIconLoader.GetIcon(startingPerk);
				if ((Object)(object)icon != (Object)null)
				{
					Image image2 = NewGameUIElements.NewImage(p.Body, "perkicon" + i, Left, y, Icon, Icon, icon, NewGameStyle.PerkColor(startingPerk.type));
					NewGameUIElements.FitPixelArt(image2, Left, y, Icon, Icon);
				}
				Text(p, "perkname" + i, textX, y, textW - 220f, 28f, startingPerk.GetLocalizedDisplayName(), 22f, NewGameStyle.PERK_TOOLTIP_NAME, wrap: false, TextAlignmentOptions.MidlineLeft);
				Text(p, "perkcost" + i, textX + textW - 220f, y, 220f, 28f, CostLabel(startingPerk.cost), 18f, NewGameStyle.PERK_TOOLTIP_COST, wrap: false, TextAlignmentOptions.MidlineRight);
				string text = string.Join(" ", (startingPerk.GetLocalizedDescription() ?? "").Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select((string s) => s.Trim()));
				float num3 = GuessIn(text, num, textW);
				Text(p, "perkdesc" + i, textX, y + 30f, textW, num3, text, num, NewGameStyle.DESCRIPTION, wrap: true);
				y += Mathf.Max(Icon, 30f + num3) + 14f;
			}
		}
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
