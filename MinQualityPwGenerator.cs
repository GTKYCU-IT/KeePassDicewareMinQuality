using System;

using KeePassLib;
using KeePassLib.Cryptography;
using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePassDicewareMinQuality
{
	/// <summary>
	/// Stands in for the upstream Diceware generator under its own UUID, so existing profiles keep working,
	/// and regenerates until KeePass's quality estimate reaches the configured minimum.
	/// </summary>
	internal sealed class MinQualityPwGenerator : CustomPwGenerator
	{
		/// <summary>UUID of upstream cmdwtf/KeePassDiceware's generator; profiles reference it.</summary>
		internal static readonly PwUuid DicewareUuid = new PwUuid(new byte[] {
			0xBA, 0x8E, 0x96, 0xF8, 0x66, 0xAE, 0x41, 0xEB,
			0x9E, 0xE3, 0x90, 0xC9, 0x9E, 0xAD, 0x86, 0x0B, });

		/// <summary>How many candidates are generated before giving up on reaching the minimum.</summary>
		internal const int MaxAttempts = 1000;

		private readonly CustomPwGenerator _inner;
		private readonly Func<long> _minQualityBits;

		private MinQualityPwGenerator(CustomPwGenerator inner, Func<long> minQualityBits)
		{
			_inner = inner;
			_minQualityBits = minQualityBits;
		}

		public override PwUuid Uuid => _inner.Uuid;

		public override string Name => _inner.Name;

		public override bool SupportsOptions => _inner.SupportsOptions;

		public override string GetOptions(string strCurrentOptions) => _inner.GetOptions(strCurrentOptions);

		/// <exception cref="InvalidOperationException">
		/// No candidate reached the minimum within <see cref="MaxAttempts"/> attempts. KeePass shows the message in
		/// interactive generator dialogs and suppresses it for auto-generated entry passwords, which come out empty.
		/// </exception>
		public override ProtectedString Generate(PwProfile prf, CryptoRandomStream crsRandomSource)
		{
			long minBits = _minQualityBits();

			for (int attempt = 0; attempt < MaxAttempts; attempt++)
			{
				// Fresh copy per attempt so nothing the inner generator does to the profile leaks into the next try.
				ProtectedString candidate = _inner.Generate(prf.CloneDeep(), crsRandomSource);
				if (candidate == null)
				{
					return null;
				}

				char[] chars = candidate.ReadChars();
				try
				{
					if (QualityEstimation.EstimatePasswordBits(chars) >= minBits)
					{
						return candidate;
					}
				}
				finally
				{
					MemUtil.ZeroArray(chars);
				}
			}

			throw new InvalidOperationException(
				$"{_inner.Name} could not generate a password with an estimated quality of at least {minBits} bits " +
				$"after {MaxAttempts} attempts. Increase the word count or salt in the {_inner.Name} options. " +
				$"The minimum is set by {KeePassDicewareMinQualityExt.MinQualityBitsKey} in the KeePass configuration.");
		}

		/// <summary>
		/// Replaces the Diceware generator in <paramref name="pool"/> with a wrapper, keeping its position.
		/// Returns null if Diceware isn't registered or is already wrapped.
		/// </summary>
		internal static MinQualityPwGenerator Install(CustomPwGeneratorPool pool, Func<long> minQualityBits)
		{
			CustomPwGenerator inner = pool.Find(DicewareUuid);
			if (inner == null || inner is MinQualityPwGenerator)
			{
				return null;
			}

			var wrapper = new MinQualityPwGenerator(inner, minQualityBits);
			pool.Add(wrapper); // Same UUID: replaces in place.
			return wrapper;
		}

		/// <summary>Puts the original generator back if this wrapper is still the registered one.</summary>
		internal void Uninstall(CustomPwGeneratorPool pool)
		{
			if (ReferenceEquals(pool.Find(Uuid), this))
			{
				pool.Add(_inner);
			}
		}
	}
}
