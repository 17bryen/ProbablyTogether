namespace ProbablyTogether;

public sealed class Mod : IMod
{
	public const string Id = "humangenome_accomplice";

	public const string Version = "0.2.8";

	public void Init(ModManifest manifest)
	{
		Boot.Init(manifest);
	}

	public void OnEnable()
	{
		Boot.Enable();
	}

	public void OnDisable()
	{
		Boot.Disable();
	}
}
