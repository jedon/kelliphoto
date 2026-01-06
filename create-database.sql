-- Run this SQL script on your PostgreSQL server to create the database and user
-- Connect as postgres user first: psql -h 192.168.10.150 -p 15432 -U postgres

-- Create the database
CREATE DATABASE kelli_photo;

-- Create the user
CREATE USER kelli_photo_app WITH PASSWORD '!kelliphoto13!';

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE kelli_photo TO kelli_photo_app;

-- Connect to the new database
\c kelli_photo

-- Grant schema privileges
GRANT ALL ON SCHEMA public TO kelli_photo_app;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO kelli_photo_app;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO kelli_photo_app;

-- Set default privileges for future objects
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO kelli_photo_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO kelli_photo_app;
