using ProbablyTogether.Systems;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PingOffline : PingBotBase
{
	private int cash0;

	private int items0;

	private int lines0;

	public override string Name => "ping-offline";

	public override BotRole Role => BotRole.Any;

	public override float TimeoutSeconds => 600f;

	protected override void Build()
	{
		Check("offline", () => !Net.IsOnline && Net.Role == ProbablyTogether.Role.Off, () => "role " + Net.Role);
		WaitFor("menu", 180f, () => BotProbes.Scene == "EmporiumMenu" && (Object)(object)NewGameData.Instance != (Object)null, () => "scene " + BotProbes.Scene);
		WaitFor("key-row", 30f, () => GameOptionRegistry.Find(ModHelper.GameOptionId("bellun_probably_together", "ping")) != null, () => "Options > Key binds row '" + ModHelper.GameOptionId("bellun_probably_together", "ping") + "', key " + ((object)Pointing.PointKey).ToString() + ", chat key " + ((object)Chat.ChatKey).ToString());
		Delay("menu-settle", 3f);
		Do("new-run", delegate
		{
			NewGameData instance = NewGameData.Instance;
			instance.saveFile = null;
			instance.isLoadFromMainMenu = false;
			instance.saveSlot = -1;
			instance.startType = (NewGameData.StartType)1;
			instance.startScenarioId = "";
			instance.skipIntro = true;
			instance.disableTutorialHint = true;
			instance.difficultyId = "normal";
			EmporiumEntry.sceneLoadStartTime = Time.realtimeSinceStartup;
			SceneManager.LoadScene("InventoryScene", (LoadSceneMode)0);
			return "new WATER_MERCHANT run (offline), loading InventoryScene";
		});
		WaitFor("shop", 240f, () => Items.Ready, () => "scene " + BotProbes.Scene);
		Delay("shop-settle", 3f);
		KeysStep("keys-free");
		Do("raycast-survey", () => PingBotBase.RaycastSurvey(out var _, out var _));
		Do("state", delegate
		{
			cash0 = Game.Store.playerCash;
			items0 = Ids.All().Count;
			lines0 = Chat.Lines.Count;
			return "role " + Net.Role.ToString() + ", cash " + cash0 + ", " + items0 + " items, " + lines0 + " chat line(s)";
		});
		Step("point", 0f, delegate(BotStep st)
		{
			string how;
			long id = PingBotBase.PickItem(out how);
			string text = Pointing.PointAt(id, PingBotBase.CenterOf(id));
			string text2 = Pointing.PointAt(0L, PingBotBase.SpotAt);
			st.Detail = "item " + id + " (" + how + ") -> " + (text ?? "SENT") + "; spot -> " + (text2 ?? "SENT");
			return (text == "offline" && text2 == "offline") ? StepStatus.Pass : StepStatus.Fail;
		});
		Delay("ticks", 2f);
		Check("nothing", () => Pointing.Sent == 0 && Pointing.Received == 0 && PointMarks.Made == 0 && PointMarks.Marks.Count == 0 && Chat.Lines.Count == lines0, () => "sent " + Pointing.Sent + ", taken " + Pointing.Received + ", markers made " + PointMarks.Made + ", chat lines " + lines0 + " -> " + Chat.Lines.Count);
		Check("shop-unchanged", () => !Net.IsOnline && Game.Store.playerCash == cash0 && Ids.All().Count == items0, () => "role " + Net.Role.ToString() + ", cash " + cash0 + " -> " + Game.Store.playerCash + ", items " + items0 + " -> " + Ids.All().Count);
	}
}
