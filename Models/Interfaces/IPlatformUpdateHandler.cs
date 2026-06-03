namespace CashBeacon;

public interface IPlatformUpdateHandler<TUpdate>
{
    Task HandleAsync(TUpdate update, CancellationToken ct = default);
}