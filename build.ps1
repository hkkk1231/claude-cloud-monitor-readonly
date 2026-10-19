param([switch]$RestoreOnly)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$deps=Join-Path $root '.deps'
$package=Join-Path $deps 'webview2.zip'
$expanded=Join-Path $deps 'webview2'
$expected='56F7F4B8BF9AEE4B8EFEFBBDD4F67D5F74EBD1B100ED0806DA71BF76AF481AA9'
[IO.Directory]::CreateDirectory($deps) | Out-Null
if(!(Test-Path -LiteralPath $package)) {
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.4258.31/microsoft.web.webview2.1.0.4258.31.nupkg' -OutFile $package
}
if((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expected){throw 'WebView2 package checksum mismatch'}
Expand-Archive -LiteralPath $package -DestinationPath $expanded -Force
if($RestoreOnly){return}
$out=Join-Path $root 'bin'
[IO.Directory]::CreateDirectory($out) | Out-Null
Copy-Item -LiteralPath (Join-Path $expanded 'lib\net462\Microsoft.Web.WebView2.Core.dll') -Destination $out
Copy-Item -LiteralPath (Join-Path $expanded 'lib\net462\Microsoft.Web.WebView2.WinForms.dll') -Destination $out
Copy-Item -LiteralPath (Join-Path $expanded 'runtimes\win-x64\native\WebView2Loader.dll') -Destination $out
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path -LiteralPath $compiler)){throw '.NET Framework compiler not found'}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $root 'source') -Filter '*.cs' -File | ForEach-Object {$_.FullName})
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/reference:$out\Microsoft.Web.WebView2.Core.dll" "/reference:$out\Microsoft.Web.WebView2.WinForms.dll" "/out:$out\ClaudeMonitor.exe" @sources
if($LASTEXITCODE -ne 0){throw 'Build failed'}
Copy-Item -LiteralPath (Join-Path $root 'ClaudeMonitor.exe.config') -Destination $out
foreach($item in Get-ChildItem -LiteralPath $root -File) {
    if($item.Extension -in @('.py','.html','.cmd','.js') -or $item.Name -in @('Configure.ps1','Verify-Connection.ps1','README.md','LICENSE','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath $item.FullName -Destination $out}
}
foreach($directory in @('dashboard','vendor','licenses')) {
    $path=Join-Path $root $directory
    if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
}
Write-Host ('Built: '+(Join-Path $out 'ClaudeMonitor.exe'))