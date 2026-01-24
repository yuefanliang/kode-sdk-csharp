@echo off
echo Stopping application...
echo.
echo Deleting old database files...
del app.db 2>nul
del app.db-shm 2>nul
del app.db-wal 2>nul
echo.
echo Database reset complete!
echo.
echo Please restart the application.
pause
