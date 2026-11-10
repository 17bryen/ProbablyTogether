using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public abstract class PicsBase2 : PicsBase
{
	protected static EmporiumEntry E => EmporiumEntry.Instance;

	protected void JoinN(int n)
	{
		Do("wire", delegate
		{
			DialogueBotNet.Ensure();
			return "dialogue lab channel wired";
		});
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("shop-live", 240f, () => Ui.ShopOnline && DialogueSystem.Phone.Ready, () => "state " + Net.ClientState.ToString() + ", scene " + BotProbes.Scene);
		WaitFor("players-" + n, 400f, () => Net.Players.Count >= n && Presence.RosterTexts.Count >= n && Net.Players.Keys.Count(Net.IsPlayerLive) >= n, () => Net.Players.Count + " player(s), " + Net.Players.Keys.Count(Net.IsPlayerLive) + " live");
		Delay("settle", 3f);
	}

	protected static Vector2 CursorArg(Vector2 fallback)
	{
		string text = Args.Get("--br-cursor");
		if (string.IsNullOrEmpty(text))
		{
			return fallback;
		}
		string[] array = text.Split(',');
		if (array.Length == 2 && float.TryParse(array[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && float.TryParse(array[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
		{
			return new Vector2(result, result2);
		}
		return fallback;
	}

	protected void OpenShop()
	{
		Do("options", delegate
		{
			EmporiumOption instance = EmporiumOption.Instance;
			if ((Object)(object)instance != (Object)null)
			{
				instance.autoCallNext = false;
				instance.autoNextDialog = false;
			}
			return "autoCallNext off, autoNextDialog off";
		});
		Do("open", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "dayNight.open sent";
		});
		WaitFor("opened", 30f, () => PicsBase.State == "OPEN", () => "state " + PicsBase.State);
		WaitFor("shutter-settled", 15f, () => PicsBase.Ps.lockShutterButtonFlag == 0, () => "flag " + PicsBase.Ps.lockShutterButtonFlag);
	}

	protected void CloseShop()
	{
		Do("queue", delegate
		{
			if (PicsBase.Ps.CanEndDay())
			{
				return "every waiting customer can be sent away";
			}
			Net.ToServer("dayNight.bot", new MsgDnBot
			{
				op = "clearqueue"
			});
			return "a story customer is waiting: lab op clearqueue";
		});
		WaitFor("closable", 20f, () => PicsBase.Ps.CanEndDay(), () => "queue " + PicsBase.Ps.storeClientManager.clientStack.Count);
		float pressed = -100f;
		S("close", 40f, delegate(BotStep st)
		{
			if (PicsBase.State == "CLOSED")
			{
				st.Detail = "closed";
				return StepStatus.Pass;
			}
			if (Time.realtimeSinceStartup - pressed >= 5f)
			{
				pressed = Time.realtimeSinceStartup;
				StoreShutterButton.Instance.EndAndClose();
			}
			st.Detail = "state " + PicsBase.State + ", last refusal " + DayNight.LastRefusedWhy;
			return StepStatus.Running;
		});
		Delay("past-close-timers", 5f);
	}

	protected static GameItem CheapestGoods()
	{
		GameItem result = null;
		long num = long.MaxValue;
		foreach (GameItem childItem in ((GameInventory)E.frontInvinvElement).childItems)
		{
			if (childItem != null && !GeneralHelper.IsItemOwned(childItem))
			{
				long negociatedValue = childItem.GetNegociatedValue();
				if (negociatedValue < num)
				{
					num = negociatedValue;
					result = childItem;
				}
			}
		}
		return result;
	}
}
