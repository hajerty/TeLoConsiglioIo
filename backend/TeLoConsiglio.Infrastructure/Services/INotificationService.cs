namespace TeLoConsiglio.Infrastructure.Services;

public interface INotificationService
{
    Task NotifyAssignedToAgendaAsync(string userId, Guid sittingId, Guid agendaItemId, CancellationToken ct = default);
    Task NotifyAgendaDecisionChangedAsync(Guid sittingId, Guid agendaItemId, string oldDecision, string newDecision, CancellationToken ct = default);
    Task NotifyNewDocumentOnAgendaAsync(Guid sittingId, Guid agendaItemId, string documentName, CancellationToken ct = default);
    Task NotifyActDepositedAsync(Guid actId, CancellationToken ct = default);
}
