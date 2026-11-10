namespace ProbablyTogether.UI;

public sealed class MsgCursor
{
	public int p;

	public float x;

	public float y;

	public string v;

	public long h;

	public long d;

	public int n;

	public int g;

	public int c;

	/// orientation (0-3) and flip of the dragged item's shape
	public int o;

	public bool f;

	/// hover target while dragging: inventory, cell, and whether the drop would be accepted (null when over nothing)
	public ItemRef t;

	public int tx;

	public int ty;

	public bool ok;

	/// held item centre: in game-area coordinates (cx, cy) and, over an inventory, local to that inventory (lx, ly)
	public float cx;

	public float cy;

	public float lx;

	public float ly;
}
