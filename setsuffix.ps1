$versionSuffix = ""

if ($args[0] -eq 'refs/heads/development')
{
    $gitHash = git rev-parse --short HEAD
	$versionSuffix = "-prerelease-" + $gitHash
}

# set version number in workflow variable in GitHub actions
echo "::set-output name=versionsuffix::$versionSuffix"

# display file content
echo "New version suffix:" $versionSuffix
