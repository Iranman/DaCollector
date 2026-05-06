using System;

#nullable enable
namespace DaCollector.Server.Providers.TMDB;

public class TmdbApiKeyUnavailableException() : Exception("You need to provide an api key before using the TMDB provider!") { }
