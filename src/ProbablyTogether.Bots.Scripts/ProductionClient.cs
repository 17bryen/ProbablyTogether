using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Bots.Scripts;

public sealed class ProductionClient : ProductionScript
{
	private string set = "1";

	private int day0;

	private int kcal0;

	private int refused0;

	private int applied0;

	private int blocked0;

	private int loads0;

	private int itemsSent0;

	private int orig0;

	private int suppressed0;

	private long foodId;

	private static readonly FieldInfo fCur = AccessTools.Field(typeof(ItemMouseDragHandler), "currentItem");

	private static readonly FieldInfo fLastItem = AccessTools.Field(typeof(ItemMouseDragHandler), "lastItem");

	private static readonly FieldInfo fLastInv = AccessTools.Field(typeof(ItemMouseDragHandler), "lastInventory");

	private static readonly FieldInfo fRemove = AccessTools.Field(typeof(ItemMouseDragHandler), "currentRemove");

	private static readonly FieldInfo fDragging = AccessTools.Field(typeof(ItemMouseDragHandler), "isDragging");

	private static readonly MethodInfo mEndDrag = AccessTools.Method(typeof(ItemMouseDragHandler), "EndDrag", (Type[])null, (Type[])null);

	public override string Name => "production";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	private string Vend
	{
		get
		{
			if (!(set == "1"))
			{
				return "fountain";
			}
			return "vend";
		}
	}

	private GameItem F(string role)
	{
		return ProductionLab.Item(set + "." + role);
	}

	private string K(string role)
	{
		return set + "." + role;
	}

	private IEnumerable<string> MyHashes()
	{
		return ProductionLab.Roles.Select((string r) => "prod.h." + set + "." + r);
	}

	private IEnumerable<string> MyLabels()
	{
		return new string[3]
		{
			"prod.wl." + set,
			"prod.wl2." + set,
			"prod.c." + set
		};
	}

	private static IEnumerable<string> AllHashes()
	{
		return from k in ProductionLab.HashKeys()
			select "prod.h." + k;
	}

	private IEnumerable<string> NeededKeys()
	{
		return from k in ProductionLab.Keys()
			where !ProductionLab.Extra.Any((string x) => k.EndsWith("." + x, StringComparison.Ordinal)) || k.StartsWith(set + ".", StringComparison.Ordinal)
			select k;
	}

	private void Mark(string name)
	{
		Net.ToServer("production.bot.mark", new ProductionLab.Mark
		{
			name = name
		});
	}

	private static string DropOnto(GameItem item, GameItem target, GameGridInventory grid)
	{
		ItemMouseDragHandler current = ItemMouseDragHandler.current;
		if ((Object)(object)current != (Object)null)
		{
			GameItemElement val = (GameItemElement)(object)((item is GameItemElement) ? item : null);
			if (val != null && GeneralHelper.MayPlayerInsertInto(target) && target.MayHaveValidInventorySlot(item))
			{
				string text = "";
				fCur.SetValue(current, val);
				fLastItem.SetValue(current, target);
				fLastInv.SetValue(current, null);
				fRemove.SetValue(current, item.unitCount);
				fDragging.SetValue(current, true);
				try
				{
					BotDrag.End(current);
				}
				catch (Exception ex)
				{
					text = " (EndDrag's clean-up threw " + (ex.InnerException ?? ex).GetType().Name + ")";
				}
				finally
				{
					fDragging.SetValue(current, false);
					fCur.SetValue(current, null);
					fLastItem.SetValue(current, null);
				}
				return "dropped on " + Production.Describe(target) + " by the drag handler's EndDrag" + text;
			}
		}
		ItemsCapture.Ctx c;
		bool entered = ItemsCapture.Enter("quick", out c);
		int num;
		try
		{
			num = GraphUtils.TryAcceptOnce((GraphNodeStorage)(object)grid, item, -1);
		}
		finally
		{
			ItemsCapture.Exit(entered);
		}
		return "the drop was not offered (handler " + ((Object)(object)current != (Object)null) + "); quick transfer into the storage grid (left " + num + ")";
	}

