using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProbablyTogether.Systems;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProbablyTogether.UI;

/// The Random and Force checkboxes on the game's trade and perk screens: on the host's New Game screens and on the
/// screens a joining player gets. A ticked Random greys the screen's own choices; the server deals at Continue.
internal static class RandomBoxes
{
	private sealed class Box
	{
		public GameObject Root;

		public GameObject Mark;

		public Button Button;

		public CanvasGroup Group;

		public bool On;

		public bool Locked;

		public void Set(bool on)
		{
			On = on;
			if (Mark != null)
			{
				Mark.SetActive(on);
			}
		}

		public void Lock(bool locked)
		{
			Locked = locked;
			if ((Object)(object)Button != (Object)null)
			{
				Button.interactable = !locked;
			}
			if ((Object)(object)Group != (Object)null)
			{
				Group.alpha = locked ? 0.6f : 1f;
			}
		}

		public void Show(bool show)
		{
			if (Root != null)
			{
				Root.SetActive(show);
			}
		}
	}

	private const string BodyName = "bellun_probably_together_random";

	private const float Left = 58f;

	/// The host's choices for a new shop.
	internal static bool HostTrade;

	internal static bool HostPerks;

	internal static bool HostForceTrade;

	internal static bool HostForcePerks;

	/// A joining player's (or a repick's) choices on the screens up now.
	internal static bool JoinTrade;

	internal static bool JoinPerks;

	private static Box tradeBox;

	private static Box tradeForce;

	private static Box perksBox;

	private static Box perksForce;

	private static NewGameSpecializationFrame specFrame;

	private static NewGamePerksFrame perksFrame;

	private static bool applying;

	private static readonly FieldInfo selectedField = AccessTools.Field(typeof(NewGameSpecializationUI), "selected");

	private static readonly FieldInfo scenariosField = AccessTools.Field(typeof(NewGameSpecializationUI), "scenarios");

	private static readonly MethodInfo showScenario = AccessTools.Method(typeof(NewGameSpecializationUI), "ShowScenario", Type.EmptyTypes, (Type[])null);

	private static readonly MethodInfo refreshStrip = AccessTools.Method(typeof(NewGameSpecializationUI), "RefreshStrip", Type.EmptyTypes, (Type[])null);

	private static readonly MethodInfo rebuildPerks = AccessTools.Method(typeof(NewGamePerksUI), "Rebuild", Type.EmptyTypes, (Type[])null);

	private static bool Hosting => HostFlow.Active;

	private static bool Joining => !HostFlow.Active && PerkScreens.Covering;

	internal static bool TradeOn => Hosting ? HostTrade : (Joining && (JoinTrade || RandomStarts.ForceTrade));

	internal static bool PerksOn => Hosting ? HostPerks : (Joining && (JoinPerks || RandomStarts.ForcePerks));

	/// A new shop starts with nothing random.
	internal static void ResetHost()
	{
		HostTrade = false;
		HostPerks = false;
		HostForceTrade = false;
		HostForcePerks = false;
	}

	internal static void ResetJoin()
	{
		JoinTrade = false;
		JoinPerks = false;
	}

	private static NewGameCheckbox Source()
	{
		CustomUIManager instance = CustomUIManager.Instance;
		NewGameDifficultyFrame newGameDifficultyFrame = ((Object)(object)instance != (Object)null && (Object)(object)instance.newGameDifficultyPrefab != (Object)null) ? instance.newGameDifficultyPrefab.GetComponent<NewGameDifficultyFrame>() : null;
		return newGameDifficultyFrame?.endless;
	}

