using System.Linq;
using ProbablyTogether.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class PicsMenu : BotScript
{
	public override string Name => "pics-menu";

	public override float TimeoutSeconds => 300f;

	protected override void Build()
	{
		WaitFor("menu", 150f, () => Ui.AtMenu && (Object)(object)MainMenuUIController.Instance != (Object)null, () => "scene " + BotProbes.Scene);
		WaitFor("button", 30f, () => (Object)(object)JoinMenu.Button != (Object)null, () => "fallback " + JoinMenu.UsingFallbackButton);
		Delay("menu-settle", 4f);
		Do("pic-menu", () => "PIC the main menu with the Join server button");
		Delay("pic-menu-hold", 9f);
		Do("open-join", delegate
		{
			((UnityEvent)JoinMenu.Button.GetComponentInChildren<Button>(true).onClick).Invoke();
			return "clicked Join server";
		});
		WaitFor("join-window", 10f, () => JoinMenu.DialogOpen, () => "open " + JoinMenu.DialogOpen);
		Delay("join-settle", 1f);
		Do("pic-join", () => "PIC the Join window");
		Delay("pic-join-hold", 9f);
		Do("click-about", delegate
		{
			((UnityEvent)Win("humangenome_accomplice.join").Get("about").button.onClick).Invoke();
			return "clicked About";
		});
		WaitFor("about-open", 10f, () => JoinMenu.AboutOpen, () => "about open " + JoinMenu.AboutOpen);
		Delay("about-settle", 1f);
		Do("pic-about", () => "PIC the About window");
		Delay("pic-about-hold", 9f);
		Do("back", delegate
		{
			((UnityEvent)Win("humangenome_accomplice.about").Get("back").button.onClick).Invoke();
			return "clicked Back";
		});
		WaitFor("back-to-join", 10f, () => !JoinMenu.AboutOpen && JoinMenu.DialogOpen, () => "about " + JoinMenu.AboutOpen + ", join " + JoinMenu.DialogOpen);
		Do("click-find", delegate
		{
			((UnityEvent)Win("humangenome_accomplice.join").Get("find").button.onClick).Invoke();
			return "clicked Find a server";
		});
		WaitFor("list-open", 10f, () => ServerList.IsOpen, () => "list open " + ServerList.IsOpen);
		WaitFor("list-answer", 20f, () => ServerList.State != "loading", () => "state " + ServerList.State);
		Delay("list-settle", 1f);
		Do("pic-servers", () => "PIC the server list: " + ServerList.State + ", " + ServerList.Rows.Count + " row(s), " + ServerList.Rows.Count((ServerList.Entry r) => r.Why != null) + " dimmed");
		Delay("pic-servers-hold", 9f);
		Do("list-back", delegate
		{
			((UnityEvent)Win("humangenome_accomplice.servers").Get("back").button.onClick).Invoke();
			return "clicked Back";
		});
		WaitFor("back-to-join-2", 10f, () => !ServerList.IsOpen && JoinMenu.DialogOpen, () => "list " + ServerList.IsOpen + ", join " + JoinMenu.DialogOpen);
		Do("close", delegate
		{
			JoinMenu.CloseDialog();
			return "closed";
		});
	}

	private static CustomUIWindow Win(string id)
	{
		CustomUIManager instance = CustomUIManager.Instance;
		if (instance == null)
		{
			return null;
		}
		return instance.GetWindow(id);
	}
}
