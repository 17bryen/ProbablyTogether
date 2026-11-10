using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class UiNotice : BotScript
{
	public override string Name => "ui-notice";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 500f;

	protected override void Build()
	{
		WaitFor("live", 180f, () => Ui.ShopOnline, () => "state " + Net.ClientState);
		Do("waiting", () => "in the shop; stop the server now");
		WaitFor("dropped", 300f, () => Net.Role == ProbablyTogether.Role.Off, () => "role " + Net.Role);
		WaitFor("at-menu", 60f, () => Ui.AtMenu && (Object)(object)MainMenuUIController.Instance != (Object)null, () => "scene " + BotProbes.Scene);
		WaitFor("notice", 20f, () => (Object)(object)CustomUIManager.Instance != (Object)null && CustomUIManager.Instance.IsOpen("humangenome_accomplice.notice"), () => "last message '" + JoinMenu.LastMessage + "', reason '" + Net.LastDisconnectReason + "'");
		Check("plain", () => JoinMenu.LastMessage.Length > 0 && !JoinMenu.LastMessage.Contains("refused ("), delegate
		{
			string[] obj = new string[5] { "notice '", null, null, null, null };
			CustomUIWindow window = CustomUIManager.Instance.GetWindow("humangenome_accomplice.notice");
			object obj2;
			if (window == null)
			{
				obj2 = null;
			}
			else
			{
				CustomUIElement obj3 = window.Get("reason");
				obj2 = ((obj3 != null) ? obj3.GetText() : null);
			}
			obj[1] = (string)obj2;
			obj[2] = "' from reason '";
			obj[3] = Net.LastDisconnectReason;
			obj[4] = "'";
			return string.Concat(obj);
		});
	}
}
