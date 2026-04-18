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
        var profiles = Enum.GetValues<Profile>()
            .Select(p => new
            {
                Id = p.ToString(),
                Label = ToLabel(p),
                Image = ToImage(p)
            });

        return Ok(profiles);
    }

    public static string ToImage(Profile profile) => profile switch
    {
        Profile.Proprietaire => "house",
        Profile.Locataire => "flat",
        Profile.Entreprise => "company",
        _ => "unknown"
    };

    public static string ToLabel(Profile profile) => profile switch
    {
        Profile.Proprietaire => "Propriétaire",
        Profile.Locataire => "Locataire",
        Profile.Entreprise => "Entreprise",
        _ => profile.ToString()
    };
}