param(
	[int]$bump
)
$ErrorActionPreference = "Stop";

$solutionDir = Split-Path -parent $PSScriptRoot;
$msbuild = Join-Path ${Env:Programfiles} "Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe";
$credentials = (Get-Content "$solutionDir/../.user/credentials.json" | Out-String | ConvertFrom-Json);
$nugetApiKey = $credentials.NugetApiKey;
$nuget = Join-Path $PSScriptRoot "nuget.exe";
$msverbosity = "minimal";

# Version
$versionFile = Join-Path $PSScriptRoot "version.json"
$versionJson = (Get-Content $versionFile | Out-String | ConvertFrom-Json);
$bumpTime = [datetime]::Parse($versionJson.BumpTime);
$version = [version]::Parse($versionJson.Version);
if ( $bump -gt 0 )
{
	$isVersionBumped = $true;
	$version = [version]::new($version.Major, $version.Minor, $version.Build + 1);
	$versionJson.Version = $version.ToString(3);
	$versionJson.BumpTime = [datetime]::UtcNow.ToString("o");
	$versionJson.Prerelease = ($bump -eq 2);
	$versionJson | ConvertTo-Json -depth 10 | Out-File $versionFile;
}

$prerelease=$versionJson.Prerelease;
$isLatest=$versionJson.Prerelease -eq $false; 
$versionParam = $version.ToString(3);
$versionTag="v$versionParam" + (&{if($prerelease) {"-prerelease"} else {""}});
$releaseDate = Get-Date -asUTC -Format "s";
$deprecatedVersion = $versionJson.DeprecatedVersion;

# Packages Directory
$packagesRootDir = "$PSScriptRoot/bin/" + $versionTag;
$packagesClientDir="$packagesRootDir/Client";
$packagesServerDir="$packagesRootDir/Server";
New-Item -ItemType Directory -Path $packagesClientDir -Force | Out-Null
New-Item -ItemType Directory -Path $packagesServerDir -Force | Out-Null


# Prepare the latest folder
$packagesRootDirLatest = "$PSScriptRoot/bin/latest" + (&{if($isLatest) {""} else {"/????"}});
$packagesClientDirLatest="$packagesRootDirLatest/Client";
$packagesServerDirLatest="$packagesRootDirLatest/Server";
$moduleGooglePlayLastestDir = "$solutionDir/pub/Android.GooglePlay/apk/latest";
if ($isLatest)
{
	New-Item -ItemType Directory -Path $packagesClientDirLatest -Force | Out-Null
	New-Item -ItemType Directory -Path $packagesServerDirLatest -Force | Out-Null
}

# UpdateProjectVersion
Function UpdateProjectVersion([string] $projectFile) 
{
	$xml = New-Object XML;
	$xml.PreserveWhitespace = $true;
	$xml.Load($projectFile);
	$assemblyVersion = $xml.SelectSingleNode("Project/PropertyGroup/AssemblyVersion");
	$fileVersion = $xml.SelectSingleNode("Project/PropertyGroup/FileVersion");
	$packageVersion = $xml.SelectSingleNode("Project/PropertyGroup/Version");
	if ($assemblyVersion -and $assemblyVersion.InnerText -ne $versionParam){
		$assemblyVersion.InnerText = $versionParam;
		$fileVersion.InnerText = '$([System.DateTime]::Now.ToString("yyyy.M.d.HHmm"))';
		$packageVersion.InnerText = $versionParam;
		$xml.Save($projectFile);
	}
}

# ReportVersion
Function ReportVersion() 
{
	Write-Host "version: $versionParam" -ForegroundColor GREEN;
}

# ZipFiles, PowerShell Compression has a bug and does not respoect slash for linux
function ZipFiles([string]$Path, [string]$DestinationPath)
{
	# PowerShell Compress-Archive is not compatible on linux
	# Compress-Archive -Path "$distDir\*" -DestinationPath $dest1 -Force; 
	tar.exe -C "$Path" -a -cf "$DestinationPath" "*"
}

function PrepareModuleFolder([string]$moduleDir, [string]$moduleDirLatest)
{
	# Remove old files
	try { Remove-Item -path "$moduleDir" -Force -Recurse } catch {}
	New-Item -ItemType Directory -Path $moduleDir -Force | Out-Null;

	if ($isLatest)
	{
		try { Remove-Item -path $moduleDirLatest -Force -Recurse } catch {}
		New-Item -ItemType Directory -Path $moduleDirLatest -Force | Out-Null;
	}
}

function UpdateRepoVersionInFile()
{
	$files = Get-ChildItem -Path @($packagesRootDirLatest, $moduleGooglePlayLastestDir) `
			-File -Recurse | Where-Object { $_.Extension -eq '.json' -or $_.Extension -eq '.txt' -or $_.Extension -eq '.sh'  }
	
	# Loop through each file and apply the change
	foreach ($file in $files) 
	{
		$fileContent = Get-Content $file.FullName -Raw;
		$fileContent = $fileContent -replace "/download/v(\d+\.\d+\.\d+)/", "/download/$versionTag/";
		Set-Content -Path $file.FullName -Value $fileContent
	}	
}