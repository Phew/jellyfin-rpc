@echo off
echo Stopping Jellyfin RPC background process...
taskkill /F /IM python.exe /FI "WINDOWTITLE eq theater.cx rpc*"
echo.
echo If the above didn't find the process, it might be running as a generic python process.
echo You can manually stop it by finding python.exe in Task Manager.
pause
