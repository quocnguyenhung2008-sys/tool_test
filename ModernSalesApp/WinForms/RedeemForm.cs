using ModernSalesApp.Core;
using System.Drawing;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;

namespace ModernSalesApp.WinForms;

public sealed class RedeemForm : Form
{
    private readonly long _recordId;
    private readonly string _customerName;
    private readonly DataGridView _grid;
    private readonly Button _btnToggle;
    private readonly Label _lblStatus;

    public RedeemForm(long recordId, string customerName)
    {
        _recordId = recordId;
        _customerName = customerName;

        Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        Text = $"Chuộc - ID {_recordId} - {_customerName}";
        Width = 1040;
        Height = 620;
        StartPosition = FormStartPosition.CenterParent;

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

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID món", DataPropertyName = nameof(ItemRow.ItemId), AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SL", DataPropertyName = nameof(ItemRow.Qty), AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Món hàng", DataPropertyName = nameof(ItemRow.ItemName), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Trọng lượng (Chỉ)", DataPropertyName = nameof(ItemRow.WeightChi), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 14 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ghi chú", DataPropertyName = nameof(ItemRow.Note), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 26 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Chuộc", DataPropertyName = nameof(ItemRow.RedeemedText), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 10 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ngày chuộc", DataPropertyName = nameof(ItemRow.RedeemedAtText), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 20 });
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }
            var col = _grid.Columns[e.ColumnIndex];
            if (!string.Equals(col.DataPropertyName, nameof(ItemRow.RedeemedText), StringComparison.Ordinal))
            {
                return;
            }
            if (_grid.Rows[e.RowIndex].DataBoundItem is not ItemRow row)
            {
                return;
            }
            var baseStyle = e.CellStyle ?? new DataGridViewCellStyle();
            var style = new DataGridViewCellStyle(baseStyle)
            {
                ForeColor = row.IsRedeemed ? Color.FromArgb(22, 163, 74) : Color.FromArgb(220, 38, 38)
            };
            e.CellStyle = style;
        };
        _grid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var col = _grid.Columns[e.ColumnIndex];
            if (!string.Equals(col.DataPropertyName, nameof(ItemRow.RedeemedText), StringComparison.Ordinal))
            {
                return;
            }

            if (_grid.Rows[e.RowIndex].DataBoundItem is not ItemRow row)
            {
                return;
            }

            try
            {
                SetEnabled(false);
                await AppServices.Pawn.UpdateItemsRedeemedAsync(_recordId, new[] { (row.ItemId, !row.IsRedeemed) });
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                AppServices.Logger.Error("RedeemForm.ToggleSingleItemAsync failed", ex);
                MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetEnabled(true);
            }
        };

        _btnToggle = new Button { Text = "Đánh dấu: Đã chuộc", Width = 190, Height = 40 };
        _btnToggle.UseVisualStyleBackColor = false;
        _btnToggle.BackColor = Color.FromArgb(37, 99, 235);
        _btnToggle.ForeColor = Color.White;
        _btnToggle.FlatStyle = FlatStyle.Flat;
        _btnToggle.Click += async (_, __) => await ToggleAsync();

        _lblStatus = new Label { AutoSize = true, Text = "Trạng thái chuộc: ..." };

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        top.Controls.Add(_btnToggle);
        top.Controls.Add(new Label { Width = 16 });
        top.Controls.Add(_lblStatus);

        var btnOpenData = new Button { Text = "Mở thư mục dữ liệu", Width = 180, Height = 40 };
        btnOpenData.Click += (_, __) =>
        {
            try
            {
                var dbPath = AppPaths.EffectiveDatabasePath;
                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                AppServices.Logger.Error("RedeemForm.OpenDataFolder failed", ex);
                MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        top.Controls.Add(new Label { Width = 16 });
        top.Controls.Add(btnOpenData);

        Controls.Add(_grid);
        Controls.Add(top);

        Shown += async (_, __) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            SetEnabled(false);

            var items = await AppServices.Pawn.GetItemsByRecordIdAsync(_recordId);
            var rows = items.Select(it =>
            {
                var redeemedAtText = it.RedeemedAt?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";
                return new ItemRow(it.ItemId, it.Qty, it.ItemName, it.WeightChi, it.Note, it.IsRedeemed, redeemedAtText);
            }).ToList();

            _grid.DataSource = rows;

            var anyUnredeemed = rows.Any(x => !x.IsRedeemed);
            _lblStatus.Text = anyUnredeemed ? "Trạng thái chuộc: Chưa chuộc" : "Trạng thái chuộc: Đã chuộc";
            _lblStatus.ForeColor = anyUnredeemed ? Color.FromArgb(220, 38, 38) : Color.FromArgb(22, 163, 74);
            _btnToggle.Text = anyUnredeemed ? "Đánh dấu: Đã chuộc" : "Đánh dấu: Chưa chuộc";
        }
        catch (Exception ex)
        {
            AppServices.Logger.Error("RedeemForm.ReloadAsync failed", ex);
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetEnabled(true);
        }
    }

    private async Task ToggleAsync()
    {
        try
        {
            SetEnabled(false);

            var items = await AppServices.Pawn.GetItemsByRecordIdAsync(_recordId);
            var shouldRedeem = items.Any(x => !x.IsRedeemed);
            var updates = items.Select(x => (x.ItemId, shouldRedeem)).ToList();
            await AppServices.Pawn.UpdateItemsRedeemedAsync(_recordId, updates);

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            AppServices.Logger.Error("RedeemForm.ToggleAsync failed", ex);
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetEnabled(true);
        }
    }

    private void SetEnabled(bool enabled)
    {
        _grid.Enabled = enabled;
        _btnToggle.Enabled = enabled;
    }

    public sealed class ItemRow
    {
        public long ItemId { get; }
        public long Qty { get; }
        public string ItemName { get; }
        public double WeightChi { get; }
        public string Note { get; }
        public bool IsRedeemed { get; }
        public string RedeemedAtText { get; }

        public string RedeemedText => IsRedeemed ? "Đã chuộc" : "Chưa chuộc";

        public ItemRow(long itemId, long qty, string itemName, double weightChi, string note, bool isRedeemed, string redeemedAtText)
        {
            ItemId = itemId;
            Qty = qty;
            ItemName = itemName;
            WeightChi = weightChi;
            Note = note;
            IsRedeemed = isRedeemed;
            RedeemedAtText = redeemedAtText;
        }
    }
}
