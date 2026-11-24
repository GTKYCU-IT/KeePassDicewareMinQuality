using System;
using System.Drawing;
using System.Windows.Forms;

using KeePass.UI;

namespace KeePassDicewareMinQuality
{
	/// <summary>Tools menu dialog for viewing and, unless enforced, changing the minimum quality.</summary>
	internal sealed class MinQualityForm : Form
	{
		private const int TextWidth = 340;

		private readonly NumericUpDown _bitsInput;

		internal MinQualityForm(long currentBits, bool enforced)
		{
			Text = "Diceware Minimum Quality";
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
				Enabled = !enforced,
			};

			var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
			layout.Controls.Add(new Label { Text = "Minimum quality (bits):", AutoSize = true, Anchor = AnchorStyles.Left });
			layout.Controls.Add(_bitsInput);
			AddWideLabel(layout,
				"Diceware passwords are regenerated until KeePass's quality estimate reaches this value. " +
				"0 turns the check off.");
			if (enforced)
			{
				AddWideLabel(layout,
					"Set by your administrator in KeePass.config.enforced.xml. It can't be changed here.");
			}

			var buttons = new FlowLayoutPanel
			{
				AutoSize = true,
				FlowDirection = FlowDirection.RightToLeft,
				Dock = DockStyle.Fill,
				Margin = new Padding(0, 8, 0, 0),
			};
			var cancel = new Button { Text = enforced ? "Close" : "Cancel", DialogResult = DialogResult.Cancel };
			CancelButton = cancel;
			if (!enforced)
			{
				var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
				AcceptButton = ok;
				buttons.Controls.Add(cancel);
				buttons.Controls.Add(ok);
			}
			else
			{
				buttons.Controls.Add(cancel);
			}
			layout.Controls.Add(buttons);
			layout.SetColumnSpan(buttons, 2);

			Controls.Add(layout);
		}

		internal long MinQualityBits => (long)_bitsInput.Value;

		internal bool IsEditable => _bitsInput.Enabled;

		private static void AddWideLabel(TableLayoutPanel layout, string text)
		{
			var label = new Label
			{
				Text = text,
				AutoSize = true,
				MaximumSize = new Size(TextWidth, 0),
				Margin = new Padding(3, 8, 3, 0),
			};
			layout.Controls.Add(label);
			layout.SetColumnSpan(label, 2);
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
