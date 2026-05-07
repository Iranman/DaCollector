# API Reference

DaCollector exposes versioned API endpoints under:

```text
/api/v3
```

Most endpoints require authentication. Admin-only endpoints require an admin user.

## Status

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/v3/DaCollectorStatus` | Overall DaCollector readiness. |
| `GET` | `/api/v3/DaCollectorStatus/Providers` | Provider readiness without secrets. |
| `GET` | `/api/v3/DaCollectorStatus/Plex` | Plex readiness without exposing the token. |

## Collection Builders

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/v3/CollectionBuilder` | List supported builders. |
| `POST` | `/api/v3/CollectionBuilder/Preview` | Preview one rule. |

## Managed Collections

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/v3/ManagedCollection` | List collections. |
| `GET` | `/api/v3/ManagedCollection/{collectionID}` | Get one collection. |
| `POST` | `/api/v3/ManagedCollection` | Create one collection. |
| `PUT` | `/api/v3/ManagedCollection/{collectionID}` | Replace one collection. |
| `DELETE` | `/api/v3/ManagedCollection/{collectionID}` | Delete one collection. |
| `POST` | `/api/v3/ManagedCollection/Preview` | Preview an unsaved collection. |
| `POST` | `/api/v3/ManagedCollection/{collectionID}/Preview` | Preview a saved collection. |
| `POST` | `/api/v3/ManagedCollection/Sync` | Sync all enabled collections. |
| `POST` | `/api/v3/ManagedCollection/{collectionID}/Sync` | Sync one collection. |

## Plex Target

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/v3/PlexTarget/Identity` | Test configured Plex identity. |
| `POST` | `/api/v3/PlexTarget/Identity` | Test supplied Plex identity. |
| `GET` | `/api/v3/PlexTarget/Library` | List configured Plex libraries. |
| `POST` | `/api/v3/PlexTarget/Library` | List supplied Plex libraries. |
| `GET` | `/api/v3/PlexTarget/Library/{sectionKey}/Item` | List configured Plex library items. |
| `POST` | `/api/v3/PlexTarget/Library/{sectionKey}/Item` | List supplied Plex library items. |
| `POST` | `/api/v3/PlexTarget/Library/{sectionKey}/Match` | Match a collection definition. |
| `POST` | `/api/v3/PlexTarget/Library/{sectionKey}/Apply` | Apply a collection definition. |

## Duplicates

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/v3/Duplicates/Exact/Summary` | Summarize exact duplicates. |
| `GET` | `/api/v3/Duplicates/Exact` | List exact duplicate sets. |
| `GET` | `/api/v3/Duplicates/Exact/CleanupPlan` | List cleanup candidates. |
| `DELETE` | `/api/v3/Duplicates/Exact/Location/{locationID}` | Dry-run or confirm one candidate delete. |

Use `confirm=false` for dry-run deletes and `confirm=true` for confirmed deletes.
