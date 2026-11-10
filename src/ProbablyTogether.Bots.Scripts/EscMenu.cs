using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.UI;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class EscMenu : EscBase
{
	public override string Name => "esc-menu";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 300f;

	protected override void Build()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("shop-live", 240f, () => Ui.ShopOnline && (Object)(object)EscBase.Esc != (Object)null, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		Delay("settle", 3f);
		Do("lang-start", delegate
		{
			Lang.LabOverride = null;
			Lang.ForceRefresh();
			return "our texts follow '" + Lang.Code + "'";
		});
		Check("english-first", () => Lang.Code == "en", () => "language '" + Lang.Code + "' (the lab copy should start in English)").Soft();
		Open("open");
		Do("dump", (Func<string>)EscBase.Dump);
		Check("invite-shown", () => EscapeMenu.InviteShown && EscapeMenu.InviteLabel == "Copy invite", () => "shown " + EscapeMenu.InviteShown + ", label '" + EscapeMenu.InviteLabel + "'; " + EscapeMenu.Layout);
		Check("invite-above-main", delegate
		{
			List<(string, Rect, string)> list = EscBase.Rows();
			int num = list.FindIndex(((string name, Rect r, string label) x) => x.name == "bellun_probably_together_invite");
			if (num >= 0 && num + 1 < list.Count && list[num + 1].Item3 == EscapeMenu.MainMenuLabel)
			{
				(string, Rect, string) tuple = list[num + 1];
				float yMax = tuple.Item2.yMax;
				tuple = list[num];
				return yMax <= tuple.Item2.yMin + 1f;
			}
			return false;
		}, () => string.Join(" / ", from x in EscBase.Rows()
			select x.name + " '" + x.label + "' " + x.r.yMin.ToString("F0") + ".." + x.r.yMax.ToString("F0")));
		Check("no-overlap", () => EscBase.Overlaps().Length == 0, () => "overlapping rows: " + EscBase.Overlaps());
		Check("leave-label", () => EscapeMenu.MainMenuLabel == "Leave server", () => "main menu label '" + EscapeMenu.MainMenuLabel + "'");
		Check("footer", () => string.Equals(EscapeMenu.FooterLabel, "The server saves the shop after each night.", StringComparison.OrdinalIgnoreCase), () => "footer '" + EscapeMenu.FooterLabel + "'");
		Check("footer-is-the-line", () => EscBase.FooterOk(), () => EscBase.FooterWhy());
		Check("box-fits", () => EscBase.Outside().Length == 0, () => "outside the box: " + EscBase.Outside() + "; " + EscapeMenu.Layout + ", grown " + EscapeMenu.Grown.ToString("F0"));
		int copies0 = 0;
		int lines0 = 0;
		Do("click-invite", delegate
		{
			copies0 = EscapeMenu.Copies;
			lines0 = Chat.Lines.Count;
			((UnityEvent)EscBase.Esc.menuPanel.GetComponentsInChildren<Button>(true).First((Button x) => ((Object)((Component)x).gameObject).name == "bellun_probably_together_invite").onClick).Invoke();
			return "clicked Copy invite";
		});
		Check("copied", () => EscapeMenu.Copies == copies0 + 1 && EscapeMenu.LastCopied == Net.ServerAddress && Net.ServerAddress.Length > 0, () => "copies " + EscapeMenu.Copies + ", copied " + EscapeMenu.LastCopied.Length + " chars, address " + Net.ServerAddress.Length + " chars");
		Check("clipboard", () => GUIUtility.systemCopyBuffer == Net.ServerAddress, () => "clipboard " + (GUIUtility.systemCopyBuffer ?? "").Length + " chars (a lab copy without a desktop may have no clipboard)").Soft();
		WaitFor("copied-line", 5f, () => Chat.Lines.Count > lines0 && Chat.Lines.Last().Text.StartsWith("Server address copied", StringComparison.Ordinal), () => Chat.Lines.Count + " line(s), last '" + ((Chat.Lines.Count > 0) ? Chat.Lines.Last().Text : "") + "'");
		Check("address-never-shown", () => Chat.Lines.All((Chat.Line l) => Net.ServerAddress.Length == 0 || !l.Text.Contains(Net.ServerAddress)), () => Chat.Lines.Count((Chat.Line l) => Net.ServerAddress.Length > 0 && l.Text.Contains(Net.ServerAddress)) + " of " + Chat.Lines.Count + " chat line(s) show the address");
		Close("close");
		Do("ru", delegate
		{
			Lang.LabOverride = "ru";
			Lang.ForceRefresh();
			return "override ru: code '" + Lang.Code + "', version " + Lang.Version;
		});
		Check("ru-words", () => Lang.Tr("Join server") == "Подключиться" && Lang.Tr("Leave server") == "Покинуть сервер", () => "'" + Lang.Tr("Join server") + "' / '" + Lang.Tr("Leave server") + "'");
		Check("ru-template", () => Lang.Tr("Mara joined the shop.") == "Mara зашёл в магазин." && Lang.Trf("Day {0}", 3) == "День 3" && Lang.Tr("Someone else moved that item first.") == "Кто-то другой уже переместил этот предмет.", () => "'" + Lang.Tr("Mara joined the shop.") + "' / '" + Lang.Trf("Day {0}", 3) + "' / '" + Lang.Tr("Someone else moved that item first.") + "'");
		Check("ru-fallback", () => Lang.Tr("Lab: a text with no entry.") == "Lab: a text with no entry." && Lang.Tr("Theo") == "Theo", () => "'" + Lang.Tr("Lab: a text with no entry.") + "'");
		Check("ru-names-stay", () => Lang.Tr("Ready joined the shop.") == "Ready зашёл в магазин." || Lang.Tr("Ready joined the shop.") == "Готово зашёл в магазин.", () => "'" + Lang.Tr("Ready joined the shop.") + "' (a name that is also a word)").Soft();
		Open("open-ru");
		Check("ru-menu", () => EscapeMenu.InviteLabel == "Копировать приглашение" && EscapeMenu.MainMenuLabel == "Покинуть сервер" && string.Equals(EscapeMenu.FooterLabel, "Магазин сохраняется каждую ночь.", StringComparison.OrdinalIgnoreCase), () => "'" + EscapeMenu.InviteLabel + "' / '" + EscapeMenu.MainMenuLabel + "' / '" + EscapeMenu.FooterLabel + "'");
		Check("ru-no-overlap", () => EscBase.Overlaps().Length == 0, () => "overlapping rows: " + EscBase.Overlaps());
		Check("ru-box-fits", () => EscBase.Outside().Length == 0, () => "outside the box: " + EscBase.Outside());
		Do("dump-ru", (Func<string>)EscBase.Dump);
		Close("close-ru");
		Do("chat-ru", delegate
		{
			Chat.Local("Voice chat was turned off on this server.");
			return "local line '" + Chat.Lines.Last().Text + "'";
		});
		Check("chat-ru-line", () => Chat.Lines.Last().Text == "На этом сервере голосовой чат выключен.", () => "'" + Chat.Lines.Last().Text + "'");
		Do("zh", delegate
		{
			Lang.LabOverride = "zh";
			Lang.ForceRefresh();
			return "override zh: code '" + Lang.Code + "'";
		});
		Open("open-zh");
		Check("zh-menu", () => EscapeMenu.MainMenuLabel == "离开服务器" && EscapeMenu.InviteLabel == "复制邀请" && EscapeMenu.FooterLabel == "服务器会在每晚结束后保存店铺。", () => "'" + EscapeMenu.InviteLabel + "' / '" + EscapeMenu.MainMenuLabel + "' / '" + EscapeMenu.FooterLabel + "'");
		Check("zh-font", () => (from t in EscBase.Esc.menuPanel.GetComponentsInChildren<TextMeshProUGUI>(true)
			where ((Component)t).gameObject.activeInHierarchy && ((TMP_Text)t).text.Any((char ch) => ch > '⺀')
			select t).All((TextMeshProUGUI t) => (Object)(object)((TMP_Text)t).font != (Object)null && ((TMP_Text)t).text.All((char ch) => ch < '\u0080' || ((TMP_Text)t).font.HasCharacter(ch, true, true))), () => string.Join(" / ", from t in EscBase.Esc.menuPanel.GetComponentsInChildren<TextMeshProUGUI>(true)
			where ((Component)t).gameObject.activeInHierarchy
			select "'" + ((TMP_Text)t).text + "' " + (((Object)(object)((TMP_Text)t).font != (Object)null) ? ((Object)((TMP_Text)t).font).name : "-")));
		Check("zh-no-overlap", () => EscBase.Overlaps().Length == 0, () => "overlapping rows: " + EscBase.Overlaps());
		Close("close-zh");
		Do("de", delegate
		{
			Lang.LabOverride = "de";
			Lang.ForceRefresh();
			return "override de: code '" + Lang.Code + "'";
		});
		Check("de-english", () => Lang.Tr("Leave server") == "Leave server" && Lang.Trf("Day {0}", 3) == "Day 3", () => "'" + Lang.Tr("Leave server") + "'");
		Do("english-back", delegate
		{
			Lang.LabOverride = null;
			return "override off";
		});
		Do("english-read", delegate
		{
			Lang.ForceRefresh();
			return "our texts follow '" + Lang.Code + "'";
		});
		Open("open-en");
		Check("en-menu", () => EscapeMenu.MainMenuLabel == "Leave server" && EscapeMenu.InviteLabel == "Copy invite", () => "'" + EscapeMenu.InviteLabel + "' / '" + EscapeMenu.MainMenuLabel + "'");
		Close("close-en");
	}
}
