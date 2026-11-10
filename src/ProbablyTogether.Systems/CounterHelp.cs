using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ProbablyTogether.UI;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether.Systems;

public static class CounterHelp
{
	private sealed class Bucket
	{
		public float Tokens = 6f;

		public float At;
	}

	private static readonly Dictionary<string, string> InfoTools = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		{ "magnifier", "look" },
		{ "aquascan", "scan" },
		{ "chem_scanner", "chem" },
		{ "water_test_strip", "strip" },
		{ "plant_analyser", "seed" }
	};

	private const float Burst = 6f;

	private const float PerSecond = 1f;

	private static readonly Dictionary<int, Bucket> buckets = new Dictionary<int, Bucket>();

	private const string SlowDown = "One check at a time, please.";

	private static UseContext admitted;

	public static int Admitted;

	public static int RateRefused;

	public static int Announced;

	public static int Received;

	public static int FromOthers;

	public static int ChatShown;

	public static int ResMismatch;

	public static int ClosedGone;

	public static int ClosedHeld;

	public static string LastClosed = "";

	public static string LastText = "";

	public static string LastRes = "";

	public static string LastWhat = "";

	public static long LastSeq;

	public static long LastItem;

	public static int LastBy = -1;

	public static readonly SortedDictionary<long, string> Views = new SortedDictionary<long, string>();

	public static readonly Dictionary<long, string> Whats = new Dictionary<long, string>();

	private static bool inited;

	[SystemInit]
	internal static void Init()
	{
		if (inited)
		{
			return;
		}
		inited = true;
		Net.On<MsgHelpChecked>("help.checked", null, OnChecked);
		ItemsUse.Performed += OnPerformed;
		Net.PlayerLeft += delegate(PlayerInfo p)
		{
			if (p != null)
			{
				buckets.Remove(p.Id);
			}
		};
	}

	public static string WhatOf(GameItem tool)
	{
		if (tool == null || tool.identifier == null)
		{
			return null;
		}
		if (!InfoTools.TryGetValue(tool.identifier, out var value))
		{
			return null;
		}
		return value;
	}

	public static bool IsPartnerCheck(UseContext ctx)
	{
		try
		{
			if (ctx == null || ctx.Player <= 0 || ctx.Op != UseOp.Target)
			{
				return false;
			}
			if (WhatOf(ctx.Source) == null || ctx.Target == null)
			{
				return false;
			}
			if (!Customers.SomeoneAtCounter)
			{
				return false;
			}
			if (GeneralHelper.IsItemOwned(ctx.Target))
			{
				return false;
			}
			if (!ItemsUse.InCounter(ctx.Target) || ItemsUse.InCounter(ctx.Source))
			{
				return false;
			}
			return Locks.Holder(Customers.CounterLockNow) != ctx.Player;
		}
		catch (Exception ex)
		{
			Log.Warn("counterhelp: classification failed (" + ex.GetType().Name + ": " + ex.Message + "): treated as a counter action");
			return false;
		}
	}

	public static string Admit(UseContext ctx)
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (!buckets.TryGetValue(ctx.Player, out var value))
		{
			Dictionary<int, Bucket> dictionary = buckets;
			int player = ctx.Player;
			Bucket obj = new Bucket
			{
				At = realtimeSinceStartup
			};
			value = obj;
			dictionary[player] = obj;
		}
		value.Tokens = Math.Min(6f, value.Tokens + (realtimeSinceStartup - value.At) * 1f);
		value.At = realtimeSinceStartup;
		if (value.Tokens < 1f)
		{
			RateRefused++;
			if (Log.Allow("counterhelp|rate|" + ctx.Player, 5000L, out var skipped))
			{
				Log.Info("counterhelp: " + Net.PlayerName(ctx.Player) + " checks too fast: refused" + Log.More(skipped));
			}
			return "One check at a time, please.";
		}
		value.Tokens -= 1f;
		admitted = ctx;
		Admitted++;
		return null;
	}

	private static void OnPerformed(UseContext ctx)
	{
		if (ctx != null && ctx.OnServer && ctx == admitted)
		{
			admitted = null;
			Announce(ctx);
		}
	}

	private static void Announce(UseContext ctx)
	{
		string text = WhatOf(ctx.Source);
		GameItem target = ctx.Target;
		if (text == null || target == null)
		{
			return;
		}
		string text2 = "";
		string view = "";
		try
		{
			text2 = Result(text, target);
			view = View(text, target);
		}
		catch (Exception ex)
		{
			Log.Warn("counterhelp: result of " + text + " on " + target.identifier + " failed: " + ex.Message);
		}
		Keep(ctx.Seq, text, view);
		MsgHelpChecked msgHelpChecked = new MsgHelpChecked
		{
			seq = ctx.Seq,
			by = ctx.Player,
			tool = ctx.Source.identifier,
			item = ctx.TargetId,
			name = target.name,
			what = text,
			res = text2,
			holder = Locks.Holder(Customers.CounterLockNow)
		};
		int num = 0;
		foreach (int item in new List<int>(Net.Players.Keys))
		{
			if (Net.IsPlayerLive(item))
			{
				Net.ToPlayer(item, "help.checked", msgHelpChecked);
				num++;
			}
		}
		Announced++;
		Log.Info("counterhelp: " + Net.PlayerName(ctx.Player) + " " + Verb(text) + " the customer's " + target.identifier + " #" + ctx.TargetId + " (use #" + ctx.Seq + ", counter " + ((msgHelpChecked.holder >= 0) ? Net.PlayerName(msgHelpChecked.holder) : "free") + "): '" + text2 + "' -> " + num + " player(s)");
	}

	private static void Keep(long seq, string what, string view)
	{
		Views[seq] = view ?? "";
		Whats[seq] = what;
		while (Views.Count > 64)
		{
			long key = 0L;
			using (SortedDictionary<long, string>.KeyCollection.Enumerator enumerator = Views.Keys.GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					key = enumerator.Current;
				}
			}
			Views.Remove(key);
			Whats.Remove(key);
		}
	}

	private static void OnChecked(MsgHelpChecked m)
	{
		if (m == null || !Net.IsClient)
		{
			return;
		}
		Received++;
		LastSeq = m.seq;
		LastItem = m.item;
		LastBy = m.by;
		LastWhat = m.what ?? "";
		LastRes = m.res ?? "";
		GameItem val = Ids.Find(m.item);
		string text = null;
		if (val != null && m.what != null)
		{
			try
			{
				text = Result(m.what, val);
				Keep(m.seq, m.what, View(m.what, val));
			}
			catch (Exception ex)
			{
				Log.Warn("counterhelp: own result of " + m.what + " failed: " + ex.Message);
			}
			if (text != null && text != (m.res ?? ""))
			{
				ResMismatch++;
				Log.Warn("counterhelp: use #" + m.seq + " result here '" + text + "', server '" + m.res + "'");
			}
		}
		if (m.by != Net.LocalPlayer)
		{
			FromOthers++;
			string text2 = ((val != null) ? val.name : null);
			if (string.IsNullOrEmpty(text2))
			{
				text2 = m.name ?? "item";
			}
			string text3 = (LastText = Kit.Name(Net.PlayerName(m.by)) + " " + Verb(m.what) + " the customer's " + text2 + (string.IsNullOrEmpty(m.res) ? "" : (": " + m.res)) + ".");
			if (!DayLog.Replaying && !Ui.Headless)
			{
				Chat.Local(text3);
				ChatShown++;
			}
		}
	}

	[SystemTick]
	internal static void Tick()
	{
		if (!Net.IsClient || !Net.IsOnline || !Game.ShopLoaded)
		{
			return;
		}
		try
		{
			Sweep();
		}
		catch (Exception ex)
		{
			if (Log.Allow("counterhelp|sweep", 10000L, out var skipped))
			{
				Log.Warn("counterhelp: panel sweep failed (" + ex.GetType().Name + ": " + ex.Message + ")" + Log.More(skipped));
			}
		}
	}

	private static void Sweep()
	{
		InspectionUIManager instance = InspectionUIManager.Instance;
		if ((Object)(object)instance != (Object)null && (Object)(object)instance.panel != (Object)null && instance.panel.activeSelf && Gone(instance.currentInspectingItem))
		{
			Closed("look", instance.currentInspectingItem, held: false, (Action)instance.CloseUI);
		}
		WaterScannerUIManager instance2 = WaterScannerUIManager.Instance;
		if ((Object)(object)instance2 != (Object)null && (Object)(object)instance2.panel != (Object)null && instance2.panel.activeSelf && instance2.coroutine != null)
		{
			GameItem currentWaterContainer = instance2.currentWaterContainer;
			if (Gone(currentWaterContainer))
			{
				Closed("scan", currentWaterContainer, held: false, (Action)instance2.Close);
			}
			else if (OursOnHeldCounter(currentWaterContainer))
			{
				Closed("scan", currentWaterContainer, held: true, (Action)instance2.Close);
			}
		}
		ChemScannerUIManager instance3 = ChemScannerUIManager.Instance;
		if ((Object)(object)instance3 != (Object)null && (Object)(object)instance3.panel != (Object)null && instance3.panel.activeSelf && instance3.coroutine != null)
		{
			GameItem currentScannedItem = instance3.currentScannedItem;
			if (Gone(currentScannedItem))
			{
				Closed("chem", currentScannedItem, held: false, (Action)instance3.Close);
			}
			else if (OursOnHeldCounter(currentScannedItem))
			{
				Closed("chem", currentScannedItem, held: true, (Action)instance3.Close);
			}
		}
		WaterStripUIManager instance4 = WaterStripUIManager.Instance;
		if ((Object)(object)instance4 != (Object)null && (Object)(object)instance4.panel != (Object)null && instance4.panel.activeSelf)
		{
			GameItem currentWaterContainer2 = instance4.currentWaterContainer;
			if (Gone(currentWaterContainer2))
			{
				Closed("strip", currentWaterContainer2, held: false, (Action)instance4.Close);
			}
			else if (OursOnHeldCounter(currentWaterContainer2))
			{
				Closed("strip", currentWaterContainer2, held: true, (Action)instance4.Close);
			}
		}
		PlantAnalyserUIManager instance5 = PlantAnalyserUIManager.Instance;
		if ((Object)(object)instance5 != (Object)null && (Object)(object)instance5.UIPanel != (Object)null && instance5.UIPanel.activeSelf && Gone(instance5.currentItem))
		{
			Closed("seed", instance5.currentItem, held: false, (Action)instance5.HideUI);
		}
	}

	private static bool Gone(GameItem it)
	{
		if (it != null)
		{
			if (!Items.IsGone(it))
			{
				return it.parentInventory == null;
			}
			return true;
		}
		return false;
	}

	private static bool OursOnHeldCounter(GameItem it)
	{
		if (it != null && GeneralHelper.IsItemOwned(it) && ItemsUse.InCounter(it))
		{
			return Customers.CounterHolderOther(Net.LocalPlayer) != null;
		}
		return false;
	}

	private static void Closed(string what, GameItem it, bool held, Action close)
	{
		long num = ((it != null) ? it.uniqueId : 0);
		string text = (held ? Customers.LocalCounterRefusal() : null);
		close();
		if (held)
		{
			ClosedHeld++;
		}
		else
		{
			ClosedGone++;
		}
		LastClosed = what + ":" + (held ? "held" : "gone") + ":#" + num;
		Log.Info("counterhelp: closed the " + what + " panel on " + (((it != null) ? it.identifier : null) ?? "?") + " #" + num + ": " + (held ? ("ours on the counter while " + (text ?? "another player holds it")) : "the item is gone"));
		if (held && !string.IsNullOrEmpty(text))
		{
			Notices.Show(text, "help");
		}
	}

	internal static string Verb(string what)
	{
		return what switch
		{
			"look" => "inspected", 
			"scan" => "scanned", 
			"chem" => "ran the chem scanner over", 
			"strip" => "tested", 
			"seed" => "analysed", 
			_ => "checked", 
		};
	}

	public static string Result(string what, GameItem t)
	{
		if (t == null)
		{
			return "";
		}
		switch (what)
		{
		case "scan":
		case "strip":
			return WaterShort(t);
		case "chem":
			if (t.IsTag("CHEM_ITEM_TAG"))
			{
				if (!t.IsTag("CHEMICAL_PRODUCT_TAG"))
				{
					return "";
				}
				ItemFeature val = t.FindItemFeatureByCategory("CATEGORY_CHEMICAL_PURITY");
				double num = t.GetTagReadonly("CHEMICAL_PRODUCT_PURITY_TAG").GetDouble();
				return WaterFeatureHelper.FormatPurityPercentageFromDouble(num) + " " + (val?.realCondition?.display ?? "?") + ", labeled " + (val?.fakeCondition?.display ?? "?");
			}
			if (!t.IsTag("LIQUID_CONTAINER_TAG"))
			{
				return "";
			}
			return WaterShort(t);
		case "seed":
			return Flat(SeedRange(t));
		default:
			return "";
		}
	}

	private static string WaterShort(GameItem t)
	{
		if (!t.IsTag("LIQUID_CONTAINER_TAG"))
		{
			return "";
		}
		if (WaterHelper.IsEmpty(t))
		{
			return "empty";
		}
		int waterPurity = WaterHelper.GetWaterPurity(t);
		ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(t);
		return WaterFeatureHelper.FormatPurityPercentage(waterPurity) + " " + WaterFeatureHelper.GetWaterDisplayName(waterPurity) + ((waterFeature != null) ? (", labeled " + waterFeature.GetCurrentItemCondition()?.display) : "");
	}

	private static string SeedRange(GameItem t)
	{
		double num = (float)(HydroponicHelper.GetNutrimentLevel(t) - HydroponicHelper.GetNutrimentRange(t)) / 100f;
		double num2 = (float)(HydroponicHelper.GetNutrimentLevel(t) + HydroponicHelper.GetNutrimentRange(t)) / 100f;
		if (num < 0.0)
		{
			num = 0.0;
		}
		return LocHelper.GetLocalizedMechanic("mech_seed_nutriment_range", new object[2] { num, num2 });
	}

	private static string Flat(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return s.Replace("\r", "").Replace("\n", " ").Trim();
		}
		return "";
	}

	public static string View(string what, GameItem t)
	{
		if (t == null)
		{
			return "";
		}
		switch (what)
		{
		case "look":
			return LookView(t);
		case "scan":
			if (WaterHelper.IsEmpty(t))
			{
				return "|";
			}
			return WaterScannerUIManager.GetTestedString(t) + "|" + WaterScannerUIManager.GetLabeledString(t);
		case "chem":
			if (t.IsTag("CHEM_ITEM_TAG"))
			{
				if (!t.IsTag("CHEMICAL_PRODUCT_TAG"))
				{
					return "|";
				}
				ItemFeature val = t.FindItemFeatureByCategory("CATEGORY_CHEMICAL_PURITY");
				double num = t.GetTagReadonly("CHEMICAL_PRODUCT_PURITY_TAG").GetDouble();
				return LocHelper.GetLocalizedMechanic("mech_tooltip_tested", Array.Empty<object>()) + "\n" + WaterFeatureHelper.FormatPurityPercentageFromDouble(num) + "\n" + val?.realCondition?.display + "|" + LocHelper.GetLocalizedMechanic("mech_tooltip_labeled", Array.Empty<object>()) + "\n" + val?.fakeCondition?.display;
			}
			if (WaterHelper.IsEmpty(t))
			{
				return "|";
			}
			return WaterScannerUIManager.GetTestedString(t) + "|" + WaterScannerUIManager.GetLabeledString(t);
		case "strip":
		{
			int waterPurity = WaterHelper.GetWaterPurity(t);
			ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(t);
			return LocHelper.GetLocalizedMechanic("mech_tooltip_tested", Array.Empty<object>()) + "\n" + WaterFeatureHelper.GetWaterDisplayName(waterPurity) + "|" + WaterFeatureHelper.FormatPurityPercentage(waterPurity) + "|" + LocHelper.GetLocalizedMechanic("mech_tooltip_labeled", Array.Empty<object>()) + "\n" + ((waterFeature == null) ? null : waterFeature.GetCurrentItemCondition()?.display);
		}
		case "seed":
			return t.name + "|" + SeedRange(t);
		default:
			return "";
		}
	}

	private static string LookView(GameItem t)
	{
		List<string> list = new List<string>();
		string text = "none";
		if (t.IsTag("CIGARETTE_TAG"))
		{
			list.Add("cigarette");
			text = Claim(t, "CATEGORY_GENUINE_CIGARETTE", "genuineCigarette", injector: false);
		}
		if (t.IsTag("PLASMA_CELL_TAG"))
		{
			list.Add("fuel");
		}
		if (t.IsTag("FOOD_STAMP_TAG"))
		{
			list.Add("stamp");
			text = Claim(t, "CATEGORY_GENUINE_STAMP", "genuineStamp", injector: false);
		}
		if (t.IsTag("INS_INJECTOR_TAG"))
		{
			list.Add("injector");
			text = Claim(t, "CATEGORY_GENUINE_INJECTOR", "genuineInjector", injector: true);
		}
		if (t.IsTag("RIBWICH_TAG"))
		{
			list.Add("ribwich");
		}
		if (t.IsTag("HAND_TAG"))
		{
			list.Add("hand");
		}
		if (t.IsTag("BERRY_TAG"))
		{
			list.Add("berry");
		}
		if (t.IsTag("APPRAISAL_ITEM"))
		{
			PlayerStore instance = PlayerStore.Instance;
			if (instance != null)
			{
				StoreClientInstance currentClientInstance = instance.currentClientInstance;
				if (((currentClientInstance == null) ? ((StoreClient.ClientIntent?)null) : currentClientInstance.GetClientBlueprint()?.clientIntent) == (StoreClient.ClientIntent?)14)
				{
					list.Add("appraisal");
				}
			}
		}
		list.Sort(StringComparer.Ordinal);
		return string.Join("+", list) + "|confront:" + text;
	}

	private static string Claim(GameItem t, string category, string genuineId, bool injector)
	{
		if (GeneralHelper.IsItemOwned(t) || t.IsTag("ALREADY_CONFRONTED_TAG") || NegociationUIManager.IsTradeLocked())
		{
			return "none";
		}
		ItemFeature obj = t.FindItemFeatureByCategory(category);
		if (((obj == null) ? null : obj.GetCurrentItemCondition()?.identifier) == genuineId)
		{
			return "genuine";
		}
		if (!injector)
		{
			return "counterfeit";
		}
		return "none";
	}

	public static string PanelText(string what, GameItem t)
	{
		if (t == null)
		{
			return null;
		}
		switch (what)
		{
		case "look":
		{
			InspectionUIManager instance5 = InspectionUIManager.Instance;
			if ((Object)(object)instance5 == (Object)null || (Object)(object)instance5.panel == (Object)null || !instance5.panel.activeSelf || instance5.currentInspectingItem != t)
			{
				return null;
			}
			List<string> list = new List<string>();
			if (On((Component)(object)instance5.cigaretteUILarge))
			{
				list.Add("cigarette");
			}
			if (On((Component)(object)instance5.fuelUILarge))
			{
				list.Add("fuel");
			}
			if (On((Component)(object)instance5.stampUILarge))
			{
				list.Add("stamp");
			}
			if (On((Component)(object)instance5.injectorUILarge))
			{
				list.Add("injector");
			}
			if (On((Component)(object)instance5.ribwichUILarge))
			{
				list.Add("ribwich");
			}
			if (On((Component)(object)instance5.appHandUILarge))
			{
				list.Add("hand");
			}
			if (On((Component)(object)instance5.insBerryUILarge))
			{
				list.Add("berry");
			}
			if ((Object)(object)instance5.appraisalPanel != (Object)null && instance5.appraisalPanel.activeSelf)
			{
				list.Add("appraisal");
			}
			list.Sort(StringComparer.Ordinal);
			string text = "none";
			if ((Object)(object)instance5.confrontPanel != (Object)null && instance5.confrontPanel.activeSelf)
			{
				string text2 = Tmp((TMP_Text)(object)instance5.confrontTitleTMP);
				text = (text2.Contains("genuine_claim") ? "genuine" : (text2.Contains("counterfeit_claim") ? "counterfeit" : "?"));
			}
			return string.Join("+", list) + "|confront:" + text;
		}
		case "scan":
		{
			WaterScannerUIManager instance3 = WaterScannerUIManager.Instance;
			if ((Object)(object)instance3 == (Object)null || (Object)(object)instance3.panel == (Object)null || !instance3.panel.activeSelf || instance3.currentWaterContainer != t)
			{
				return null;
			}
			return Tmp((TMP_Text)(object)instance3.testedTMP) + "|" + Tmp((TMP_Text)(object)instance3.labelTMP);
		}
		case "chem":
		{
			ChemScannerUIManager instance4 = ChemScannerUIManager.Instance;
			if ((Object)(object)instance4 == (Object)null || (Object)(object)instance4.panel == (Object)null || !instance4.panel.activeSelf || instance4.currentScannedItem != t)
			{
				return null;
			}
			return Tmp((TMP_Text)(object)instance4.testedTMP) + "|" + Tmp((TMP_Text)(object)instance4.labelTMP);
		}
		case "strip":
		{
			WaterStripUIManager instance2 = WaterStripUIManager.Instance;
			if ((Object)(object)instance2 == (Object)null || (Object)(object)instance2.panel == (Object)null || !instance2.panel.activeSelf || instance2.currentWaterContainer != t)
			{
				return null;
			}
			return Tmp((TMP_Text)(object)instance2.testedTMP) + "|" + Tmp((TMP_Text)(object)instance2.purityTMP) + "|" + Tmp((TMP_Text)(object)instance2.labelTMP);
		}
		case "seed":
		{
			PlantAnalyserUIManager instance = PlantAnalyserUIManager.Instance;
			if ((Object)(object)instance == (Object)null || (Object)(object)instance.UIPanel == (Object)null || !instance.UIPanel.activeSelf || instance.currentItem != t)
			{
				return null;
			}
			return Tmp((TMP_Text)(object)instance.titleTMP) + "|" + Tmp((TMP_Text)(object)instance.nutrimentRangeTMP);
		}
		default:
			return null;
		}
	}

	public static bool ClosePanel(string what, GameItem t, bool onlyIfShowing)
	{
		switch (what)
		{
		case "look":
		{
			InspectionUIManager instance3 = InspectionUIManager.Instance;
			if ((Object)(object)instance3 == (Object)null || (Object)(object)instance3.panel == (Object)null || !instance3.panel.activeSelf)
			{
				return false;
			}
			if (onlyIfShowing && !Showing(instance3.currentInspectingItem, t))
			{
				return false;
			}
			instance3.CloseUI();
			return true;
		}
		case "scan":
		{
			WaterScannerUIManager instance2 = WaterScannerUIManager.Instance;
			if ((Object)(object)instance2 == (Object)null || (Object)(object)instance2.panel == (Object)null || !instance2.panel.activeSelf || instance2.coroutine == null)
			{
				return false;
			}
			if (onlyIfShowing && !Showing(instance2.currentWaterContainer, t))
			{
				return false;
			}
			instance2.Close();
			return true;
		}
		case "chem":
		{
			ChemScannerUIManager instance5 = ChemScannerUIManager.Instance;
			if ((Object)(object)instance5 == (Object)null || (Object)(object)instance5.panel == (Object)null || !instance5.panel.activeSelf || instance5.coroutine == null)
			{
				return false;
			}
			if (onlyIfShowing && !Showing(instance5.currentScannedItem, t))
			{
				return false;
			}
			instance5.Close();
			return true;
		}
		case "strip":
		{
			WaterStripUIManager instance4 = WaterStripUIManager.Instance;
			if ((Object)(object)instance4 == (Object)null || (Object)(object)instance4.panel == (Object)null || !instance4.panel.activeSelf)
			{
				return false;
			}
			if (onlyIfShowing && !Showing(instance4.currentWaterContainer, t))
			{
				return false;
			}
			instance4.Close();
			return true;
		}
		case "seed":
		{
			PlantAnalyserUIManager instance = PlantAnalyserUIManager.Instance;
			if ((Object)(object)instance == (Object)null || (Object)(object)instance.UIPanel == (Object)null || !instance.UIPanel.activeSelf)
			{
				return false;
			}
			if (onlyIfShowing && !Showing(instance.currentItem, t))
			{
				return false;
			}
			instance.HideUI();
			return true;
		}
		default:
			return false;
		}
	}

	private static bool Showing(GameItem shown, GameItem t)
	{
		if (shown != null)
		{
			if (shown != t)
			{
				return Gone(shown);
			}
			return true;
		}
		return false;
	}

	private static bool On(Component c)
	{
		if ((Object)(object)c != (Object)null)
		{
			return c.gameObject.activeSelf;
		}
		return false;
	}

	private static string Tmp(TMP_Text t)
	{
		object obj;
		if (!((Object)(object)t == (Object)null))
		{
			obj = t.text;
			if (obj == null)
			{
				return "";
			}
		}
		else
		{
			obj = "";
		}
		return (string)obj;
	}

	public static string ViewsText(IDictionary<long, string> views)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<long, string> view in views)
		{
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(' ');
			}
			Whats.TryGetValue(view.Key, out var value);
			stringBuilder.Append(view.Key.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value ?? "?")
				.Append(':')
				.Append(Fnv(view.Value).ToString("x8"));
		}
		if (stringBuilder.Length != 0)
		{
			return stringBuilder.ToString();
		}
		return "none";
	}

	internal static uint Fnv(string s)
	{
		uint num = 2166136261u;
		if (s != null)
		{
			foreach (char c in s)
			{
				num ^= c;
				num *= 16777619;
			}
		}
		return num;
	}
}
