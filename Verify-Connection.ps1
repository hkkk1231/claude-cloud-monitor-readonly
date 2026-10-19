$ErrorActionPreference = 'Stop'
try {
    $exe = Join-Path $PSScriptRoot 'ClaudeMonitor.exe'
    $check = Start-Process -FilePath $exe -ArgumentList '--config-check' -PassThru -Wait -WindowStyle Hidden
    if ($check.ExitCode -ne 0) { throw '连接配置尚未完成，请先打开 ClaudeMonitor.exe 或运行 修改连接.cmd。' }
    $config = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'config.json') -Raw | ConvertFrom-Json
    $ssh = (Get-Command ssh.exe -ErrorAction Stop).Source
    $port = if ($null -eq $config.port) { 22 } else { [int]$config.port }
    $common = @('-F','NUL','-p',"$port",'-i',$config.identityFile,'-o','IdentitiesOnly=yes','-o','ClearAllForwardings=yes','-o','PermitLocalCommand=no','-o','ConnectTimeout=8','-o','ConnectionAttempts=1')
    Write-Host '请核对自己服务器的 SSH 主机指纹；本脚本不会自动接受指纹。'
    $probe = "import os,json,sys;print(json.dumps({'ssh':True,'python':sys.version_info[:3],'proc':os.path.exists('/proc/meminfo'),'cswapFound':os.path.isfile(os.path.expanduser('~/.local/bin/cswap'))}))"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($probe))
    $remote = "python3 -c `"import base64;exec(base64.b64decode('$encoded'))`""
    & $ssh @common -o StrictHostKeyChecking=ask -o BatchMode=no "$($config.user)@$($config.host)" $remote
    if ($LASTEXITCODE -ne 0) { throw 'SSH/Python 检查未成功。请检查网络、指纹、密钥及服务器 Python；未安装或修改服务器软件。' }
    Write-Host '检查后台无交互认证（加密私钥需要预先载入 ssh-agent）…'
    & $ssh @common -o StrictHostKeyChecking=yes -o BatchMode=yes "$($config.user)@$($config.host)" 'true'
    if ($LASTEXITCODE -ne 0) { throw '后台认证未通过；请先将自己的密钥载入 ssh-agent 或修复密钥权限。' }
    Write-Host 'SSH 与 Python 检查通过。cswapFound=false 时请先在自己的服务器配置兼容账号池，性能监控仍可使用。'
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
