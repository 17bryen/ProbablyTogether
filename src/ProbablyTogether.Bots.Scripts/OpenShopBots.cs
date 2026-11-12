using System;
using System.Collections.Generic;
using System.Linq;
using ProbablyTogether.Systems;

namespace ProbablyTogether.Bots.Scripts;

/// The host opens the shop; the player at home records its own queue and the host's, entry by entry.
public sealed class OpenShopProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("cust.text", () => (!Net.IsServer) ? "" : Customers.DigestText());
	}
}

public sealed class OpenH : DayNightBotBase
{
	public override string Name => "open-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 420f;

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive + ", day " + DayNightBotBase.Day);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Delay("b-settles", 8f);
		Do("members", () => Members.Describe());
		Do("look-at-the-clientele-panel", delegate
		{
			// What the Clientele panel does when opened: asks which customers the showcase will draw.
			if (!Args.Has("--br-host-peeks"))
			{
				return "the host does not look (only the other player does)";
			}
			return string.Join(", ", from x in ShowcaseHelper.GetDisplayCasePrediction()
				select x.Item1 + " " + x.Item2 + "% (" + x.Item3 + ")");
		});
		Do("open-click", delegate
		{
			StoreShutterButton.Instance.OnClick();
			return "sent";
		});
		WaitFor("opened", 30f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Do("my-queue", () => Customers.DigestText());
		Delay("b-compares", 40f);
	}
}

public sealed class OpenB : DayNightBotBase
{
	private string mine = "";

	private string theirs = "";

	public override string Name => "open-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 420f;

	private static string[] Parts(string s)
	{
		return (s ?? "").Split(new char[2] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
	}

	protected override void Build()
	{
		JoinSteps();
		Do("look-at-the-clientele-panel", delegate
		{
			return string.Join(", ", from x in ShowcaseHelper.GetDisplayCasePrediction()
				select x.Item1 + " " + x.Item2 + "% (" + x.Item3 + ")");
		});
		WaitFor("opened-by-host", 300f, () => DayNightBotBase.State == "OPEN", () => "state " + DayNightBotBase.State);
		Delay("the-opening-lands", 2f);
		Do("my-queue", delegate
		{
			mine = Customers.DigestText();
			return mine;
		});
		WaitServer("the-hosts-queue", 20f, delegate(IReadOnlyDictionary<string, string> v)
		{
			theirs = DayNightBotBase.V(v, "cust.text");
			return theirs != "";
		}, "cust.text");
		Check("same-customers-as-the-host", () => Customers.DigestText() == theirs || mine == theirs, delegate
		{
			string[] array = Parts(Customers.DigestText());
			string[] array2 = Parts(theirs);
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
			return (list.Count == 0) ? ("same, " + array.Length + " part(s)") : (list.Count + " of " + Math.Max(array.Length, array2.Length) + " part(s) differ: " + string.Join("  ", list.Take(12)));
		});
		Check("opened-with-the-hosts-supplier-picks", () => SupplierPicks.Adopted >= 1, () => "took over the host's picks " + SupplierPicks.Adopted + " time(s)");
		Check("a-trades-supplier-asks-for-its-holder", delegate
		{
			StoreClient storeClient = DayNightBotBase.Ps.storeClientManager.clientStack.FirstOrDefault((StoreClient c) => c != null && c.identifier == "retired_rancher");
			int num = Specs.TakenBy("RANCHER");
			return storeClient == null || num == 0 || (TradeVisits.SeatFor("retired_rancher") == num && (storeClient.mainDialogue?.plainText ?? "").Contains(Members.NameOf(num)));
		}, delegate
		{
			StoreClient storeClient = DayNightBotBase.Ps.storeClientManager.clientStack.FirstOrDefault((StoreClient c) => c != null && c.identifier == "retired_rancher");
			return (storeClient == null) ? "no rancher's supplier today" : ("for seat " + TradeVisits.SeatFor("retired_rancher") + " (the rancher is seat " + Specs.TakenBy("RANCHER") + "); first line: '" + (storeClient.mainDialogue?.plainText ?? "") + "'");
		});
		Check("no-reload-asked", () => Digest.ReloadsAsked == 0 && DayNight.Mismatches == 0, () => "reloads asked " + Digest.ReloadsAsked + ", day/night mismatches " + DayNight.Mismatches);
	}
}
