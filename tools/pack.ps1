$csproj = "$PSScriptRoot\..\src\NuExt.System.Data.SQLite.csproj"
$Configuration = "Release"
$outDir = $PSScriptRoot

dotnet clean $csproj -c $Configuration
dotnet pack $csproj -c $Configuration -o $outDir