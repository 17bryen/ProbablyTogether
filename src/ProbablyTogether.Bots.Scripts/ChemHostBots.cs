using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProbablyTogether.Systems;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

/// The host stays home: the plate heats one game second per real second, the client sees each second, and the bottle cools once the plate is off.
public static class ChemLab
{
	/// Set by the host's script; the client reads it through the probe "chem.id".
	public static long BottleId;

	public static long PlateId;

	public static GameItem Plate => (PlateId != 0L) ? Ids.Find(PlateId) : null;

	public static GameItem Bottle => (BottleId != 0L) ? Ids.Find(BottleId) : null;

	public static double Temp(GameItem bottle)
	{
		return (bottle == null) ? (-999.0) : bottle.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
	}

	public static int Mode(GameItem plate)
	{
		return (plate == null) ? (-1) : plate.GetTagReadonly("HEATING_PLATE_STATE").GetInt();
	}

	public static bool OnPlate(GameItem bottle, GameItem plate)
	{
		return bottle != null && plate != null && GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)plate, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null).Contains(bottle);
	}

	public static string T(double d)
	{
		return d.ToString("0.0", CultureInfo.InvariantCulture);
	}

	/// The game's arithmetic for a plate on Low: MachineHeatingPlate.HeatLiquidOnStove, then ChemicalHelper.GetCooledTemp.
	public static double After(double from, int seconds, int volume)
	{
		double num = 0.05 / (Math.Sqrt(volume) + 11.0) * 125.0;
		double num2 = from;
		for (int i = 0; i < seconds; i++)
		{
			if (num2 < 99.0)
			{
				num2 = Math.Min(num2 + (99.0 - num2) * (10.0 / 350.0) + 1.0, 99.0);
			}
			num2 = 26.0 + (num2 - 26.0) * Math.Exp(0.0 - num);
		}
		return num2;
	}
}

public sealed class ChemProbes : BotProbeSet
{
	public override void Register()
	{
		BotProbes.Add("chem.id", () => (!Net.IsServer) ? "" : BotProbeSet.Str(ChemLab.BottleId));
		BotProbes.Add("chem.temp", () => (!Net.IsServer) ? "" : ChemLab.T(ChemLab.Temp(ChemLab.Bottle)));
		BotProbes.Add("chem.mode", () => (!Net.IsServer) ? "" : BotProbeSet.Str(ChemLab.Mode(ChemLab.Plate)));
	}
}

public sealed class ChemH : DayNightBotBase
{
	private HashSet<long> before;

	private int q;

	private double temp0;

	private double temp1;

	private double peak;

	private float lowAt;

	private int volume;

	public override string Name => "chem-h";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 420f;

	private static EmporiumEntry E => EmporiumEntry.Instance;

	private static void Radio(int index)
	{
		object obj = MachinesLab.Widget<MachineryRadioButton>(ChemLab.Plate, index);
		MachineryRadioButton val = (MachineryRadioButton)((obj is MachineryRadioButton) ? obj : null);
		if (val == null)
		{
			throw new InvalidOperationException("the heating plate has no radio button " + index);
		}
		val.SetOn(true, true);
	}

