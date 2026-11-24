using System.Linq;
using System.Xml;

using KeePass.App.Configuration;

using KeePassLib;
using KeePassLib.Cryptography;
using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Security;

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

		private sealed class NamedGenerator : CustomPwGenerator
		{
			public override PwUuid Uuid { get; } = new(true);

			public override string Name => "Named";

			public override ProtectedString Generate(PwProfile prf, CryptoRandomStream crsRandomSource) => ProtectedString.Empty;
		}

		private static GeneratorTargets Read(string generatorsValue)
		{
			var config = new AceCustomConfig();
			if (generatorsValue != null)
			{
				config.SetString(MinQualitySetting.GeneratorsKey, generatorsValue);
			}
			return MinQualitySetting.ReadGenerators(config);
		}

		[Fact]
		public void ReadGenerators_MissingMeansDicewareAndEmptyMeansNone()
		{
			GeneratorTargets missing = Read(null);
			Assert.True(missing.IsValid);
			Assert.Equal(new[] { MinQualitySetting.DicewareUuid }, missing.Uuids);

			GeneratorTargets empty = Read(" , ");
			Assert.True(empty.IsValid);
			Assert.Empty(empty.Uuids);
		}

		[Fact]
		public void ReadGenerators_ParsesCommaSeparatedBase64()
		{
			var a = new PwUuid(true);
			var b = new PwUuid(true);
			GeneratorTargets targets = Read($" {MinQualitySetting.ToBase64(a)} ,{MinQualitySetting.ToBase64(b)},,{MinQualitySetting.ToBase64(a)}");

			Assert.True(targets.IsValid);
			Assert.Equal(new[] { a, b }, targets.Uuids);

			var config = new AceCustomConfig();
			MinQualitySetting.WriteGenerators(config, new[] { b });
			Assert.Equal(new[] { b }, MinQualitySetting.ReadGenerators(config).Uuids);
		}

		[Fact]
		public void MistypedGeneratorList_FailsClosedToEveryInstalledGenerator()
		{
			// A typo in an (enforced) list must not leave the intended generator without the minimum.
			var intended = new NamedGenerator();
			var unrelated = new NamedGenerator();
			var installed = new CustomPwGenerator[] { intended, unrelated };

			GeneratorTargets targets = Read($"{MinQualitySetting.ToBase64(unrelated.Uuid)},not-a-uuid,AAAA");

			Assert.False(targets.IsValid);
			Assert.Equal(new[] { "not-a-uuid", "AAAA" }, targets.InvalidEntries);
			Assert.Equal(new[] { intended.Uuid, unrelated.Uuid }, MinQualitySetting.EffectiveTargets(targets, installed));
			Assert.Contains("not-a-uuid, AAAA", MinQualitySetting.InvalidGeneratorsWarning(targets));
		}

		[Fact]
		public void ValidGeneratorList_TargetsOnlyTheListedGenerators()
		{
			var listed = new NamedGenerator();
			var installed = new CustomPwGenerator[] { new NamedGenerator(), listed };

			GeneratorTargets targets = Read(MinQualitySetting.ToBase64(listed.Uuid));

			Assert.Equal(new[] { listed.Uuid }, MinQualitySetting.EffectiveTargets(targets, installed));
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
