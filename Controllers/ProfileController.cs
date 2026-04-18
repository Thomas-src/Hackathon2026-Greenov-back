using Backend.Domains;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/profiles")]
public class ProfileController : ControllerBase
{
    [HttpGet]
    public IActionResult GetProfiles()
    {
        var profiles = ProfileType.All.Select(p => new
        {
            Id = p,
            Label = ToLabel(p),
            Image = ToImage(p)
        });

        return Ok(profiles);
    }

    private static string ToImage(string profile) => profile switch
    {
        ProfileType.Proprietaire => "house",
        ProfileType.Locataire => "flat",
        ProfileType.Entreprise => "company",
        _ => profile,
    };

    private static string ToLabel(string profile) => profile switch
    {
        ProfileType.Proprietaire => "Propriétaire",
        ProfileType.Locataire => "Locataire",
        ProfileType.Entreprise => "Entreprise",
        _ => profile,
    };
}