set-location $args[0]

$files=get-childitem | where {$_.Name -like "*.sln"}

foreach($file in $files) {
  nuget restore $file.name
}
