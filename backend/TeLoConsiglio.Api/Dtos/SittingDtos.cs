using System.ComponentModel.DataAnnotations;
using TeLoConsiglio.Domain.Entities;

namespace TeLoConsiglio.Api.Dtos;

public record SittingCreateDto([Required] DateTime Data, [Required] string Luogo, [Required] string Titolo);
public record SittingListDto(Guid Id, DateTime Data, string Luogo, string Titolo);
public record SittingDetailDto(Guid Id, DateTime Data, string Luogo, string Titolo, List<AgendaItemDto> Items);

public record AgendaItemCreateDto(int Ordine, [Required] string Descrizione, AgendaDecision Decisione, string? Motivazione, Guid? ActId, List<string>? AssignedUserIds);
public record AgendaItemUpdateDto(int Ordine, string Descrizione, AgendaDecision Decisione, string? Motivazione, Guid? ActId, List<string>? AssignedUserIds);
public record AgendaItemDto(Guid Id, int Ordine, string Descrizione, AgendaDecision Decisione, string Motivazione, Guid? ActId, List<AssignedUserDto> AssignedUsers);
public record AssignedUserDto(string UserId, string Email, string FullName);
