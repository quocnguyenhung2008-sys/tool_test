using ModernSalesApp.Core;
using ModernSalesApp.Models;
using System.Drawing;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;

namespace ModernSalesApp.WinForms;

public sealed class CatalogControl : UserControl
{
    private readonly DataGridView _grid;
    private readonly TextBox _txtName;
    private readonly TextBox _txtWeight;
    private readonly TextBox _txtNote;
    private readonly Button _btnRefresh;
    private readonly Button _btnNew;
    private readonly Button _btnSave;
    private readonly Button _btnDelete;

    private readonly BindingList<CatalogRow> _rows = new();
    private long? _editingId;

    public CatalogControl()
    {
        var root = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.None,
            Panel1MinSize = 0,
            Panel2MinSize = 0
        };
        void UpdateSplitter()
        {
            if (root.Width <= 0)
            {
                return;
            }

            const int panel1Min = 520;
            const int panel2Min = 360;
            if (root.Width < panel1Min + panel2Min + root.SplitterWidth)
            {
                return;
            }

            var minSplitter = panel1Min;
            var maxSplitter = root.Width - panel2Min - root.SplitterWidth;
            if (maxSplitter < minSplitter)
            {
                return;
            }

            var desired = (int)(root.Width * 0.68);
            desired = Math.Max(minSplitter, Math.Min(desired, maxSplitter));
            if (desired >= minSplitter && desired <= maxSplitter)
            {
                root.SplitterDistance = desired;
                root.Panel1MinSize = panel1Min;
                root.Panel2MinSize = panel2Min;
            }
        }

        root.HandleCreated += (_, __) => UpdateSplitter();
        root.Resize += (_, __) => UpdateSplitter();

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };
        _grid.EnableHeadersVisualStyles = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle(_grid.ColumnHeadersDefaultCellStyle)
        {
            Font = new Font(_grid.Font, FontStyle.Bold)
        };
        _grid.RowTemplate.Height = 32;
        _grid.ColumnHeadersHeight = 36;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", DataPropertyName = nameof(CatalogRow.Id), AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Món hàng", DataPropertyName = nameof(CatalogRow.ItemName), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 38 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Trọng lượng (Chỉ)", DataPropertyName = nameof(CatalogRow.DefaultWeightChi), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 20 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ghi chú", DataPropertyName = nameof(CatalogRow.Note), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 42 });

        _grid.DataSource = _rows;
        _grid.SelectionChanged += (_, __) => OnSelectedChanged();

        var leftTop = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _btnRefresh = new Button { Text = "Tải lại", Width = 110, Height = 40 };
        _btnRefresh.Click += async (_, __) => await LoadAsync();
        leftTop.Controls.Add(_btnRefresh);

        root.Panel1.Controls.Add(_grid);
        root.Panel1.Controls.Add(leftTop);

        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(8)
        };
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        editor.Controls.Add(new Label { Text = "Tên món hàng" }, 0, 0);
        _txtName = new TextBox { Dock = DockStyle.Top };
        editor.Controls.Add(_txtName, 0, 1);

        editor.Controls.Add(new Label { Text = "Trọng lượng mặc định (Chỉ)" }, 0, 2);
        _txtWeight = new TextBox { Dock = DockStyle.Top };
        editor.Controls.Add(_txtWeight, 0, 3);

        editor.Controls.Add(new Label { Text = "Ghi chú" }, 0, 4);
        _txtNote = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
        editor.Controls.Add(_txtNote, 0, 5);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        _btnSave = new Button { Text = "Lưu", Width = 110, Height = 40 };
        _btnSave.UseVisualStyleBackColor = false;
        _btnSave.BackColor = Color.FromArgb(37, 99, 235);
        _btnSave.ForeColor = Color.White;
        _btnSave.FlatStyle = FlatStyle.Flat;
        _btnSave.Click += async (_, __) => await SaveAsync();
        _btnDelete = new Button { Text = "Xóa", Width = 110, Height = 40 };
        _btnDelete.UseVisualStyleBackColor = false;
        _btnDelete.BackColor = Color.FromArgb(220, 38, 38);
        _btnDelete.ForeColor = Color.White;
        _btnDelete.FlatStyle = FlatStyle.Flat;
        _btnDelete.Click += async (_, __) => await DeleteAsync();
        _btnNew = new Button { Text = "Mới", Width = 110, Height = 40 };
        _btnNew.Click += (_, __) => ClearEditor();

        buttons.Controls.Add(_btnSave);
        buttons.Controls.Add(_btnDelete);
        buttons.Controls.Add(_btnNew);

        root.Panel2.Controls.Add(editor);
        root.Panel2.Controls.Add(buttons);

        Controls.Add(root);

        Load += async (_, __) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            SetEnabled(false);
            var items = await AppServices.Catalog.GetAllAsync();
            _rows.Clear();
            foreach (var it in items)
            {
                _rows.Add(new CatalogRow(it.Id, it.ItemName, it.DefaultWeightChi, it.Note ?? ""));
            }
            ClearEditor();
        }
        catch (Exception ex)
        {
            AppServices.Logger.Error("CatalogControl.LoadAsync failed", ex);
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetEnabled(true);
        }
    }

    private void OnSelectedChanged()
    {
        if (_grid.CurrentRow?.DataBoundItem is not CatalogRow row)
        {
            return;
        }

        _editingId = row.Id;
        _txtName.Text = row.ItemName;
        _txtWeight.Text = row.DefaultWeightChi.ToString(CultureInfo.InvariantCulture);
        _txtNote.Text = row.Note ?? "";
    }

    private async Task SaveAsync()
    {
        var name = (_txtName.Text ?? "").Trim();
        var weightText = (_txtWeight.Text ?? "").Trim().Replace(",", ".");
        var note = (_txtNote.Text ?? "").Trim();

        if (name.Length == 0)
        {
            MessageBox.Show("Vui lòng nhập tên món hàng.", "Thiếu dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!double.TryParse(weightText, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight) || weight < 0)
        {
            MessageBox.Show("Trọng lượng không hợp lệ.", "Sai dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            SetEnabled(false);
            if (_editingId == null)
            {
                await AppServices.Catalog.CreateAsync(name, weight, note);
            }
            else
            {
                await AppServices.Catalog.UpdateAsync(_editingId.Value, name, weight, note);
            }

            await LoadAsync();
        }
        catch (Exception ex)
        {
            AppServices.Logger.Error("CatalogControl.SaveAsync failed", ex);
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetEnabled(true);
        }
    }

    private async Task DeleteAsync()
    {
        if (_editingId == null)
        {
            return;
        }

        if (MessageBox.Show($"Xóa món hàng ID {_editingId.Value}?\nHành động này không thể hoàn tác.", "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            SetEnabled(false);
            await AppServices.Catalog.DeleteAsync(_editingId.Value);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            AppServices.Logger.Error("CatalogControl.DeleteAsync failed", ex);
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetEnabled(true);
        }
    }

    private void ClearEditor()
    {
        _editingId = null;
        _txtName.Text = "";
        _txtWeight.Text = "0";
        _txtNote.Text = "";
        _txtName.Focus();
    }

    private void SetEnabled(bool enabled)
    {
        _grid.Enabled = enabled;
        _txtName.Enabled = enabled;
        _txtWeight.Enabled = enabled;
        _txtNote.Enabled = enabled;
        _btnRefresh.Enabled = enabled;
        _btnNew.Enabled = enabled;
        _btnSave.Enabled = enabled;
        _btnDelete.Enabled = enabled;
    }

    private sealed record CatalogRow(long Id, string ItemName, double DefaultWeightChi, string Note);
}
