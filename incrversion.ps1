$file = ".\version.txt"
$fileVersion = (Get-Content $file | Select -First 1).Split(".")
$fileVersion[2] = [int]$fileVersion[2] + 1
$fileVersion -join "." | Set-Content $file
echo "New version number:"
Get-Content $file
