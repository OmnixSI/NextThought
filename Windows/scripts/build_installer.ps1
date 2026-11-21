param(
    [Parameter(Mandatory)][string]$Payload,
    [Parameter(Mandatory)][string]$Version,
    [switch]$Unsigned,
    [string]$CertificateThumbprint,
    [string]$SignToolPath,
    [string]$InstallId = 'NextThought.Desktop',
    [string]$ShortcutGroup = 'NextThought',
    [string]$OutputDirectory,
    [string]$OutputName,
    [switch]$FailBeforeActivation
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid version.' }
if ($InstallId -notmatch '^NextThought\.Desktop(?:\.QA[A-Za-z0-9_-]+)?$') { throw 'Invalid installation identity.' }
if ($ShortcutGroup -notmatch '^NextThought[A-Za-z0-9 ._-]{0,70}$') { throw 'Invalid shortcut group.' }
if ($Unsigned -and $CertificateThumbprint) { throw 'Choose unsigned preview or a real certificate, not both.' }
if (-not $Unsigned -and -not $CertificateThumbprint) { throw 'A real signing certificate is required. Pass -Unsigned explicitly for a validation preview.' }
$payloadRoot = [IO.Path]::GetFullPath($Payload)
$manifest = Get-Content -LiteralPath (Join-Path $payloadRoot 'release-manifest.json') -Raw | ConvertFrom-Json
if ($manifest.product -notin @('NextThought', 'NextThought.Desktop') -or $manifest.version -ne $Version) { throw 'Release manifest does not match the installer.' }
if ($Unsigned -and $manifest.signing -ne 'unsigned-preview') { throw 'The manifest signing status must match the installer.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -Path $OutputDirectory -ItemType Directory -Force | Out-Null
if (-not $OutputName) { $OutputName = 'NextThought-' + $Version + '-windows-x64-setup' }
if ($OutputName -notmatch '^[A-Za-z0-9._-]+$') { throw 'Invalid output name.' }
& (Join-Path $PSScriptRoot 'bootstrap_installer.ps1')
$compiler = Join-Path $root '.tools\inno-7.1.1\ISCC.exe'
$arguments = @('--quiet-progress', '--no-ide-signtools', ('--define=AppVersion=' + $Version), ('--define=Payload=' + $payloadRoot), ('--define=InstallId=' + $InstallId), ('--define=ShortcutGroup=' + $ShortcutGroup), ('--define=OutputDirectory=' + $OutputDirectory), ('--define=OutputName=' + $OutputName))
if ($FailBeforeActivation) { $arguments += '--define=FailBeforeActivation=1' }
if ($CertificateThumbprint) {
    if ($CertificateThumbprint -notmatch '^[A-Fa-f0-9]{40}$' -or -not (Test-Path -LiteralPath $SignToolPath)) { throw 'A valid certificate thumbprint and Windows SDK SignTool path are required.' }
    $certificate = Get-Item -LiteralPath ('Cert:\CurrentUser\My\' + $CertificateThumbprint)
    if (-not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or $certificate.EnhancedKeyUsageList.ObjectId -notcontains '1.3.6.1.5.5.7.3.3') { throw 'The selected code-signing certificate is unavailable or invalid.' }
    if ($manifest.signing -ne ('authenticode:' + $CertificateThumbprint.ToLowerInvariant())) { throw 'Payload must be signed with the selected identity before writing its manifest.' }
    $arguments += '--define=SigningCommand=1'
    $arguments += ('--signtool=ntsign=$q' + [IO.Path]::GetFullPath($SignToolPath) + '$q sign /sha1 ' + $CertificateThumbprint + ' /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $f')
}
$arguments += (Join-Path $root 'installer\NextThought.Desktop.iss')
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw ('Installer compilation failed: ' + $LASTEXITCODE) }
$setup = Join-Path $OutputDirectory ($OutputName + '.exe')
if (-not $Unsigned) {
    $signature = Get-AuthenticodeSignature -LiteralPath $setup
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint) { throw 'Installer signature verification failed.' }
}
$digest = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($setup + '.sha256', "$digest  $([IO.Path]::GetFileName($setup))`n", [Text.UTF8Encoding]::new($false))
Write-Output ('Installer: ' + $setup)
Write-Output ('SHA256: ' + $digest)
