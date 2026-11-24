using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using KeePass.App.Configuration;

using KeePassLib;
using KeePassLib.Cryptography.PasswordGenerator;

namespace KeePassGeneratorMinQuality
{
	/// <summary>The plugin's settings, stored as KeePass custom configuration items.</summary>
	internal static class MinQualitySetting
	{
		/// <summary>Custom configuration key holding the minimum quality in bits.</summary>
		internal const string BitsKey = "KeePassGeneratorMinQuality_MinQualityBits";

		/// <summary>
		/// Custom configuration key holding the target generators: comma-separated base64 UUIDs, the same form
		/// KeePass stores in a profile's <c>CustomAlgorithmUuid</c>.
		/// </summary>
		internal const string GeneratorsKey = "KeePassGeneratorMinQuality_Generators";

		/// <summary>Used when the bits key is missing or not an integer.</summary>
		internal const long DefaultBits = 90;

		/// <summary>UUID of cmdwtf/KeePassDiceware's generator, the target when the generators key is missing.</summary>
		internal static readonly PwUuid DicewareUuid = new PwUuid(new byte[] {
			0xBA, 0x8E, 0x96, 0xF8, 0x66, 0xAE, 0x41, 0xEB,
			0x9E, 0xE3, 0x90, 0xC9, 0x9E, 0xAD, 0x86, 0x0B, });

		internal static long ReadBits(AceCustomConfig config) => config.GetLong(BitsKey, DefaultBits);

		internal static void WriteBits(AceCustomConfig config, long bits) => config.SetLong(BitsKey, bits);

		/// <summary>
		/// Reads the target generators. A missing key means Diceware only; an empty value means none. Blank entries
		/// are ignored; any other entry that isn't a base64 16-byte UUID makes the list invalid.
		/// </summary>
		internal static GeneratorTargets ReadGenerators(AceCustomConfig config)
		{
			string value = config.GetString(GeneratorsKey, null);
			if (value == null)
			{
				return new GeneratorTargets(new List<PwUuid> { DicewareUuid }, new List<string>());
			}

			var uuids = new List<PwUuid>();
			var invalid = new List<string>();
			foreach (string entry in value.Split(',').Select(e => e.Trim()).Where(e => e.Length != 0))
			{
				PwUuid uuid = TryParseUuid(entry);
				if (uuid == null)
				{
					invalid.Add(entry);
				}
				else if (!uuids.Contains(uuid))
				{
					uuids.Add(uuid);
				}
			}
			return new GeneratorTargets(uuids, invalid);
		}

		/// <summary>
		/// The generators to wrap. An invalid list fails closed: a mistyped UUID must not leave the generator it was
		/// meant to name without the minimum, so every installed plugin generator is targeted until it's fixed.
		/// </summary>
		internal static List<PwUuid> EffectiveTargets(GeneratorTargets targets, IEnumerable<CustomPwGenerator> installed) =>
			targets.IsValid ? targets.Uuids.ToList() : installed.Select(g => g.Uuid).ToList();

		internal static string InvalidGeneratorsWarning(GeneratorTargets targets) =>
			$"The generator list ({GeneratorsKey}) has entries that aren't generator UUIDs: " +
			string.Join(", ", targets.InvalidEntries) + ". Until it's fixed, the minimum quality applies to every " +
			"plugin password generator.";

		internal static void WriteGenerators(AceCustomConfig config, IEnumerable<PwUuid> generators) =>
			config.SetString(GeneratorsKey, string.Join(",", generators.Select(ToBase64)));

		internal static string ToBase64(PwUuid uuid) => Convert.ToBase64String(uuid.UuidBytes);

		private static PwUuid TryParseUuid(string base64)
		{
			try
			{
				byte[] bytes = Convert.FromBase64String(base64);
				return bytes.Length == PwUuid.UuidSize ? new PwUuid(bytes) : null;
			}
			catch (FormatException)
			{
				return null;
			}
		}

		/// <summary>
		/// Whether the enforced configuration sets <paramref name="key"/>. KeePass merges enforced <c>Custom/Item</c>
		/// elements over the user's by <c>Key</c> at every start, so a value saved by the user would be replaced anyway.
		/// </summary>
		/// <param name="enforcedConfig"><c>AppConfigSerializer.EnforcedConfigXml</c>; null when there is no enforced file.</param>
		internal static bool IsEnforced(XmlDocument enforcedConfig, string key) =>
			enforcedConfig?.SelectSingleNode($"/Configuration/Custom/Item[Key='{key}']") != null;
	}

	/// <summary>The configured target generators, plus any entries that couldn't be parsed.</summary>
	internal sealed class GeneratorTargets
	{
		internal GeneratorTargets(IReadOnlyList<PwUuid> uuids, IReadOnlyList<string> invalidEntries)
		{
			Uuids = uuids;
			InvalidEntries = invalidEntries;
		}

		internal IReadOnlyList<PwUuid> Uuids { get; }

		internal IReadOnlyList<string> InvalidEntries { get; }

		internal bool IsValid => InvalidEntries.Count == 0;
	}
}
