using System;

using KeePass.Plugins;

namespace KeePassDicewareMinQuality
{
	/// <summary>
	/// KeePass plugin entry point. KeePass locates it by convention: the class must be named
	/// "&lt;AssemblyName&gt;Ext" and live in the namespace "&lt;AssemblyName&gt;".
	/// </summary>
	public sealed class KeePassDicewareMinQualityExt : Plugin
	{
		/// <summary>KeePass custom configuration key holding the minimum quality in bits.</summary>
		internal const string MinQualityBitsKey = "KeePassDicewareMinQuality_MinQualityBits";

		/// <summary>Used when the key is missing or not an integer. Matches the former fork's default.</summary>
		internal const long DefaultMinQualityBits = 90;

		private IPluginHost _host;
		private MinQualityPwGenerator _generator;

		public override string UpdateUrl =>
			"https://raw.githubusercontent.com/GTKYCU-IT/KeePassDicewareMinQuality/main/.meta/version.info";

		public override bool Initialize(IPluginHost host)
		{
			if (host == null)
			{
				return false;
			}

			_host = host;

			// KeePass doesn't order plugin loading, so Diceware may not be registered yet. FormLoadPost fires at the
			// end of main-window startup, after every plugin's Initialize.
			TryInstall();
			_host.MainWindow.FormLoadPost += OnFormLoadPost;
			return true;
		}

		public override void Terminate()
		{
			if (_host == null)
			{
				return;
			}

			_host.MainWindow.FormLoadPost -= OnFormLoadPost;
			_generator?.Uninstall(_host.PwGeneratorPool);
			_generator = null;
			_host = null;
		}

		private void OnFormLoadPost(object sender, EventArgs e) => TryInstall();

		private void TryInstall()
		{
			if (_generator != null)
			{
				return;
			}

			IPluginHost host = _host;
			_generator = MinQualityPwGenerator.Install(host.PwGeneratorPool,
				() => host.CustomConfig.GetLong(MinQualityBitsKey, DefaultMinQualityBits));
		}
	}
}
