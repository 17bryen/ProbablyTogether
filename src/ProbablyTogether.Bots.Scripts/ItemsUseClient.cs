using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ItemsUseClient : BotScript
{
	private string k = "0";

	private float lastAsk;

	private float lastNote;

	private int askId = -1;

	private int asks;

	private int replies;

	private int before;

	private string fxDetail = "no reply yet";

	public override string Name => "items-use";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 400f;

	private GameItem I(string name)
	{
		return ItemsUseLab.Item(k + "." + name);
	}

	protected override void Build()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "player " + Net.LocalPlayer);
		WaitFor("shop-loaded", 180f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene + ", live " + Net.IsLive);
		string value;
		Step("fixtures", 150f, delegate(BotStep st)
		{
			if (askId >= 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(askId);
				if (dictionary != null)
				{
					askId = -1;
					replies++;
					dictionary.TryGetValue("use.ready", out value);
					dictionary.TryGetValue("use.fx", out var value2);
					fxDetail = "reply " + replies + ": server ready=" + (value ?? "?") + ", " + (value2 ?? "").Split(';').Length + " fixture ids";
					if (value == "1" && !string.IsNullOrEmpty(value2))
					{
						ItemsUseLab.ParseFx(value2);
						k = ItemsUseLab.MySet();
						List<string> list = ItemsUseLab.Fx.Keys.Where((string text) => (text.StartsWith(k + ".", StringComparison.Ordinal) || text == "s.toolbox") && ItemsUseLab.Item(text) == null).ToList();
						fxDetail = "set " + k + " (players " + string.Join(",", Net.Players.Keys) + "), " + ItemsUseLab.Fx.Count + " fixture ids from the server, " + Ids.Count + " ids indexed here" + ((list.Count > 0) ? ("; not found here: " + string.Join(",", list.Select((string text) => text + "=" + ItemsUseLab.Fx[text]))) : "");
						st.Detail = fxDetail;
						if (list.Count == 0)
						{
							return StepStatus.Pass;
						}
					}
				}
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (askId < 0 && realtimeSinceStartup - lastAsk > 2f)
			{
				lastAsk = realtimeSinceStartup;
				asks++;
				askId = BotProbes.AskServer();
			}
			if (realtimeSinceStartup - lastNote > 10f)
			{
				lastNote = realtimeSinceStartup;
				BotLog.Info(Name, "fixtures: " + asks + " asks, " + fxDetail);
			}
			return StepStatus.Running;
		}).TimeoutDetail = () => asks + " asks; " + fxDetail;
		AskServer("ask-server-use");
		WaitFor("server-use-seen", 30f, () => base.Server.TryGetValue("use.s.slots", out value) && ItemsUseLab.Slots("s").ToString(CultureInfo.InvariantCulture) == value, () => "server's toolbox " + ItemsUseLab.Slots("s") + " slots here, " + (base.Server.TryGetValue("use.s.slots", out value) ? value : "?") + " on the server");
		for (int num = 1; num <= 3; num++)
		{
			int n = num;
			if (n < 3)
			{
				Do("upgrade-" + n, delegate
				{
					before = ItemsUseLab.Slots(k);
					I("scrap" + n).Target(I("toolbox"));
					return "set " + k + ": scrap " + n + " on the toolbox (" + before + " slots), request sent";
				});
			}
			else
			{
				Check("upgrade-" + n + "-drag", delegate
				{
					before = ItemsUseLab.Slots(k);
					GameItem obj = I("scrap" + n);
					GameInventory parentInventory = obj.parentInventory;
					int sentCount = ItemsUse.SentCount;
					int sentCount2 = ItemsMove.SentCount;
					string text = ItemsUseLab.Drop(obj, I("toolbox"));
					int num2 = ItemsUse.SentCount - sentCount;
					int num3 = ItemsMove.SentCount - sentCount2;
					bool flag = obj.parentInventory == parentInventory && ItemsUseLab.Slots(k) == before;
					Vars["drag"] = "EndDrag scrap " + n + " onto the toolbox: " + num2 + " items-use request(s), " + num3 + " items-move request(s), " + (flag ? "unchanged here before the answer" : "FAIL changed locally") + text;
					return num2 == 1 && num3 == 0 && flag;
				}, () => (!Vars.TryGetValue("drag", out value)) ? "" : value);
			}
			WaitFor("upgrade-" + n + "-applied", 20f, () => ItemsUseLab.Slots(k) == before + 1 && !ItemsUseLab.Alive(k + ".scrap" + n), () => ItemsUseLab.Slots(k) + " slots, scrap " + (ItemsUseLab.Alive(k + ".scrap" + n) ? "still here" : "gone"));
		}
		Check("put-tool-drag", delegate
		{
			GameItem obj = I("tool");
			GameInventory parentInventory = obj.parentInventory;
			int sentCount = ItemsUse.SentCount;
			int sentCount2 = ItemsMove.SentCount;
			string text = ItemsUseLab.Drop(obj, I("toolbox"));
			int num2 = ItemsUse.SentCount - sentCount;
			int num3 = ItemsMove.SentCount - sentCount2;
			bool flag = obj.parentInventory == parentInventory;
			Vars["put"] = "EndDrag flashlight onto the toolbox: " + num3 + " items-move request(s) (last " + ItemsMove.LastSentKind + "), " + num2 + " items-use request(s), " + (flag ? "unchanged here before the answer" : "FAIL moved locally") + text;
			return num3 == 1 && ItemsMove.LastSentKind == "items.insertInto" && flag;
		}, () => (!Vars.TryGetValue("put", out value)) ? "" : value);
		WaitFor("put-tool-applied", 20f, () => ItemsUseLab.Tool(k).StartsWith("box:toolbox_slot_", StringComparison.Ordinal), () => "flashlight " + ItemsUseLab.Tool(k));
		AskServer("ask-upgrade");
		Same("same-slots", "slots");
		Same("same-scrap", "scrap");
		Same("same-tool", "tool");
		Do("expand", delegate
		{
			before = ItemsUseLab.Tabs(k);
			I("kit").Target(I("bay"));
			return before + " tab(s); request sent";
		});
		WaitFor("expand-applied", 20f, () => ItemsUseLab.Tabs(k) == before + 2 && !ItemsUseLab.Alive(k + ".kit"), () => ItemsUseLab.Tabs(k) + " tabs, kit " + (ItemsUseLab.Alive(k + ".kit") ? "still here" : "gone"));
		Do("rename", delegate
		{
			StorageExpansionHelper.SetTabName(I("bay"), 1, "bot" + Net.LocalPlayer);
			return "tab 2 -> bot" + Net.LocalPlayer;
		});
		WaitFor("rename-applied", 20f, () => ItemsUseLab.TabName(k, 1) == "bot" + Net.LocalPlayer, () => "tab 2 is '" + ItemsUseLab.TabName(k, 1) + "'");
		AskServer("ask-storage");
		Same("same-tabs", "tabs");
		Same("same-names", "names");
		Same("same-kit", "kit");
		Do("toggle", delegate
		{
			Vars["on"] = (ItemsUseLab.ScannerOn(k) ? "1" : "0");
			I("scanner").ToggleSlotItem();
			return "scanner was " + ((Vars["on"] == "1") ? "on" : "off");
		});
		WaitFor("toggle-applied", 20f, () => (ItemsUseLab.ScannerOn(k) ? "1" : "0") != Vars["on"], () => "scanner " + (ItemsUseLab.ScannerOn(k) ? "on" : "off"));
		Do("activate-chem", delegate
		{
			I("chem").ActivateSlotItem();
			return "request sent; RETIRED_CHEMIST " + (NetworkUpgrade.IsUnlocked("RETIRED_CHEMIST") ? "unlocked (no question)" : "locked (a question first)");
		});
		WaitFor("confirm-open", 20f, () => !ItemsUseLab.Alive(k + ".chem") || BotProbes.Read("use.confirmui") == "1", () => "confirm panel " + BotProbes.Read("use.confirmui") + ", chem " + (ItemsUseLab.Alive(k + ".chem") ? "here" : "gone"));
		Do("confirm-yes", delegate
		{
			if (!ItemsUseLab.Alive(k + ".chem"))
			{
				return "no question asked (unpacked at once)";
			}
			GenericConfirmationUIManager.Instance.ConfirmButtonPressed();
			return "pressed Yes (the answer goes to the server)";
		});
		WaitFor("chem-unpacked", 20f, () => !ItemsUseLab.Alive(k + ".chem"), () => "chem " + (ItemsUseLab.Alive(k + ".chem") ? "still here" : "gone") + ", confirm panel " + BotProbes.Read("use.confirmui"));
		Do("activate-ticket", delegate
		{
			I("ticket").ActivateSlotItem();
			return "ticket " + ItemsUseLab.Ticket(k);
		});
		WaitFor("scratch-panel", 20f, delegate
		{
			ScratcherUIManager instance = ScratcherUIManager.Instance;
			return (Object)(object)instance != (Object)null && instance.currentActiveScratcherItem == I("ticket");
		}, () => "scratch panel holds '" + BotProbes.Read("use.scratchui") + "', my ticket is " + Ids.Of(I("ticket")));
		Do("scratch-start", delegate
		{
			ScratcherUIManager instance = ScratcherUIManager.Instance;
			if (instance.scratchInputs == null || instance.scratchInputs.Count == 0)
			{
				throw new InvalidOperationException("the scratch panel has no inputs");
			}
			instance.scratchInputs[0].OnAnyScratch();
			return "first stroke";
		});
		WaitFor("scratch-started", 20f, () => !I("ticket").IsTag("SCRATCHER_NEW_TAG"), () => ItemsUseLab.Ticket(k));
		Do("scratch-symbols", delegate
		{
			for (int i = 1; i <= 3; i++)
			{
				ScratcherHelper.MarkSymbolScratched(I("ticket"), i);
			}
			return "symbols 1-3 sent";
		});
		WaitFor("scratched", 20f, () => ItemsUseLab.Ticket(k).StartsWith("s111", StringComparison.Ordinal), () => ItemsUseLab.Ticket(k));
		Do("close-panel", delegate
		{
			ScratcherUIManager.Instance.CloseUI();
			return "closed";
		});
		AskServer("ask-use");
		Same("same-scanner", "scanner");
		Same("same-chem", "chem");
		Same("same-ticket", "ticket");
		Do("charge", delegate
		{
			before = ItemsUseLab.Charge(k);
			I("elec").Target(I("mind"));
			return "electronic on the mind reprogrammer (charge " + before + ")";
		});
		WaitFor("charge-applied", 20f, () => ItemsUseLab.Charge(k) == before + 1 && !ItemsUseLab.Alive(k + ".elec"), () => "charge " + ItemsUseLab.Charge(k) + ", electronic " + (ItemsUseLab.Alive(k + ".elec") ? "still here" : "gone"));
		Do("perk-window", delegate
		{
			I("mind").ActivateSlotItem();
			return "activate sent";
		});
		WaitFor("perk-window-open", 20f, ItemsUseLab.PerkWindowOpen, () => "perk window " + (ItemsUseLab.PerkWindowOpen() ? "open" : "closed"));
		Check("perk-apply", delegate
		{
			GameItem val = I("mind");
			StartingPerk val2 = ItemsUseLab.PickPerk(val, k);
			if (val2 == null)
			{
				Vars["perk"] = "no positive perk this run can add (perks: " + ItemsUseLab.Perks() + ", charge " + ItemsUseLab.Charge(k) + ")";
				return false;
			}
			Vars["perkId"] = val2.id;
			Vars["cost"] = MindReprogrammerHelper.GetCost(val2).ToString(CultureInfo.InvariantCulture);
			before = ItemsUseLab.Charge(k);
			int sentCount = ItemsUse.SentCount;
			MindReprogrammerHelper.Apply(val, val2);
			bool flag = !StartingPerk.IsPerkActive(val2.id) && ItemsUseLab.Charge(k) == before;
			Vars["perk"] = "add " + val2.id + " (cost " + Vars["cost"] + ", charge " + before + "): " + (ItemsUse.SentCount - sentCount) + " request(s), " + (flag ? "unchanged here before the answer" : "FAIL applied locally");
			return ItemsUse.SentCount - sentCount == 1 && flag;
		}, () => (!Vars.TryGetValue("perk", out value)) ? "" : value);
		WaitFor("perk-applied", 20f, delegate
		{
			int num2 = int.Parse(Vars["cost"], CultureInfo.InvariantCulture);
			return StartingPerk.IsPerkActive(Vars["perkId"]) && ItemsUseLab.Charge(k) == before - num2 && ItemsUseLab.Cooldown(k) == num2 * 5;
		}, () => Vars["perkId"] + (StartingPerk.IsPerkActive(Vars["perkId"]) ? " active" : " not active") + ", charge " + ItemsUseLab.Charge(k) + ", cooldown " + ItemsUseLab.Cooldown(k));
		Do("perk-again", delegate
		{
			before = ItemsUse.RefusedCount;
			StartingPerk val = ((IEnumerable<StartingPerk>)MindReprogrammerHelper.GetEditablePerks()).FirstOrDefault((Func<StartingPerk, bool>)((StartingPerk p) => p.id == Vars["perkId"]));
			MindReprogrammerHelper.Apply(I("mind"), val);
			return "remove " + Vars["perkId"] + " while cooling down (" + ItemsUseLab.Cooldown(k) + " days)";
		});
		WaitFor("perk-refused", 20f, () => ItemsUse.RefusedCount == before + 1, () => "refusals " + ItemsUse.RefusedCount + " (last: " + ItemsUse.LastRefusal + ")");
		Do("confirm-forged", delegate
		{
			before = ItemsUse.RefusedCount;
			Net.ToServer("items.use.confirm", new ItemsUse.MsgConfirm
			{
				of = 1L,
				index = -1,
				yes = true
			});
			return "sent items.use.confirm {of 1, index -1}";
		});
		WaitFor("confirm-forged-refused", 20f, () => ItemsUse.RefusedCount == before + 1 && ItemsUse.LastRefusal.Contains("no such question"), () => "refusals " + ItemsUse.RefusedCount + " (last: " + ItemsUse.LastRefusal + ")");
		Do("perk-close", delegate
		{
			MindReprogrammerHelper.CloseWindow();
			return "closed; perks " + ItemsUseLab.Perks();
		});
		AskServer("ask-perk");
		Same("same-charge", "charge");
		Same("same-cooldown", "cooldown");
		Same("same-elec", "elec");
		Step("same-perks", 0f, delegate(BotStep st)
		{
			string text = ItemsUseLab.Perks();
			string text2 = (base.Server.TryGetValue("use.perks", out value) ? value : "?");
			st.Detail = "perks here [" + text + "], server [" + text2 + "]";
			return (text == text2) ? StepStatus.Pass : StepStatus.Fail;
		});
		Check("quiet-scope", delegate
		{
			bool quietUi = ItemsUse.QuietUi;
			int quietSoundsBlocked = ItemsUse.QuietSoundsBlocked;
			int sentCount = ItemsUse.SentCount;
			bool quietUi2;
			bool flag;
			int num2;
			int num3;
			using (ItemsUse.Quiet())
			{
				quietUi2 = ItemsUse.QuietUi;
				flag = (Object)(object)AudioManager.Instance != (Object)null && AudioManager.Instance.Play2("turbo");
				num2 = ItemsUse.QuietSoundsBlocked - quietSoundsBlocked;
				EmporiumAudioPlayer instance = EmporiumAudioPlayer.Instance;
				if ((Object)(object)instance != (Object)null && (Object)(object)instance.ratchet != (Object)null)
				{
					// Go through QuietAudioCallSites, the same way the game's own audio calls do.
					QuietAudioCallSites.Play(instance.ratchet);
				}
				num3 = ItemsUse.QuietSoundsBlocked - quietSoundsBlocked - num2;
			}
			bool quietUi3 = ItemsUse.QuietUi;
			Vars["quiet"] = "QuietUi outside " + quietUi + ", inside " + quietUi2 + ", after " + quietUi3 + "; AudioManager.Play2 " + (flag ? "PLAYED" : "refused") + " (" + num2 + " blocked), ratchet AudioSource.Play " + num3 + " blocked (EmporiumAudioPlayer " + (((Object)(object)EmporiumAudioPlayer.Instance != (Object)null) ? "here" : "absent") + "), stopped at scope end so far " + ItemsUse.QuietSoundsStopped + "; " + (ItemsUse.SentCount - sentCount) + " request(s)";
			return !quietUi && quietUi2 && !quietUi3 && !flag && ((Object)(object)AudioManager.Instance == (Object)null || num2 == 1) && ItemsUse.SentCount == sentCount;
		}, () => (!Vars.TryGetValue("quiet", out value)) ? "" : value);
		Check("audiosource-hook", () => (Object)(object)EmporiumAudioPlayer.Instance == (Object)null || Vars["quiet"].Contains("ratchet AudioSource.Play 1 blocked"), () => Vars["quiet"]).Soft();
		Do("refused", delegate
		{
			before = ItemsUse.RefusedCount;
			I("toolbox").ToggleSlotItem();
			return "toggle on a toolbox sent";
		});
		WaitFor("refused-answer", 20f, () => ItemsUse.RefusedCount == before + 1, () => "refusals " + ItemsUse.RefusedCount + " (last: " + ItemsUse.LastRefusal + ")");
		Step("converged", 60f, delegate(BotStep st)
		{
			if (askId >= 0)
			{
				Dictionary<string, string> r = BotProbes.TakeReply(askId);
				if (r != null)
				{
					askId = -1;
					List<string> list = (from key in ItemsUseLab.StateKeys()
						where !r.TryGetValue(key, out value) || value != BotProbes.Read(key)
						select key + " here " + BotProbes.Read(key) + " server " + (r.TryGetValue(key, out value) ? value : "?")).ToList();
					st.Detail = ((list.Count == 0) ? ("all " + ItemsUseLab.StateKeys().Count() + " fixture values equal the server's: " + BotProbes.Format(r, (from x in ItemsUseLab.StateKeys()
						where x.StartsWith("use." + k + ".")
						select x).ToArray())) : (list.Count + " differ: " + string.Join("; ", list.Take(6))));
					if (list.Count == 0)
					{
						return StepStatus.Pass;
					}
				}
			}
			if (askId < 0 && Time.realtimeSinceStartup - lastAsk > 3f)
			{
				lastAsk = Time.realtimeSinceStartup;
				askId = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		AskServer("ask-final");
		SameAsServer("same-uid", "use.uid").Soft();
		SettlesLikeServer("same-digest-part", "use.part", 30f);
		Check("no-mismatch", () => ItemsUse.MismatchCount == 0, () => ItemsUse.MismatchCount + " replay mismatch(es); " + ItemsUse.AppliedCount + " uses replayed here");
		Check("other-uses-quiet", () => ItemsUse.QuietSoundsBlocked + ItemsUse.QuietSoundsStopped > 0, () => "the other player's uses replayed quiet here: sounds blocked/stopped " + BotProbes.Read("use.quietsounds")).Soft();
		Check("machines-checked", () => Machines.MismatchCount == 0, () => "machine.check after uses on machines (reprogrammer, scanner): ok " + Machines.ChecksOk + ", mismatches " + Machines.MismatchCount + ", repairs asked " + Machines.RepairsAsked).Soft();
		Check("other-set-quiet", delegate
		{
			string text = BotProbes.Read("use.scratchui");
			return text == "" || text == BotScript.Str(Ids.Of(I("ticket")));
		}, () => "scratch panel holds '" + BotProbes.Read("use.scratchui") + "' (never the other player's ticket)");
	}

	private BotStep Same(string name, string field)
	{
		return Step(name, 0f, delegate(BotStep st)
		{
			string text = "use." + k + "." + field;
			string text2 = BotProbes.Read(text);
			string value;
			string text3 = (base.Server.TryGetValue(text, out value) ? value : "?");
			st.Detail = text + " here " + text2 + ", server " + text3;
			return (text2 == text3 && text2 != "") ? StepStatus.Pass : StepStatus.Fail;
		});
	}
}
