using System;
using System.Collections.Generic;
using System.Linq;

using KeePassLib;
using KeePassLib.Cryptography.PasswordGenerator;

namespace KeePassGeneratorMinQuality
{
	/// <summary>Keeps the generators wrapped in a pool in line with the configured targets.</summary>
	internal sealed class WrapperSet
	{
		private readonly CustomPwGeneratorPool _pool;
		private readonly Func<long> _minQualityBits;
		private readonly Dictionary<PwUuid, MinQualityPwGenerator> _wrappers = new();

		internal WrapperSet(CustomPwGeneratorPool pool, Func<long> minQualityBits)
		{
			_pool = pool;
			_minQualityBits = minQualityBits;
		}

		/// <summary>
		/// Wraps every target currently registered in the pool and restores generators that are no longer targeted.
		/// Safe to call repeatedly; targets that aren't registered yet are picked up by a later call.
		/// </summary>
		internal void Apply(IEnumerable<PwUuid> targets)
		{
			var wanted = new HashSet<PwUuid>(targets);

			foreach (KeyValuePair<PwUuid, MinQualityPwGenerator> stale in _wrappers.Where(w => !wanted.Contains(w.Key)).ToList())
			{
				stale.Value.Uninstall(_pool);
				_wrappers.Remove(stale.Key);
			}

			foreach (PwUuid target in wanted.Where(t => !_wrappers.ContainsKey(t)))
			{
				MinQualityPwGenerator wrapper = MinQualityPwGenerator.Install(_pool, target, _minQualityBits);
				if (wrapper != null)
				{
					_wrappers[target] = wrapper;
				}
			}
		}
	}
}
