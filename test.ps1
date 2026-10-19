$ErrorActionPreference='Stop'
python -m unittest quota_probe_test performance_probe_test
if($LASTEXITCODE -ne 0){throw 'Python tests failed'}
$p=Start-Process -FilePath (Join-Path $PSScriptRoot 'bin\ClaudeMonitor.exe') -ArgumentList '--self-test' -Wait -PassThru -WindowStyle Hidden
if($p.ExitCode -ne 0){throw 'Desktop self-test failed: --self-test'}