	/// A copy of the difficulty screen's "Endless Mode" checkbox.
	private static Box NewBox(RectTransform parent, string id, float x, float y, float width, string text, Action<bool> changed)
	{
		NewGameCheckbox newGameCheckbox = Source();
		if (newGameCheckbox == null || (Object)(object)newGameCheckbox.root == (Object)null)
		{
			return null;
		}
		GameObject gameObject = Object.Instantiate(newGameCheckbox.root.gameObject, parent);
		gameObject.name = "bellun_probably_together_" + id;
		gameObject.SetActive(true);
		Image[] componentsInChildren = newGameCheckbox.root.GetComponentsInChildren<Image>(includeInactive: true);
		Image[] componentsInChildren2 = gameObject.GetComponentsInChildren<Image>(includeInactive: true);
		int num = Array.IndexOf(componentsInChildren, newGameCheckbox.mark);
		int num2 = Array.IndexOf(componentsInChildren, newGameCheckbox.border);
		Image image = (num >= 0 && num < componentsInChildren2.Length) ? componentsInChildren2[num] : null;
		Image image2 = (num2 >= 0 && num2 < componentsInChildren2.Length) ? componentsInChildren2[num2] : null;
		TextMeshProUGUI componentInChildren = gameObject.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
		foreach (LocalizeStringEvent componentsInChild in gameObject.GetComponentsInChildren<LocalizeStringEvent>(includeInactive: true))
		{
			componentsInChild.enabled = false;
		}
		WildNetworkUI.SetRect((RectTransform)gameObject.transform, x, y, width, 32f);
		if ((Object)(object)componentInChildren != (Object)null)
		{
			componentInChildren.text = text;
			componentInChildren.enableWordWrapping = false;
			WildNetworkUI.SetRect(componentInChildren.rectTransform, 44f, 0f, width - 44f, 32f);
			WildNetworkUI.AutoSize(componentInChildren, 14f);
		}
		if ((Object)(object)image2 != (Object)null)
		{
			image2.color = NewGameStyle.DIFF_CHECKBOX_BORDER;
		}
		CanvasGroup canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
		canvasGroup.alpha = 1f;
		Box box = new Box
		{
			Root = gameObject,
			Mark = ((Object)(object)image != (Object)null) ? image.gameObject : null,
			Button = gameObject.GetComponentInChildren<Button>(includeInactive: true),
			Group = canvasGroup
		};
		box.Set(on: false);
		if ((Object)(object)box.Button != (Object)null)
		{
			box.Button.interactable = true;
			box.Button.onClick.RemoveAllListeners();
			box.Button.onClick.AddListener(delegate
			{
				if (!box.Locked)
				{
					NewGameUIElements.PlayClick();
					box.Set(!box.On);
					changed(box.On);
				}
			});
		}
		return box;
	}

	/// Where the screen's Continue button is, in the coordinates of a rect laid over the panel.
	private static Rect ContinueRect(NewGamePanelFrame frame, RectTransform body)
	{
		try
		{
			Component component = ((Object)(object)frame.continueOuter != (Object)null) ? (Component)frame.continueOuter : frame.continueButton;
			Vector3[] array = new Vector3[4];
			((RectTransform)component.transform).GetWorldCorners(array);
			Vector3 vector = body.InverseTransformPoint(array[1]);
			Vector3 vector2 = body.InverseTransformPoint(array[3]);
			Rect result = new Rect(vector.x, 0f - vector.y, vector2.x - vector.x, vector.y - vector2.y);
			if (result.width > 60f && result.height > 16f)
			{
				return result;
			}
		}
		catch
		{
		}
		return new Rect(427f, 890f, 256f, 40f);
	}

	private static RectTransform Body(NewGamePanelFrame frame)
	{
		Transform transform = frame.panel.Find(BodyName);
		if ((Object)(object)transform != (Object)null)
		{
			Object.Destroy(transform.gameObject);
		}
		return NewGameUIElements.NewRect(frame.panel, BodyName, 0f, 0f, frame.panel.rect.width, frame.panel.rect.height);
	}

