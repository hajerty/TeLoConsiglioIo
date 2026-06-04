using TeLoConsiglio.Domain.Entities;

namespace TeLoConsiglio.Api.Dtos;

public record DocumentDto(Guid Id, string OriginalName, DocumentType Type, DateTime CreatedAt, bool HasSummary);
public record DocumentDetailDto(Guid Id, string OriginalName, DocumentType Type, DateTime CreatedAt, string ExtractedText, DocumentSummaryDto? Summary);
public record DocumentSummaryDto(string SummaryMd, List<string> KeyPoints, List<string> Criticities, DateTime GeneratedAt);
