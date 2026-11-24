using System.Xml;

using KeePass.App.Configuration;

namespace KeePassDicewareMinQuality
{
	/// <summary>The minimum-quality floor, stored as a KeePass custom configuration item.</summary>
	internal static class MinQualitySetting
	{
		/// <summary>KeePass custom configuration key holding the minimum quality in bits.</summary>
		internal const string Key = "KeePassDicewareMinQuality_MinQualityBits";

		/// <summary>Used when the key is missing or not an integer. Matches the former fork's default.</summary>
		internal const long DefaultBits = 90;

		internal static long Read(AceCustomConfig config) => config.GetLong(Key, DefaultBits);

		internal static void Write(AceCustomConfig config, long bits) => config.SetLong(Key, bits);

		/// <summary>
		/// Whether the enforced configuration sets the floor. KeePass merges enforced <c>Custom/Item</c> elements
		/// over the user's by <c>Key</c> at every start, so a value saved by the user would be replaced anyway.
		/// </summary>
		/// <param name="enforcedConfig"><c>AppConfigSerializer.EnforcedConfigXml</c>; null when there is no enforced file.</param>
		internal static bool IsEnforced(XmlDocument enforcedConfig) =>
			enforcedConfig?.SelectSingleNode($"/Configuration/Custom/Item[Key='{Key}']") != null;
	}
}
