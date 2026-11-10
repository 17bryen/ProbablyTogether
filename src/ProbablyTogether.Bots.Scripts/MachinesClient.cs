using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public sealed class MachinesClient : BotScript
{
	private string k = "0";

	private float lastAsk;

	private float lastNote;

	private int askId = -1;

	private int asks;

	private int before;

	private int applied0;

	private int suppressed0;

	private int checks0;

	private int loaded0;

	private int mismatch0;

	private int prod0;

	private string fxDetail = "no reply yet";

	public override string Name => "machines";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 400f;

	private GameItem I(string name)
	{
		return MachinesLab.Item(k + "." + name);
	}

	private BotStep Same(string name, string probe)
	{
		return Step(name, 0f, delegate(BotStep st)
		{
			string text = probe.Replace("{k}", k);
			string text2 = BotProbes.Read(text);
			if (!base.Server.TryGetValue(text, out var value))
			{
				st.Detail = "server sent no '" + text + "'";
				return StepStatus.Fail;
			}
			st.Detail = text + " here " + text2 + ", server " + value;
			return (text2 == value && text2 != "") ? StepStatus.Pass : StepStatus.Fail;
		});
	}

	private BotStep WaitMarks(string name, float timeout, params string[] want)
	{
		int id = -1;
		float last = 0f;
		string seen = "no reply yet";
		BotStep botStep = Step(name, timeout, delegate(BotStep st)
		{
			if (id >= 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(id);
				if (dictionary != null)
				{
					id = -1;
					seen = (dictionary.TryGetValue("machx.marks", out var value) ? value : "?");
					st.Detail = "server marks " + seen + " (" + Net.Players.Count + " player(s)) after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s";
					if (want.All((string m) => MachinesLab.HasMark(seen, m)))
					{
						return StepStatus.Pass;
					}
				}
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (id < 0 && realtimeSinceStartup - last > 1f)
			{
				last = realtimeSinceStartup;
				id = BotProbes.AskServer();
			}
			return StepStatus.Running;
		});
		botStep.TimeoutDetail = () => "waited " + timeout.ToString("F0", CultureInfo.InvariantCulture) + " s for " + string.Join(", ", want) + "; server marks " + seen + ", " + Net.Players.Count + " player(s) (the machines set needs both clients)";
		return botStep;
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
					dictionary.TryGetValue("mach.ready", out value);
					dictionary.TryGetValue("mach.fx", out var value2);
					fxDetail = "server ready=" + (value ?? "?");
					if (value == "1" && !string.IsNullOrEmpty(value2))
					{
						MachinesLab.ParseFx(value2);
						k = MachinesLab.MySet();
						List<string> list = MachinesLab.Fx.Keys.Where((string n) => (n.StartsWith(k + ".", StringComparison.Ordinal) || n.StartsWith("s.", StringComparison.Ordinal)) && MachinesLab.Item(n) == null).ToList();
						fxDetail = "set " + k + ", " + MachinesLab.Fx.Count + " fixture ids" + ((list.Count > 0) ? ("; not here: " + string.Join(",", list)) : "");
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
		Do("mark-live", delegate
		{
			MachinesX.Mark("mach-live-" + k);
			return "told the server set " + k + " is live";
		});
		WaitMarks("both-live", 200f, "mach-live-0", "mach-live-1");
		WaitFor("server-press-seen", 20f, () => MachinesLab.OutCount("s") == 1 && MachinesLab.Energy("s") == 46, () => MachinesLab.Box("s"));
		AskServer("ask-server-press");
		Same("same-server-box", "mach.box.s");
		Same("same-server-box-digest", "mach.dg.s.box");
		Do("dispense", delegate
		{
			object obj = MachinesLab.Widget<MachineryButton>(I("box"), 0);
			MachineryButton val = (MachineryButton)((obj is MachineryButton) ? obj : null);
			if (val == null)
			{
				throw new InvalidOperationException("no button in the bottle printer window (" + Machines.Widgets(I("box")).Count + " widgets)");
			}
			before = Machines.SentCount;
			MachinesLab.Press(val);
			return "pressed '" + val.text + "' (widget " + Machines.Widgets(I("box")).IndexOf(val) + "); requests sent " + before + " -> " + Machines.SentCount + "; output here still " + MachinesLab.OutCount(k);
		});
		WaitFor("dispense-applied", 20f, () => MachinesLab.OutCount(k) == 1 && MachinesLab.Energy(k) == 46, () => MachinesLab.Box(k));
		AskServer("ask-dispense");
		Same("same-box", "mach.box.{k}");
		Same("same-box-digest", "mach.dg.{k}.box");
		Do("plate-mode", delegate
		{
			object obj = MachinesLab.Widget<MachineryRadioButton>(I("plate"), 2);
			MachineryRadioButton val = (MachineryRadioButton)((obj is MachineryRadioButton) ? obj : null);
			if (val == null)
			{
				throw new InvalidOperationException("no third radio button on the heating plate");
			}
			Vars["plate0"] = MachinesLab.Tag(k + ".plate", "HEATING_PLATE_STATE").ToString(CultureInfo.InvariantCulture);
			val.SetOn(true, true);
			return "radio '" + val.text + "' on; state here still " + MachinesLab.Tag(k + ".plate", "HEATING_PLATE_STATE") + " (was " + Vars["plate0"] + ")";
		});
		WaitFor("plate-mode-applied", 20f, () => MachinesLab.Tag(k + ".plate", "HEATING_PLATE_STATE") == 2, () => "state " + MachinesLab.Tag(k + ".plate", "HEATING_PLATE_STATE"));
		Do("feed-target", delegate
		{
			object obj = MachinesLab.Widget<MachinerySlider>(I("feed"), 0);
			object obj2 = ((obj is MachinerySlider) ? obj : null) ?? throw new InvalidOperationException("no slider on the feed dispenser");
			before = Machines.SentCount;
			prod0 = Production.AppliedCount;
			((MachinerySlider)obj2).SetValue(50f, true);
			return "slider -> 50 (the closure ran here: production carries it); machine requests " + before + " -> " + Machines.SentCount;
		});
		WaitFor("feed-target-applied", 20f, () => MachinesLab.Tag(k + ".feed", "FEED_DISPENSER_TARGET_PERCENT_INT") == 50 && Production.AppliedCount > prod0, () => "target " + MachinesLab.Tag(k + ".feed", "FEED_DISPENSER_TARGET_PERCENT_INT") + ", production actions applied " + (Production.AppliedCount - prod0));
		Check("feed-target-not-machines", () => Machines.SentCount == before, () => "machine requests for the feeder slider: " + (Machines.SentCount - before) + "; production last: " + Production.LastDone);
		Do("feed-empty", delegate
		{
			before = Machines.SentCount;
			prod0 = Production.AppliedCount;
			MachineFeedDispenser.EmptyStorage(I("feed"));
			return "EmptyStorage called";
		});
		WaitFor("feed-empty-applied", 20f, () => Production.AppliedCount > prod0 && Production.LastDone.StartsWith("FeedEmpty", StringComparison.Ordinal), () => "production last: " + Production.LastDone);
		Check("feed-empty-not-machines", () => Machines.SentCount == before, () => "machine requests for the empty: " + (Machines.SentCount - before));
		AskServer("ask-widgets");
		Same("same-plate", "mach.plate.{k}");
		Same("same-feed", "mach.feed.{k}");
		Same("same-plate-digest", "mach.dg.{k}.plate");
		Same("same-feed-digest", "mach.dg.{k}.feed");
		Do("hopper-run", () => "RunAndRecord here returned " + HopperHelper.RunAndRecord(I("in")) + " (request sent); items " + MachinesLab.Hop(k));
		WaitFor("hopper-run-applied", 20f, () => MachinesLab.Hop(k).StartsWith("BBB", StringComparison.Ordinal), () => MachinesLab.Hop(k));
		Do("hopper-settings", delegate
		{
			HopperHelper.SetFilterList(I("in"), new List<string> { "scrap_metal" });
			I("in").ModifyTag("HOPPER_FILTER_MODE_INT", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(1);
			}, false);
			I("in").ModifyTag("HOPPER_ON_ADD_BOOL", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetBool(true);
			}, false);
			return "filter scrap_metal (allow), on-add on; written here: " + MachinesLab.Hop(k);
		});
		WaitFor("hopper-settings-applied", 20f, () => Machines.LastOpOn(I("in").uniqueId).StartsWith("tags", StringComparison.Ordinal), () => "last on the intake: " + Machines.LastOpOn(I("in").uniqueId));
		AskServer("ask-hopper");
		Same("same-hopper", "mach.hop.{k}");
		Do("hopper-auto", delegate
		{
			suppressed0 = Machines.SuppressedAuto;
			applied0 = Machines.AppliedCount;
			return "j1 back to bag A (items request " + ItemsMove.QuickMove(I("j1"), (GraphNodeStorage)(object)MachinesLab.Inner(I("bagA")), all: true, -1, "bot") + "); " + MachinesLab.Hop(k);
		});
		WaitFor("hopper-auto-applied", 25f, () => Machines.LastOpOn(I("in").uniqueId).StartsWith("hopper by 0 ", StringComparison.Ordinal) && MachinesLab.Where(k, 1) == "B", () => MachinesLab.Hop(k) + "; last on the intake: " + Machines.LastOpOn(I("in").uniqueId));
		Check("hopper-auto-not-local", () => Machines.SuppressedAuto > suppressed0, () => "hopper passes this copy did not start itself: " + (Machines.SuppressedAuto - suppressed0));
		AskServer("ask-hopper-auto");
		Same("same-hopper-auto", "mach.hop.{k}");
		Same("same-intake-digest", "mach.dg.{k}.in");
		Do("chem-open", delegate
		{
			ChemFinisherUIManager.Instance.OpenUI(I("chem"));
			return "recipe panel open on " + Machines.Describe(I("chem"));
		});
		WaitFor("chem-lock-mine", 15f, () => Locks.HeldBy(Machines.LockName(I("chem").uniqueId)) == Net.LocalPlayer, () => "lock holder " + Locks.HeldBy(Machines.LockName(I("chem").uniqueId)));
		Do("chem-select", delegate
		{
			Vars["recipe"] = ChemFinisherRecipe.chemFinisherRecipes[Net.LocalPlayer % ChemFinisherRecipe.chemFinisherRecipes.Count].identifier;
			ChemFinisherUIManager.Instance.OnChemRecipeSelected(Vars["recipe"]);
			return "picked " + Vars["recipe"];
		});
		WaitFor("chem-select-applied", 20f, () => Machines.LastOpOn(I("chem").uniqueId).StartsWith("tags", StringComparison.Ordinal), () => "selected here " + MachinesLab.TagS(k + ".chem", "SELECTED_CHEM_ID_TAG") + "; last on it: " + Machines.LastOpOn(I("chem").uniqueId));
		Step("chem-hold", 110f, delegate(BotStep st)
		{
			if (k != "0" || Net.Players.Count < 2)
			{
				st.Detail = "no hold (set " + k + ", " + Net.Players.Count + " players)";
				return StepStatus.Pass;
			}
			if (askId >= 0)
			{
				Dictionary<string, string> dictionary = BotProbes.TakeReply(askId);
				if (dictionary != null)
				{
					askId = -1;
					dictionary.TryGetValue("mach.refused", out value);
					st.Detail = "held the panel " + st.Elapsed.ToString("F0", CultureInfo.InvariantCulture) + " s; server refusals " + (value ?? "?");
					if (int.TryParse(value, out var result) && result >= 1)
					{
						return StepStatus.Pass;
					}
				}
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (askId < 0 && realtimeSinceStartup - lastAsk > 1f)
			{
				lastAsk = realtimeSinceStartup;
				askId = BotProbes.AskServer();
			}
			return StepStatus.Running;
		}).Soft();
		Step("other-locked", 100f, delegate(BotStep st)
		{
			if (k != "1" || Net.Players.Count < 2)
			{
				st.Detail = "skipped (set " + k + ")";
				return StepStatus.Pass;
			}
			GameItem val = MachinesLab.Item("0.chem");
			if (val == null)
			{
				st.Detail = "0.chem not here";
				return StepStatus.Fail;
			}
			int num3 = Locks.HeldBy(Machines.LockName(val.uniqueId));
			st.Detail = "0.chem lock holder " + num3;
			if (num3 < 0 || num3 == Net.LocalPlayer)
			{
				return StepStatus.Running;
			}
			before = Machines.RejectCount;
			ChemFinisherUIManager instance = ChemFinisherUIManager.Instance;
			GameItem currentItem = instance.currentItem;
			instance.currentItem = val;
			instance.OnChemRecipeSelected((ChemFinisherRecipe.chemFinisherRecipes[0].identifier == Vars["recipe"]) ? ChemFinisherRecipe.chemFinisherRecipes[1].identifier : ChemFinisherRecipe.chemFinisherRecipes[0].identifier);
			instance.currentItem = currentItem;
			st.Detail = "held by " + Net.PlayerName(num3) + "; wrote a recipe on 0.chem here";
			return StepStatus.Pass;
		}).Soft();
		WaitFor("other-refused", 20f, () => k != "1" || Net.Players.Count < 2 || (Machines.RejectCount > before && Machines.LastReject.Contains("locked")), () => "rejects " + Machines.RejectCount + ", last " + Machines.LastReject).Soft();
		Do("chem-close", delegate
		{
			ChemFinisherUIManager.Instance.HideUI();
			return "panel closed";
		});
		WaitFor("chem-lock-free", 15f, () => Locks.HeldBy(Machines.LockName(I("chem").uniqueId)) == -1, () => "lock holder " + Locks.HeldBy(Machines.LockName(I("chem").uniqueId)));
		Do("turbo", delegate
		{
			Vars["farm0"] = MachinesLab.Farm(k);
			checks0 = Machines.ChecksOk;
			GameItem val = I("turbo");
			GameItem val2 = I("farm");
			if (!val.MayTarget(val2) || !val.CanTarget(val2))
			{
				throw new InvalidOperationException("the game says the turbo booster cannot be used on the farm here (" + MachinesLab.Farm(k) + ")");
			}
			val.Target(val2);
			return "turbo booster on the moisture farm (request sent); farm here " + MachinesLab.Farm(k);
		});
		WaitFor("turbo-applied", 20f, () => Items.IsGone(I("turbo")) && Machines.ChecksOk > checks0, () => "farm " + MachinesLab.Farm(k) + " (was " + Vars["farm0"] + "), checks ok " + (Machines.ChecksOk - checks0) + ", mismatches " + Machines.MismatchCount);
		Do("feed-autoblend-on", delegate
		{
			I("feed").ToggleSlotItem();
			return "toggle sent; auto-blend here " + I("feed").GetTagReadonly("FEED_DISPENSER_AUTO_BLEND_BOOL").GetBool();
		});
		WaitFor("feed-autoblend-applied", 20f, () => I("feed").GetTagReadonly("FEED_DISPENSER_AUTO_BLEND_BOOL").GetBool(), () => "auto-blend " + I("feed").GetTagReadonly("FEED_DISPENSER_AUTO_BLEND_BOOL").GetBool());
		Do("feed-pellet", delegate
		{
			suppressed0 = Production.BlockedAuto;
			return "pellet into the dispenser (items request " + ItemsMove.QuickMove(I("pellet"), (GraphNodeStorage)(object)MachineFeedDispenser.GetStorageGrid(I("feed")), all: true, -1, "bot") + "); " + BotProbes.Read("mach.feedkcal." + k);
		});
		WaitFor("feed-pellet-blended", 20f, () => Items.IsGone(I("pellet")) && MachinesLab.Tag(k + ".feed", "STORED_KCAL_TAG") > 0, () => BotProbes.Read("mach.feedkcal." + k));
		WaitFor("feed-pass-not-local", 10f, () => Production.BlockedAuto > suppressed0, () => "auto-blend passes this copy skipped (production): " + (Production.BlockedAuto - suppressed0)).Soft();
		AskServer("ask-feed");
		Same("same-feed-kcal", "mach.feedkcal.{k}");
		Same("same-feed-digest-2", "mach.dg.{k}.feed");
		Check("butcher-local-buttons", () => Machines.Widgets(I("butch1")).OfType<MachineryButton>().Count((MachineryButton b) => Machines.Classify(Machines.CallbackOf(b)) == 1) == 2, () => "local buttons " + string.Join(",", from b in Machines.Widgets(I("butch1")).OfType<MachineryButton>()
			select b.text + "=" + Machines.Classify(Machines.CallbackOf(b))));
		Do("butcher-range", delegate
		{
			object obj = MachinesLab.Widget<MachineryRangeSlider>(I("butch1"), 0);
			MachineryRangeSlider val = (MachineryRangeSlider)((obj is MachineryRangeSlider) ? obj : null);
			if (val == null)
			{
				throw new InvalidOperationException("no range slider on the auto butcher");
			}
			Vars["b1"] = MachinesLab.Dg(k + ".butch1");
			before = Machines.SentCount;
			float num3 = val.min + (val.max - val.min) * 0.25f;
			float num4 = val.min + (val.max - val.min) * 0.75f;
			val.SetValues(num3, num4, true);
			return "range " + val.min + ".." + val.max + " -> " + val.low + ".." + val.high + " (the closure ran here: production carries it)";
		});
		WaitFor("butcher-range-applied", 20f, () => MachinesLab.Dg(k + ".butch1") != Vars["b1"], () => "digest " + MachinesLab.Dg(k + ".butch1"));
		Check("butcher-range-not-machines", () => Machines.SentCount == before, () => "machine requests for the butcher range: " + (Machines.SentCount - before));
		Do("butcher-paste", delegate
		{
			before = Machines.SentCount;
			prod0 = Production.AppliedCount;
			MachineAutoButcher.CopySettings(I("butch1"));
			MachineAutoButcher.PasteSettings(I("butch2"));
			return "copied butcher 1, pasted on butcher 2 (written here, production sends the settings)";
		});
		WaitFor("butcher-paste-applied", 20f, () => Production.AppliedCount > prod0 && Production.LastDone.StartsWith("Set auto_butcher#" + I("butch2").uniqueId, StringComparison.Ordinal), () => "production last: " + Production.LastDone);
		Check("butcher-paste-not-machines", () => Machines.SentCount == before, () => "machine requests for the paste: " + (Machines.SentCount - before));
		Check("butcher-paste-equal", () => I("butch2").GetTagReadonly("AUTO_BUTCHER_CAGE_WEIGHT_MIN_INT").GetInt() == I("butch1").GetTagReadonly("AUTO_BUTCHER_CAGE_WEIGHT_MIN_INT").GetInt() && I("butch2").GetTagReadonly("AUTO_BUTCHER_CAGE_WEIGHT_MAX_INT").GetInt() == I("butch1").GetTagReadonly("AUTO_BUTCHER_CAGE_WEIGHT_MAX_INT").GetInt(), () => "weight " + I("butch1").GetTagReadonly("AUTO_BUTCHER_CAGE_WEIGHT_MIN_INT").GetInt() + ".." + I("butch1").GetTagReadonly("AUTO_BUTCHER_CAGE_WEIGHT_MAX_INT").GetInt() + " / " + I("butch2").GetTagReadonly("AUTO_BUTCHER_CAGE_WEIGHT_MIN_INT").GetInt() + ".." + I("butch2").GetTagReadonly("AUTO_BUTCHER_CAGE_WEIGHT_MAX_INT").GetInt());
		AskServer("ask-butcher");
		Same("same-butcher1", "mach.dg.{k}.butch1");
		Same("same-butcher2", "mach.dg.{k}.butch2");
		Do("vending-cashout", delegate
		{
			if (k != "0")
			{
				return "skipped (set " + k + ")";
			}
			GameItem m = MachinesLab.Item("v.vend") ?? throw new InvalidOperationException("no vending machine fixture");
			Vars["cash"] = PlayerStore.Instance.playerCash.ToString(CultureInfo.InvariantCulture);
			MachinesLab.Press((MachineryButton)MachinesLab.Widget<MachineryButton>(m, 0));
			return "transfer pressed; cash here " + Vars["cash"] + ", machine holds " + MachinesLab.Tag("v.vend", "VENDING_CASH");
		});
		WaitFor("vending-applied", 60f, () => (!(k != "0")) ? (MachinesLab.Tag("v.vend", "VENDING_CASH") == 0 && PlayerStore.Instance.playerCash.ToString(CultureInfo.InvariantCulture) != Vars["cash"]) : (MachinesLab.Tag("v.vend", "VENDING_CASH") == 0), () => "cash " + PlayerStore.Instance.playerCash + " (was " + (Vars.TryGetValue("cash", out value) ? value : "?") + "), machine " + MachinesLab.Tag("v.vend", "VENDING_CASH"));
		Do("dealmaker-no-customer", delegate
		{
			if (k != "0")
			{
				return "skipped (set " + k + ")";
			}
			before = Machines.RejectCount;
			MachinesLab.Press((MachineryButton)MachinesLab.Widget<MachineryButton>(I("deal"), 0));
			return "deal maker pressed with nobody at the counter";
		});
		WaitFor("dealmaker-nothing", 20f, () => k != "0" || (Machines.LastOpOn(I("deal").uniqueId).StartsWith("button", StringComparison.Ordinal) && Machines.RejectCount == before && !PlayerStore.Instance.dealMakerActivatedThisCustomer), () => (!(k != "0")) ? ("last on the deal maker: " + Machines.LastOpOn(I("deal").uniqueId) + ", rejects " + (Machines.RejectCount - before) + " (" + Machines.LastReject + "), activated " + PlayerStore.Instance.dealMakerActivatedThisCustomer) : ("skipped (set " + k + ")"));
		AskServer("ask-cash");
		Same("same-cash", "cash");
		Same("same-vending", "mach.vend");
		WaitFor("water-heated", 40f, () => MachinesLab.Temp(k) >= 40, () => "water " + MachinesLab.Temp(k) + " C here, 1 s functions skipped here " + Machines.SuppressedTicks + ", ticks applied " + Machines.TicksApplied);
		AskServer("ask-temp");
		Step("temp-close", 0f, delegate(BotStep st)
		{
			int num3 = MachinesLab.Temp(k);
			int result;
			int num4 = ((base.Server.TryGetValue("mach.temp." + k, out value) && int.TryParse(value, out result)) ? result : (-999));
			st.Detail = "water here " + num3 + " C, server " + num4 + " C (pushed every 3 s, the server keeps heating in between)";
			return (Math.Abs(num3 - num4) <= 25 && num3 > 26) ? StepStatus.Pass : StepStatus.Fail;
		});
		Delay("settle", 6f);
		AskServer("ask-final");
		Same("same-chem", "mach.chem.{k}");
		Same("same-chem-0", "mach.chem.0");
		Same("same-chem-digest", "mach.dg.{k}.chem");
		Same("final-box", "mach.dg.{k}.box");
		Same("final-plate", "mach.dg.{k}.plate");
		Same("final-feed", "mach.dg.{k}.feed");
		Same("final-intake", "mach.dg.{k}.in");
		Same("same-farm", "mach.farm.{k}");
		Same("same-farm-digest", "mach.dg.{k}.farm");
		Do("mark-done", delegate
		{
			MachinesX.Mark("mach-done-" + k);
			return "set " + k + " finished with its machines; told the server";
		});
		WaitMarks("both-done", 150f, "mach-done-0", "mach-done-1");
		Do("resync", delegate
		{
			if (k != "1" && Net.Players.Count >= 2)
			{
				return "skipped (set " + k + ")";
			}
			loaded0 = Machines.LoadedCount;
			mismatch0 = Machines.MismatchCount;
			Snapshot.RequestResync();
			return "resync asked (snapshots loaded so far " + loaded0 + ", machine actions applied " + Machines.AppliedCount + ")";
		});
		WaitFor("resync-loaded", 90f, () => (k != "1" && Net.Players.Count >= 2) || (Machines.LoadedCount > loaded0 && Net.IsLive && !DayLog.Replaying), () => "loaded " + Machines.LoadedCount + ", live " + Net.IsLive + ", state " + Net.ClientState);
		Delay("resync-settle", 5f);
		AskServer("ask-after-resync");
		string[] array = new string[2] { "0", "1" };
		foreach (string text in array)
		{
			string[] array2 = new string[8] { "box", "plate", "feed", "chem", "in", "farm", "butch1", "butch2" };
			foreach (string text2 in array2)
			{
				Same("replayed-dg-" + text + "-" + text2, "mach.dg." + text + "." + text2);
			}
			array2 = new string[3] { "hop", "chem", "farm" };
			foreach (string text3 in array2)
			{
				Same("replayed-" + text3 + "-" + text, "mach." + text3 + "." + text);
			}
		}
		Same("replayed-server-box", "mach.box.s");
		Check("resync-no-mismatch", () => Machines.MismatchCount == mismatch0, () => "machine mismatches during the replay " + (Machines.MismatchCount - mismatch0) + ", repairs asked " + Machines.RepairsAsked);
		Check("ticks-suppressed", () => true, () => "1 s functions not run here " + Machines.SuppressedTicks + ", ticks applied " + Machines.TicksApplied + ", mismatches " + Machines.MismatchCount + ", repairs asked " + Machines.RepairsAsked);
	}
}
