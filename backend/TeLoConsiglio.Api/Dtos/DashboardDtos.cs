using TeLoConsiglio.Domain.Entities;

namespace TeLoConsiglio.Api.Dtos;

public record DashboardDocumentDto(
    Guid Id,
    string OriginalName,
    DocumentType Type,
    DateTime CreatedAt,
    bool HasSummary);

public record DashboardNextSittingDto(
    Guid Id,
    DateTime Data,
    string Luogo,
    string Titolo,
    int AgendaCount,
    int DaAnalizzareCount);

public record DashboardAgendaItemDto(
    Guid SittingId,
    DateTime SittingData,
    string SittingTitolo,
    Guid AgendaItemId,
    string Descrizione,
    Guid? DocumentId);

public record DashboardCountersDto(
    int ActsBozza,
    int UpcomingSittings,
    int PendingInvitations);

public record DashboardDto(
    List<DashboardDocumentDto> RecentDocuments,
    DashboardNextSittingDto? NextSitting,
    List<DashboardAgendaItemDto> DocumentsToAnalyze,
    DashboardCountersDto Counters);
