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
		p.OpenPanel(Lang.Tr("Step out and keep the shop open? The other players keep playing and you can rejoin at any time. Choose No to stop hosting instead."), (Action)StepOut, (Action)AskStop, false);
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
