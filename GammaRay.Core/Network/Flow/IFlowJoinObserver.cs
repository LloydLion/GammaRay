namespace GammaRay.Core.Network.Flow
{
	public interface IFlowJoinObserver
	{
		public void NotifyDataFromAToB(ReadOnlyMemory<byte> data);

		public void NotifyDataFromBToA(ReadOnlyMemory<byte> data);

		public void NotifyEndOfJoin();
	}
}
