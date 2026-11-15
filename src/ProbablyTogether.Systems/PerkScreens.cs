using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProbablyTogether.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

/// Shows the game's own trade and perk screens over a cover; Continue asks the server (Specs.Pick, Perks.Pick).
/// Back and Escape exist only when choosing again (Systems.Repick); the mod's trade window is the fallback.
public static class PerkScreens
{
	private const string SpecWindow = "new_game_specialization";

	private const string PerkWindow = "new_game_perks";

	private static GameObject cover;

	private static TextMeshProUGUI label;

	private static float nextLook;

	private static bool waiting;

	private static float waitingSince;

	private static string refusal = "";

	private static bool specFailed;

	private static bool perksFailed;

	private static bool closing;

	/// Shows the screens for bots too.
	internal static bool LabOn;

	/// Screens opened and Continue presses since the game started.
	internal static int Opened;

	internal static int Continued;

	internal static string LastProblem = "";

	private static readonly Dictionary<string, StartingPerk> standIns = new Dictionary<string, StartingPerk>(StringComparer.Ordinal);

	private static CustomUIWindow Window(string id)
	{
		try
		{
			CustomUIManager instance = CustomUIManager.Instance;
			CustomUIWindow customUIWindow = ((Object)(object)instance != (Object)null) ? instance.GetWindow(id) : null;
			return (customUIWindow != null && customUIWindow.IsAlive) ? customUIWindow : null;
		}
		catch
		{
			return null;
		}
	}

	internal static bool SpecOpen => Window(SpecWindow) != null && Covering;

	internal static bool PerksOpen => Window(PerkWindow) != null && Covering;

	private static bool Covering => (Object)(object)cover != (Object)null && cover.activeSelf;

	/// True where the game's trade screen does the asking: the mod's own trade window stays away.
	public static bool HandlesTrade => !specFailed && (!Bots.BotRunner.Enabled || LabOn);

	private static void ShowCover(string text)
	{
		if ((Object)(object)cover == (Object)null)
		{
			cover = new GameObject("bellun_probably_together_setup_cover");
			Object.DontDestroyOnLoad((Object)(object)cover);
			Canvas canvas = cover.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			// Under the game's window layer (900), over the shop.
			canvas.sortingOrder = 890;
			CanvasScaler canvasScaler = cover.AddComponent<CanvasScaler>();
			canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
			canvasScaler.matchWidthOrHeight = 0.5f;
			cover.AddComponent<GraphicRaycaster>();
			Image image = Kit.Box(cover.transform, "dim", new Color(Kit.PlateColor.r * 0.55f, Kit.PlateColor.g * 0.55f, Kit.PlateColor.b * 0.55f, 1f), raycast: true);
			Kit.Stretch(image.rectTransform);
			label = Kit.Label(cover.transform, "title", 24f, Kit.Dim, 1400f, (TextAlignmentOptions)514);
			RectTransform rectTransform = label.rectTransform;
			rectTransform.anchorMin = new Vector2(0.5f, 1f);
			rectTransform.anchorMax = new Vector2(0.5f, 1f);
			rectTransform.pivot = new Vector2(0.5f, 1f);
			rectTransform.anchoredPosition = new Vector2(0f, -36f);
			rectTransform.sizeDelta = new Vector2(1400f, 80f);
		}
		if (!cover.activeSelf)
		{
			cover.SetActive(true);
			Log.Info("ui: setup cover up (" + text + ")");
		}
		if ((Object)(object)label != (Object)null && label.text != text)
		{
			label.text = text;
		}
	}

	private static void HideAll(string why)
	{
		bool flag = Covering;
		closing = true;
		try
		{
			Window(SpecWindow)?.Close();
			Window(PerkWindow)?.Close();
		}
		catch (Exception ex)
		{
			Log.Warn("ui: closing the setup screens threw: " + ex.Message);
		}
		finally
		{
			closing = false;
		}
		if (flag)
		{
			cover.SetActive(false);
			Log.Info("ui: setup cover down (" + why + ")");
		}
		waiting = false;
		refusal = "";
	}

