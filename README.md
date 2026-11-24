# KeePassGeneratorMinQuality

A KeePass 2.x plugin that adds a **minimum password quality** to password generators provided by other plugins,
without modifying them. You choose which generators it applies to. By default it applies to the upstream
[cmdwtf/KeePassDiceware](https://github.com/cmdwtf/KeePassDiceware) generator. Passwords from a selected
generator are regenerated until KeePass's quality estimate reaches the configured number of bits.

> **Internal deployment docs (GTKYCU staff):** see Confluence (IT space → *11.14 KeePass* → *KeePass Diceware Plugin*).

## How it works

At startup the plugin replaces each selected generator in KeePass's generator list with a wrapper. The wrapper has
the same UUID, name, and options dialog as the original. Saved password-generator profiles, including the one
KeePass uses for new entries, keep working unchanged. When KeePass generates a password with a selected generator:

1. The wrapper asks the original generator for a password.
2. It accepts the password if `QualityEstimation.EstimatePasswordBits` is at least the minimum. This is the same
   number KeePass shows in its quality bar.
3. Otherwise it tries again, up to 1000 times. After that it throws an error naming the minimum. KeePass shows the
   error in its password generator dialog. For passwords generated automatically for new entries, KeePass ignores the
   error and leaves the password empty.

The check only measures **estimated** quality. A generator's true entropy comes from its own settings, and
rejecting low-scoring passwords doesn't add any.

Only generators added by plugins can be selected. KeePass's built-in character set and pattern generators run
inside KeePass itself, so a plugin can't wrap them.

## Installing

Copy `KeePassGeneratorMinQuality.dll` from
[this repo's releases](https://github.com/GTKYCU-IT/KeePassGeneratorMinQuality/releases) into KeePass's `Plugins`
folder, next to the generator plugins it should apply to, such as `KeePassDiceware.dll` from
[upstream's releases](https://github.com/cmdwtf/KeePassDiceware/releases). Check **Tools → Plugins**: all of them
should be listed.

## Settings

Open **Tools → Generator Minimum Quality...**. The dialog sets:

- **Minimum quality (bits):** if never set, or not an integer, it is **90**. A value of 0 or less turns the check off.
- **Generators:** a checklist of every installed plugin generator, showing each one's name and UUID. If this has
  never been set, only Diceware is selected. A selected generator that isn't installed stays in the list as
  *Not installed*.

Changes apply to the next password generated. KeePass saves them with the rest of its configuration when it exits.
They are stored as KeePass custom configuration items:

| Key | Value |
|---|---|
| `KeePassGeneratorMinQuality_MinQualityBits` | Minimum quality in bits |
| `KeePassGeneratorMinQuality_Generators` | Comma-separated generator UUIDs, base64-encoded. This is the form KeePass stores in a profile's `CustomAlgorithmUuid` and the dialog displays. An empty value selects none. Entries that aren't a valid UUID are ignored. |

### Enforcing settings for every user

Add the items to `KeePass.config.enforced.xml` in the KeePass program folder. If the file already exists, add the
`<Custom>` element inside its `<Configuration>`. Each item is enforced separately. Leave one out and users can still
change that setting. In the dialog, enforced settings are read-only.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Configuration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
	<Custom>
		<Item>
			<Key>KeePassGeneratorMinQuality_MinQualityBits</Key>
			<Value>90</Value>
		</Item>
		<!-- Diceware; separate further generator UUIDs with commas -->
		<Item>
			<Key>KeePassGeneratorMinQuality_Generators</Key>
			<Value>uo6W+GauQeue45DJnq2GCw==</Value>
		</Item>
	</Custom>
</Configuration>
```

KeePass matches `<Custom>` items by `<Key>`. Each time it starts, it merges the enforced file over the user's
configuration, so enforced values replace any values a user sets in their own `KeePass.config.xml`. This was checked
on KeePass 2.61.1. See KeePass's [Enforced Configuration](https://keepass.info/help/kb/config_enf.html) page for how
the file works.

KeePass saves the merged configuration on exit, so enforced values are copied into each user's
`KeePass.config.xml`. Removing an item from the enforced file later leaves users with its last enforced value, not
the default.

## Moving from the GTKYCU-IT KeePassDiceware fork

Install upstream Diceware and this plugin in place of the fork. The fork's **Minimum Quality** option was stored per
profile. Upstream Diceware ignores that setting when it reads a profile, and drops it the next time someone saves
the profile from the Diceware options dialog. After switching, only the minimum above applies. Profiles themselves
need no changes.

## Building and testing

You need Windows, the .NET SDK 9 (pinned by `global.json`), and KeePass 2.x. Only `KeePass.exe` is used, as the API
reference.

```powershell
./build.ps1                                  # compile against C:\Program Files\KeePass Password Safe 2
./build.ps1 -KeePassPath 'D:\Tools\KeePass'  # compile against another KeePass folder
./build.ps1 -DownloadKeePass                 # download the pinned portable KeePass into .keepass\ (what CI does)
```

`build.ps1` downloads the pinned upstream `KeePassDiceware.dll` into `.deps\` and checks its SHA-256. The
integration tests wrap that DLL. The script then checks that `.meta/version.info` matches the csproj
`VersionPrefix`, runs the tests, and writes `artifacts/KeePassGeneratorMinQuality.dll` plus its SHA-256. The pinned
KeePass and Diceware versions and hashes are at the top of `build.ps1`.

## Releasing

1. On a branch, set the same version in `KeePassGeneratorMinQuality.csproj` (`<VersionPrefix>`) and
   `.meta/version.info`. Then build and open a PR. CI builds and tests every PR.
2. Merge to `main`. KeePass's update check reads `.meta/version.info` from `main`, so users see the new version
   from this point.
3. Tag and push the tag, for example `git tag v1.0.0 && git push origin v1.0.0`. CI checks that the tag matches
   `.meta/version.info` and creates a **draft** GitHub Release with the DLL and its `.sha256`.
4. Add a short summary to the draft, then publish.
