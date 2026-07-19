SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename = 'FolderCoverPhotos';
SELECT f."Id", f."Name", COUNT(p."Id") AS photos
FROM "Folders" f
LEFT JOIN "Photos" p ON p."FolderId" = f."Id"
WHERE f."ParentId" = (
    SELECT "Id" FROM "Folders" WHERE "Name" = '2004' ORDER BY "Id" LIMIT 1
)
GROUP BY f."Id", f."Name"
ORDER BY f."Name"
LIMIT 10;
