namespace Backend.Domains;

public static class ProfileType
{
    public const string Proprietaire = "proprietaire";
    public const string Locataire = "locataire";
    public const string Entreprise = "entreprise";

    public static readonly IReadOnlyList<string> All =
    [
        Proprietaire,
        Locataire,
        Entreprise,
    ];
}