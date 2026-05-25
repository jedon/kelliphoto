-- Apply AddFolderCoverPhotos migration (20260525120000)
-- Run when folder tiles show no thumbnails after deploy.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'FolderCoverPhotos'
    ) THEN
        CREATE TABLE "FolderCoverPhotos" (
            "FolderId" integer NOT NULL,
            "PhotoId" integer NOT NULL,
            "SortOrder" integer NOT NULL,
            CONSTRAINT "PK_FolderCoverPhotos" PRIMARY KEY ("FolderId", "PhotoId"),
            CONSTRAINT "FK_FolderCoverPhotos_Folders_FolderId"
                FOREIGN KEY ("FolderId") REFERENCES "Folders" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_FolderCoverPhotos_Photos_PhotoId"
                FOREIGN KEY ("PhotoId") REFERENCES "Photos" ("Id") ON DELETE CASCADE
        );

        CREATE UNIQUE INDEX "IX_FolderCoverPhotos_FolderId_SortOrder"
            ON "FolderCoverPhotos" ("FolderId", "SortOrder");

        CREATE INDEX "IX_FolderCoverPhotos_PhotoId"
            ON "FolderCoverPhotos" ("PhotoId");

        INSERT INTO "FolderCoverPhotos" ("FolderId", "PhotoId", "SortOrder")
        SELECT "Id", "ThumbnailPhotoId", 0
        FROM "Folders"
        WHERE "ThumbnailPhotoId" IS NOT NULL;

        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20260525120000_AddFolderCoverPhotos', '10.0.0')
        ON CONFLICT DO NOTHING;

        RAISE NOTICE 'FolderCoverPhotos table created';
    ELSE
        RAISE NOTICE 'FolderCoverPhotos table already exists';
    END IF;
END $$;
