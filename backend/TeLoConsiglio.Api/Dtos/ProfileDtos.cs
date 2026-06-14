using TeLoConsiglio.Domain.Entities;

namespace TeLoConsiglio.Api.Dtos;

public record PoliticalProfileDto(
    string LineaPoliticaMd,
    List<string> ArgomentiForti,
    List<string> TemiInteresse,
    LineaPoliticaSource LineaPoliticaSource
);

public record PoliticalProfileUpdateDto(
    string? LineaPoliticaMd,
    List<string>? ArgomentiForti,
    List<string>? TemiInteresse
);

public record ElectoralProgramDto(Guid Id, string OriginalName, DateTime UploadedAt);

public record NotificationPreferencesDto(bool EmailNotificationsEnabled);

public record NotificationPreferencesUpdateDto(bool EmailNotificationsEnabled);
