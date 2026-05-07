# Duplicate Management

DaCollector can identify exact duplicate file locations by hash and file size. The duplicate tools are designed around review first, deletion second.

## Web UI

Open the duplicate review page:

```text
http://127.0.0.1:38111/webui/dacollector-duplicates.html
```

Use it to load the exact duplicate summary, inspect cleanup candidates, dry-run delete actions, and then confirm deletes when the candidate is correct.

## Exact Duplicate API

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/v3/Duplicates/Exact/Summary` | Count exact duplicate sets and cleanup candidates. |
| `GET` | `/api/v3/Duplicates/Exact` | List exact duplicate sets. |
| `GET` | `/api/v3/Duplicates/Exact/CleanupPlan` | List recommended remove candidates. |
| `DELETE` | `/api/v3/Duplicates/Exact/Location/{locationID}?confirm=false` | Dry-run removal for one candidate. |
| `DELETE` | `/api/v3/Duplicates/Exact/Location/{locationID}?confirm=true` | Remove one confirmed candidate. |

Delete calls require an admin user.

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
