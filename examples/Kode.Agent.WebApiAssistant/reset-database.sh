#!/bin/bash

echo "Stopping application..."
echo ""
echo "Deleting old database files..."
rm -f app.db app.db-shm app.db-wal
echo ""
echo "Database reset complete!"
echo ""
echo "Please restart the application."
