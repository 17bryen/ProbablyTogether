using System.Linq;
using ProbablyTogether.UI;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class UiLeave : BotScript
{
	public override string Name => "ui-leave";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 120f;

	protected override void Build()
	{
		WaitFor("in-shop", 60f, () => Ui.ShopOnline, () => "state " + Net.ClientState);
		Step("left-line", 20f, delegate(BotStep st)
		{
			if (Net.Players.Count >= 2)
			{
				st.Detail = "the other player is still here";
				return StepStatus.Pass;
			}
			bool flag = Chat.Lines.Any((Chat.Line l) => l.N == 0 && l.Text.EndsWith("left the shop."));
			st.Detail = (flag ? ("saw '" + Chat.Lines.Last((Chat.Line l) => l.N == 0 && l.Text.EndsWith("left the shop.")).Text + "'") : UiBot.Lines());
			return flag ? StepStatus.Pass : StepStatus.Running;
		});
		Do("main-menu", delegate
		{
			EscapeUIManager escapeUIManager = StoreUIManager.Instance.escapeUIManager;
			escapeUIManager.OpenEscapeMenu();
			escapeUIManager.OnMainMenu();
			return "escape menu > Main menu";
		});
		Check("confirm-shown", () => Net.IsClient && StoreUIManager.Instance.confirmPanelUIManager.panel.activeSelf && ((TMP_Text)StoreUIManager.Instance.confirmPanelUIManager.bodyTMP).text.StartsWith("Leave "), () => "still connected " + Net.IsClient + ", panel '" + ((TMP_Text)StoreUIManager.Instance.confirmPanelUIManager.bodyTMP).text + "'");
		Do("confirm-yes", delegate
		{
			StoreUIManager.Instance.confirmPanelUIManager.OnYesClick();
			return "yes";
		});
		WaitFor("at-menu", 60f, () => Net.Role == ProbablyTogether.Role.Off && Ui.AtMenu && (Object)(object)MainMenuUIController.Instance != (Object)null, () => "role " + Net.Role.ToString() + ", scene " + BotProbes.Scene);
		WaitFor("join-button", 20f, () => (Object)(object)JoinMenu.Button != (Object)null, () => "fallback " + JoinMenu.UsingFallbackButton);
		Delay("settle", 2f);
		Check("no-notice", () => (Object)(object)CustomUIManager.Instance != (Object)null && !CustomUIManager.Instance.IsOpen("bellun_probably_together.notice"), () => "last message '" + JoinMenu.LastMessage + "'");
	}
}
