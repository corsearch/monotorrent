$versionSuffix = ""

if ($args[0] -like '*development*')
{
	$versionSuffix = "-prerelease"
}

# set version number in workflow variable in GitHub actions
echo "::set-output name=versionsuffix::$versionSuffix"

# display file content
echo "New version suffix:" $versionSuffix
