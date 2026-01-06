-- Complete database setup for kelli_photo
-- Run these commands while connected as kelli_photo_app user

-- Connect to the database (you're already here)
\c kelli_photo

-- Grant privileges on schema
GRANT ALL ON SCHEMA public TO kelli_photo_app;
ALTER SCHEMA public OWNER TO kelli_photo_app;

-- Grant privileges on all existing tables (for future migrations)
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO kelli_photo_app;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO kelli_photo_app;

-- Set default privileges for future objects created by migrations
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO kelli_photo_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO kelli_photo_app;

-- Verify setup
\du kelli_photo_app
\l kelli_photo
