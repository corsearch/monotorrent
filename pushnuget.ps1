set-location $args[0]

$files=get-childitem | where {$_.Name -like "*.nupkg" -and $_.Name -notlike "*symbols*"}

foreach($file in $files) {
  dotnet nuget push $file.name --skip-duplicate --source "https://nuget.pkg.github.com/marketly/index.json" -k $args[1]
}
