using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

using KeePass.App.Configuration;
using KeePass.Plugins;
using KeePass.UI;

using KeePassLib;
using KeePassLib.Utility;

namespace KeePassGeneratorMinQuality
{
	/// <summary>
	/// KeePass plugin entry point. KeePass locates it by convention: the class must be named
	/// "&lt;AssemblyName&gt;Ext" and live in the namespace "&lt;AssemblyName&gt;".
	/// </summary>
	public sealed class KeePassGeneratorMinQualityExt : Plugin
	{
		private IPluginHost _host;
		private WrapperSet _wrappers;

		public override string UpdateUrl =>
			"https://raw.githubusercontent.com/GTKYCU-IT/KeePassGeneratorMinQuality/main/.meta/version.info";

		public override bool Initialize(IPluginHost host)
		{
			if (host == null)
			{
				return false;
			}

			_host = host;
			AceCustomConfig config = host.CustomConfig;
			_wrappers = new WrapperSet(host.PwGeneratorPool, () => MinQualitySetting.ReadBits(config));

			// KeePass doesn't order plugin loading, so target generators may not be registered yet. FormLoadPost fires
			// at the end of main-window startup, after every plugin's Initialize.
			ApplyTargets();
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
			_wrappers.Apply(Enumerable.Empty<PwUuid>());
			_wrappers = null;
			_host = null;
		}

		public override ToolStripMenuItem GetMenuItem(PluginMenuType t)
		{
			if (t != PluginMenuType.Main)
			{
				return null;
			}

			var item = new ToolStripMenuItem("Generator Minimum Quality...");
			item.Click += (sender, e) => ShowSettings();
			return item;
		}

		private void OnFormLoadPost(object sender, EventArgs e)
		{
			GeneratorTargets targets = ApplyTargets();
			if (!targets.IsValid)
			{
				MessageService.ShowWarning(MinQualitySetting.InvalidGeneratorsWarning(targets));
			}
		}

		private GeneratorTargets ApplyTargets()
		{
			GeneratorTargets targets = MinQualitySetting.ReadGenerators(_host.CustomConfig);
			_wrappers.Apply(MinQualitySetting.EffectiveTargets(targets, _host.PwGeneratorPool));
			return targets;
		}

		private void ShowSettings()
		{
			AceCustomConfig config = _host.CustomConfig;
			XmlDocument enforced = AppConfigSerializer.EnforcedConfigXml;
			GeneratorTargets targets = MinQualitySetting.ReadGenerators(config);
			List<PwUuid> effective = MinQualitySetting.EffectiveTargets(targets, _host.PwGeneratorPool);

			// Wrappers report the wrapped generator's UUID and name, so the pool lists every plugin generator once.
			var choices = _host.PwGeneratorPool
				.Select(g => new GeneratorChoice(g.Uuid, g.Name, effective.Contains(g.Uuid)))
				.ToList();
			choices.AddRange(targets.Uuids
				.Where(t => choices.All(c => !c.Uuid.Equals(t)))
				.Select(t => new GeneratorChoice(t, null, true)));

			using var form = new MinQualityForm(
				MinQualitySetting.ReadBits(config), MinQualitySetting.IsEnforced(enforced, MinQualitySetting.BitsKey),
				choices, MinQualitySetting.IsEnforced(enforced, MinQualitySetting.GeneratorsKey),
				targets.IsValid ? null : MinQualitySetting.InvalidGeneratorsWarning(targets));
			if (form.ShowDialog(GlobalWindowManager.TopWindow) != DialogResult.OK)
			{
				return;
			}

			// Takes effect on the next generated password; KeePass saves its configuration on exit.
			if (form.BitsEditable)
			{
				MinQualitySetting.WriteBits(config, form.MinQualityBits);
			}
			if (form.GeneratorsEditable)
			{
				MinQualitySetting.WriteGenerators(config, form.SelectedGenerators);
				ApplyTargets();
			}
		}
	}
}
