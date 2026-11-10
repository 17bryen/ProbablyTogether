using System.Collections.Generic;

namespace ProbablyTogether;

public sealed class MsgSecState
{
	public bool idle;

	public string fp = "";

	public int day;

	public List<string> crimeTypes = new List<string>();

	public List<int> crimeAmount = new List<int>();

	public List<int> crimeAmountTotal = new List<int>();

	public int evidence;

	public int secState;

	public int[] trackers = new int[8];

	public int wrongful;

	public Dictionary<string, int> wanted = new Dictionary<string, int>();

	public int augPresence;

	public bool augIntroduced;

	public bool augEnded;

	public int nextAugDay;

	public bool shootingUnlocked;

	public bool vandalized;

	public int daySinceVandalism;

	public int inspLossDay;

	public int inspLossValue;

	public int theftDay;

	public int theftValue;

	public bool alarmActive;

	public int cartelCount;

	public int cartelPurity;

	public int cartelStage;

	public bool cartelExited;

	public bool poster;

	public List<string> arrested = new List<string>();

	public int daySinceInspection;

	public int dayUntilInspection;

	public bool inspectionSeeded;

	public bool arresting;

	public bool shootout;

	public bool shot;

	public bool reportedAug;

	public bool monologue;

	public bool glassClosed;

	public int bellFlag;
}