	/// The trade screen opened (or came back): its boxes, and the random look if ticked.
	internal static void AttachSpec()
	{
		CustomUIWindow customUIWindow = CustomUIManager.Instance?.GetWindow(NewGameSpecializationUI.WINDOW_ID);
		NewGameSpecializationFrame newGameSpecializationFrame = (customUIWindow != null && customUIWindow.IsAlive) ? customUIWindow.root.GetComponent<NewGameSpecializationFrame>() : null;
		if ((Object)(object)newGameSpecializationFrame == (Object)null || (!Hosting && !Joining))
		{
			return;
		}
		if ((Object)(object)specFrame != (Object)(object)newGameSpecializationFrame || (Object)(object)newGameSpecializationFrame.panel.Find(BodyName) == (Object)null)
		{
			specFrame = newGameSpecializationFrame;
			RectTransform rectTransform = Body(newGameSpecializationFrame);
			Rect rect = ContinueRect(newGameSpecializationFrame, rectTransform);
			float y = rect.y + (rect.height - 32f) / 2f;
			tradeBox = NewBox(rectTransform, "random_trade", Left, y, rect.x - Left - 24f, Lang.Tr("Random trade"), delegate(bool on)
			{
				if (Hosting)
				{
					HostTrade = on;
				}
				else
				{
					JoinTrade = on;
				}
				SpecLook();
			});
			tradeForce = Hosting ? NewBox(rectTransform, "force_trade", rect.xMax + 24f, y, rectTransform.rect.width - rect.xMax - 24f - Left, Lang.Tr("Force on players who join"), delegate(bool on)
			{
				HostForceTrade = on;
			}) : null;
			Log.Info("random: boxes on the trade screen (" + (Hosting ? "host" : "joining player") + ")");
		}
		if (tradeBox != null)
		{
			bool flag = !Hosting && RandomStarts.ForceTrade;
			tradeBox.Set(TradeOn);
			tradeBox.Lock(flag);
		}
		if (tradeForce != null)
		{
			tradeForce.Set(HostForceTrade);
		}
		SpecLook();
	}

	internal static void AttachPerks()
	{
		CustomUIWindow customUIWindow = CustomUIManager.Instance?.GetWindow("new_game_perks");
		NewGamePerksFrame newGamePerksFrame = (customUIWindow != null && customUIWindow.IsAlive) ? customUIWindow.root.GetComponent<NewGamePerksFrame>() : null;
		if ((Object)(object)newGamePerksFrame == (Object)null || (!Hosting && !Joining))
		{
			return;
		}
		if ((Object)(object)perksFrame != (Object)(object)newGamePerksFrame || (Object)(object)newGamePerksFrame.panel.Find(BodyName) == (Object)null)
		{
			perksFrame = newGamePerksFrame;
			RectTransform rectTransform = Body(newGamePerksFrame);
			Rect rect = ContinueRect(newGamePerksFrame, rectTransform);
			float y = rect.y + (rect.height - 32f) / 2f;
			perksBox = NewBox(rectTransform, "random_perks", Left, y, rect.x - Left - 24f, Lang.Tr("Random perks"), delegate(bool on)
			{
				if (Hosting)
				{
					HostPerks = on;
				}
				else
				{
					JoinPerks = on;
				}
				PerksChanged(on);
			});
			perksForce = Hosting ? NewBox(rectTransform, "force_perks", rect.xMax + 24f, y, rectTransform.rect.width - rect.xMax - 24f - Left, Lang.Tr("Force on players who join"), delegate(bool on)
			{
				HostForcePerks = on;
			}) : null;
			Log.Info("random: boxes on the perk screen (" + (Hosting ? "host" : "joining player") + ")");
		}
		if (perksBox != null)
		{
			bool flag = !Hosting && RandomStarts.ForcePerks;
			perksBox.Set(PerksOn);
			perksBox.Lock(flag);
		}
		if (perksForce != null)
		{
			perksForce.Set(HostForcePerks);
		}
		if (PerksOn)
		{
			PerksChanged(on: true);
		}
		else
		{
			PerksLook();
		}
	}

