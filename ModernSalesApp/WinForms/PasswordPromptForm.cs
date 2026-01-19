using System.Drawing;
using System.Windows.Forms;

namespace ModernSalesApp.WinForms;

public sealed class PasswordPromptForm : Form
{
    private readonly TextBox _txt;

    public string Password => _txt.Text;

    public PasswordPromptForm()
    {
        Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        Text = "Xác thực xuất Excel";
        Width = 420;
        Height = 200;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        var lbl = new Label { Text = "Nhập mật khẩu", Dock = DockStyle.Top, Height = 28 };
        _txt = new TextBox { Dock = DockStyle.Top, UseSystemPasswordChar = true, Height = 32 };

        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };

        var btnOk = new Button { Text = "OK", Width = 110, Height = 40, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Hủy", Width = 110, Height = 40, DialogResult = DialogResult.Cancel };
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        pnlButtons.Controls.Add(btnOk);
        pnlButtons.Controls.Add(btnCancel);

        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        root.Controls.Add(_txt);
        root.Controls.Add(lbl);

        Controls.Add(root);
        Controls.Add(pnlButtons);
    }
}
