# Collection Management

DaCollector managed collections are saved collection definitions that can be previewed locally and then applied to Plex. The design is API-first so the Web UI, scripts, and external tools can all work against the same server state.

## Supported Builders

Use `GET /api/v3/CollectionBuilder` to list the builders available in the running server.

## Web UI

Open the managed collections page:

```text
http://127.0.0.1:38111/webui/dacollector-collections.html
```

Use it to create collection definitions, edit builder rules, preview output, run sync dry runs, and apply saved collections to the configured Plex target.

Current builder names include:

| Provider | Builders |
| --- | --- |
| TMDB | `tmdb_movie`, `tmdb_show`, `tmdb_collection`, `tmdb_popular`, `tmdb_top_rated`, `tmdb_trending_daily`, `tmdb_trending_weekly`, `tmdb_discover`, `tmdb_now_playing`, `tmdb_upcoming`, `tmdb_airing_today`, `tmdb_on_the_air` |
| IMDb | `imdb_id`, `imdb_list`, `imdb_chart`, `imdb_search` |
| TVDB | `tvdb_movie`, `tvdb_show`, `tvdb_list` |

## Preview a Rule

Preview a single rule without saving a collection:

```http
POST /api/v3/CollectionBuilder/Preview
```

The request body is a collection rule. The response lists the local output DaCollector can resolve for that rule.

## Manage Saved Collections

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/v3/ManagedCollection` | List saved collection definitions. |
| `GET` | `/api/v3/ManagedCollection/{collectionID}` | Read one collection definition. |
| `POST` | `/api/v3/ManagedCollection` | Create a collection definition. |
| `PUT` | `/api/v3/ManagedCollection/{collectionID}` | Replace a collection definition. |
| `DELETE` | `/api/v3/ManagedCollection/{collectionID}` | Delete a collection definition. |
| `POST` | `/api/v3/ManagedCollection/Preview` | Preview an unsaved collection definition. |
| `POST` | `/api/v3/ManagedCollection/{collectionID}/Preview` | Preview a saved collection definition. |

Create, update, delete, and sync actions require an admin user.

## Sync Collections

Sync calls evaluate saved definitions and can either preview or apply the result.

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `POST` | `/api/v3/ManagedCollection/Sync?apply=false` | Evaluate all enabled collections without changing Plex. |
| `POST` | `/api/v3/ManagedCollection/Sync?apply=true` | Apply all enabled collection changes to Plex. |
| `POST` | `/api/v3/ManagedCollection/{collectionID}/Sync?apply=false` | Evaluate one collection. |
| `POST` | `/api/v3/ManagedCollection/{collectionID}/Sync?apply=true` | Apply one collection to Plex. |

Use `apply=false` before the first real sync. It lets you inspect matched items, missing items, warnings, and intended changes.

## Sync Modes

Collection definitions can be evaluated in preview mode or applied to the target media server. When using a sync mode that removes membership, DaCollector compares the target output to the existing Plex collection and removes items that no longer match.

## Scheduling

Collection scheduling is controlled by collection manager settings:

| Setting | Purpose |
| --- | --- |
| `ScheduledSyncEnabled` | Enables scheduled collection evaluation. |
| `SyncIntervalMinutes` | Minimum interval between scheduled sync runs. |

Keep scheduled sync disabled until your collection previews match what you expect.
