using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ProbablyTogether;

public sealed class ServerConfig
{
	public sealed class ServerMod
	{
		public string id = "";

		public string ws = "";
	}

	public sealed class NewRun
	{
		public string scenario = "WATER_MERCHANT";

		public string difficulty = "normal";

		public bool skipIntro = true;

		/// The founder's trade and perks are dealt at random (Systems.RandomStarts).
		public bool randomTrade;

		public bool randomPerks;
	}

	public int port = 27700;

	public string name = "My shop";

	public string password = "";

	public int maxPlayers = 4;

	public int saveSlot = 1;

	public NewRun newRun = new NewRun();

	public int readyTimeoutSeconds = 120;

	/// Each player runs their own shop and they compete (Systems.Versus); readyTimeoutSeconds is the bedtime timer.
	public bool versus;

	/// A versus run ends at the bedtime of this day.
	public int versusDays = 14;

	[JsonIgnore]
	public string[] adminSteamIds = new string[0];

	public int frameRate = 10;

	public bool endless;

	public List<ServerMod> mods = new List<ServerMod>();

	public bool modsMustMatch;

	/// Unused; see customersPerPlayer.
	public double extraCustomersPerPlayer = 0.5;

	/// Extra customers a day for every player of the shop after the first, connected or not (Systems.Members).
	public double customersPerPlayer = 2.0;

	/// Rent goes up by this many percent for every player of the shop after the first.
	public int rentPercentPerPlayer = 50;

	/// Every player gets a storage bay of their own and picks a trade whose machines are theirs alone (Systems.Bays, Systems.Specs).
	/// False: nothing is handed out or picked; bays and trades a shop already has keep working.
	public bool playerOwnership = true;

	/// Seconds a counter holder must be idle before another player's action takes the counter over. -1 = strict (stock).
	public double counterTakeoverSeconds = 1.0;

	/// Players go out one by one while the others stay in. False: the stock group trip.
	public bool individualOutings;

	/// Players who join are dealt their trade, or their perks, at random (with playerOwnership only).
	public bool forceRandomTrade;

	public bool forceRandomPerks;

	[JsonIgnore]
	public string LoadedFrom;

	[JsonIgnore]
	public bool HadAdminIds;

	public ulong WorkshopIdOf(string id)
	{
		if (mods == null || id == null)
		{
			return 0uL;
		}
		foreach (ServerMod mod in mods)
		{
			if (mod != null && string.Equals((mod.id ?? "").Trim(), id, StringComparison.Ordinal) && ulong.TryParse((mod.ws ?? "").Trim(), out var result))
			{
				return result;
			}
		}
		return 0uL;
	}

	public static ServerConfig Load()
	{
		string text = Args.Config;
		if (string.IsNullOrEmpty(text))
		{
			text = Paths.Under("server.json");
		}
		ServerConfig serverConfig = new ServerConfig
		{
			LoadedFrom = text
		};
		try
		{
			if (File.Exists(text))
			{
				JObject val = JObject.Parse(File.ReadAllText(text));
				JsonReader val2 = ((JToken)val).CreateReader();
				try
				{
					Wire.Serializer.Populate(val2, (object)serverConfig);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
				if (serverConfig.newRun == null)
				{
					serverConfig.newRun = new NewRun();
				}
				serverConfig.LoadedFrom = text;
				foreach (JProperty item in val.Properties())
				{
					if (string.Equals(item.Name, "adminSteamIds", StringComparison.OrdinalIgnoreCase))
					{
						serverConfig.HadAdminIds = true;
					}
				}
			}
			else
			{
				serverConfig.LoadedFrom = text + " (missing: defaults)";
			}
		}
		catch (Exception ex)
		{
			Log.Error("server.json at " + text + " is unreadable (" + ex.Message + "); using defaults");
			serverConfig = new ServerConfig
			{
				LoadedFrom = text + " (unreadable: defaults)"
			};
		}
		serverConfig.Clamp();
		return serverConfig;
	}

	private void Clamp()
	{
		if (port < 1 || port > 65535)
		{
			port = 27700;
		}
		if (maxPlayers < 1)
		{
			maxPlayers = 1;
		}
		if (maxPlayers > 4)
		{
			maxPlayers = 4;
		}
		if (customersPerPlayer < 0.0)
		{
			customersPerPlayer = 0.0;
		}
		if (customersPerPlayer > 6.0)
		{
			customersPerPlayer = 6.0;
		}
		if (rentPercentPerPlayer < 0)
		{
			rentPercentPerPlayer = 0;
		}
		if (rentPercentPerPlayer > 500)
		{
			rentPercentPerPlayer = 500;
		}
		if (saveSlot < 0 || saveSlot > 9999)
		{
			saveSlot = 1;
		}
		if (frameRate < 1)
		{
			frameRate = 1;
		}
		if (frameRate > 60)
		{
			frameRate = 60;
		}
		if (readyTimeoutSeconds < 5)
		{
			readyTimeoutSeconds = 5;
		}
		if (versusDays < 1)
		{
			versusDays = 1;
		}
		if (versusDays > 365)
		{
			versusDays = 365;
		}
		if (double.IsNaN(extraCustomersPerPlayer) || double.IsInfinity(extraCustomersPerPlayer))
		{
			extraCustomersPerPlayer = 0.5;
		}
		if (extraCustomersPerPlayer < 0.0)
		{
			extraCustomersPerPlayer = 0.0;
		}
		if (extraCustomersPerPlayer > 2.0)
		{
			extraCustomersPerPlayer = 2.0;
		}
		if (name == null)
		{
			name = "My shop";
		}
		if (name.Length > 64)
		{
			name = name.Substring(0, 64);
		}
		if (password == null)
		{
			password = "";
		}
		if (mods == null)
		{
			mods = new List<ServerMod>();
		}
		if (mods.Count > 64)
		{
			mods = mods.GetRange(0, 64);
		}
		adminSteamIds = new string[0];
	}

	public string Describe()
	{
		return "port " + port + ", name '" + name + "', " + ((password.Length > 0) ? "password set" : "no password") + ", max " + maxPlayers + " players, slot " + saveSlot + ", new run " + newRun.scenario + "/" + newRun.difficulty + (endless ? " (endless)" : "") + ", ready timeout " + readyTimeoutSeconds + " s, " + frameRate + " fps" + ((mods.Count > 0 || modsMustMatch) ? (", " + mods.Count + " Workshop mod(s) named" + (modsMustMatch ? ", players must match" : "")) : "") + ", extra customers per extra player " + extraCustomersPerPlayer.ToString("0.##", CultureInfo.InvariantCulture) + " (from " + LoadedFrom + ")";
	}
}
