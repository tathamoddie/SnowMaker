param (
	[Parameter(Mandatory=$true)]
	[ValidatePattern("\d\.\d\.\d\.\d")]
	[string]
	$ReleaseVersionNumber
)

$ErrorActionPreference = "Stop"

$PSScriptFilePath = (Get-Item $MyInvocation.MyCommand.Path).FullName

$SolutionRoot = Split-Path -Path $PSScriptFilePath -Parent

# Build the NuGet package
$ProjectPath = Join-Path -Path $SolutionRoot -ChildPath "SnowMaker\SnowMaker.csproj"
& nuget pack $ProjectPath -Prop Configuration=Release -OutputDirectory $SolutionRoot
if (-not $?)
{
	throw "The NuGet process returned an error code."
}

# Upload the NuGet package
$NuPkgPath = Join-Path -Path $SolutionRoot -ChildPath "SnowMaker.$ReleaseVersionNumber.nupkg"
& nuget push $NuPkgPath
if (-not $?)
{
	throw "The NuGet process returned an error code."
}