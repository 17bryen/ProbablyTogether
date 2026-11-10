using ProbablyTogether.UI;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

public abstract class PicsFeatBase : PicsBase
{
	protected static readonly Vector2 SpotAt = new Vector2(0.36f, 0.56f);

	protected static EmporiumEntry E => EmporiumEntry.Instance;

	protected static GameItem StorageItem(long not = 0L)
	{
		GameItem val = null;
		EmporiumEntry e = E;
		if (((e != null) ? e.invElement : null) == null)
		{
			return null;
		}
		foreach (GameItem childItem in ((GameInventory)E.invElement).childItems)
		{
			if (childItem != null && !Items.IsGone(childItem) && childItem.uniqueId > 0 && childItem.uniqueId != not && !Ids.IsStandIn(childItem) && GeneralHelper.IsItemOwned(childItem) && childItem.MayRemove() && childItem.MaxNumRemove() >= 1 && childItem.contentWindow == null && !ItemPinHelper.IsPinned(childItem) && PointMarks.ItemArea(childItem, out var area) && !(area.width < 0.01f) && !(area.height < 0.01f) && (val == null || childItem.uniqueId < val.uniqueId))
			{
				val = childItem;
			}
		}
		return val;
	}

	protected static Vector2 CentreOf(GameItem it)
	{
		if (!PointMarks.ItemArea(it, out var area))
		{
			return new Vector2(0.5f, 0.5f);
		}
		return area.center;
	}
}
