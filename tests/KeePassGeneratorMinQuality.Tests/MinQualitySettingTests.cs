using System.Linq;
using System.Xml;

using KeePass.App.Configuration;

using KeePassLib;

using Xunit;

namespace KeePassGeneratorMinQuality.Tests
{
	public class MinQualitySettingTests
	{
		private static XmlDocument Enforced(string customItemKey)
		{
			var doc = new XmlDocument();
			doc.LoadXml(
				"<Configuration><Application><Start><CheckForUpdate>false</CheckForUpdate></Start></Application>" +
				$"<Custom><Item><Key>{customItemKey}</Key><Value>70</Value></Item></Custom></Configuration>");
			return doc;
		}

		[Fact]
		public void IsEnforced_OnlyForKeysTheEnforcedConfigSets()
		{
			XmlDocument bitsOnly = Enforced(MinQualitySetting.BitsKey);

			Assert.False(MinQualitySetting.IsEnforced(null, MinQualitySetting.BitsKey));
			Assert.True(MinQualitySetting.IsEnforced(bitsOnly, MinQualitySetting.BitsKey));
			Assert.False(MinQualitySetting.IsEnforced(bitsOnly, MinQualitySetting.GeneratorsKey));
		}

		[Fact]
		public void ReadBits_FallsBackToDefaultForMissingOrNonIntegerValues()
		{
			var config = new AceCustomConfig();
			Assert.Equal(MinQualitySetting.DefaultBits, MinQualitySetting.ReadBits(config));

			config.SetString(MinQualitySetting.BitsKey, "ninety");
			Assert.Equal(MinQualitySetting.DefaultBits, MinQualitySetting.ReadBits(config));

			MinQualitySetting.WriteBits(config, 55);
			Assert.Equal(55, MinQualitySetting.ReadBits(config));
		}

		[Fact]
		public void ReadGenerators_MissingMeansDicewareAndEmptyMeansNone()
		{
			var config = new AceCustomConfig();
			Assert.Equal(new[] { MinQualitySetting.DicewareUuid }, MinQualitySetting.ReadGenerators(config));

			config.SetString(MinQualitySetting.GeneratorsKey, "");
			Assert.Empty(MinQualitySetting.ReadGenerators(config));
		}

		[Fact]
		public void ReadGenerators_ParsesCommaSeparatedBase64AndSkipsInvalidEntries()
		{
			var a = new PwUuid(true);
			var b = new PwUuid(true);
			var config = new AceCustomConfig();
			config.SetString(MinQualitySetting.GeneratorsKey,
				$" {MinQualitySetting.ToBase64(a)} ,not-base64,AAAA,{MinQualitySetting.ToBase64(b)},{MinQualitySetting.ToBase64(a)}");

			Assert.Equal(new[] { a, b }, MinQualitySetting.ReadGenerators(config));

			MinQualitySetting.WriteGenerators(config, new[] { b });
			Assert.Equal(new[] { b }, MinQualitySetting.ReadGenerators(config));
		}

		[Theory]
		[InlineData(5000, true)]
		[InlineData(-5, false)]
		public void Form_ShowsBitsOutsideTheUsualRange(long bits, bool enforced)
		{
			using var form = new MinQualityForm(bits, enforced, new GeneratorChoice[0], generatorsEnforced: false);

			Assert.Equal(bits, form.MinQualityBits);
			Assert.Equal(!enforced, form.BitsEditable);
		}

		[Fact]
		public void Form_KeepsSelectedGeneratorsThatAreNotInstalled()
		{
			var installed = new PwUuid(true);
			var missing = new PwUuid(true);
			using var form = new MinQualityForm(90, bitsEnforced: true, new[]
			{
				new GeneratorChoice(installed, "Installed", selected: false),
				new GeneratorChoice(missing, null, selected: true),
			}, generatorsEnforced: false);

			Assert.False(form.BitsEditable);
			Assert.True(form.GeneratorsEditable);
			Assert.Equal(new[] { missing }, form.SelectedGenerators.ToArray());
		}
	}
}
