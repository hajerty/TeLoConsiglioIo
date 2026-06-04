namespace TeLoConsiglio.Api.Dtos;

public record PoliticalProfileDto(string LineaPoliticaMd, List<string> PuntiEvidenza);
public record ElectoralProgramDto(Guid Id, string OriginalName, DateTime UploadedAt);
