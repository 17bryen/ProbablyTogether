using System;

namespace ProbablyTogether;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class SystemInitAttribute : Attribute
{
	public int Order { get; set; }
}
