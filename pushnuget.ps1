set-location $args[0]

$files=get-childitem | where {$_.Name -like "*.nupkg" -and $_.Name -notlike "*symbols*"}

foreach($file in $files) {
  nuget push $file.name -SkipDuplicate -Source "https://nuget.pkg.github.com/marketly/index.json" -ApiKey $args[1]
}
