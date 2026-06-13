namespace TeLoConsiglio.Domain.Entities;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Capogruppo = "Capogruppo";
    public const string Vice = "Vice";
    public const string Consigliere = "Consigliere";

    public static readonly string[] All = { Admin, Capogruppo, Vice, Consigliere };
}
