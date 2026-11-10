using System;
using System.Globalization;
using System.Linq;

namespace ProbablyTogether.Systems;

public static class CrewCustomers
{
	public const int MinMajor = 0;

	public const int MinMinor = 2;

	public const int MinPatch = 8;

	public const string MinText = "0.2.8";

	public const int Cap = 6;

	private static int today;

	private static int todayDay = -1;

	public static int Today
	{
		get
		{
			if (todayDay != Game.Day)
			{
				return 0;
			}
			return today;
		}
	}

	public static string LastDecision { get; private set; } = "";

	public static bool AtLeastMin(string v)
	{
		if (string.IsNullOrEmpty(v))
		{
			return false;
		}
		string[] array = v.Trim().Split('.', '-', ' ');
		int[] array2 = new int[3];
		for (int i = 0; i < 3; i++)
		{
			if (array.Length <= i || !int.TryParse(array[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out array2[i]))
			{
				return false;
			}
		}
		if (array2[0] != 0)
		{
			return array2[0] > 0;
		}
		if (array2[1] != 2)
		{
			return array2[1] > 2;
		}
		return array2[2] >= 8;
	}

	private static bool TutorialDay()
	{
		PlayerStore instance = PlayerStore.Instance;
		StoreStation instance2 = StoreStation.Instance;
		if (instance == null || instance2 == null)
		{
			return true;
		}
		if (instance.skipIntro || instance2.dayCounter > 13)
		{
			if (instance.skipIntro)
			{
				return instance2.dayCounter == 1;
			}
			return false;
		}
		return true;
	}

	internal static int Decide()
	{
		today = 0;
		todayDay = Game.Day;
		double num = ServerMode.Config?.extraCustomersPerPlayer ?? 0.0;
		int playerCount = Server.PlayerCount;
		StoreClientManager obj = Game.Store?.storeClientManager;
		int valueOrDefault = (obj?.clientStack?.Count).GetValueOrDefault();
		bool flag = Args.Bot != null && (Args.Get("--br-crew-anyday") ?? "") == "1";
		string text = null;
		if (obj == null)
		{
			text = "no queue";
		}
		else if (num <= 0.0)
		{
			text = "extraCustomersPerPlayer is 0";
		}
		else if (playerCount < 2)
		{
			text = playerCount + " player(s) on";
		}
		else if (!flag && TutorialDay())
		{
			text = "a tutorial day (the game queues no regular customers today)";
		}
		else
		{
			Server.Peer peer = Server.ByPlayer.Values.FirstOrDefault((Server.Peer p) => !AtLeastMin(p.ModVersion));
			if (peer != null)
			{
				text = (peer.Info?.Name ?? "a player") + " runs ProbablyTogether " + ((peer.ModVersion.Length > 0) ? peer.ModVersion : "(unknown)") + ", older than 0.2.8";
			}
		}
		int num2 = 0;
		if (text == null)
		{
			num2 = (int)Math.Round(num * (double)(playerCount - 1) * (double)valueOrDefault, MidpointRounding.AwayFromZero);
			if (num2 > 6)
			{
				num2 = 6;
			}
			if (num2 <= 0)
			{
				num2 = 0;
				text = "nobody queued to scale (" + valueOrDefault + ")";
			}
		}
		LastDecision = ((num2 > 0) ? ("+" + num2 + " for " + playerCount + " players (queued " + valueOrDefault + ", extraCustomersPerPlayer " + num.ToString("0.##", CultureInfo.InvariantCulture) + ", cap " + 6 + ")") : ("none: " + text));
		Log.Info("crew customers: day " + Game.Day + ", " + LastDecision);
		today = num2;
		return num2;
	}

	internal static int Add(int n)
	{
		StoreClientManager val = Game.Store?.storeClientManager;
		if (val?.clientStack == null || n <= 0)
		{
			return 0;
		}
		int count = val.clientStack.Count;
		try
		{
			for (int i = 0; i < n && i < 6; i++)
			{
				StoreClient val2 = val.PickClient();
				if (val2 != null)
				{
					val.AddClient(val2);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("crew customers: the game's PickClient threw after " + (val.clientStack.Count - count) + " extra customer(s): " + ex.Message);
		}
		return val.clientStack.Count - count;
	}

	internal static bool JoinOk(string mod, out string why)
	{
		why = null;
		if (Today <= 0 || AtLeastMin(mod))
		{
			return true;
		}
		why = "Today this shop has extra customers that need the upstream mod 0.2.8 or newer (you have " + Text.Clip(string.IsNullOrEmpty(mod) ? "?" : mod, 24) + "). Update the mod, or join again tomorrow morning.";
		Log.Info("crew customers: refused a join from ProbablyTogether " + Text.Clip(mod ?? "?", 24) + " (today +" + Today + ")");
		return false;
	}
}