	/// A force box only for a shop of own trades, and only while the host's own is random.
	private static void ForceShown()
	{
		bool ownTrades = HostFlow.OwnTrades;
		if (tradeForce != null)
		{
			tradeForce.Show(Hosting && ownTrades && HostTrade);
			if (!HostTrade || !ownTrades)
			{
				HostForceTrade = false;
				tradeForce.Set(on: false);
			}
		}
		if (perksForce != null)
		{
			perksForce.Show(Hosting && ownTrades && HostPerks);
			if (!HostPerks || !ownTrades)
			{
				HostForcePerks = false;
				perksForce.Set(on: false);
			}
		}
	}

	private static void Grey(Component c, bool grey)
	{
		if ((Object)(object)c == (Object)null)
		{
			return;
		}
		CanvasGroup canvasGroup = c.GetComponent<CanvasGroup>() ?? c.gameObject.AddComponent<CanvasGroup>();
		canvasGroup.alpha = grey ? 0.3f : 1f;
		canvasGroup.blocksRaycasts = !grey;
		canvasGroup.interactable = !grey;
	}

	/// The trade screen in its random look: the strip greyed, the texts say what happens.
	internal static void SpecLook()
	{
		NewGameSpecializationFrame newGameSpecializationFrame = specFrame;
		if ((Object)(object)newGameSpecializationFrame == (Object)null || applying)
		{
			return;
		}
		ForceShown();
		bool tradeOn = TradeOn;
		Grey(newGameSpecializationFrame.stripSlots, tradeOn);
		Grey(newGameSpecializationFrame.prevButton, tradeOn);
		Grey(newGameSpecializationFrame.nextButton, tradeOn);
		if ((Object)(object)newGameSpecializationFrame.startWithLabel != (Object)null)
		{
			newGameSpecializationFrame.startWithLabel.gameObject.SetActive(!tradeOn);
		}
		if ((Object)(object)newGameSpecializationFrame.bulletsHost != (Object)null)
		{
			newGameSpecializationFrame.bulletsHost.gameObject.SetActive(!tradeOn);
		}
		if (!tradeOn)
		{
			if (newGameSpecializationFrame.title.text == Lang.Tr("A trade at random"))
			{
				applying = true;
				try
				{
					showScenario?.Invoke(null, null);
				}
				finally
				{
					applying = false;
				}
			}
			return;
		}
		if (Hosting)
		{
			// The game's Continue wants a trade that can start; the server replaces it with the dealt one.
			SelectStartable();
		}
		newGameSpecializationFrame.title.text = Lang.Tr("A trade at random");
		newGameSpecializationFrame.tag.text = Lang.Tr("Dealt when the shop starts");
		newGameSpecializationFrame.tag.color = NewGameStyle.TagColor(StartingScenario.TagTone.Warning);
		newGameSpecializationFrame.description.text = Hosting ? Lang.Tr("One of the trades is dealt to you when the shop starts, Rock Bottom included. You find out which once you are in the shop.") : Lang.Tr("One of the trades nobody in this shop has yet is dealt to you, Rock Bottom included. You find out which once you are in the shop.");
		newGameSpecializationFrame.warning.text = (!Hosting && RandomStarts.ForceTrade) ? Lang.Tr("This shop deals every new player's trade at random.") : "";
		newGameSpecializationFrame.warning.color = NewGameStyle.WARNING;
		newGameSpecializationFrame.continueButton.interactable = true;
		NewGameUIElements.RefreshContinueLook(newGameSpecializationFrame, hovered: false);
	}