	protected override void Build()
	{
		WaitFor("hosting", 240f, () => Net.IsHost && Net.IsLive && Live, () => "role " + Net.Role + ", live " + Net.IsLive + ", day " + DayNightBotBase.Day);
		WaitFor("b-here", 240f, () => ProbablyTogether.Server.PlayerCount >= 2, () => ProbablyTogether.Server.PlayerCount + " player(s)");
		Delay("b-settles", 6f);
		Do("ask-the-lab", delegate
		{
			before = new HashSet<long>(from i in Items.All()
				select Ids.Of(i));
			LabKit.Do("give", "heating_plate", "1");
			LabKit.Do("give", "bottled_water", "1");
			return "asked for a heating plate and a bottle of water";
		});
		WaitFor("plate-and-bottle", 20f, delegate
		{
			GameItem val = Items.All().FirstOrDefault((GameItem i) => i.identifier == "heating_plate" && !before.Contains(Ids.Of(i)));
			GameItem val2 = Items.All().FirstOrDefault((GameItem i) => i.identifier == "bottled_water" && !before.Contains(Ids.Of(i)));
			if (val == null || val2 == null)
			{
				return false;
			}
			ChemLab.PlateId = Ids.Of(val);
			ChemLab.BottleId = Ids.Of(val2);
			return true;
		}, () => "plate #" + ChemLab.PlateId + ", bottle #" + ChemLab.BottleId);
		Do("fill-the-bottle", delegate
		{
			LabKit.Do("water", ChemLab.BottleId.ToString(CultureInfo.InvariantCulture), "2");
			return "asked the lab to fill bottle #" + ChemLab.BottleId;
		});
		WaitFor("bottle-has-liquid", 15f, () => !WaterHelper.IsEmpty(ChemLab.Bottle), delegate
		{
			volume = WaterHelper.GetTotalVolume(ChemLab.Bottle);
			return "volume " + volume + ", " + ChemLab.T(ChemLab.Temp(ChemLab.Bottle)) + " C";
		});
		Step("bottle-onto-the-plate", 25f, delegate(BotStep st)
		{
			if (q == 0)
			{
				q = ItemsMove.InsertInto(ChemLab.Bottle, ChemLab.Plate, -1, chain: false, "bot");
				st.Detail = "bottle #" + ChemLab.BottleId + " into plate #" + ChemLab.PlateId;
				return StepStatus.Running;
			}
			string text = ItemsMove.Outcome(q);
			if (text.StartsWith("rejected", StringComparison.Ordinal))
			{
				st.Detail = st.Detail + "; server " + text;
				return StepStatus.Fail;
			}
			return (text == "applied") ? StepStatus.Pass : StepStatus.Running;
		});
		Check("bottle-is-on-the-plate", () => ChemLab.OnPlate(ChemLab.Bottle, ChemLab.Plate), () => "on the plate: " + ChemLab.OnPlate(ChemLab.Bottle, ChemLab.Plate));
		Do("temp0", delegate
		{
			temp0 = ChemLab.Temp(ChemLab.Bottle);
			return ChemLab.T(temp0) + " C with the plate off (mode " + ChemLab.Mode(ChemLab.Plate) + ")";
		});
		Delay("plate-off", 5f);
		Check("nothing-heats-while-off", () => ChemLab.Temp(ChemLab.Bottle) <= temp0 + 0.5, () => ChemLab.T(ChemLab.Temp(ChemLab.Bottle)) + " C (was " + ChemLab.T(temp0) + ")");
		Do("low", delegate
		{
			temp1 = ChemLab.Temp(ChemLab.Bottle);
			lowAt = Time.realtimeSinceStartup;
			Radio(1);
			return "the host switches the plate to Low at " + ChemLab.T(temp1) + " C, AT HOME (shop " + DayNightBotBase.State + ", nobody out: " + !Outings.AnyOut + ")";
		});
		WaitFor("mode-low", 10f, () => ChemLab.Mode(ChemLab.Plate) == 1, () => "mode " + ChemLab.Mode(ChemLab.Plate));
		Delay("ten-seconds-at-home", 10f);
		Check("heats-one-second-per-second-at-home", delegate
		{
			int num = Mathf.RoundToInt(Time.realtimeSinceStartup - lowAt);
			double num2 = ChemLab.Temp(ChemLab.Bottle);
			return num2 >= ChemLab.After(temp1, num - 3, volume) && num2 <= ChemLab.After(temp1, num + 2, volume);
		}, delegate
		{
			int num = Mathf.RoundToInt(Time.realtimeSinceStartup - lowAt);
			return ChemLab.T(ChemLab.Temp(ChemLab.Bottle)) + " C after " + num + " real second(s) on Low; the game's arithmetic gives " + ChemLab.T(ChemLab.After(temp1, num - 3, volume)) + " to " + ChemLab.T(ChemLab.After(temp1, num + 2, volume)) + " C for " + (num - 3) + " to " + (num + 2) + " game seconds; once-a-second functions run here " + Machines.ServerTicks + ", held back " + Machines.SuppressedTicks + "; pushes sent " + Machines.TicksSent;
		});
		Delay("b-watches", 8f);
		Do("off", delegate
		{
			Radio(0);
			return "the host switches the plate off";
		});
		WaitFor("mode-off", 10f, () => ChemLab.Mode(ChemLab.Plate) == 0, () => "mode " + ChemLab.Mode(ChemLab.Plate));
		Delay("last-heat-lands", 2f);
		Do("peak", delegate
		{
			peak = ChemLab.Temp(ChemLab.Bottle);
			return "peak " + ChemLab.T(peak) + " C";
		});
		Delay("cooling", 10f);
		Check("cools-at-home", () => ChemLab.Temp(ChemLab.Bottle) < peak - 0.5, () => ChemLab.T(ChemLab.Temp(ChemLab.Bottle)) + " C ten seconds after the peak of " + ChemLab.T(peak));
		Delay("b-finishes", 30f);
	}
}

