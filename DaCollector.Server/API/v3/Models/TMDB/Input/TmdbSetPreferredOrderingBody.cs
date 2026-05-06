using System.ComponentModel.DataAnnotations;
using DaCollector.Server.API.v3.Controllers;

#nullable enable
namespace DaCollector.Server.API.v3.Models.TMDB.Input;

public class TmdbSetPreferredOrderingBody
{
    /// <summary>
    /// The new preferred ordering to use.
    /// </summary>
    [Required]
    [RegularExpression(TmdbController.AlternateOrderingIdRegex)]
    public string AlternateOrderingID { get; set; } = string.Empty;
}
