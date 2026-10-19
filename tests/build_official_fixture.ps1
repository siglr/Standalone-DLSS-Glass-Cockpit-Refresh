# Build only the upstream test host and fake upscalers. Never rebuild the DLL under test.
param([Parameter(Mandatory=$true)][string]$Source, [Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
$Source=(Resolve-Path -LiteralPath $Source).Path
$Output=[IO.Path]::GetFullPath($Output)
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\build'))+'\'
if (-not $Output.StartsWith($workspace,[StringComparison]::OrdinalIgnoreCase)) { throw 'Test output must be under repository build.' }
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$installation = & 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$tools=Get-ChildItem (Join-Path $installation 'VC\Tools\MSVC') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$sdkRoot='C:\Program Files (x86)\Windows Kits\10'
$sdk=Get-ChildItem "$sdkRoot\Include" -Directory | Where-Object { $_.Name -match '^\d+(\.\d+){3}$' -and (Test-Path "$sdkRoot\bin\$($_.Name)\x64\dxc.exe") } | Sort-Object Name -Descending | Select-Object -First 1
$compiler=Join-Path $tools.FullName 'bin\Hostx64\x64\cl.exe'
$dxc="$sdkRoot\bin\$($sdk.Name)\x64\dxc.exe"
$env:INCLUDE=@("$($tools.FullName)\include","$($sdk.FullName)\ucrt","$($sdk.FullName)\shared","$($sdk.FullName)\um","$($sdk.FullName)\winrt") -join ';'
$env:LIB=@("$($tools.FullName)\lib\x64","$sdkRoot\Lib\$($sdk.Name)\ucrt\x64","$sdkRoot\Lib\$($sdk.Name)\um\x64") -join ';'
$env:PATH=(Split-Path $compiler)+';'+$env:PATH
function Shader($name,$profile,$sourceFile,$defines) {
    & $dxc -T $profile -HV 2021 -E main -WX -O3 -Fo "$Output\$name.cso" @defines "$Source\tests\$sourceFile.hlsl"
    if($LASTEXITCODE){throw "Shader failed: $name"}
}
Shader 'fixture_vs' 'vs_6_6' 'fixture_vs' @()
Shader 'fixture_vr_vs' 'vs_6_6' 'fixture_vs' @('-D','VR=1','-D','REFERENCE=0')
foreach($eye in 0,1){Shader "reference_vr_vs_eye$eye" 'vs_6_6' 'fixture_vs' @('-D','VR=1','-D','REFERENCE=1','-D',"REFERENCE_EYE=$eye")}
foreach($ref in 0,1){foreach($vr in 0,1){foreach($t0 in 0,1){
    $name=if($ref){'reference'}else{'fixture'}
    $defs=@('-D',"REFERENCE=$ref")
    if($vr){$name+='_vr';$defs+=@('-D','VR=1')}
    if($t0){$name+='_t0';$defs+=@('-D','SCREEN_T0=1')}
    Shader ($name+'_ps') 'ps_6_6' 'fixture_ps' $defs
}}}
Push-Location $Output
try {
    & $compiler /nologo /W4 /WX /MD /O2 /LD /hotpatch "$Source\tests\fake_nvngx.cpp" /link /OPT:NOICF /FUNCTIONPADMIN:16 "/DEF:$Source\tests\fake_nvngx.def" /OUT:_nvngx.dll
    if($LASTEXITCODE){throw 'Fake NGX failed'}
    & $compiler /nologo /W4 /WX /MD /O2 /LD "$Source\tests\fake_ffx.cpp" /link /OUT:amd_fidelityfx_dx12.dll
    if($LASTEXITCODE){throw 'Fake FSR failed'}
    New-Item -ItemType Directory -Force OptiScaler | Out-Null
    Copy-Item amd_fidelityfx_dx12.dll OptiScaler\amd_fidelityfx_upscaler_dx12.dll -Force
    & $compiler /nologo /W4 /WX /MD /O2 /LD "$Source\tests\fake_proxy.cpp" /link "/DEF:$Source\tests\fake_proxy.def" /OUT:OptiScaler.asi
    if($LASTEXITCODE){throw 'Fake proxy failed'}
    & $compiler /nologo /std:c++20 /EHsc /W4 /WX /MD /O2 /permissive- /Fe:FlightSimulator2024.exe "$Source\tests\fixture.cpp" /link d3d12.lib dxgi.lib ole32.lib windowscodecs.lib
    if($LASTEXITCODE){throw 'Fixture failed'}
    New-Item -ItemType Directory -Force startup,private | Out-Null
    & $compiler /nologo /std:c++20 /EHsc /MD /O2 /DNOMINMAX /DWIN32_LEAN_AND_MEAN /Fe:startup\FlightSimulator2024.exe "$PSScriptRoot\official_startup.cpp" /link d3d12.lib dxgi.lib user32.lib
    if($LASTEXITCODE){throw 'Startup probe failed'}
    & $compiler /nologo /std:c++20 /EHsc /MD /O2 /DNOMINMAX /DWIN32_LEAN_AND_MEAN /Fe:private\FlightSimulator2024.exe "$PSScriptRoot\dxgi_private_adapter.cpp" /link d3d12.lib dxgi.lib d3d11.lib
    if($LASTEXITCODE){throw 'Private adapter probe failed'}
} finally { Pop-Location }
