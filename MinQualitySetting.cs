using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using KeePass.App.Configuration;

using KeePassLib;

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
		/// Target generator UUIDs. A missing key means Diceware only; an empty value means none. Entries that aren't
		/// a base64 16-byte UUID are skipped.
		/// </summary>
		internal static List<PwUuid> ReadGenerators(AceCustomConfig config)
		{
			string value = config.GetString(GeneratorsKey, null);
			if (value == null)
			{
				return new List<PwUuid> { DicewareUuid };
			}

			var result = new List<PwUuid>();
			foreach (string entry in value.Split(','))
			{
				PwUuid uuid = TryParseUuid(entry.Trim());
				if (uuid != null && !result.Contains(uuid))
				{
					result.Add(uuid);
				}
			}
			return result;
		}

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
}
