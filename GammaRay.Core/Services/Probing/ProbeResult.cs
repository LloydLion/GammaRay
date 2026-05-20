namespace GammaRay.Core.Services.Probing;

public class ProbeResult(ProbeResult.CommunicationStatus l7Status, ProbeResult.CommunicationStatus l6Status, TimeSpan probeDuration)
{
	public ProbeResult(CommunicationStatus l7Status, TimeSpan probeDuration)
		: this(l7Status, CommunicationStatus.Skipped, probeDuration) { }


	public CommunicationStatus L7Status { get; } = l7Status;

	public CommunicationStatus L6Status { get; } = l6Status;

	public string? FailureComment { get; set; }

	public TimeSpan ProbeDuration { get; } = probeDuration;


	public override string ToString()
	{
		return $"ProbeResult (L7={L7Status}/L6={L6Status} in {ProbeDuration.Milliseconds}ms) //{FailureComment}";
	}


	public enum CommunicationStatus
	{
		/// <summary>
		/// Probe completed successfully
		/// </summary>
		Success,

		/// <summary>
		/// Remote server actively refused communication
		/// </summary>
		RemoteServerBan,

		/// <summary>
		/// Unexcepted data from remote server
		/// </summary>s
		UnexceptedData,

		/// <summary>
		/// Timeout while waiting response at any stage of probing process except L6 dial up
		/// </summary>
		Timeout,

		/// <summary>
		/// Other data flow failure at any stage of probing process except L6 dial up
		/// </summary>
		FlowFailure,

		/// <summary>
		/// Communication was not attempted due to failure at previous stage of probing process or configuration
		/// </summary>
		Skipped
	}
}
