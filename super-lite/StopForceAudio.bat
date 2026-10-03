@echo off
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p = Join-Path $env:TEMP 'KuroForceAudio.pid'; if (Test-Path $p) { $procId = Get-Content $p; try { Stop-Process -Id $procId -Force -ErrorAction Stop; Write-Host 'Berhenti memantau.' -ForegroundColor Green } catch { Write-Host 'Proses sudah tidak berjalan.' -ForegroundColor Yellow }; Remove-Item $p -Force } else { Write-Host 'Nggak ada yang lagi mantau.' -ForegroundColor Yellow }"
pause
