using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;

namespace ProbablyTogether.UI;

/// Versus: the board (rank, cash minus debt, each rival's status) and the bedtime bar, over the player's own shop.
internal static class VersusPanel
{
	internal const string RootName = "bellun_probably_together_versus";

	private static RectTransform root;

	private static RectTransform boardPlate;

	private static TextMeshProUGUI boardTitle;

	private static TextMeshProUGUI boardRows;

	private static TextMeshProUGUI detailLabel;

	private static RectTransform bedPlate;

	private static TextMeshProUGUI bedTitle;

	private static TextMeshProUGUI bedNames;

	private static TextMeshProUGUI bedButtonLabel;

	private static Button bedButton;

	private static bool detail;

	private static float readAt;

	/// The board as last shown, for the rig.
	internal static string Text { get; private set; } = "";

	internal static string BedText { get; private set; } = "";

	internal static bool Visible => (Object)(object)root != (Object)null && ((Component)root).gameObject.activeSelf;

	private static bool Build()
	{
		if ((Object)(object)root != (Object)null)
		{
			return true;
		}
		CustomUIManager instance = CustomUIManager.Instance;
		RectTransform rectTransform = ((Object)(object)instance != (Object)null) ? instance.GetLayerRoot("overlay") : null;
		if ((Object)(object)rectTransform == (Object)null)
		{
			return false;
		}
		Transform val = ((Transform)rectTransform).Find(RootName);
		if ((Object)(object)val != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)val).gameObject);
		}
		root = Kit.Rect(RootName, (Transform)(object)rectTransform);
		Kit.Stretch(root);
		boardPlate = Kit.Plate((Transform)(object)root, "board", out var content, 0.9f, 10, 2f, raycast: true);
		Kit.Pin(boardPlate, new Vector2(1f, 1f), new Vector2(-16f, -96f));
		boardTitle = Kit.Label((Transform)(object)content, "title", 17f, Kit.Text, 0f, (TextAlignmentOptions)4097);
		boardRows = Kit.Label((Transform)(object)content, "rows", 15f, Kit.Text, 460f, (TextAlignmentOptions)4097);
		Kit.TextButton((Transform)(object)content, "detail", Lang.Tr("More"), 13f, delegate
		{
			detail = !detail;
			readAt = -10f;
		}, out detailLabel);
		bedPlate = Kit.Plate((Transform)(object)root, "bed", out var content2, 0.92f, 10, 3f, raycast: true);
		Kit.Pin(bedPlate, new Vector2(0.5f, 1f), new Vector2(0f, -16f));
		bedTitle = Kit.Label((Transform)(object)content2, "title", 18f, Kit.Text, 0f, (TextAlignmentOptions)4097);
		bedNames = Kit.Label((Transform)(object)content2, "names", 15f, Kit.Dim, 460f, (TextAlignmentOptions)4097);
		bedButton = Kit.TextButton((Transform)(object)content2, "button", Lang.Tr("Go to bed"), 16f, delegate
		{
			Versus.PressBed(!Versus.LocalReady);
		}, out bedButtonLabel);
		((Object)((Component)bedButton).gameObject).name = "bellun_probably_together_versus_bed";
		((Component)root).gameObject.SetActive(false);
		return true;
	}

	[SystemTick]
	private static void Tick()
	{
		if (Ui.Headless)
		{
			return;
		}
		bool flag = Net.Versus && Versus.Rules != null && Versus.ShopUp && Game.ShopLoaded;
		if (!flag)
		{
			if ((Object)(object)root != (Object)null && ((Component)root).gameObject.activeSelf)
			{
				((Component)root).gameObject.SetActive(false);
			}
			return;
		}
		if (Time.realtimeSinceStartup - readAt < 0.25f)
		{
			return;
		}
		readAt = Time.realtimeSinceStartup;
		try
		{
			if (!Build())
			{
				return;
			}
			if (!((Component)root).gameObject.activeSelf)
			{
				((Component)root).gameObject.SetActive(true);
			}
			((Transform)root).SetAsLastSibling();
			Refresh();
		}
		catch (Exception e)
		{
			UiErrors.Report("versus panel", e);
		}
	}

	private static void Refresh()
	{
		VsRules rules = Versus.Rules;
		IReadOnlyList<VsRow> board = Versus.Board;
		Kit.SetText(boardTitle, Versus.Ended ? Lang.Trf("{0}: final standings", Kit.Name(rules.name)) : Lang.Trf("{0}: day {1} of {2}", Kit.Name(rules.name), rules.lobbyDay.ToString(CultureInfo.InvariantCulture), rules.days.ToString(CultureInfo.InvariantCulture)));
		List<string> list = new List<string>();
		int num = 1;
		foreach (VsRow item in board)
		{
			string text = "<color=#" + Kit.Hex(Kit.PlayerColor(item.player)) + ">" + Kit.Name(item.name) + "</color>";
			string text2 = Money(item.Worth);
			string text3 = "<color=#" + Kit.Hex(Kit.Dim) + ">" + StateText(item) + "</color>";
			string text4 = num + ". " + text + "  " + text2 + "  " + text3;
			if (detail)
			{
				text4 = text4 + "\n    " + Lang.Trf("{0}, day {1}, cash {2}, debt {3}", SpecName(item.spec), item.day.ToString(CultureInfo.InvariantCulture), Money(item.cash), Money(item.debt)) + (item.InSync ? ("  <color=#" + Kit.Hex(Kit.Good) + ">" + Lang.Tr("copy ok") + "</color>") : "");
				if (item.rep != null && item.rep.Count > 0)
				{
					text4 = text4 + "\n    " + string.Join("  ", item.rep.OrderBy((KeyValuePair<string, int> k) => k.Key).Select((KeyValuePair<string, int> k) => FactionName(k.Key) + " " + k.Value.ToString(CultureInfo.InvariantCulture)));
				}
			}
			list.Add(text4);
			num++;
		}
		Kit.SetText(boardRows, string.Join("\n", list));
		Kit.SetText(detailLabel, Lang.Tr(detail ? "Less" : "More"));
		Text = Kit.Plain(((TMP_Text)boardTitle).text + " | " + string.Join(" | ", list));
		bool flag = !Versus.Ended && board.Any((VsRow r) => r.ready) || Versus.LocalReady || IsClosed();
		if (((Component)bedPlate).gameObject.activeSelf != flag)
		{
			((Component)bedPlate).gameObject.SetActive(flag);
		}
		if (flag)
		{
			List<VsRow> list2 = board.Where((VsRow r) => r.state != "offline" && r.state != "over" && r.state != "starting" && r.state != "loading").ToList();
			int secondsLeft = Versus.SecondsLeft;
			string text5 = ((secondsLeft >= 0) ? ("  " + Lang.Trf("{0} left", (secondsLeft / 60).ToString(CultureInfo.InvariantCulture) + ":" + (secondsLeft % 60).ToString("00", CultureInfo.InvariantCulture))) : "");
			Kit.SetText(bedTitle, Lang.Trf("Bedtime: {0}/{1} shops ready", list2.Count((VsRow r) => r.ready).ToString(CultureInfo.InvariantCulture), list2.Count.ToString(CultureInfo.InvariantCulture)) + text5);
			Kit.SetText(bedNames, string.Join("   ", list2.Select((VsRow r) => "<color=#" + Kit.Hex(Kit.PlayerColor(r.player)) + ">" + Kit.Name(r.name) + "</color> " + (r.ready ? ("<color=#" + Kit.Hex(Kit.Good) + ">" + Lang.Tr("ready") + "</color>") : ("<color=#" + Kit.Hex(Kit.Dim) + ">" + Lang.Tr("not yet") + "</color>")))));
			Kit.SetText(bedButtonLabel, Lang.Tr(Versus.LocalReady ? "Not yet" : "Go to bed"));
			((Selectable)bedButton).interactable = Versus.LocalReady || IsClosed();
			BedText = Kit.Plain(((TMP_Text)bedTitle).text + " | " + ((TMP_Text)bedNames).text);
		}
	}

	private static bool IsClosed()
	{
		PlayerStore store = Game.Store;
		return store != null && (int)store.storeState == 0 && !Versus.Ended;
	}

	private static string StateText(VsRow r)
	{
		switch (r.state)
		{
		case "morning":
			return Lang.Tr("morning");
		case "open":
			return Lang.Tr("open");
		case "closed":
			return r.ready ? Lang.Tr("ready for bed") : Lang.Tr("closed");
		case "out":
			return Lang.Tr("out tonight");
		case "night":
			return Lang.Tr("asleep");
		case "over":
			return Lang.Tr("run over");
		case "offline":
			return Lang.Tr("offline");
		default:
			return Lang.Tr("starting");
		}
	}

	private static string Money(int v)
	{
		return ((v < 0) ? "-$" : "$") + Math.Abs(v).ToString("N0", CultureInfo.InvariantCulture);
	}

	private static string SpecName(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return "?";
		}
		string text = id.Replace('_', ' ').ToLowerInvariant();
		return char.ToUpperInvariant(text[0]) + text.Substring(1);
	}

	private static string FactionName(string id)
	{
		string text = (id ?? "").Replace("FACTION_", "").Replace('_', ' ').ToLowerInvariant();
		if (text.Length == 0)
		{
			return "?";
		}
		return char.ToUpperInvariant(text[0]) + text.Substring(1);
	}
}
