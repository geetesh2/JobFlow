namespace JobFlow.Worker.Cancellation;

public interface IJobCancellationService
{
    CancellationToken Register(Guid jobId);
    bool Cancel(Guid jobId);
    void Unregister(Guid jobId);
}
