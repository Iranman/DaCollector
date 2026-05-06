using Microsoft.AspNetCore.Authentication;

namespace DaCollector.Server.API.Authentication;

public class CustomAuthOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "DaCollector";
    public string Scheme => DefaultScheme;
}
