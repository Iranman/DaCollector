# Plex Target

DaCollector can read a Plex server directly and use Plex as the target for managed collection updates.

## Required Plex Settings

| Setting | Environment variable | Example |
| --- | --- | --- |
| Plex base URL | `PLEX_TARGET_BASE_URL` | `http://127.0.0.1:32400` |
| Plex token | `PLEX_TARGET_TOKEN` | `xxxxxxxxxxxxxxxxxxxx` |
| Library section key | `PLEX_TARGET_SECTION_KEY` | `1` |

The section key is the Plex library identifier, not the display name. Use the library API below to list available keys.

## Test Plex Connectivity

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/v3/PlexTarget/Identity` | Test the configured Plex target. |
| `POST` | `/api/v3/PlexTarget/Identity` | Test a Plex URL without saving settings. |
| `GET` | `/api/v3/PlexTarget/Library` | List configured Plex libraries. |
| `POST` | `/api/v3/PlexTarget/Library` | List libraries from a supplied URL and token. |

Example body for the `POST` test endpoints:

```json
{
  "baseUrl": "http://127.0.0.1:32400",
  "token": "<plex-token>"
}
```

## Read Library Items

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/v3/PlexTarget/Library/{sectionKey}/Item` | List items from the configured Plex target. |
| `POST` | `/api/v3/PlexTarget/Library/{sectionKey}/Item` | List items from a supplied URL and token. |

DaCollector reads Plex GUIDs and maps `imdb`, `tmdb`, and `tvdb` identifiers to local collection preview output.

## Match and Apply Collections

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `POST` | `/api/v3/PlexTarget/Library/{sectionKey}/Match` | Match an unsaved collection definition against a Plex library. |
| `POST` | `/api/v3/PlexTarget/Library/{sectionKey}/Apply` | Apply an unsaved collection definition to Plex. |

Apply calls require an admin user. Use collection preview and match calls first so missing Plex items are visible before DaCollector changes collection membership.

## Common Issues

| Symptom | Check |
| --- | --- |
| Plex identity works but libraries fail | The token is missing or cannot read libraries. |
| Libraries list but no items match | Confirm Plex items have TMDB, IMDb, or TVDB GUIDs. |
| Collection sync previews only | Confirm sync was called with `apply=true`. |
| LAN clients cannot open DaCollector | Open Windows firewall for TCP `38111`. |
