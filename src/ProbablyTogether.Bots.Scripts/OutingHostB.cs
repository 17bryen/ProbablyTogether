using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

/// The host travels, B stays home: B keeps the server's shop and uses the back counter while Dr. Jackson's stall holds it on the host's screen.
public sealed class OutingHostB : DayNightBotBase
{
	private static readonly string[] Same = new string[9] { "core.digest", "items.digest", "dn.digest", "econ.digest", "nego.digest", "events.hash", "cash", "dn.visit", "dn.state" };

	private int moveQ;

	private int clearQ;

	private bool askedLab;

	private long yId;

	public override string Name => "outing-hb";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 900f;

	private static EmporiumEntry E => EmporiumEntry.Instance;

	protected override void Build()
	{
		JoinSteps();
		Do("day0", delegate
		{
			day0 = DayNightBotBase.Day;
			return "day " + day0;
		});
		WaitFor("closed-by-host", 300f, () => DayNightBotBase.State == "CLOSED", () => "state " + DayNightBotBase.State);
		Delay("past-close-timers", 5f);
		Agree("same-closed", 30f, Same);
		WaitFor("host-out", 120f, () => Outings.AnyOut, () => "nobody is out yet");
		Check("still-in-shop", () => DayNightOuting.LocationId == "" && !TransitUI.IsOpen && !Outings.Private && Presence.LocalView == "store", () => "at '" + DayNightOuting.LocationId + "', map " + TransitUI.IsOpen + ", view " + Presence.LocalView);
		Delay("host-travels", 8f);
		Agree("same-while-host-at-dump", 30f, Same);
		WaitFor("host-home", 240f, () => !Outings.AnyOut, () => Outings.WhoIsOut() + " is still out");
		Delay("settle", 3f);
		Agree("same-after-dump", 30f, Same);
		WaitServer("host-at-jackson", 240f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "out.jackson") == "1", "out.jackson", "out.count");
		Check("still-in-shop-2", () => DayNightOuting.LocationId == "" && DayNightBotBase.Ps.currentClientInstance == null && !Outings.Private, () => "at '" + DayNightOuting.LocationId + "', customer " + ((DayNightBotBase.Ps.currentClientInstance != null) ? "at the counter" : "none"));
		Agree("same-while-host-at-jackson", 30f, Same);
		Step("y-on-counter", 25f, delegate(BotStep st)
		{
			if (moveQ == 0)
			{
				int x = 0;
				int y = 0;
				GameItem val = ItemsBotBase.Movables((GameInventory)(object)E.invElement).FirstOrDefault((GameItem i) => ItemsBotBase.Spot((GameInventory)(object)E.backInvinvElement, i, i.unitCount, i.modifiedShape.orientation, out x, out y));
				if (val == null)
				{
					// In a shop with storage bays the starting stock sits in seat 1's bay: ask the lab for something loose.
					if (!askedLab)
					{
						askedLab = true;
						LabKit.Do("give", "bottled_water", "2");
					}
					st.Detail = "no item fits the back counter (asked the lab for two bottles)";
					return StepStatus.Running;
				}
				yId = Ids.Of(val);
				ItemsBotBase.Spot((GameInventory)(object)E.backInvinvElement, val, val.unitCount, val.modifiedShape.orientation, out x, out y);
				moveQ = ItemsMove.Move(val, (GraphNodeStorage)(object)E.backInvinvElement, x, y, val.modifiedShape.flipped, val.modifiedShape.orientation, -1, rep: false, "bot");
				st.Detail = val.identifier + "#" + yId + " -> back counter " + x + "," + y + " while the stall is on it for the host";
				return StepStatus.Running;
			}
			string text = ItemsMove.Outcome(moveQ);
			if (text.StartsWith("rejected", StringComparison.Ordinal) || text == "applied-mismatch")
			{
				st.Detail = st.Detail + "; server " + text;
				return StepStatus.Fail;
			}
			GameItem val2 = Ids.Find(yId);
			return (text == "applied" && val2 != null && (object)val2.parentInventory == E.backInvinvElement) ? StepStatus.Pass : StepStatus.Running;
		});
		Agree("same-after-my-move", 30f, Same);
		Step("clear-the-counter", 25f, delegate(BotStep st)
		{
			if (clearQ == 0)
			{
				clearQ = ItemsMove.TransferButton(frontVisible: false, "bot");
				st.Detail = "pressed with " + ((GameInventory)E.backInvinvElement).childItems.Count + " item(s) on the back counter";
				return StepStatus.Running;
			}
			string text = ItemsMove.Outcome(clearQ);
			if (text.StartsWith("rejected", StringComparison.Ordinal) || text == "applied-mismatch")
			{
				st.Detail = st.Detail + "; server " + text;
				return StepStatus.Fail;
			}
			GameItem val = Ids.Find(yId);
			return (text == "applied" && val != null && (object)val.parentInventory == E.invElement && ((GameInventory)E.backInvinvElement).childItems.Count((GameItem i) => GeneralHelper.IsItemOwned(i)) == 0) ? StepStatus.Pass : StepStatus.Running;
		});
		Agree("same-after-clearing", 30f, Same);
		WaitFor("host-home-2", 240f, () => !Outings.AnyOut, () => Outings.WhoIsOut() + " is still out");
		Delay("settle-2", 3f);
		Check("y-still-in-inventory", delegate
		{
			GameItem val = Ids.Find(yId);
			return val != null && (object)val.parentInventory == E.invElement;
		}, () => "my item #" + yId + " is in the inventory, where clearing the counter sent it");
		Agree("nego-after-jackson", 12f, "out.nego").ContinueOnFail = true;
		Agree("same-after-jackson", 30f, Same);
		Check("no-reloads", () => DayNight.Mismatches == 0 && Digest.ReloadsAsked == 0, () => "day/night mismatches " + DayNight.Mismatches + "; reloads asked for " + Digest.ReloadsAsked + "; differences sat out while the host was away " + Digest.Deferred);
		Check("sat-out-the-hosts-deal", () => Digest.Deferred >= 1, () => "differences sat out " + Digest.Deferred + " (the host's deal at the stall was not ours to see until it came home)");
		Ready("ready");
		WaitFor("night", 200f, () => DayNightBotBase.Live && DayNightBotBase.Day == day0 + 1 && DayNightBotBase.State == "MORNING", () => "day " + DayNightBotBase.Day + ", " + DayNightBotBase.State);
		Agree("same-night", 30f, "day", "cash", "dn.state", "dn.digest", "items.digest");		WaitFor("opened-next-day", 120f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Delay("the-opening-lands", 2f);
		string theirs = "";
		WaitServer("the-hosts-queue", 20f, delegate(IReadOnlyDictionary<string, string> v)
		{
			theirs = DayNightBotBase.V(v, "cust.text");
			return theirs != "";
		}, "cust.text");
		Check("same-customers-the-morning-after-the-hosts-trip", () => Customers.DigestText() == theirs, delegate
		{
			string[] array = Customers.DigestText().Split(new char[2] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
			string[] array2 = theirs.Split(new char[2] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
			List<string> list = new List<string>();
			for (int i = 0; i < Math.Max(array.Length, array2.Length); i++)
			{
				string text = (i < array.Length) ? array[i] : "(nothing)";
				string text2 = (i < array2.Length) ? array2[i] : "(nothing)";
				if (text != text2)
				{
					list.Add("[" + i + "] here " + text + " / host " + text2);
				}
			}
			return (list.Count == 0) ? ("same, " + array.Length + " part(s)") : (list.Count + " part(s) differ: " + string.Join("  ", list.Take(10)));
		});
		Check("no-reload-at-the-opening", () => DayNight.Mismatches == 0, () => "day/night mismatches " + DayNight.Mismatches + "; reloads asked " + Digest.ReloadsAsked);
	}
}
