namespace GammaRay.Core.Settings;

public interface IRawSettingsProvider<TObject> where TObject : class
{
	public bool IsInitialized { get; }


	public TObject Get();
}
