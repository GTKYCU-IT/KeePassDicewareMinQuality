# KeePassDicewareMinQuality

A KeePass 2.x plugin that adds a **minimum password quality** to the upstream
[cmdwtf/KeePassDiceware](https://github.com/cmdwtf/KeePassDiceware) generator, without modifying it. Run the
unmodified upstream plugin next to this one. Every Diceware password KeePass generates is then regenerated until
KeePass's quality estimate reaches the configured number of bits.

> **Internal deployment docs (GTKYCU staff):** see Confluence (IT space → *11.14 KeePass* → *KeePass Diceware Plugin*).

## How it works

At startup the plugin finds the Diceware generator in KeePass's generator list. It replaces it with a wrapper that
has the same UUID, name, and options dialog. Saved password-generator profiles, including the one KeePass uses for
new entries, keep working unchanged. When KeePass generates a password:

1. The wrapper asks upstream Diceware for a password.
2. It accepts the password if `QualityEstimation.EstimatePasswordBits` is at least the minimum, the same number
   KeePass shows in its quality bar.
3. Otherwise it tries again, up to 1000 times. After that it throws an error naming the minimum. KeePass shows the
   error in its password generator dialog. For passwords generated automatically for new entries, KeePass ignores the
   error and leaves the password empty.

The check only measures **estimated** quality. True entropy comes from the Diceware settings: word count, word lists,
and salt. Rejecting low-scoring passwords doesn't add any.

Without upstream Diceware installed, the plugin does nothing.

## Installing

Copy both DLLs into KeePass's `Plugins` folder:

- `KeePassDiceware.dll` from [upstream's releases](https://github.com/cmdwtf/KeePassDiceware/releases)
- `KeePassDicewareMinQuality.dll` from [this repo's releases](https://github.com/GTKYCU-IT/KeePassDicewareMinQuality/releases)

Check **Tools → Plugins**: both should be listed.

## Setting the minimum

The minimum is a single KeePass setting that applies to every profile. The custom configuration key is
`KeePassDicewareMinQuality_MinQualityBits`. If the key is missing or isn't an integer, the minimum is **90 bits**.
A value of 0 or less turns the check off.

To set it for every user and stop users from changing it, add it to `KeePass.config.enforced.xml` in the KeePass
program folder. If the file already exists, add the `<Custom>` element inside its `<Configuration>`.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Configuration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
	<Custom>
		<Item>
			<Key>KeePassDicewareMinQuality_MinQualityBits</Key>
			<Value>90</Value>
		</Item>
	</Custom>
</Configuration>
```

KeePass matches `<Custom>` items by `<Key>`. Each time it starts, it merges the enforced file over the user's
configuration, so the enforced value replaces any value a user sets in their own `KeePass.config.xml`. This was
checked on KeePass 2.61.1: with an enforced value of 70 and a user value of 10, the plugin used 70. See KeePass's
[Enforced Configuration](https://keepass.info/help/kb/config_enf.html) page for how the file works.

## Moving from the GTKYCU-IT KeePassDiceware fork

The fork's **Minimum Quality** option was stored per profile. Upstream Diceware ignores that setting when it reads
a profile, and drops it the next time someone saves the profile from the Diceware options dialog. After switching,
only the global minimum above applies. Profiles themselves need no changes.

## Building and testing

You need Windows, the .NET SDK 9 (pinned by `global.json`), and KeePass 2.x. Only `KeePass.exe` is used, as the API
reference.

```powershell
./build.ps1                                  # compile against C:\Program Files\KeePass Password Safe 2
./build.ps1 -KeePassPath 'D:\Tools\KeePass'  # compile against another KeePass folder
./build.ps1 -DownloadKeePass                 # download the pinned portable KeePass into .keepass\ (what CI does)
```

`build.ps1` downloads the pinned upstream `KeePassDiceware.dll` into `.deps\` and checks its SHA-256. The tests run
against that DLL. The script then checks that `.meta/version.info` matches the csproj `VersionPrefix`, runs the tests,
and writes `artifacts/KeePassDicewareMinQuality.dll` plus its SHA-256. The pinned KeePass and Diceware versions and
hashes are at the top of `build.ps1`.

## Releasing

1. On a branch, set the same version in `KeePassDicewareMinQuality.csproj` (`<VersionPrefix>`) and
   `.meta/version.info`. Then build and open a PR. CI builds and tests every PR.
2. Merge to `main`. KeePass's update check reads `.meta/version.info` from `main`, so users see the new version
   from this point.
3. Tag and push the tag, for example `git tag v1.0.0 && git push origin v1.0.0`. CI checks that the tag matches
   `.meta/version.info` and creates a **draft** GitHub Release with the DLL and its `.sha256`.
4. Add a short summary to the draft, then publish.
