$file = ".\version.txt"

# get version number from file
$fileVersion = (Get-Content $file | Select -First 1).Split(".")

# incr version number
$fileVersion[2] = [int]$fileVersion[2] + 1

# form new version number
$newFileVersion = $fileVersion -join "."

# write new version number to file
echo $newFileVersion | Set-Content $file

# set version number in workflow variable in GitHub actions
echo "::set-output name=version::$newFileVersion"

# display file content
echo "New version number:"
Get-Content $file
