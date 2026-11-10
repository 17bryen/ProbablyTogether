using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public abstract class DayNight3Base : DayNightBotBase
{
	protected int shown0;

	protected int dropped0;

	protected int loads0;

	protected static bool PanelUp
	{
		get
		{
			if ((Object)(object)GameOverUIManager.Instance != (Object)null)
			{
				return GameOverUIManager.Instance.IsShowing;
			}
			return false;
		}
	}

	protected static bool EndedLineShown
	{
		get
		{
			try
			{
				return NewRun.Visible && NewRun.Text.Contains("The run has ended. A new run starts when everyone here presses Start a new run, or when the server owner starts one.");
			}
			catch
			{
				return false;
			}
		}
	}

	protected void MarkEnding(string name)
	{
		Do(name, delegate
		{
			shown0 = DayNightRun.ShownCount;
			dropped0 = DayNightRun.DroppedLocal;
			loads0 = DayNightProbes.Loads;
			return "shown " + shown0 + ", dropped " + dropped0 + ", loads " + loads0;
		});
	}

	protected void NewRunUp(string name, float timeout)
	{
		WaitFor(name, timeout, () => DayNightBotBase.Live && DayNightProbes.Loads > loads0 && !DayNightRun.RunOver && DayNightBotBase.Day == 1 && DayNightBotBase.State == "MORNING", () => "loads " + DayNightProbes.Loads + " (" + loads0 + "), run over " + DayNightRun.RunOver + ", day " + DayNightBotBase.Day + ", " + DayNightBotBase.State + ", why '" + DayNightRun.LastNewRunWhy + "'");
	}
}
