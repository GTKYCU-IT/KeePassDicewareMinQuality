using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Xml.Serialization;

using KeePass.App.Configuration;
using KeePass.DataExchange;
using KeePass.Ecas;
using KeePass.Forms;
using KeePass.Plugins;
using KeePass.UI;
using KeePass.Util;

using KeePassDiceware;

using KeePassLib;
using KeePassLib.Cryptography;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Keys;
using KeePassLib.Security;

using Xunit;

namespace KeePassDicewareMinQuality.Tests
{
	public class MinQualityPwGeneratorTests
	{
		private static CryptoRandomStream NewRandom(byte seed) =>
			new(CrsAlgorithm.ChaCha20, Enumerable.Repeat(seed, 32).ToArray());

		private static uint Bits(string password) => QualityEstimation.EstimatePasswordBits(password.ToCharArray());

		private static PwProfile DicewareProfile(string options = "") => new()
		{
			GeneratorType = PasswordGeneratorType.Custom,
			CustomAlgorithmUuid = Convert.ToBase64String(MinQualityPwGenerator.DicewareUuid.UuidBytes),
			CustomAlgorithmOptions = options,
		};

		/// <summary>Registers under Diceware's UUID and returns a fixed sequence, repeating the last value.</summary>
		private sealed class ScriptedGenerator(params string[] passwords) : CustomPwGenerator
		{
			public int Calls { get; private set; }

			public override PwUuid Uuid => MinQualityPwGenerator.DicewareUuid;

			public override string Name => "Scripted";

			public override ProtectedString Generate(PwProfile prf, CryptoRandomStream crsRandomSource) =>
				new(false, passwords[Math.Min(Calls++, passwords.Length - 1)]);
		}

		private sealed class OtherGenerator : CustomPwGenerator
		{
			public override PwUuid Uuid { get; } = new(true);

			public override string Name => "Other";

			public override ProtectedString Generate(PwProfile prf, CryptoRandomStream crsRandomSource) => ProtectedString.Empty;
		}

		/// <summary>Upstream's generator reads only CustomConfig (legacy global options) from the host.</summary>
		private sealed class FakeHost : IPluginHost
		{
			public MainForm MainWindow => null;
			public PwDatabase Database => null;
			public CommandLineArgs CommandLineArgs => null;
			public AceCustomConfig CustomConfig { get; } = new();
			public CipherPool CipherPool => null;
			public KeyProviderPool KeyProviderPool => null;
			public KeyValidatorPool KeyValidatorPool => null;
			public FileFormatPool FileFormatPool => null;
			public TempFilesPool TempFilesPool => null;
			public EcasPool EcasPool => null;
			public EcasTriggerSystem TriggerSystem => null;
			public CustomPwGeneratorPool PwGeneratorPool => null;
			public ColumnProviderPool ColumnProviderPool => null;
			public ResourceManager Resources => null;
		}

		private static string TwoWordDicewareOptions()
		{
			Options options = Options.Default();
			options.WordCount = 2;
			using var writer = new StringWriter();
			new XmlSerializer(typeof(Options)).Serialize(writer, options);
			return writer.ToString();
		}

		[Fact]
		public void Generate_ReturnsFirstCandidateMeetingMinimum()
		{
			const string weak = "aaaa";
			const string strong = "Correct-Horse-Battery-Staple-9Q!";
			Assert.True(Bits(weak) < Bits(strong));

			var inner = new ScriptedGenerator(weak, weak, strong, weak);
			var pool = new CustomPwGeneratorPool { inner };
			MinQualityPwGenerator wrapper = MinQualityPwGenerator.Install(pool, () => Bits(strong));

			ProtectedString result = wrapper.Generate(DicewareProfile(), NewRandom(1));

			Assert.Equal(strong, result.ReadString());
			Assert.Equal(3, inner.Calls);
		}

		[Fact]
		public void Generate_UnreachableMinimum_ThrowsAfterMaxAttempts()
		{
			var inner = new ScriptedGenerator("aaaa");
			var pool = new CustomPwGeneratorPool { inner };
			MinQualityPwGenerator wrapper = MinQualityPwGenerator.Install(pool, () => 10_000);

			var ex = Assert.Throws<InvalidOperationException>(() => wrapper.Generate(DicewareProfile(), NewRandom(1)));

			Assert.Equal(MinQualityPwGenerator.MaxAttempts, inner.Calls);
			Assert.Contains("at least 10000 bits", ex.Message);
			Assert.Contains(KeePassDicewareMinQualityExt.MinQualityBitsKey, ex.Message);
		}

		[Fact]
		public void Install_ReplacesDicewareInPlace_UninstallRestoresIt()
		{
			var other = new OtherGenerator();
			var inner = new ScriptedGenerator("x");
			var pool = new CustomPwGeneratorPool { other, inner };

			MinQualityPwGenerator wrapper = MinQualityPwGenerator.Install(pool, () => 0);

			Assert.NotNull(wrapper);
			Assert.Equal(new CustomPwGenerator[] { other, wrapper }, pool.ToArray());
			Assert.Null(MinQualityPwGenerator.Install(pool, () => 0)); // Already wrapped.

			wrapper.Uninstall(pool);

			Assert.Equal(new CustomPwGenerator[] { other, inner }, pool.ToArray());
		}

		[Fact]
		public void Install_WithoutDiceware_ChangesNothing()
		{
			var other = new OtherGenerator();
			var pool = new CustomPwGeneratorPool { other };

			Assert.Null(MinQualityPwGenerator.Install(pool, () => 0));
			Assert.Equal(new CustomPwGenerator[] { other }, pool.ToArray());
		}

		[Fact]
		public void UpstreamDiceware_ThroughKeePass_EveryPasswordMeetsMinimum()
		{
			var upstream = new DicewarePwGenerator(new FakeHost());
			PwProfile profile = DicewareProfile(TwoWordDicewareOptions());

			// Pick a floor that unwrapped upstream output often misses, so the test proves the wrapper rejects.
			CryptoRandomStream random = NewRandom(7);
			List<uint> rawBits = Enumerable.Range(0, 200)
				.Select(_ => Bits(upstream.Generate(profile.CloneDeep(), random).ReadString()))
				.OrderBy(b => b)
				.ToList();
			uint floor = rawBits[rawBits.Count / 2];
			Assert.Contains(rawBits, b => b < floor);

			var pool = new CustomPwGeneratorPool { upstream };
			Assert.NotNull(MinQualityPwGenerator.Install(pool, () => floor));

			for (int i = 0; i < 200; i++)
			{
				Assert.Equal(PwgError.Success, PwGenerator.Generate(out ProtectedString password, profile, null, pool));
				Assert.True(Bits(password.ReadString()) >= floor);
			}
		}

		[Fact]
		public void UpstreamDiceware_ThroughKeePass_UnreachableMinimumThrows()
		{
			var pool = new CustomPwGeneratorPool { new DicewarePwGenerator(new FakeHost()) };
			MinQualityPwGenerator.Install(pool, () => 10_000);

			Assert.Throws<InvalidOperationException>(
				() => PwGenerator.Generate(out _, DicewareProfile(TwoWordDicewareOptions()), null, pool));
		}
	}
}
