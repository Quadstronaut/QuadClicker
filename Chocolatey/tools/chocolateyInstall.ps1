$ErrorActionPreference = 'Stop'
$packageName= 'quadclicker'
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = 'https://github.com/Quadstronaut/QuadClicker/releases/download/v0.1.1/QuadClicker.exe'

$packageArgs = @{
  packageName   = $packageName
  unzippedLocation = $toolsDir
  fileType      = 'exe'
  url           = $url
  softwareName  = 'QuadClicker'
  checksum      = '' # Optional but recommended: SHA256 hash of the exe
  checksumType  = 'sha256'
}

Install-ChocolateyPackage @packageArgs