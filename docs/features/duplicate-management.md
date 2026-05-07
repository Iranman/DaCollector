# Duplicate Management

DaCollector separates exact duplicate files from duplicate media entries. Exact duplicates are file cleanup candidates based on stored hash and size. Media duplicates are Plex library entries that may point to the same movie or show based on provider IDs, title/year, rating keys, or matching file path hashes.

The duplicate tools are designed around review first, deletion second.

## Duplicate Types

| Type | Source | Signals | Delete behavior |
| --- | --- | --- | --- |
| Exact file duplicates | DaCollector file database | File hash, file size, managed folder preference, path preference, file availability | Can dry-run and then delete one selected remove candidate. |
| Plex media duplicates | Plex target library | Provider ID, title/year, Plex rating key, file path hash | Read-only review. Delete or merge in Plex after inspection. |

## Web UI

Open the duplicate review page:

```text
http://127.0.0.1:38111/webui/dacollector-duplicates.html
```

The page has two modes selected by tabs at the top:

**Exact File Duplicates** — loads the cleanup plan for files tracked in the DaCollector database. Shows keep/remove candidates with folder, availability, and potential reclaim bytes. Supports dry-run and confirmed delete for individual remove candidates.

**Plex Media Duplicates** — enter a Plex library section key and click Load. Renders duplicate sets grouped by score, with match type, scoring reasons, Plex rating keys, provider IDs, title/year, and file paths. Safe-delete candidates (entries where all files appear in another entry in the same set) are highlighted with an amber border. No delete actions are available from this view — review and clean up in Plex directly.

## Exact Duplicate API

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/v3/Duplicates/Exact/Summary` | Count exact duplicate sets and cleanup candidates. |
| `GET` | `/api/v3/Duplicates/Exact` | List exact duplicate sets. |
| `GET` | `/api/v3/Duplicates/Exact/CleanupPlan` | List recommended remove candidates. |
| `DELETE` | `/api/v3/Duplicates/Exact/Location/{locationID}?confirm=false` | Dry-run removal for one candidate. |
| `DELETE` | `/api/v3/Duplicates/Exact/Location/{locationID}?confirm=true` | Remove one confirmed candidate. |

Delete calls require an admin user.

## Plex Media Duplicate API

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/v3/Duplicates/Media/Plex/Library/{sectionKey}` | List possible duplicate media entries from one Plex library section. |

The media duplicate response includes a score, scoring reasons, Plex rating keys, provider IDs, title/year data, file paths, and path hashes. DaCollector does not delete media duplicate entries from this endpoint.

## Useful Query Options

| Query option | Purpose |
| --- | --- |
| `includeIgnored` | Include ignored file locations in duplicate calculations. |
| `onlyAvailable` | Restrict results to currently available locations. |
| `preferredManagedFolderID` | Prefer keeping files from one managed folder. |
| `preferredPathContains` | Prefer keeping paths that contain the given text. |
| `page` | Page number for list endpoints. |
| `pageSize` | Page size, up to `1000`. |
| `deleteFile` | Delete the physical file during confirmed delete. Defaults to `true`. |
| `deleteEmptyFolders` | Remove empty parent folders after confirmed delete. Defaults to `true`. |

## Safe Workflow

1. Run `/api/v3/Duplicates/Exact/Summary`.
2. Review `/api/v3/Duplicates/Exact/CleanupPlan`.
3. For one candidate, call delete with `confirm=false`.
4. Confirm the response says the candidate is removable.
5. Call the same endpoint with `confirm=true`.
6. Re-run the cleanup plan.

Do not bulk-delete from outside DaCollector while it is running. Let the server update both the file system and its local database state.
