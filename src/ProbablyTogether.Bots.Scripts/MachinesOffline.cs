using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace ProbablyTogether.Bots.Scripts;

public sealed class MachinesOffline : BotScript
{
	private readonly Dictionary<string, GameItem> fx = new Dictionary<string, GameItem>(StringComparer.Ordinal);

	private int sent0;

	private int quiet0;

	private int loud0;

	private int blocked0;

	public override string Name => "machines-offline";

	public override float TimeoutSeconds => 300f;

	private GameItem F(string n)
	{
		if (!fx.TryGetValue(n, out var value))
		{
			return null;
		}
		return value;
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

	private GameItem Spawn(string name, string kind, GameGridInventory inv)
	{
		GameItem val = DirectoryMaster.Item(kind, true) ?? throw new InvalidOperationException("no item " + kind);
		for (int i = 0; i < 2; i++)
		{
			if (val.parentInventory != null)
			{
				break;
			}
			int num = 4;
			while (num >= 1 && val.parentInventory == null)
			{
				for (int j = 0; j < inv.inventoryShape.height; j++)
				{
					if (val.parentInventory != null)
					{
						break;
					}
					for (int k = 0; k < inv.inventoryShape.width; k++)
					{
						if (val.parentInventory != null)
						{
							break;
						}
						SlotMarker val2 = ((GameInventory)inv).TryInventorySlot(val, val.unitCount, (GridShape)(object)new GridShapeBuilder(val.modifiedShape).SetTransform(k, j, i == 1, num), (TagSystem)null);
						if (val2 != null && val2.targetItem == null && (object)val2.inventory == inv && val2.numTransfer >= val.unitCount)
						{
							val2.TryAcceptOnce(-1);
						}
					}
				}
				num--;
			}
		}
		if (val.parentInventory == null)
		{
			val.Destroy();
			throw new InvalidOperationException("no room for " + kind);
		}
		GeneralHelper.SetItemOwned(val, true);
		foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
		{
			GeneralHelper.SetItemOwned(item, true);
		}
		fx[name] = val;
		return val;
	}

	private void Battery(GameItem m)
	{
		GameItem val = PreBuiltItemHelper.EnergyCredit(50);
		GraphUtils.TryAcceptAll((GraphNodeStorage)(object)MachineHelper.GetBatterySlot(m), val, -1);
		GeneralHelper.SetItemOwned(val, true);
	}

	private int Out()
	{
		GameInventory slot = MachineHelper.GetSlot(F("box"), 1);
		if (slot == null)
		{
			return -1;
		}
		return slot.childItems.Count;
	}

	private GameItem Cp()
	{
		return EmporiumEntry.Instance.cassettePlayer;
	}

	private string CpText()
	{
		return Cp().GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt() + "|" + Cp().GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt();
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
			return "new run from the menu, offline";
		});
		WaitFor("shop", 120f, () => BotProbes.InShop && Game.ShopLoaded, () => "scene " + BotProbes.Scene);
		Delay("settle", 2f);
		Do("spawn", delegate
		{
			EmporiumEntry instance = EmporiumEntry.Instance;
			Spawn("box", "bottle_printer", instance.invElement);
			Battery(F("box"));
			Spawn("plate", "heating_plate", instance.invElement);
			Spawn("coord", "storage_coordinator", instance.invElement);
			Spawn("bagA", "backpack_large", instance.invElement);
			Spawn("bagB", "backpack_large", instance.invElement);
			GraphNodeStorage child = F("bagA").contentWindow.child;
			GameGridInventory inv = (GameGridInventory)(object)((child is GameGridInventory) ? child : null);
			GraphNodeStorage child2 = F("bagB").contentWindow.child;
			GameGridInventory inv2 = (GameGridInventory)(object)((child2 is GameGridInventory) ? child2 : null);
			Spawn("in", "intake_hopper", inv);
			Spawn("out", "outlet_hopper", inv2);
			Spawn("scrap", "scrap_metal", inv);
			HopperHelper.Link(F("in"), F("out"));
			string[] array = new string[2] { "cassette1", "cassette2" };
			for (int i = 0; i < array.Length; i++)
			{
				GameItem val = DirectoryMaster.Item(array[i], true);
				GeneralHelper.SetItemOwned(val, true);
				GraphUtils.TryAcceptAll((GraphNodeStorage)(object)MachineCassettePlayer.GetCassetteInventory(), val, -1);
			}
			sent0 = Machines.SentCount;
			quiet0 = Machines.QuietRuns;
			loud0 = Machines.LoudRuns;
			blocked0 = ItemsUse.QuietSoundsBlocked + ItemsUse.QuietNoticesBlocked;
			return fx.Count + " fixtures; cassettes in the deck " + MachineCassettePlayer.GetTrackListFromCassette(MachineCassettePlayer.GetCassetteInventory()).Count;
		});
		AtOnce("button", delegate
		{
			MachinesX.Press(MachinesX.ButtonText(F("box"), "Print Small Bottle") ?? MachinesX.Widget0(F("box")));
			return "printer button";
		}, () => Out() == 1, () => "output " + Out());
		AtOnce("radio", delegate
		{
			MachineryRadioButton val = Machines.Widgets(F("plate")).OfType<MachineryRadioButton>().ElementAt(2);
			val.SetOn(true, true);
			return "plate radio '" + val.text + "'";
		}, () => F("plate").GetTagReadonly("HEATING_PLATE_STATE").GetInt() == 2, () => "state " + F("plate").GetTagReadonly("HEATING_PLATE_STATE").GetInt());
		AtOnce("hopper-run", delegate
		{
			int num = HopperHelper.RunAndRecord(F("in"));
			Vars["moved"] = num.ToString();
			return "RunAndRecord returned " + num;
		}, () => (object)F("scrap").parentInventory == F("bagB").contentWindow.child, () => "scrap in bag " + (((object)F("scrap").parentInventory == F("bagB").contentWindow.child) ? "B" : "A"));
		AtOnce("hopper-setting", delegate
		{
			F("in").ModifyTag("HOPPER_KEEP_INT", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(3);
			}, false);
			return "keep 3 written";
		}, () => F("in").GetTagReadonly("HOPPER_KEEP_INT").GetInt() == 3, () => "keep " + F("in").GetTagReadonly("HOPPER_KEEP_INT").GetInt());
		AtOnce("presets", delegate
		{
			MachineStorageCoordinator.SavePresets(F("coord"), new List<MachineStorageCoordinator.Preset>
			{
				new MachineStorageCoordinator.Preset
				{
					name = "solo"
				}
			});
			return "preset saved";
		}, () => MachineStorageCoordinator.LoadPresets(F("coord")).Count == 1, () => MachineStorageCoordinator.LoadPresets(F("coord")).Count + " preset(s)");
		AtOnce("cassette-play", delegate
		{
			MachinesX.Press(MachinesX.ButtonCalling(Cp(), AccessTools.Method(typeof(MachineCassettePlayer), "OnPlayButtonPressed", (Type[])null, (Type[])null)));
			return "Play";
		}, () => Cp().GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt() == 1, () => "cassette " + CpText());
		AtOnce("track-end", delegate
		{
			Vars["i0"] = Cp().GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt().ToString();
			AudioManager.OnTrackDone();
			return "AudioManager.OnTrackDone (index " + Vars["i0"] + ")";
		}, () => Cp().GetTagReadonly("CURRENT_PLAYING_CASSETTE_INDEX_TAG").GetInt().ToString() != Vars["i0"], () => "cassette " + CpText());
		Check("nothing-sent", () => Machines.SentCount == sent0 && Net.Role == ProbablyTogether.Role.Off, () => "machine requests " + (Machines.SentCount - sent0) + ", role " + Net.Role);
		Check("nothing-silenced", () => Machines.QuietRuns == quiet0 && ItemsUse.QuietSoundsBlocked + ItemsUse.QuietNoticesBlocked == blocked0 && Machines.LoudRuns == loud0, () => "quiet runs " + (Machines.QuietRuns - quiet0) + ", loud runs " + (Machines.LoudRuns - loud0) + ", sounds and notices kept off " + (ItemsUse.QuietSoundsBlocked + ItemsUse.QuietNoticesBlocked - blocked0));
	}
}
