using System;
using System.Windows.Forms;

using KeePass.App.Configuration;
using KeePass.Plugins;
using KeePass.UI;

namespace KeePassDicewareMinQuality
{
	/// <summary>
	/// KeePass plugin entry point. KeePass locates it by convention: the class must be named
	/// "&lt;AssemblyName&gt;Ext" and live in the namespace "&lt;AssemblyName&gt;".
	/// </summary>
	public sealed class KeePassDicewareMinQualityExt : Plugin
	{
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

		public override ToolStripMenuItem GetMenuItem(PluginMenuType t)
		{
			if (t != PluginMenuType.Main)
			{
				return null;
			}

			var item = new ToolStripMenuItem("Diceware Minimum Quality...");
			item.Click += (sender, e) => ShowSettings();
			return item;
		}

		private void ShowSettings()
		{
			AceCustomConfig config = _host.CustomConfig;
			using var form = new MinQualityForm(MinQualitySetting.Read(config),
				MinQualitySetting.IsEnforced(AppConfigSerializer.EnforcedConfigXml));
			if (form.ShowDialog(GlobalWindowManager.TopWindow) == DialogResult.OK && form.IsEditable)
			{
				// Takes effect on the next generated password; KeePass saves its configuration on exit.
				MinQualitySetting.Write(config, form.MinQualityBits);
			}
		}

		private void TryInstall()
		{
			if (_generator != null)
			{
				return;
			}

			IPluginHost host = _host;
			_generator = MinQualityPwGenerator.Install(host.PwGeneratorPool, () => MinQualitySetting.Read(host.CustomConfig));
		}
	}
}
