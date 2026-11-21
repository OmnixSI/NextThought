param([Parameter(Mandatory)][string]$Directory, [Parameter(Mandatory)][string]$CertificateThumbprint, [Parameter(Mandatory)][string]$SignToolPath)
$ErrorActionPreference = 'Stop'
if ($CertificateThumbprint -notmatch '^[A-Fa-f0-9]{40}$') { throw 'Invalid certificate thumbprint.' }
$certificate = Get-Item -LiteralPath ('Cert:\CurrentUser\My\' + $CertificateThumbprint)
if (-not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or $certificate.EnhancedKeyUsageList.ObjectId -notcontains '1.3.6.1.5.5.7.3.3') { throw 'A valid existing code-signing identity is required.' }
if (-not (Test-Path -LiteralPath $SignToolPath)) { throw 'Install the Windows SDK SignTool before requesting a signed release.' }
$targetRoot = [IO.Path]::GetFullPath($Directory)
$files = Get-ChildItem -LiteralPath $targetRoot -File -Recurse | Where-Object {$_.Name -match '^NextThought(?:\.[A-Za-z]+)*\.(exe|dll)$'}
if (-not $files) { throw 'No NextThought binaries to sign.' }
foreach ($file in $files) {
    & $SignToolPath sign /sha1 $CertificateThumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $file.FullName
    if ($LASTEXITCODE -ne 0) { throw ('Signing failed: ' + $file.Name) }
    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint) { throw ('Signature verification failed: ' + $file.Name) }
}
