using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

/// Host mode: the host can step out without closing the shop and rejoin later, through the game's modal confirm panel.
internal static class HostAwayUi
{
	private static string yesOriginal;

	private static string noOriginal;

	private static bool labelsSet;

	private static bool confirming;

	private static ConfirmPanelUIManager Panel => ((Object)(object)StoreUIManager.Instance != (Object)null) ? StoreUIManager.Instance.confirmPanelUIManager : null;

	/// The host pressed "Leave server": first offer to keep the shop open.
	internal static void AskLeave(ConfirmPanelUIManager p)
	{
		try
		{
			CustomUIManager instance = CustomUIManager.Instance;
			if (HostPanels.Available)
			{
				try
				{
					// The game's own panel, on the layer the plain window was on.
					HostPanels.OpenMessage("bellun_probably_together.leave", Lang.Tr("Leaving your shop"), new string[1] { Lang.Tr("You are hosting this shop. Step away and the others keep playing (you can rejoin at any time), or close the shop and everyone is disconnected.") }, -1, new HostPanels.Choice[3]
					{
						// Ordered by how much each does.
						new HostPanels.Choice(Lang.Tr("Keep playing")),
						new HostPanels.Choice(Lang.Tr("Step away"), StepOut),
						new HostPanels.Choice(Lang.Tr("Close the shop"), AskStop)
					}, null, "meta");
					return;
				}
				catch (Exception ex2)
				{
					Log.Warn("host: the game's panel could not be used for the leave window: " + ex2.Message);
				}
			}
			if ((Object)(object)instance != (Object)null)
			{
				CustomUIBuilder val = instance.CreateWindow("bellun_probably_together.leave", Lang.Tr("Leaving your shop"), "meta", (GameObject)null, (CustomUIManager.Style)0).SetCloseOnEscape(true).SetSize(620f, 330f);
				val.AddLabel(Lang.Tr("You are hosting this shop. Step away and the others keep playing (you can rejoin at any time), or close the server and everyone is disconnected."), (string)null)
					.AddButton(Lang.Tr("Step away (the shop stays open)"), (Action)delegate
					{
						CloseLeave();
						StepOut();
					}, "stepaway")
					.AddButton(Lang.Tr("Close the server"), (Action)delegate
					{
						CloseLeave();
						AskStop();
					}, "close")
					.AddButton(Lang.Tr("Keep playing"), (Action)CloseLeave, "cancel");
				val.Center();
				val.Show();
				return;
			}
		}
		catch (Exception ex)
		{
			Log.Warn("host: the leave window did not open (" + ex.Message + "); using the yes/no panel");
		}
		p.OpenPanel(Lang.Tr("Step out and keep the shop open? The other players keep playing and you can rejoin at any time. Choose No to stop hosting instead."), (Action)StepOut, (Action)AskStop, false);
	}

	private static void CloseLeave()
	{
		HostPanels.CloseMessage("bellun_probably_together.leave");
		try
		{
			CustomUIManager instance = CustomUIManager.Instance;
			if (instance != null)
			{
				instance.CloseWindow("bellun_probably_together.leave");
			}
		}
		catch
		{
		}
	}

	private static void StepOut()
	{
		if (ServerMode.HostStepOut())
		{
			Show();
		}
	}

	private static void AskStop()
	{
		ConfirmPanelUIManager p = Panel;
		if ((Object)(object)p == (Object)null)
		{
			ServerMode.HostLeave();
			return;
		}
		confirming = true;
		RestoreLabels();
		p.OpenPanel(Lang.Tr("Stop hosting? The shop is saved as of this morning and the other players are disconnected."), (Action)delegate
		{
			confirming = false;
			ServerMode.HostLeave();
		}, (Action)delegate
		{
			confirming = false;
			if (Net.HostAway)
			{
				Show();
			}
		}, false);
	}

	internal static void Show()
	{
		if (!Net.HostAway)
		{
			return;
		}
		ConfirmPanelUIManager p = Panel;
		if ((Object)(object)p == (Object)null)
		{
			return;
		}
		try
		{
			if (!labelsSet)
			{
				yesOriginal = ((Object)(object)p.yesTMP != (Object)null) ? p.yesTMP.text : null;
				noOriginal = ((Object)(object)p.noTMP != (Object)null) ? p.noTMP.text : null;
				labelsSet = true;
			}
			p.OpenPanel(Lang.Tr("You stepped out. The shop stays open and the other players keep playing."), (Action)Rejoin, (Action)AskStop, false);
			if ((Object)(object)p.yesTMP != (Object)null)
			{
				p.yesTMP.text = Lang.Tr("Rejoin");
			}
			if ((Object)(object)p.noTMP != (Object)null)
			{
				p.noTMP.text = Lang.Tr("Stop hosting");
			}
		}
		catch (Exception ex)
		{
			Log.Warn("host: away panel: " + ex.Message);
		}
	}

	private static void Rejoin()
	{
		RestoreLabels();
		ServerMode.HostRejoin();
	}

	private static void RestoreLabels()
	{
		if (!labelsSet)
		{
			return;
		}
		labelsSet = false;
		try
		{
			ConfirmPanelUIManager p = Panel;
			if ((Object)(object)p != (Object)null)
			{
				if ((Object)(object)p.yesTMP != (Object)null && yesOriginal != null)
				{
					p.yesTMP.text = yesOriginal;
				}
				if ((Object)(object)p.noTMP != (Object)null && noOriginal != null)
				{
					p.noTMP.text = noOriginal;
				}
			}
		}
		catch
		{
		}
	}

	internal static void Reset()
	{
		confirming = false;
		RestoreLabels();
	}

	[SystemTick]
	private static void Tick()
	{
		if (!Net.HostAway || confirming)
		{
			return;
		}
		ConfirmPanelUIManager p = Panel;
		if ((Object)(object)p != (Object)null && (Object)(object)p.panel != (Object)null && !p.panel.activeSelf)
		{
			Show();
		}
	}
}
