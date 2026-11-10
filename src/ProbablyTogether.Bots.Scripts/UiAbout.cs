using System.Globalization;
using System.Linq;
using ProbablyTogether.UI;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProbablyTogether.Bots.Scripts;

public sealed class UiAbout : BotScript
{
	public override string Name => "ui-about";

	public override float TimeoutSeconds => 200f;

	protected override void Build()
	{
		Check("plain-copy", () => Args.Join == null, () => "--br-join " + (Args.Join ?? "absent"));
		WaitFor("menu", 120f, () => Ui.AtMenu && (Object)(object)MainMenuUIController.Instance != (Object)null, () => "scene " + BotProbes.Scene);
		WaitFor("button", 30f, () => (Object)(object)JoinMenu.Button != (Object)null, () => "fallback " + JoinMenu.UsingFallbackButton);
		Do("open-join", delegate
		{
			((UnityEvent)JoinMenu.Button.GetComponentInChildren<Button>(true).onClick).Invoke();
			return "clicked Join server";
		});
		WaitFor("join-window", 10f, () => JoinMenu.DialogOpen, () => "open " + JoinMenu.DialogOpen);
		Check("about-button", delegate
		{
			CustomUIWindow obj = Win("humangenome_accomplice.join");
			return (Object)(object)((obj == null) ? null : obj.Get("about")?.button) != (Object)null;
		}, delegate
		{
			CustomUIWindow obj = Win("humangenome_accomplice.join");
			return "Join window buttons: about " + (((obj != null) ? obj.Get("about") : null) != null);
		});
		Do("click-about", delegate
		{
			((UnityEvent)Win("humangenome_accomplice.join").Get("about").button.onClick).Invoke();
			return "clicked About";
		});
		WaitFor("about-open", 10f, () => JoinMenu.AboutOpen, () => "about open " + JoinMenu.AboutOpen);
		Delay("layout", 1f);
		Check("about-title", () => Title("humangenome_accomplice.about") == "ProbablyTogether".ToUpperInvariant(), () => "title '" + Title("humangenome_accomplice.about") + "', Brand.Name 'ProbablyTogether'");
		Check("about-text", delegate
		{
			string[] source = Brand.AboutParagraphs(Chat.ChatKeyName);
			CustomUIWindow w = Win("humangenome_accomplice.about");
			return source.Select(delegate(string p, int i)
			{
				CustomUIWindow obj = w;
				object obj2;
				if (obj == null)
				{
					obj2 = null;
				}
				else
				{
					CustomUIElement obj3 = obj.Get("p" + i);
					obj2 = ((obj3 != null) ? obj3.GetText() : null);
				}
				return (string?)obj2 == p;
			}).All((bool x) => x);
		}, delegate
		{
			CustomUIWindow w = Win("humangenome_accomplice.about");
			return string.Join(" / ", Enumerable.Range(0, 4).Select(delegate(int i)
			{
				CustomUIWindow obj = w;
				CustomUIElement val = ((obj != null) ? obj.Get("p" + i) : null);
				Rect val2 = (Rect)(((Object)(object)val?.text != (Object)null) ? ((TMP_Text)val.text).rectTransform.rect : default(Rect));
				return "p" + i + " " + val2.width.ToString("F0", CultureInfo.InvariantCulture) + "x" + val2.height.ToString("F0", CultureInfo.InvariantCulture) + " wrap " + ((Object)(object)val?.text != (Object)null && ((TMP_Text)val.text).enableWordWrapping) + " '" + (((val != null) ? val.GetText() : null) ?? "missing") + "'";
			}));
		});
		Check("no-workshop", delegate
		{
			bool hasWorkshopPage = Brand.HasWorkshopPage;
			CustomUIWindow obj = Win("humangenome_accomplice.about");
			return hasWorkshopPage == (((obj != null) ? obj.Get("workshop") : null) != null);
		}, delegate
		{
			CustomUIWindow obj = Win("humangenome_accomplice.about");
			return "Workshop id '3832445441', button " + (((obj != null) ? obj.Get("workshop") : null) != null);
		});
		Do("back", delegate
		{
			((UnityEvent)Win("humangenome_accomplice.about").Get("back").button.onClick).Invoke();
			return "clicked Back";
		});
		WaitFor("back-to-join", 10f, () => !JoinMenu.AboutOpen && JoinMenu.DialogOpen, () => "about " + JoinMenu.AboutOpen + ", join " + JoinMenu.DialogOpen);
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

	private static string Title(string id)
	{
		CustomUIWindow val = Win(id);
		if (val == null)
		{
			return "";
		}
		Transform val2 = (((Object)(object)val.root != (Object)null) ? val.root.transform : null);
		if ((Object)(object)val2 == (Object)null)
		{
			return "";
		}
		TextMeshProUGUI[] componentsInChildren = ((Component)val2).GetComponentsInChildren<TextMeshProUGUI>(true);
		foreach (TextMeshProUGUI val3 in componentsInChildren)
		{
			if (((TMP_Text)val3).text == "ProbablyTogether".ToUpperInvariant())
			{
				return ((TMP_Text)val3).text;
			}
		}
		return "(title not found)";
	}
}