	protected override void Build()
	{
		WaitFor("joined", 180f, () => Net.IsClient && Net.LocalPlayer > 0, () => "role " + Net.Role.ToString() + ", player " + Net.LocalPlayer);
		WaitFor("shop-loaded", 180f, () => BotProbes.InShop && Net.IsLive, () => "scene " + BotProbes.Scene + ", live " + Net.IsLive);
		Do("watch-loads", delegate
		{
			Snapshot.Loaded += delegate
			{
				ProductionLab.Loads++;
			};
			set = ProductionLab.SetFor(Net.LocalPlayer);
			return "player " + Net.LocalPlayer + " works set " + set;
		});
		UntilServer("fixtures", 200f, (IReadOnlyDictionary<string, string> r) => r.TryGetValue("prod.ready", out var value) && value == "1", (IReadOnlyDictionary<string, string> r) => "server fixtures " + (r.TryGetValue("prod.fx", out var value) ? value : "?"));
		Do("parse-fixtures", () => ProductionLab.ParseFx(Srv["prod.fx"]) + " fixture ids");
		WaitFor("fixtures-here", 120f, () => NeededKeys().All((string k) => ProductionLab.Item(k) != null), () => "found " + NeededKeys().Count((string k) => ProductionLab.Item(k) != null) + " of " + NeededKeys().Count() + " (a fresh snapshot follows the server's spawn); missing " + string.Join(",", from k in NeededKeys()
			where ProductionLab.Item(k) == null
			select k));
		Delay("settle", 3f);
		AllSame("same-start", 30f, () => MyHashes().Concat(MyLabels()));
		SameSoon("hireling-features", () => "prod.w.hw");
		Do("baseline", delegate
		{
			day0 = Game.Day;
			kcal0 = ProductionLab.Tag(K("feeder"), "STORED_KCAL_TAG");
			applied0 = Production.AppliedCount;
			return "day " + day0 + ", kcal " + kcal0 + ", feeder food " + ProductionLab.Food(set) + ", feeder animals " + ProductionLab.FeederAnimals(set) + ", cage rats " + ProductionLab.LiveRats(set) + ", dead in tray " + ProductionLab.DeadInTray(set) + ", live fish " + ProductionLab.LiveFish(set) + ", dead fish in tank " + ProductionLab.DeadFishInTank(set) + ", fish carcass in tray " + ProductionLab.CarcassInTray(set);
		});
		Do("feed-target", delegate
		{
			F("feeder").ModifyTag("FEED_DISPENSER_TARGET_PERCENT_INT", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(50);
			}, false);
			return "slider closure wrote 50 here; sent " + Production.SentCount;
		});
		UntilServer("feed-target-server", 30f, (IReadOnlyDictionary<string, string> r) => r.TryGetValue("prod." + set + ".target", out var value) && value == "50", (IReadOnlyDictionary<string, string> r) => "server target " + r["prod." + set + ".target"]);
		Expect("feed-target-kept", () => 50, () => ProductionLab.Tag(K("feeder"), "FEED_DISPENSER_TARGET_PERCENT_INT"));
		Do("feed-blend", delegate
		{
			kcal0 = ProductionLab.Tag(K("feeder"), "STORED_KCAL_TAG");
			GameItem obj = F("feeder");
			MachineFeedDispenser.Blend(obj, MachineFeedDispenser.GetStorageGrid(obj));
			return "Start pressed (Blend); kcal here still " + ProductionLab.Tag(K("feeder"), "STORED_KCAL_TAG") + " (the server decides)";
		});
		WaitFor("feed-blended", 30f, () => ProductionLab.Tag(K("feeder"), "STORED_KCAL_TAG") > kcal0 && ProductionLab.Food(set) == 0, () => "kcal " + kcal0 + " -> " + ProductionLab.Tag(K("feeder"), "STORED_KCAL_TAG") + ", food left " + ProductionLab.Food(set) + ", animals left " + ProductionLab.FeederAnimals(set));
		SameSoon("feed-blend-same", () => "prod.h." + K("feeder"));
		Do("auto-on", delegate
		{
			blocked0 = Production.BlockedAuto;
			GameItem val = F("feeder");
			if (!val.MayToggleSlotItem() || !val.CanToggleSlotItem())
			{
				throw new InvalidOperationException("the game says the switch cannot be used");
			}
			val.ToggleSlotItem();
			return "auto-blend switched (ToggleSlotItem); sent " + Production.SentCount;
		});
		UntilServer("auto-on-server", 30f, (IReadOnlyDictionary<string, string> r) => r.TryGetValue("prod." + set + ".auto", out var value) && value == "1", (IReadOnlyDictionary<string, string> r) => "server auto-blend " + r["prod." + set + ".auto"]);
		Do("drag-food", delegate
		{
			kcal0 = ProductionLab.Tag(K("feeder"), "STORED_KCAL_TAG");
			suppressed0 = Machines.SuppressedAuto;
			itemsSent0 = ItemsMove.SentCount;
			GameItem val = F("food") ?? throw new InvalidOperationException("no food to drag");
			foodId = Ids.Of(val);
			GameInventory parentInventory = val.parentInventory;
			string text = DropOnto(val, F("feeder"), ProductionLab.FeedGrid(set));
			return "food#" + foodId + " " + text + "; items requests sent " + (ItemsMove.SentCount - itemsSent0) + " (last " + ItemsMove.LastSentKind + "); " + ((val.parentInventory == parentInventory) ? "unchanged here before the server answered" : "FAIL moved locally before the server answered");
		});
		WaitFor("auto-blended", 30f, () => ItemsMove.SentCount > itemsSent0 && ProductionLab.Item(K("food")) == null && ProductionLab.Food(set) == 0 && ProductionLab.Tag(K("feeder"), "STORED_KCAL_TAG") > kcal0, () => "kcal " + kcal0 + " -> " + ProductionLab.Tag(K("feeder"), "STORED_KCAL_TAG") + ", food in the dispenser " + ProductionLab.Food(set) + ", dragged food here " + ((ProductionLab.Item(K("food")) != null) ? "still exists" : "used up") + ", items outcome " + ItemsMove.Outcome(ItemsMove.LastSentQ));
		Expect("auto-blended-once", () => kcal0 + 800, () => ProductionLab.Tag(K("feeder"), "STORED_KCAL_TAG"));
		Check("own-pass-stopped", () => Production.BlockedAuto > blocked0 || Machines.SuppressedAuto > suppressed0, () => "this copy's next-frame pass stopped by production " + (Production.BlockedAuto - blocked0) + " time(s), by machines " + (Machines.SuppressedAuto - suppressed0) + "; last production action here " + Production.LastDone);
		SameSoon("auto-blend-same", () => "prod.h." + K("feeder"));
		Do("feed-empty", delegate
		{
			MachineFeedDispenser.EmptyStorage(F("feeder"));
			return "Empty confirmed (EmptyStorage)";
		});
		WaitFor("feed-emptied", 30f, () => ProductionLab.Tag(K("feeder"), "STORED_KCAL_TAG") == 0, () => "kcal " + ProductionLab.Tag(K("feeder"), "STORED_KCAL_TAG"));
		SameSoon("feed-empty-same", () => "prod.h." + K("feeder"));
		Do("butcher-content", delegate
		{
			MachineAutoButcher.ButcherContent(F("butcher"));
			return "Butcher content pressed; dead in tray " + ProductionLab.DeadInTray(set);
		});
		WaitFor("butchered-content", 30f, () => ProductionLab.DeadInTray(set) == 0, () => "dead rats in the tray " + ProductionLab.DeadInTray(set) + ", tray items " + ProductionLab.TrayItems(set));
		SameSoon("butcher-content-same", () => "prod.h." + K("cage"));
		Do("butcher-settings", delegate
		{
			GameItem obj = F("butcher");
			obj.ModifyTag("AUTO_BUTCHER_CAGE_HARVEST_INT", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(3);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_CAGE_AGE_MIN_INT", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(1);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_CAGE_AGE_MAX_INT", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(30);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_CAGE_KEEP_FEMALES_INT", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(0);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_CAGE_KEEP_MALES_INT", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(0);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_CAGE_COUNT_PROTECTED_BOOL", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetBool(false);
			}, false);
			return "harvest by age 1-30, keep 0/0 written here; sent " + Production.SentCount;
		});
		UntilServer("butcher-settings-server", 30f, (IReadOnlyDictionary<string, string> r) => r.TryGetValue("prod." + set + ".harvest", out var value) && value == "3", (IReadOnlyDictionary<string, string> r) => "server harvest mode " + r["prod." + set + ".harvest"]);
		SameSoon("butcher-settings-same", () => "prod.h." + K("cage"));
		Do("butcher-run", delegate
		{
			MachineAutoButcher.Run(F("butcher"), true);
			return "Butcher now pressed (Run); live rats here still " + ProductionLab.LiveRats(set);
		});
		WaitFor("butcher-ran", 30f, () => ProductionLab.Tag(K("butcher"), "AUTO_BUTCHER_LAST_COUNT_INT") > 0, () => "live rats " + ProductionLab.LiveRats(set) + ", last count " + ProductionLab.Tag(K("butcher"), "AUTO_BUTCHER_LAST_COUNT_INT") + ", tray items " + ProductionLab.TrayItems(set));
		SameSoon("butcher-run-same", () => "prod.h." + K("cage"));
		UntilServer("butcher-count", 20f, (IReadOnlyDictionary<string, string> r) => r.TryGetValue("prod." + set + ".count", out var value) && value == ProductionScript.Probe("prod." + set + ".count") && value != "0", (IReadOnlyDictionary<string, string> r) => "last count here " + ProductionScript.Probe("prod." + set + ".count") + ", server " + r["prod." + set + ".count"]);
		Do("ras-settings", delegate
		{
			GameItem obj = F("rasbutcher");
			obj.ModifyTag("AUTO_BUTCHER_TANK_HARVEST_INT", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(3);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_TANK_AGE_MIN_INT", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(0);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_TANK_AGE_MAX_INT", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(100000);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_TANK_KEEP_FEMALES_INT", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(0);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_TANK_KEEP_MALES_INT", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetInt(0);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_TANK_SPARE_EGG_LAYERS_BOOL", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetBool(false);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_TANK_COUNT_PROTECTED_BOOL", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetBool(false);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_TANK_FILLET_CARCASSES_BOOL", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetBool(true);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_TANK_CUTS_TO_DISPENSER_BOOL", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetBool(true);
			}, false);
			obj.ModifyTag("AUTO_BUTCHER_TANK_COLLECT_ROE_BOOL", (Action<TagState>)delegate(TagState s)
			{
				s.Enable().SetBool(true);
			}, false);
			return "tank page: harvest by age 0-100000, keep 0/0, fillet carcasses, cuts to dispenser, collect roe; sent " + Production.SentCount;
		});
		UntilServer("ras-settings-server", 30f, (IReadOnlyDictionary<string, string> r) => r.TryGetValue("prod." + set + ".tankharvest", out var value) && value == "3", (IReadOnlyDictionary<string, string> r) => "server tank harvest mode " + r["prod." + set + ".tankharvest"]);
		SameSoon("ras-settings-same", () => "prod.h." + K("ras"));
		Do("fish-fillet", delegate
		{
			MachineAutoButcher.ButcherContent(F("rasbutcher"));
			return "Butcher content pressed on the RAS butcher; fish carcasses in its tray " + ProductionLab.CarcassInTray(set) + ", tray items " + ProductionLab.RasTrayItems(set);
		});
		WaitFor("fish-filleted", 30f, () => ProductionLab.CarcassInTray(set) == 0, () => "fish carcasses in the tray " + ProductionLab.CarcassInTray(set) + ", tray items " + ProductionLab.RasTrayItems(set));
		SameSoon("fish-fillet-same", () => "prod.h." + K("ras"));
		Do("ras-run", delegate
		{
			kcal0 = ProductionLab.RasFeederKcal(set);
			MachineAutoButcher.Run(F("rasbutcher"), true);
			return "Butcher now pressed on the RAS butcher (Run); live fish here still " + ProductionLab.LiveFish(set) + ", dead in the tank " + ProductionLab.DeadFishInTank(set);
		});
		WaitFor("ras-ran", 30f, () => ProductionLab.Tag(K("rasbutcher"), "AUTO_BUTCHER_LAST_COUNT_INT") > 0 && ProductionLab.LiveFish(set) == 0, () => "live fish " + ProductionLab.LiveFish(set) + ", dead in the tank " + ProductionLab.DeadFishInTank(set) + ", last count " + ProductionLab.Tag(K("rasbutcher"), "AUTO_BUTCHER_LAST_COUNT_INT") + ", tray items " + ProductionLab.RasTrayItems(set) + ", RAS dispenser kcal " + kcal0 + " -> " + ProductionLab.RasFeederKcal(set));
		SameSoon("ras-run-same", () => "prod.h." + K("ras"));
		UntilServer("ras-count", 20f, (IReadOnlyDictionary<string, string> r) => r.TryGetValue("prod." + set + ".rascount", out var value) && value == ProductionScript.Probe("prod." + set + ".rascount"), (IReadOnlyDictionary<string, string> r) => "last count here " + ProductionScript.Probe("prod." + set + ".rascount") + ", server " + r["prod." + set + ".rascount"] + "; fish here " + ProductionScript.Probe("prod." + set + ".fish") + ", server " + r["prod." + set + ".fish"]);
		Do("paste", delegate
		{
			MachineAutoButcher.CopySettings(F("butcher"));
			return "copied butcher 1 (own clipboard) and pasted on butcher 2: " + MachineAutoButcher.PasteSettings(F("butcher2")) + "; sent " + Production.SentCount;
		});
		UntilServer("paste-server", 30f, (IReadOnlyDictionary<string, string> r) => r.TryGetValue("prod." + set + ".harvest2", out var value) && value == "3", (IReadOnlyDictionary<string, string> r) => "server butcher 2 harvest mode " + r["prod." + set + ".harvest2"]);
		SameSoon("paste-same", () => "prod.h." + K("butcher2"));
		Do("forged", delegate
		{
			refused0 = Production.RefusedCount;
			Net.ToServer("production.req", new Production.MsgReq
			{
				n = 900001,
				op = 1,
				m = ProductionLab.Fx[K("cage")]
			});
			Net.ToServer("production.req", new Production.MsgReq
			{
				n = 900002,
				op = 6,
				m = ProductionLab.Fx[K("feeder")],
				tags = new ProdTag[1]
				{
					new ProdTag
					{
						t = "VENDING_CASH",
						e = true,
						i = 999999
					}
				}
			});
			Net.ToServer("production.req", new Production.MsgReq
			{
				n = 900003,
				op = 6,
				m = ProductionLab.Fx[K("feeder")],
				tags = new ProdTag[1]
				{
					new ProdTag
					{
						t = "FEED_DISPENSER_TARGET_PERCENT_INT",
						e = true,
						i = 5000
					}
				}
			});
			Net.ToServer("production.req", new Production.MsgReq
			{
				n = 900004,
				op = 11,
				m = ProductionLab.Fx[K("feeder")]
			});
			return "blend on a cage, a cash tag on the dispenser, a target of 5000%, a chem label match on the dispenser";
		});
		WaitFor("forged-refused", 20f, () => Production.RefusedCount >= refused0 + 4, () => Production.RefusedCount - refused0 + " refused; last: " + Production.LastRefusal);
		SameSoon("forged-harmless", () => "prod.h." + K("feeder"));
		Do("water-match", delegate
		{
			WaterStripUIManager obj = WaterStripUIManager.Instance ?? throw new InvalidOperationException("no test strip panel in this scene");
			GameItem val = F("water");
			Vars["water0"] = ProductionLab.Water(K("water"));
			if (!obj.CanMatch(val))
			{
				throw new InvalidOperationException("the test strip panel would not offer Match (" + Vars["water0"] + ")");
			}
			obj.currentWaterContainer = val;
			obj.OnMatch();
			return "Match pressed on the test strip panel (" + Vars["water0"] + "); here still " + ProductionLab.Water(K("water"));
		});
		WaitFor("water-matched", 30f, () => ProductionLab.Water(K("water")).StartsWith("exposed", StringComparison.Ordinal), () => "label " + Vars["water0"] + " -> " + ProductionLab.Water(K("water")));
		SameSoon("water-same", () => "prod.wl." + set);
		Do("chem-scan-water", delegate
		{
			ChemScannerUIManager val = ChemScannerUIManager.Instance ?? throw new InvalidOperationException("no chem scanner panel in this scene");
			GameItem val2 = F("water2");
			Vars["water20"] = ProductionLab.Water(K("water2"));
			if (!val2.IsTag("LIQUID_CONTAINER_TAG") || !WaterScannerUIManager.CanMatch(val2))
			{
				throw new InvalidOperationException("the chem scanner would not offer Match on the bottle (" + Vars["water20"] + ")");
			}
			val.currentScannedItem = val2;
			try
			{
				val.OnMatch();
			}
			finally
			{
				val.currentScannedItem = null;
			}
			return "Match pressed on the chem scanner panel with a water bottle (" + Vars["water20"] + "); here still " + ProductionLab.Water(K("water2"));
		});
		WaitFor("chem-scan-water-matched", 30f, () => ProductionLab.Water(K("water2")).StartsWith("exposed", StringComparison.Ordinal), () => "label " + Vars["water20"] + " -> " + ProductionLab.Water(K("water2")));
		SameSoon("chem-scan-water-same", () => "prod.wl2." + set);
		SameSoon("water-match-flag", () => "prod.w2." + set);
		Check("water-labels-ticked", () => Machines.WaterLabelsApplied > 0, () => "water labels set from machine.tick here " + Machines.WaterLabelsApplied + ", ticks applied " + Machines.TicksApplied);
		Do("chem-match", delegate
		{
			ChemScannerUIManager val = ChemScannerUIManager.Instance ?? throw new InvalidOperationException("no chem scanner panel in this scene");
			GameItem val2 = F("chem");
			Vars["chem0"] = ProductionLab.Chem(K("chem"));
			if (!val.CanMatchChem(val2))
			{
				throw new InvalidOperationException("the chem scanner would not offer Match on the product (" + Vars["chem0"] + ")");
			}
			val.currentScannedItem = val2;
			try
			{
				val.OnMatch();
			}
			finally
			{
				val.currentScannedItem = null;
			}
			return "Match pressed on the chem scanner panel with " + val2.identifier + " (" + Vars["chem0"] + "); here still " + ProductionLab.Chem(K("chem"));
		});
		WaitFor("chem-matched", 30f, () => ProductionLab.Chem(K("chem")) != Vars["chem0"] && ProductionLab.Chem(K("chem")).StartsWith("exposed", StringComparison.Ordinal), () => "label " + Vars["chem0"] + " -> " + ProductionLab.Chem(K("chem")));
		SameSoon("chem-same", () => "prod.c." + set);
		Func<BotStep, StepStatus> browse = EconomyLab.PhoneBrowsePoll();
		bool phoneAsked = false;
		Step("vend-network", 90f, delegate(BotStep st)
		{
			if (set != "1")
			{
				st.Detail = "set 2 does not buy";
				return StepStatus.Pass;
			}
			if (!phoneAsked)
			{
				phoneAsked = true;
				EconomyLab.Op("phone", "8376", "Regular");
				st.Detail = "Wild's phone -> Regular (economy lab op)";
				return StepStatus.Running;
			}
			return ((int)EconomyLab.WildPhone() == 4) ? browse(st) : StepStatus.Running;
		});
		Step("vend-unlock", 40f, delegate(BotStep st)
		{
			if (st.Detail == null)
			{
				if (ProductionLab.Item(Vend) != null)
				{
					st.Detail = "the " + Vend + " was already unlocked here before the test";
					return StepStatus.Fail;
				}
				orig0 = 0;
				blocked0 = Production.BlockedNight;
				if (set == "1")
				{
					Economy.RequestBuy("VENDING_MACHINE");
					st.Detail = "asked Wild's Network for VENDING_MACHINE (economy)";
				}
				else
				{
					Net.ToServer("production.bot.unlock", new ProductionLab.Unlock
					{
						what = "fountain"
					});
					st.Detail = "asked the server to unlock the fountain from game code outside any action";
				}
				return StepStatus.Running;
			}
			GameItem val = ProductionLab.Item(Vend);
			if (val == null)
			{
				return StepStatus.Running;
			}
			st.Detail = st.Detail + "; the " + val.identifier + "#" + val.uniqueId + " is here after " + st.Elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s; last production action " + Production.LastDone + "; client unlocks stopped " + (Production.BlockedNight - blocked0);
			return (Production.BlockedNight == blocked0) ? StepStatus.Pass : StepStatus.Fail;
		});
		Do("vend-network-close", delegate
		{
			if (set != "1")
			{
				return "set 2 did not open it";
			}
			WildNetworkUI.Close();
			return "closed the network window (open " + WildNetworkUI.IsOpen + ")";
		});
		SameSoon("vend-unlock-same", () => "prod.h." + Vend);
		UntilServer("vend-unlock-route", 20f, (IReadOnlyDictionary<string, string> r) => (!(set == "1")) ? (r["prod.unlock.f"] == "1") : (r["prod.unlock.m"] == "0"), (IReadOnlyDictionary<string, string> r) => (!(set == "1")) ? ("the fountain came through production op 9: production's own fountain unlocks on the server " + r["prod.unlock.f"]) : ("the vending machine came through economy's replicated buy: production's own unlocks on the server " + r["prod.unlock.m"] + ", server cash " + r["cash"]));
		Do("vend-cash", delegate
		{
			Net.ToServer("production.bot.cash", new ProductionLab.Cash
			{
				what = Vend,
				cash = ((set == "1") ? 100 : 50)
			});
			return "asked the server to put " + ((set == "1") ? 100 : 50) + " in the " + Vend;
		});
		WaitFor("vend-cashed", 30f, () => ProductionLab.Tag(Vend, "VENDING_CASH") == ((set == "1") ? 100 : 50), () => "machine cash " + ProductionLab.Tag(Vend, "VENDING_CASH"));
		Do("cashout", delegate
		{
			VendingMachineHelper.TransferCashToPlayer(ProductionLab.Item(Vend));
			return "Transfer pressed on the " + ((set == "1") ? "vending machine" : "fountain");
		});
		WaitFor("cashed-out", 30f, () => ProductionLab.Tag(Vend, "VENDING_CASH") == 0, () => "machine cash " + ProductionLab.Tag(Vend, "VENDING_CASH"));
		SameSoon("cashout-same", () => "prod.h." + Vend);
		Check("no-unseeded-rolls", () => Production.UnseededClient == 0, () => "client rolls outside a seeded replay: " + Production.UnseededClient);
		Check("no-mismatch", () => Production.MismatchCount == 0, () => Production.MismatchCount + " mismatch(es)" + ((Production.MismatchCount > 0) ? (": " + Production.LastMismatch) : "") + "; applied " + (Production.AppliedCount - applied0) + " production action(s) here, digest differed after " + Production.DigestDiffers);
		SameSoon("digest-part-same", "prod.digest");
		Step("resync", 120f, delegate(BotStep st)
		{
			if (set != "2")
			{
				st.Detail = "set 1 skips (set 2 rebuilds)";
				return StepStatus.Pass;
			}
			if (st.Detail == null)
			{
				loads0 = ProductionLab.Loads;
				applied0 = Production.AppliedCount;
				Snapshot.RequestResync();
				st.Detail = "asked";
				return StepStatus.Running;
			}
			if (ProductionLab.Loads <= loads0 || !Net.IsLive)
			{
				return StepStatus.Running;
			}
			st.Detail = "morning snapshot + today's log loaded; " + (Production.AppliedCount - applied0) + " production action(s) replayed from the log, " + ProductionLab.Cashed + " cash fixture(s) seen";
			return StepStatus.Pass;
		});
		AllSame("same-after-resync", 150f, () => MyHashes().Concat(MyLabels()).Concat(new string[3] { "prod.h.vend", "prod.h.fountain", "prod.digest" }));
		Check("no-mismatch-after-resync", () => Production.MismatchCount == 0, () => Production.MismatchCount + " mismatch(es)" + ((Production.MismatchCount > 0) ? (": " + Production.LastMismatch) : ""));
		Do("day-done", delegate
		{
			loads0 = ProductionLab.Loads;
			Mark("day-done");
			return "marked; waiting for the server's night";
		});
		WaitFor("night-snapshot", 400f, () => Game.Day == day0 + 1 && ProductionLab.Loads > loads0 && Net.IsLive, () => "day " + Game.Day + " (was " + day0 + "), loads " + ProductionLab.Loads + " (was " + loads0 + ")");
		Delay("night-settle", 3f);
		AllSame("same-after-night", 30f, AllHashes);
		AllSame("same-labels-after-night", 30f, () => ProductionLab.Sets.SelectMany((string x) => new string[3]
		{
			"prod.wl." + x,
			"prod.wl2." + x,
			"prod.c." + x
		}));
		SameSoon("same-digest-part-after-night", "prod.digest");
		SameSoon("same-cash", "cash");
		SameSoon("same-day", "day");
		Do("night-state", () => "rats " + string.Join("/", ProductionLab.Sets.Select(ProductionLab.LiveRats)) + ", kcal " + string.Join("/", ProductionLab.Sets.Select((string s) => ProductionLab.Tag(s + ".feeder", "STORED_KCAL_TAG"))) + ", live fish " + string.Join("/", ProductionLab.Sets.Select(ProductionLab.LiveFish)) + ", vending cash " + ProductionLab.Tag("vend", "VENDING_CASH") + "/" + ProductionLab.Tag("fountain", "VENDING_CASH") + ", digest part " + Production.DigestPart());
		SameSoon("hireling-features-after-night", () => "prod.w.hw");
		Check("night-not-run-here", () => Production.BlockedNight == 0 && Production.UnseededClient == 0, () => "night production calls stopped here " + Production.BlockedNight + ", unseeded rolls " + Production.UnseededClient);
		Do("night-done", delegate
		{
			Mark("night-done");
			return "marked";
		});
	}
}
