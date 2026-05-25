-- Apply AddFolderSortOrder migration (20260525200000)

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Folders' AND column_name = 'SortOrder'
    ) THEN
        ALTER TABLE "Folders" ADD COLUMN "SortOrder" integer NOT NULL DEFAULT 0;

        WITH ranked AS (
            SELECT "Id",
                   ROW_NUMBER() OVER (PARTITION BY "ParentId" ORDER BY "Name") - 1 AS rn
            FROM "Folders"
        )
        UPDATE "Folders" AS f
        SET "SortOrder" = ranked.rn
        FROM ranked
        WHERE f."Id" = ranked."Id";

        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20260525200000_AddFolderSortOrder', '10.0.0')
        ON CONFLICT DO NOTHING;

        RAISE NOTICE 'Folders.SortOrder column added';
    ELSE
        RAISE NOTICE 'Folders.SortOrder already exists';
    END IF;
END $$;
