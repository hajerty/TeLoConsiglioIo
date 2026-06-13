using System.ComponentModel.DataAnnotations;

namespace TeLoConsiglio.Api.Dtos;

public record DocumentSuggestionDto(
    Guid AgendaItemId,
    Guid SittingId,
    DateTime SittingData,
    string SittingTitolo,
    string Descrizione,
    Guid DocumentId,
    string DocumentName,
    int Score);

public record CloneDocumentDto([Required] Guid SourceAgendaItemId);
