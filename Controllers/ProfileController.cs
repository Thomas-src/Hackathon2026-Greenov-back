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
        });

        return Ok(profiles);
    }

    private static string ToLabel(string profile) => profile switch
    {
        ProfileType.Proprietaire => "Propriétaire",
        ProfileType.Locataire => "Locataire",
        ProfileType.Entreprise => "Entreprise",
        _ => profile,
    };
}