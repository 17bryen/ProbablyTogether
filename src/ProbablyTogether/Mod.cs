namespace ProbablyTogether;

public sealed class Mod : IMod
{
	public const string Id = "bellun_probably_together";

	public const string Version = "0.9.13";

	public const string Name = "Probably Together";

	/// The upstream mod; the two cannot run together.
	public const string UpstreamId = "humangenome_" + "accomplice";

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
