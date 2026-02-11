namespace GammaRay.Core.Utils;

public interface IDriverRegistry<TDriver>
{
	public TDriver ProvideDriver(string name);
}