public sealed class ChemB : DayNightBotBase
{
	private long id;

	private double seen;

	private double hostTemp;

	private double offTemp;

	public override string Name => "chem-b";

	public override BotRole Role => BotRole.Client;

	public override float TimeoutSeconds => 420f;

	private GameItem Bottle => (id != 0L) ? Ids.Find(id) : null;

	private static double Parse(string s)
	{
		double result;
		return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : (-999.0);
	}

	protected override void Build()
	{
		JoinSteps();
		WaitServer("host-set-up", 180f, delegate(IReadOnlyDictionary<string, string> v)
		{
			long.TryParse(DayNightBotBase.V(v, "chem.id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
			return id != 0L && Bottle != null;
		}, "chem.id", "chem.mode");
		WaitServer("host-heats", 120f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "chem.mode") == "1", "chem.mode", "chem.temp");
		WaitFor("i-see-it-heat", 30f, () => ChemLab.Temp(Bottle) >= 35.0 && Machines.TicksApplied >= 3, () => "bottle here " + ChemLab.T(ChemLab.Temp(Bottle)) + " C; pushes applied here " + Machines.TicksApplied + "; once-a-second functions held back here " + Machines.SuppressedTicks);
		// Three looks one second apart must each show a higher temperature.
		Do("look-1", delegate
		{
			seen = ChemLab.Temp(Bottle);
			return ChemLab.T(seen) + " C";
		});
		Delay("one-second", 1.5f);
		Check("look-2-is-hotter", delegate
		{
			double num = ChemLab.Temp(Bottle);
			bool result = num > seen + 0.5;
			seen = num;
			return result;
		}, () => ChemLab.T(seen) + " C");
		Delay("another-second", 1.5f);
		Check("look-3-is-hotter", delegate
		{
			double num = ChemLab.Temp(Bottle);
			bool result = num > seen + 0.5;
			seen = num;
			return result;
		}, () => ChemLab.T(seen) + " C");
		WaitServer("close-to-the-hosts", 20f, delegate(IReadOnlyDictionary<string, string> v)
		{
			hostTemp = Parse(DayNightBotBase.V(v, "chem.temp"));
			return Math.Abs(ChemLab.Temp(Bottle) - hostTemp) <= 8.0;
		}, "chem.temp");
		WaitServer("host-switched-off", 120f, (IReadOnlyDictionary<string, string> v) => DayNightBotBase.V(v, "chem.mode") == "0", "chem.mode", "chem.temp");
		Delay("last-heat-lands", 3f);
		Do("off-temp", delegate
		{
			offTemp = ChemLab.Temp(Bottle);
			return ChemLab.T(offTemp) + " C here with the plate off";
		});
		Delay("cooling", 8f);
		Check("i-see-it-cool", () => ChemLab.Temp(Bottle) < offTemp - 0.3, () => ChemLab.T(ChemLab.Temp(Bottle)) + " C here (was " + ChemLab.T(offTemp) + ")");
		WaitServer("still-close-to-the-hosts", 20f, delegate(IReadOnlyDictionary<string, string> v)
		{
			hostTemp = Parse(DayNightBotBase.V(v, "chem.temp"));
			return Math.Abs(ChemLab.Temp(Bottle) - hostTemp) <= 2.0;
		}, "chem.temp");
		Check("no-reloads", () => Digest.ReloadsAsked == 0, () => "reloads asked for " + Digest.ReloadsAsked + "; machine mismatches " + Machines.MismatchCount);
		Agree("same-as-server", 30f, "core.digest", "items.digest");
	}
}
