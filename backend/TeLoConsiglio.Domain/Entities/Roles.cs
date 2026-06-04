namespace TeLoConsiglio.Domain.Entities;

public static class Roles
{
    public const string Admin = "Admin";
    public const string CapogruppoConsiliare = "CapogruppoConsiliare";
    public const string Consigliere = "Consigliere";

    public static readonly string[] All = { Admin, CapogruppoConsiliare, Consigliere };
}
