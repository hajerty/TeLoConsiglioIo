using System.ComponentModel.DataAnnotations;
using TeLoConsiglio.Domain.Entities;

namespace TeLoConsiglio.Api.Dtos;

public record ActCreateDto(
    [Required] ActType Tipo,
    [Required] string Titolo,
    [Required] string Oggetto,
    string? ContextNotes,
    Guid? ParentActId,
    string? BodyMd,
    List<string>? ReferenceUrls,
    string? ReferenceNotesMd
);

public record ActUpdateDto(
    string Titolo,
    string Oggetto,
    string? ContextNotes,
    string BodyMd,
    ActStatus Status,
    List<string>? ReferenceUrls,
    string? ReferenceNotesMd
);

public record ActListDto(Guid Id, ActType Tipo, string Titolo, string Oggetto, ActStatus Status, DateTime CreatedAt, DateTime UpdatedAt);

public record ActDetailDto(
    Guid Id,
    ActType Tipo,
    string Titolo,
    string Oggetto,
    string? ContextNotes,
    string BodyMd,
    ActStatus Status,
    Guid? ParentActId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<string> ReferenceUrls,
    string? ReferenceNotesMd,
    List<ActRevisionDto> Revisions,
    List<LegalReferenceDto> LegalReferences
);

public record ActRevisionDto(Guid Id, string BodyMd, DateTime CreatedAt, string AuthorId);
public record LegalReferenceDto(Guid Id, string Citation, string Description, bool Inserted, DateTime? ConfirmedAt);
public record ActAttachmentDto(Guid Id, string OriginalName, string ContentType, long SizeBytes, DateTime CreatedAt);

public record GenerateDraftRequest(string? AdditionalInstructions);
public record GenerateDraftResponse(string BodyMd);

public record SuggestLegalRefsRequest(string Text);
public record SuggestedLegalRef(string Citation, string Description);
public record SuggestLegalRefsResponse(List<SuggestedLegalRef> References);

public record InsertLegalRefsRequest(List<Guid> ReferenceIds, string Mode); // Mode: "append" | "placeholder"

public record CreateLegalRefRequest([Required] string Citation, string? Description);