	private static void SelectStartable()
	{
		try
		{
			if (!(scenariosField?.GetValue(null) is List<StartingScenario> list))
			{
				return;
			}
			StartingScenario selected = NewGameSpecializationUI.Selected;
			if (selected != null && selected.CanStart() && !selected.IsMod)
			{
				return;
			}
			int num = list.FindIndex((StartingScenario s) => s != null && !s.IsMod && s.CanStart());
			if (num >= 0)
			{
				selectedField?.SetValue(null, num);
				refreshStrip?.Invoke(null, null);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("random: picking a trade the game can start threw: " + ex.Message);
		}
	}

	private static void PerksChanged(bool on)
	{
		if (on)
		{
			NewGameData.Instance?.startingPerks?.Clear();
		}
		applying = true;
		try
		{
			rebuildPerks?.Invoke(null, null);
		}
		catch (Exception ex)
		{
			Log.Warn("random: redrawing the perk screen threw: " + (ex.InnerException ?? ex).Message);
		}
		finally
		{
			applying = false;
		}
		PerksLook();
	}

	/// The perk screen in its random look, after each of the game's redraws.
	internal static void PerksLook()
	{
		NewGamePerksFrame newGamePerksFrame = perksFrame;
		if ((Object)(object)newGamePerksFrame == (Object)null || applying)
		{
			return;
		}
		ForceShown();
		bool perksOn = PerksOn;
		Grey(newGamePerksFrame.availableSlots, perksOn);
		Grey(newGamePerksFrame.selectedSlots, perksOn);
		Grey(newGamePerksFrame.prevButton, perksOn);
		Grey(newGamePerksFrame.nextButton, perksOn);
		Grey(newGamePerksFrame.pointsCounter, perksOn);
		Grey(newGamePerksFrame.perksCounter, perksOn);
		if (!perksOn)
		{
			return;
		}
		newGamePerksFrame.instruction.text = Lang.Tr("Your perks are dealt at random: three points' worth, good and bad mixed. You find out which once you are in the shop.");
		newGamePerksFrame.instruction.color = NewGameStyle.DESCRIPTION;
		newGamePerksFrame.continueButton.interactable = true;
		NewGameUIElements.RefreshContinueLook(newGamePerksFrame, hovered: false);
	}

	/// Lab: ticks a box as a click would.
	internal static void LabTick(bool trade, bool force)
	{
		if (trade)
		{
			if (force)
			{
				HostForceTrade = true;
				tradeForce?.Set(on: true);
			}
			else if (Hosting)
			{
				HostTrade = true;
			}
			else
			{
				JoinTrade = true;
			}
			AttachSpec();
			return;
		}
		if (force)
		{
			HostForcePerks = true;
			perksForce?.Set(on: true);
		}
		else if (Hosting)
		{
			HostPerks = true;
		}
		else
		{
			JoinPerks = true;
		}
		AttachPerks();
	}
}

[HarmonyPatch(typeof(NewGameSpecializationUI), "Open")]
internal static class RandomBoxesSpecOpenPatch
{
	[HarmonyPriority(Priority.Low)]
	private static void Postfix()
	{
		try
		{
			RandomBoxes.AttachSpec();
		}
		catch (Exception ex)
		{
			Log.Warn("random: the trade screen's boxes threw: " + ex.Message);
		}
	}
}

[HarmonyPatch(typeof(NewGamePerksUI), "Open")]
internal static class RandomBoxesPerksOpenPatch
{
	[HarmonyPriority(Priority.Low)]
	private static void Postfix()
	{
		try
		{
			RandomBoxes.AttachPerks();
		}
		catch (Exception ex)
		{
			Log.Warn("random: the perk screen's boxes threw: " + ex.Message);
		}
	}
}

[HarmonyPatch]
internal static class RandomBoxesShowScenarioPatch
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

	[HarmonyPriority(Priority.Low)]
	private static void Postfix()
	{
		try
		{
			if (RandomBoxes.TradeOn)
			{
				RandomBoxes.SpecLook();
			}
		}
		catch
		{
		}
	}
}

[HarmonyPatch]
internal static class RandomBoxesRebuildPatch
{
	private static readonly MethodBase target = AccessTools.Method(typeof(NewGamePerksUI), "Rebuild", Type.EmptyTypes, (Type[])null);

	private static bool Prepare()
	{
		return target != null;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	[HarmonyPriority(Priority.Low)]
	private static void Postfix()
	{
		try
		{
			if (RandomBoxes.PerksOn)
			{
				RandomBoxes.PerksLook();
			}
		}
		catch
		{
		}
	}
}
