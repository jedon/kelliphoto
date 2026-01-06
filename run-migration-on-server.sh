#!/bin/bash
# Run this script on your server to apply the database migration
# Copy the migration files to the server first, or clone the repo there

echo "Applying database migration..."

# Make sure you're in the project directory
cd /path/to/kelli.photo/src/KelliPhoto.Web

# Update the database
dotnet ef database update

echo "Migration complete!"
