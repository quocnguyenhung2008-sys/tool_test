using ModernSalesApp.Core;
using System.Drawing;
using System.Windows.Forms;

namespace ModernSalesApp.WinForms;

public sealed class MainForm : Form
{
    public MainForm()
    {
        Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        Text = $"Quản lý cầm cố - v{AppPaths.AppVersion}";
        Width = 1360;
        Height = 860;
        MinimumSize = new Size(1200, 760);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;

        var tabs = new TabControl { Dock = DockStyle.Fill };

        var tabPawn = new TabPage("Phiếu cầm") { Padding = new Padding(8) };
        tabPawn.Controls.Add(new PawnControl { Dock = DockStyle.Fill });

        var tabCatalog = new TabPage("Danh mục hàng") { Padding = new Padding(8) };
        tabCatalog.Controls.Add(new CatalogControl { Dock = DockStyle.Fill });

        tabs.TabPages.Add(tabPawn);
        tabs.TabPages.Add(tabCatalog);

        Controls.Add(tabs);
    }
}
