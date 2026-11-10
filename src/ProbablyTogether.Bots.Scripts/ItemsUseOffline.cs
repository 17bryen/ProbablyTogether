using System;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsUseOffline : BotScript
{
	private int before;

	private const string k = "0";

	public override string Name => "items-use-offline";

	public override float TimeoutSeconds => 300f;

	private static GameItem I(string name)
	{
		return ItemsUseLab.Item("0." + name);
	}

	private BotStep AtOnce(string name, Func<string> act, Func<bool> done, Func<string> state)
	{
		string value;
		return Check(name, delegate
		{
			string text = act();
			bool flag = done();
			Vars[name] = text + " -> " + state() + (flag ? " (at once, in the game's own call)" : " (NOT changed in the call)");
			return flag;
		}, () => (!Vars.TryGetValue(name, out value)) ? "" : value);
	}

	protected override void Build()
	{
		WaitFor("menu", 90f, () => BotProbes.Scene == "EmporiumMenu" && (Object)(object)NewGameData.Instance != (Object)null, () => "scene " + BotProbes.Scene);
		Check("offline", () => Net.Role == ProbablyTogether.Role.Off && !Net.IsOnline, () => "role " + Net.Role);
		Do("new-run", delegate
		{
			NewGameData instance = NewGameData.Instance;
			instance.saveFile = null;
			instance.isLoadFromMainMenu = false;
			instance.saveSlot = -1;
			instance.startType = (NewGameData.StartType)1;
			instance.startScenarioId = "";
			instance.skipIntro = true;
			instance.disableTutorialHint = true;
			instance.difficultyId = "normal";
			instance.endlessMode = false;
			EmporiumEntry.sceneLoadStartTime = Time.realtimeSinceStartup;
			SceneManager.LoadScene("InventoryScene", (LoadSceneMode)0);
			return "new run (WATER_MERCHANT, normal, skip intro) from the menu, offline";
		});
		WaitFor("shop", 120f, () => BotProbes.InShop && Game.ShopLoaded, () => "scene " + BotProbes.Scene);
		Delay("settle", 2f);
		Do("spawn", () => ItemsUseLab.SpawnAll());
		AtOnce("upgrade", delegate
		{
			before = ItemsUseLab.Slots("0");
			I("scrap1").Target(I("toolbox"));
			return "scrap on the toolbox (" + before + " slots)";
		}, () => ItemsUseLab.Slots("0") == before + 1 && !ItemsUseLab.Alive("0.scrap1"), () => ItemsUseLab.Slots("0") + " slots");
		AtOnce("upgrade-drag", delegate
		{
			before = ItemsUseLab.Slots("0");
			return "EndDrag scrap onto the toolbox" + ItemsUseLab.Drop(I("scrap2"), I("toolbox"));
		}, () => ItemsUseLab.Slots("0") == before + 1 && !ItemsUseLab.Alive("0.scrap2"), () => ItemsUseLab.Slots("0") + " slots");
		AtOnce("put-tool-drag", () => "EndDrag flashlight onto the toolbox" + ItemsUseLab.Drop(I("tool"), I("toolbox")), () => ItemsUseLab.Tool("0").StartsWith("box:toolbox_slot_", StringComparison.Ordinal), () => "flashlight " + ItemsUseLab.Tool("0"));
		AtOnce("expand", delegate
		{
			before = ItemsUseLab.Tabs("0");
			I("kit").Target(I("bay"));
			return "kit on the bay (" + before + " tabs)";
		}, () => ItemsUseLab.Tabs("0") == before + 2, () => ItemsUseLab.Tabs("0") + " tabs");
		AtOnce("rename", delegate
		{
			StorageExpansionHelper.SetTabName(I("bay"), 1, "solo");
			return "tab 2 -> solo";
		}, () => ItemsUseLab.TabName("0", 1) == "solo", () => "tab 2 '" + ItemsUseLab.TabName("0", 1) + "'");
		AtOnce("toggle", delegate
		{
			Vars["on"] = (ItemsUseLab.ScannerOn("0") ? "1" : "0");
			I("scanner").ToggleSlotItem();
			return "scanner was " + Vars["on"];
		}, () => (ItemsUseLab.ScannerOn("0") ? "1" : "0") != Vars["on"], () => "scanner " + (ItemsUseLab.ScannerOn("0") ? "on" : "off"));
		AtOnce("activate-chem", delegate
		{
			I("chem").ActivateSlotItem();
			return "chemical supplies used";
		}, () => !ItemsUseLab.Alive("0.chem") || BotProbes.Read("use.confirmui") == "1", () => "confirm panel " + BotProbes.Read("use.confirmui"));
		AtOnce("confirm-yes", delegate
		{
			if (ItemsUseLab.Alive("0.chem"))
			{
				GenericConfirmationUIManager.Instance.ConfirmButtonPressed();
			}
			return "Yes";
		}, () => !ItemsUseLab.Alive("0.chem"), () => "chem " + (ItemsUseLab.Alive("0.chem") ? "here" : "unpacked"));
		AtOnce("ticket", delegate
		{
			I("ticket").ActivateSlotItem();
			return "ticket used";
		}, delegate
		{
			ScratcherUIManager instance = ScratcherUIManager.Instance;
			return (Object)(object)instance != (Object)null && instance.currentActiveScratcherItem == I("ticket");
		}, () => "scratch panel '" + BotProbes.Read("use.scratchui") + "'");
		AtOnce("scratch", delegate
		{
			ScratcherUIManager.Instance.scratchInputs[0].OnAnyScratch();
			for (int i = 1; i <= 3; i++)
			{
				ScratcherHelper.MarkSymbolScratched(I("ticket"), i);
			}
			return "first stroke + 3 symbols";
		}, () => ItemsUseLab.Ticket("0").StartsWith("s111", StringComparison.Ordinal), () => ItemsUseLab.Ticket("0"));
		Do("close-scratch", delegate
		{
			ScratcherUIManager.Instance.CloseUI();
			return "closed";
		});
		AtOnce("charge", delegate
		{
			before = ItemsUseLab.Charge("0");
			I("elec").Target(I("mind"));
			return "electronic on the reprogrammer (charge " + before + ")";
		}, () => ItemsUseLab.Charge("0") == before + 1, () => "charge " + ItemsUseLab.Charge("0"));
		AtOnce("perk-window", delegate
		{
			I("mind").ActivateSlotItem();
			return "reprogrammer used";
		}, ItemsUseLab.PerkWindowOpen, () => "perk window " + BotProbes.Read("use.perkui"));
		AtOnce("perk", delegate
		{
			StartingPerk val = ItemsUseLab.PickPerk(I("mind"), "0");
			if (val == null)
			{
				return "no perk to add";
			}
			Vars["perkId"] = val.id;
			MindReprogrammerHelper.Apply(I("mind"), val);
			return "add " + val.id;
		}, () => Vars.TryGetValue("perkId", out var value) && StartingPerk.IsPerkActive(value) && ItemsUseLab.Cooldown("0") > 0, () => "perks " + ItemsUseLab.Perks() + ", cooldown " + ItemsUseLab.Cooldown("0"));
		Do("close-perk", delegate
		{
			MindReprogrammerHelper.CloseWindow();
			return "closed";
		});
		Check("nothing-sent", () => ItemsUse.SentCount == 0 && ItemsUse.AppliedCount == 0 && ItemsUse.RefusedCount == 0 && ItemsUse.QuietSoundsBlocked == 0 && ItemsUse.QuietSoundsStopped == 0 && !ItemsUse.QuietUi, () => "requests " + ItemsUse.SentCount + ", applied " + ItemsUse.AppliedCount + ", refused " + ItemsUse.RefusedCount + ", sounds blocked/stopped " + ItemsUse.QuietSoundsBlocked + "/" + ItemsUse.QuietSoundsStopped + ", quiet " + ItemsUse.QuietUi);
	}
}