	private static bool Offered(StartingScenario s)
	{
		string key = s.startType.ToString();
		return Specs.Choices().Any((KeyValuePair<string, string> c) => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
	}

	private static void OpenSpec()
	{
		if (specFailed || Window(SpecWindow) != null)
		{
			return;
		}
		try
		{
			NewGameSpecializationUI.Open();
			CustomUIWindow customUIWindow = Window(SpecWindow);
			NewGameSpecializationFrame frame = (customUIWindow != null) ? customUIWindow.root.GetComponent<NewGameSpecializationFrame>() : null;
			if ((Object)(object)frame == (Object)null)
			{
				throw new InvalidOperationException("the game's specialization screen did not open");
			}
			customUIWindow.closeOnEscape = false;
			customUIWindow.onClose = delegate
			{
				if (!closing)
				{
					Log.Info("ui: the trade screen was closed before a trade was picked; it comes back");
				}
			};
			if ((Object)(object)frame.backButton != (Object)null)
			{
				((Component)frame.backButton).gameObject.SetActive(false);
			}
			frame.continueButton.onClick.RemoveAllListeners();
			frame.continueButton.onClick.AddListener(SpecContinue);
			Opened++;
			waiting = false;
			RefreshSpec();
			Log.Info("ui: the game's trade screen is up (" + Specs.Choices().Count + " trade(s) on offer)");
		}
		catch (Exception ex)
		{
			specFailed = true;
			LastProblem = "trade screen: " + ex.Message;
			Log.Warn("ui: the game's trade screen could not be used, the mod's own window comes instead: " + ex.Message);
			closing = true;
			try
			{
				Window(SpecWindow)?.Close();
			}
			catch
			{
			}
			closing = false;
		}
	}

	internal static void SpecContinue()
	{
		try
		{
			StartingScenario selected = NewGameSpecializationUI.Selected;
			if (selected == null || waiting || !Offered(selected))
			{
				return;
			}
			waiting = true;
			waitingSince = Time.realtimeSinceStartup;
			refusal = "";
			Continued++;
			Specs.Pick(selected.startType.ToString());
			RefreshSpec();
		}
		catch (Exception ex)
		{
			Log.Warn("ui: Continue on the trade screen threw: " + ex.Message);
		}
	}

	/// What the game's screen shows for the selected trade, after the game drew it.
	internal static void RefreshSpec()
	{
		CustomUIWindow customUIWindow = Window(SpecWindow);
		if (customUIWindow == null || !Covering)
		{
			return;
		}
		NewGameSpecializationFrame component = customUIWindow.root.GetComponent<NewGameSpecializationFrame>();
		StartingScenario selected = NewGameSpecializationUI.Selected;
		if ((Object)(object)component == (Object)null || selected == null)
		{
			return;
		}
		string key = selected.startType.ToString();
		bool flag = Offered(selected);
		int num = Specs.TakenBy(key.ToUpperInvariant());
		if (!flag && (Object)(object)component.warning != (Object)null)
		{
			component.warning.text = (num > 0) ? Lang.Trf("{0} is this shop's {1}.", Members.NameOf(num), selected.GetTitle()) : Lang.Tr("Not a trade for a shared shop.");
		}
		else if (waiting && (Object)(object)component.warning != (Object)null)
		{
			component.warning.text = Lang.Tr("Asking the shop...");
		}
		else if (refusal.Length > 0 && (Object)(object)component.warning != (Object)null)
		{
			component.warning.text = Lang.Tr(refusal);
		}
		component.continueButton.interactable = flag && !waiting && selected.CanStart();
	}

	private static readonly FieldInfo firstRunField = AccessTools.Field(typeof(NewGamePerksUI), "firstRun");

	private static readonly MethodInfo rebuildMethod = AccessTools.Method(typeof(NewGamePerksUI), "Rebuild", Type.EmptyTypes, (Type[])null);

	private static void Rebuild()
	{
		try
		{
			if (Window(PerkWindow) != null)
			{
				rebuildMethod?.Invoke(null, null);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("ui: redrawing the perk screen threw: " + (ex.InnerException ?? ex).Message);
		}
	}

	private static void OpenPerks()
	{
		if (perksFailed || Window(PerkWindow) != null)
		{
			return;
		}
		try
		{
			NewGameData.Instance.startingPerks = new List<StartingPerk>();
			NewGamePerksUI.Open();
			CustomUIWindow customUIWindow = Window(PerkWindow);
			NewGamePerksFrame frame = (customUIWindow != null) ? customUIWindow.root.GetComponent<NewGamePerksFrame>() : null;
			if ((Object)(object)frame == (Object)null)
			{
				throw new InvalidOperationException("the game's perk screen did not open");
			}
			customUIWindow.closeOnEscape = false;
			customUIWindow.onClose = delegate
			{
				if (!closing)
				{
					Log.Info("ui: the perk screen was closed before perks were picked; it comes back");
				}
			};
			if ((Object)(object)frame.backButton != (Object)null)
			{
				((Component)frame.backButton).gameObject.SetActive(false);
			}
			firstRunField?.SetValue(null, false);
			try
			{
				frame.instruction.text = LocHelper.GetLocalizedMainMenu("new_perk_instruction");
				frame.instruction.color = NewGameStyle.DESCRIPTION;
			}
			catch
			{
			}
			frame.continueButton.onClick.RemoveAllListeners();
			frame.continueButton.onClick.AddListener(PerksContinue);
			waiting = false;
			Opened++;
			Rebuild();
			Log.Info("ui: the game's perk screen is up");
		}
		catch (Exception ex)
		{
			perksFailed = true;
			LastProblem = "perk screen: " + ex.Message;
			Log.Warn("ui: the game's perk screen could not be used (\"/perks none\" in the chat settles it without perks): " + ex.Message);
			closing = true;
			try
			{
				Window(PerkWindow)?.Close();
			}
			catch
			{
			}
			closing = false;
		}
	}

	internal static List<string> Selected()
	{
		List<StartingPerk> list = NewGameData.Instance?.startingPerks;
		return (list == null) ? new List<string>() : list.Where((StartingPerk p) => p != null).Select((StartingPerk p) => p.id).ToList();
	}

	internal static void PerksContinue()
	{
		try
		{
			if (waiting)
			{
				return;
			}
			List<string> list = Selected();
			string text = Perks.Refusal(Members.LocalSeat, list);
			if (text != null)
			{
				Refused(text);
				return;
			}
			waiting = true;
			waitingSince = Time.realtimeSinceStartup;
			refusal = "";
			Continued++;
			Perks.Pick(list);
		}
		catch (Exception ex)
		{
			Log.Warn("ui: Continue on the perk screen threw: " + ex.Message);
		}
	}

	/// What locks a perk: the game's answer, else the shop's.
	internal static StartingPerk LockedBy(StartingPerk perk)
	{
		if (perk == null || !PerksOpen)
		{
			return null;
		}
		string text = Perks.LockReason(Members.LocalSeat, perk.id);
		if (text == null)
		{
			return null;
		}
		if (!standIns.TryGetValue(text, out var value))
		{
			value = (standIns[text] = new StartingPerk
			{
				id = "bellun_probably_together_lock",
				rawName = text,
				incompatiblePerks = new List<string>()
			});
		}
		return value;
	}

	/// Every copy: this seat's perks are picked.
	internal static void Picked(int seat)
	{
		if (!Covering)
		{
			return;
		}
		if (seat == Members.LocalSeat)
		{
			waiting = false;
			return;
		}
		try
		{
			List<StartingPerk> list = NewGameData.Instance?.startingPerks;
			if (list != null && PerksOpen)
			{
				list.RemoveAll((StartingPerk p) => p != null && Perks.LockReason(Members.LocalSeat, p.id) != null);
				Rebuild();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("ui: after somebody else's perks: " + ex.Message);
		}
	}

	/// Every copy: a trade was picked.
	internal static void TradePicked(int seat)
	{
		if (!Covering)
		{
			return;
		}
		if (seat == Members.LocalSeat)
		{
			waiting = false;
		}
		RefreshSpec();
	}

	/// This copy's pick (trade or perks) was refused by the server.
	internal static void Refused(string why)
	{
		if (!Covering)
		{
			return;
		}
		waiting = false;
		refusal = why ?? "";
		RefreshSpec();
		try
		{
			CustomUIWindow customUIWindow = Window(PerkWindow);
			NewGamePerksFrame newGamePerksFrame = (customUIWindow != null) ? customUIWindow.root.GetComponent<NewGamePerksFrame>() : null;
			if ((Object)(object)newGamePerksFrame != (Object)null && refusal.Length > 0)
			{
				newGamePerksFrame.instruction.text = Lang.Tr(refusal);
				newGamePerksFrame.instruction.color = NewGameStyle.WARNING_DANGER;
			}
		}
		catch
		{
		}
	}

	[SystemInit]
	internal static void Init()
	{
		Snapshot.Loaded += delegate
		{
			nextLook = 0f;
			waiting = false;
		};
		Net.Disconnected += delegate
		{
			HideAll("disconnected");
			specFailed = false;
			perksFailed = false;
		};
	}

	[SystemTick]
	private static void Tick()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (realtimeSinceStartup < nextLook)
		{
			return;
		}
		nextLook = realtimeSinceStartup + 0.4f;
		try
		{
			if (!Net.IsOnline || !Game.ShopLoaded)
			{
				if (Covering)
				{
					HideAll("no shop");
				}
				return;
			}
			if (Snapshot.IsLoading || Dispatch.InRequest || !Bays.Enabled || !Net.HasLocalPlayer || !Net.IsLive)
			{
				return;
			}
			if (Bots.BotRunner.Enabled && !LabOn)
			{
				return;
			}
			int localSeat = Members.LocalSeat;
			if (localSeat <= 0 || Outings.Private || HostTrip.Out)
			{
				if (Covering)
				{
					HideAll("not a player of this shop right now");
				}
				return;
			}
			bool flag = Specs.Of(localSeat) == null;
			// Seat 1 of a brand new shop is given the shop's own trade by the server in a moment.
			bool flag2 = flag && localSeat == 1 && Game.Day == 1 && Game.Store != null && (int)Game.Store.startType != 0;
			bool flag3 = Perks.Enabled && !Perks.Picked(localSeat);
			if (!flag && !flag3)
			{
				if (Covering)
				{
					HideAll("trade and perks are picked");
				}
				return;
			}
			if (waiting && realtimeSinceStartup - waitingSince > 12f)
			{
				// No answer (a lost refusal): the buttons work again.
				waiting = false;
				RefreshSpec();
			}
			string text = (string.IsNullOrEmpty(Net.ServerName) ? Lang.Tr("This shop") : Net.ServerName);
			if (flag2 || (flag && Bays.BayOf(localSeat) == null))
			{
				ShowCover(Lang.Trf("{0}: your place in the shop is being set up...", text));
				return;
			}
			if (flag)
			{
				ShowCover(Lang.Trf("{0}: choose your trade", text));
				closing = true;
				try
				{
					Window(PerkWindow)?.Close();
				}
				finally
				{
					closing = false;
				}
				OpenSpec();
				if (specFailed)
				{
					cover.SetActive(false);
				}
				return;
			}
			ShowCover(Lang.Trf("{0}: choose your perks", text));
			if (Window(SpecWindow) != null)
			{
				closing = true;
				try
				{
					Window(SpecWindow)?.Close();
				}
				finally
				{
					closing = false;
				}
			}
			OpenPerks();
			if (perksFailed)
			{
				cover.SetActive(false);
			}
		}
		catch (Exception ex)
		{
			if (Log.Allow("perkscreens|tick", 30000L, out var skipped))
			{
				Log.Warn("ui: the setup screens' look-over threw" + Log.More(skipped) + ": " + ex);
			}
		}
	}
}

/// A trade somebody has cannot be continued with on the game's trade screen.
[HarmonyPatch(typeof(StartingScenario), "CanStart")]
internal static class PerkScreensCanStartPatch
{
	private static void Postfix(StartingScenario __instance, ref bool __result)
	{
		try
		{
			if (__result && PerkScreens.SpecOpen && __instance != null)
			{
				string key = __instance.startType.ToString();
				__result = Specs.Choices().Any((KeyValuePair<string, string> c) => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
			}
		}
		catch
		{
		}
	}
}

[HarmonyPatch]
internal static class PerkScreensShowScenarioPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(NewGameSpecializationUI), "ShowScenario", Type.EmptyTypes, (Type[])null);

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix()
	{
		try
		{
			PerkScreens.RefreshSpec();
		}
		catch
		{
		}
	}
}

/// A perk the shop keeps from this player is locked the way the game locks a perk another perk excludes.
[HarmonyPatch]
internal static class PerkScreensLockedByPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(NewGamePerksUI), "LockedBy", new Type[1] { typeof(StartingPerk) }, (Type[])null);

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix(StartingPerk perk, ref StartingPerk __result)
	{
		try
		{
			if (__result == null)
			{
				__result = PerkScreens.LockedBy(perk);
			}
		}
		catch
		{
		}
	}
}
