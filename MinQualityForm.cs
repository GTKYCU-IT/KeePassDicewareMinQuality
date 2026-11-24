using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using KeePass.UI;

using KeePassLib;

namespace KeePassGeneratorMinQuality
{
	/// <summary>A generator the dialog can target. <see cref="Name"/> is null when it isn't installed.</summary>
	internal sealed class GeneratorChoice
	{
		internal GeneratorChoice(PwUuid uuid, string name, bool selected)
		{
			Uuid = uuid;
			Name = name;
			Selected = selected;
		}

		internal PwUuid Uuid { get; }

		internal string Name { get; }

		internal bool Selected { get; }

		public override string ToString() =>
			$"{Name ?? "Not installed"} ({MinQualitySetting.ToBase64(Uuid)})";
	}

	/// <summary>Tools menu dialog for viewing and, unless enforced, changing the minimum and target generators.</summary>
	internal sealed class MinQualityForm : Form
	{
		private const int TextWidth = 380;
		private const string EnforcedNote = "Set by your administrator in KeePass.config.enforced.xml. It can't be changed here.";

		private readonly NumericUpDown _bitsInput;
		private readonly CheckedListBox _generatorList;

		internal MinQualityForm(long currentBits, bool bitsEnforced, IList<GeneratorChoice> generators, bool generatorsEnforced)
		{
			Text = "Generator Minimum Quality";
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.CenterParent;
			AutoScaleMode = AutoScaleMode.Font;
			AutoSize = true;
			AutoSizeMode = AutoSizeMode.GrowAndShrink;
			Padding = new Padding(12);

			_bitsInput = new NumericUpDown
			{
				// Widen the range to the current value: an enforced or hand-edited value outside the usual range
				// must display, not throw.
				Minimum = Math.Min(0, currentBits),
				Maximum = Math.Max(1000, currentBits),
				Value = currentBits,
				Width = 80,
				Enabled = !bitsEnforced,
			};

			_generatorList = new CheckedListBox
			{
				CheckOnClick = true,
				IntegralHeight = false,
				Width = TextWidth,
				Enabled = !generatorsEnforced,
			};
			foreach (GeneratorChoice choice in generators)
			{
				_generatorList.Items.Add(choice, choice.Selected);
			}
			_generatorList.Height = _generatorList.ItemHeight * Math.Max(3, Math.Min(generators.Count, 8)) + 4;

			var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
			layout.Controls.Add(new Label { Text = "Minimum quality (bits):", AutoSize = true, Anchor = AnchorStyles.Left });
			layout.Controls.Add(_bitsInput);
			AddWide(layout, WrappedLabel(
				"Passwords from the selected generators are regenerated until KeePass's quality estimate reaches " +
				"this value. 0 turns the check off."));
			if (bitsEnforced)
			{
				AddWide(layout, WrappedLabel(EnforcedNote));
			}

			AddWide(layout, WrappedLabel("Apply to these generators:", top: 12));
			AddWide(layout, _generatorList);
			if (generatorsEnforced)
			{
				AddWide(layout, WrappedLabel(EnforcedNote));
			}
			AddWide(layout, WrappedLabel(
				"Only generators added by plugins are listed. KeePass's built-in character set and pattern " +
				"generators can't be selected."));

			var buttons = new FlowLayoutPanel
			{
				AutoSize = true,
				FlowDirection = FlowDirection.RightToLeft,
				Dock = DockStyle.Fill,
				Margin = new Padding(0, 8, 0, 0),
			};
			bool editable = BitsEditable || GeneratorsEditable;
			var cancel = new Button { Text = editable ? "Cancel" : "Close", DialogResult = DialogResult.Cancel };
			CancelButton = cancel;
			buttons.Controls.Add(cancel);
			if (editable)
			{
				var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
				AcceptButton = ok;
				buttons.Controls.Add(ok);
			}
			AddWide(layout, buttons);

			Controls.Add(layout);
		}

		internal long MinQualityBits => (long)_bitsInput.Value;

		internal bool BitsEditable => _bitsInput.Enabled;

		internal bool GeneratorsEditable => _generatorList.Enabled;

		internal IEnumerable<PwUuid> SelectedGenerators =>
			_generatorList.CheckedItems.Cast<GeneratorChoice>().Select(c => c.Uuid).ToList();

		private static Label WrappedLabel(string text, int top = 8) => new Label
		{
			Text = text,
			AutoSize = true,
			MaximumSize = new Size(TextWidth, 0),
			Margin = new Padding(3, top, 3, 0),
		};

		private static void AddWide(TableLayoutPanel layout, Control control)
		{
			layout.Controls.Add(control);
			layout.SetColumnSpan(control, 2);
		}

		protected override void OnLoad(EventArgs e)
		{
			GlobalWindowManager.AddWindow(this);
			base.OnLoad(e);
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			GlobalWindowManager.RemoveWindow(this);
			base.OnFormClosed(e);
		}
	}
}
