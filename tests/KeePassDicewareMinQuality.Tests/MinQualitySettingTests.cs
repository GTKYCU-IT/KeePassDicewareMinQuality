using System.Xml;

using KeePass.App.Configuration;

using Xunit;

namespace KeePassDicewareMinQuality.Tests
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
		public void IsEnforced_OnlyWhenEnforcedConfigHasTheKey()
		{
			Assert.False(MinQualitySetting.IsEnforced(null));
			Assert.False(MinQualitySetting.IsEnforced(Enforced("SomeOtherPlugin_Setting")));
			Assert.True(MinQualitySetting.IsEnforced(Enforced(MinQualitySetting.Key)));
		}

		[Fact]
		public void Read_FallsBackToDefaultForMissingOrNonIntegerValues()
		{
			var config = new AceCustomConfig();
			Assert.Equal(MinQualitySetting.DefaultBits, MinQualitySetting.Read(config));

			config.SetString(MinQualitySetting.Key, "ninety");
			Assert.Equal(MinQualitySetting.DefaultBits, MinQualitySetting.Read(config));

			MinQualitySetting.Write(config, 55);
			Assert.Equal(55, MinQualitySetting.Read(config));
		}

		[Theory]
		[InlineData(5000, true)]
		[InlineData(-5, false)]
		public void Form_ShowsValuesOutsideTheUsualRange(long bits, bool enforced)
		{
			using var form = new MinQualityForm(bits, enforced);

			Assert.Equal(bits, form.MinQualityBits);
			Assert.Equal(!enforced, form.IsEditable);
		}
	}
}
