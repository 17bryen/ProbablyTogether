namespace Concentus.Common.CPlusPlus;

public class BoxedValue<T>
{
	public T Val;

	public BoxedValue(T v = default(T))
	{
		Val = v;
	}

	public override string ToString()
	{
		if (Val != null)
		{
			return Val.ToString();
		}
		return "null";
	}
}
