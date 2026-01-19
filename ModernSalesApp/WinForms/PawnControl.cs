using ModernSalesApp.Core;
using ModernSalesApp.Data.Repositories;
using ModernSalesApp.Models;
using ModernSalesApp.UI;
using System.Drawing;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;

namespace ModernSalesApp.WinForms;

public sealed class PawnControl : UserControl
{
    private const string ExportExcelPassword = "197781";

    private readonly TextBox _txtCustomerName;
    private readonly TextBox _txtCccd;
    private readonly TextBox _txtTotalAmount;
    private readonly DateTimePicker _dtPawn;
    private readonly ComboBox _cmbCatalogQuick;
    private readonly Button _btnAddItem;
    private readonly Button _btnRemoveItem;
    private readonly Button _btnAddFromCatalog;
    private readonly Button _btnSaveRecord;
    private readonly TextBox _txtRecordNote;
    private readonly DataGridView _gridEntryItems;

    private readonly ComboBox _cmbSearchField;
    private readonly TextBox _txtSearch;
    private readonly DateTimePicker _dtFrom;
    private readonly DateTimePicker _dtTo;
    private readonly Button _btnApply;
    private readonly Button _btnExport;
    private readonly Button _btnOpenData;
    private readonly DataGridView _gridRecords;
    private readonly Button _btnPrev;
    private readonly Button _btnNext;
    private readonly Label _lblPage;
    private readonly Button _btnDeleteRecord;

    private readonly BindingList<EntryItemRow> _entryItems = new();
    private readonly BindingList<RecordRow> _recordRows = new();

    private readonly Dictionary<long, string> _recordNoteBeforeEdit = new();
    private List<PawnCatalogItem> _catalogItems = new();
    private int _pageIndex;
    private int _totalCount;
    private int _pageSize = 10;
    private bool _loadingPage;
    private readonly System.Windows.Forms.Timer _pageSizeTimer;
    private bool _loaded;
    private bool _formattingTotalAmount;

    public PawnControl()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var grpEntry = new GroupBox { Text = "Tạo phiếu cầm", Dock = DockStyle.Fill, Padding = new Padding(8) };
        root.Controls.Add(grpEntry, 0, 0);

        var entryLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        entryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        entryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        entryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grpEntry.Controls.Add(entryLayout);

        var entryTop = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 2
        };
        entryTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        entryTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        entryTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        entryTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        entryTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        entryTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        entryTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        entryTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        entryTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        entryTop.Controls.Add(new Label { Text = "Tên khách hàng", Dock = DockStyle.Fill }, 0, 0);
        _txtCustomerName = new TextBox { Dock = DockStyle.Fill };
        _txtCustomerName.Leave += (_, __) => _txtCustomerName.Text = ToTitleCaseName(_txtCustomerName.Text);
        entryTop.Controls.Add(_txtCustomerName, 0, 1);

        entryTop.Controls.Add(new Label { Text = "Số CCCD", Dock = DockStyle.Fill }, 2, 0);
        _txtCccd = new TextBox { Dock = DockStyle.Fill };
        entryTop.Controls.Add(_txtCccd, 2, 1);

        entryTop.Controls.Add(new Label { Text = "Tổng tiền cầm (VNĐ)", Dock = DockStyle.Fill }, 4, 0);
        _txtTotalAmount = new TextBox { Dock = DockStyle.Fill };
        _txtTotalAmount.TextChanged += (_, __) => FormatTotalAmount();
        entryTop.Controls.Add(_txtTotalAmount, 4, 1);

        entryTop.Controls.Add(new Label { Text = "Ngày cầm", Dock = DockStyle.Fill }, 6, 0);
        _dtPawn = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short };
        entryTop.Controls.Add(_dtPawn, 6, 1);

        entryLayout.Controls.Add(entryTop, 0, 0);

        _gridEntryItems = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };
        _gridEntryItems.EnableHeadersVisualStyles = false;
        _gridEntryItems.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridEntryItems.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle(_gridEntryItems.ColumnHeadersDefaultCellStyle)
        {
            Font = new Font(_gridEntryItems.Font, FontStyle.Bold)
        };
        _gridEntryItems.RowTemplate.Height = 32;
        _gridEntryItems.ColumnHeadersHeight = 36;
        _gridEntryItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SL", DataPropertyName = nameof(EntryItemRow.Qty), AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells });
        _gridEntryItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Món hàng", DataPropertyName = nameof(EntryItemRow.ItemName), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 52 });
        _gridEntryItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Trọng lượng (Chỉ)", DataPropertyName = nameof(EntryItemRow.WeightChi), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 18 });
        _gridEntryItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ghi chú", DataPropertyName = nameof(EntryItemRow.Note), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30 });
        _gridEntryItems.DataSource = _entryItems;

        var entryButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _btnAddItem = new Button { Text = "Thêm món", Width = 120, Height = 40 };
        _btnAddItem.Click += (_, __) => _entryItems.Add(new EntryItemRow());
        _btnRemoveItem = new Button { Text = "Xóa món", Width = 120, Height = 40 };
        _btnRemoveItem.Click += (_, __) =>
        {
            if (_gridEntryItems.CurrentRow?.DataBoundItem is EntryItemRow row)
            {
                _entryItems.Remove(row);
            }
            if (_entryItems.Count == 0)
            {
                _entryItems.Add(new EntryItemRow());
            }
        };

        _cmbCatalogQuick = new ComboBox { Width = 420, DropDownStyle = ComboBoxStyle.DropDown };
        _cmbCatalogQuick.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _cmbCatalogQuick.AutoCompleteSource = AutoCompleteSource.ListItems;
        _btnAddFromCatalog = new Button { Text = "Chọn nhanh", Width = 130, Height = 40 };
        _btnAddFromCatalog.Click += (_, __) =>
        {
            if (_cmbCatalogQuick.SelectedItem is not PawnCatalogItem cat)
            {
                return;
            }
            _entryItems.Add(new EntryItemRow
            {
                Qty = "1",
                ItemName = cat.ItemName,
                WeightChi = cat.DefaultWeightChi.ToString(CultureInfo.InvariantCulture),
                Note = cat.Note ?? ""
            });
        };

        _btnSaveRecord = new Button { Text = "Lưu phiếu", Width = 130, Height = 40 };
        _btnSaveRecord.UseVisualStyleBackColor = false;
        _btnSaveRecord.BackColor = Color.FromArgb(37, 99, 235);
        _btnSaveRecord.ForeColor = Color.White;
        _btnSaveRecord.FlatStyle = FlatStyle.Flat;
        _btnSaveRecord.Click += async (_, __) => await SaveRecordAsync();

        entryButtons.Controls.Add(_btnAddItem);
        entryButtons.Controls.Add(_btnRemoveItem);
        entryButtons.Controls.Add(_cmbCatalogQuick);
        entryButtons.Controls.Add(_btnAddFromCatalog);
        entryButtons.Controls.Add(_btnSaveRecord);

        entryLayout.Controls.Add(entryButtons, 0, 1);

        var entryBottom = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.None,
            Panel1MinSize = 0,
            Panel2MinSize = 0
        };
        void UpdateEntryBottomSplitter()
        {
            if (entryBottom.Width <= 0)
            {
                return;
            }

            const int panel1Min = 420;
            const int panel2Min = 240;
            if (entryBottom.Width < panel1Min + panel2Min + entryBottom.SplitterWidth)
            {
                return;
            }

            var minSplitter = panel1Min;
            var maxSplitter = entryBottom.Width - panel2Min - entryBottom.SplitterWidth;
            if (maxSplitter < minSplitter)
            {
                return;
            }

            var desired = (int)(entryBottom.Width * 0.68);
            desired = Math.Max(minSplitter, Math.Min(desired, maxSplitter));
            if (desired >= minSplitter && desired <= maxSplitter)
            {
                entryBottom.SplitterDistance = desired;
                entryBottom.Panel1MinSize = panel1Min;
                entryBottom.Panel2MinSize = panel2Min;
            }
        }

        entryBottom.HandleCreated += (_, __) => UpdateEntryBottomSplitter();
        entryBottom.Resize += (_, __) => UpdateEntryBottomSplitter();

        var notePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        notePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        notePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        notePanel.Controls.Add(new Label { Text = "Ghi chú phiếu", Dock = DockStyle.Fill }, 0, 0);
        _txtRecordNote = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
        notePanel.Controls.Add(_txtRecordNote, 0, 1);
        entryBottom.Panel2.Controls.Add(notePanel);

        entryBottom.Panel1.Controls.Add(_gridEntryItems);

        entryLayout.Controls.Add(entryBottom, 0, 2);

        var searchPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        _cmbSearchField = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        _cmbSearchField.Items.AddRange(new object[] { "Tên", "CCCD", "Món hàng", "Số tiền" });
        _cmbSearchField.SelectedIndex = 0;
        _txtSearch = new TextBox { Width = 220 };
        _txtSearch.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await LoadPageAsync(0);
            }
        };

        _dtFrom = new DateTimePicker { Width = 160, Format = DateTimePickerFormat.Short };
        _dtTo = new DateTimePicker { Width = 160, Format = DateTimePickerFormat.Short };
        _btnApply = new Button { Text = "Tìm", Width = 90, Height = 40 };
        _btnApply.Click += async (_, __) => await LoadPageAsync(0);
        _btnExport = new Button { Text = "Xuất Excel", Width = 120, Height = 40 };
        _btnExport.UseVisualStyleBackColor = false;
        _btnExport.BackColor = Color.FromArgb(22, 163, 74);
        _btnExport.ForeColor = Color.White;
        _btnExport.FlatStyle = FlatStyle.Flat;
        _btnExport.Click += async (_, __) => await ExportAsync();
        _btnOpenData = new Button { Text = "Mở thư mục dữ liệu", Width = 180, Height = 40 };
        _btnOpenData.Click += (_, __) => OpenDataFolder();

        searchPanel.Controls.Add(_cmbSearchField);
        searchPanel.Controls.Add(_txtSearch);
        searchPanel.Controls.Add(new Label { Text = "Từ", AutoSize = true, Padding = new Padding(8, 8, 0, 0) });
        searchPanel.Controls.Add(_dtFrom);
        searchPanel.Controls.Add(new Label { Text = "Đến", AutoSize = true, Padding = new Padding(8, 8, 0, 0) });
        searchPanel.Controls.Add(_dtTo);
        searchPanel.Controls.Add(_btnApply);
        searchPanel.Controls.Add(_btnExport);
        searchPanel.Controls.Add(_btnOpenData);

        root.Controls.Add(searchPanel, 0, 1);

        _gridRecords = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };
        _gridRecords.EnableHeadersVisualStyles = false;
        _gridRecords.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridRecords.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle(_gridRecords.ColumnHeadersDefaultCellStyle)
        {
            Font = new Font(_gridRecords.Font, FontStyle.Bold)
        };
        _gridRecords.RowTemplate.Height = 32;
        _gridRecords.ColumnHeadersHeight = 36;
        _gridRecords.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", DataPropertyName = nameof(RecordRow.Id), ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells });
        _gridRecords.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Khách", DataPropertyName = nameof(RecordRow.CustomerName), ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 16 });
        _gridRecords.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "CCCD", DataPropertyName = nameof(RecordRow.Cccd), ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 11 });
        _gridRecords.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Món hàng", DataPropertyName = nameof(RecordRow.ItemsSummary), ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 26 });
        _gridRecords.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tổng tiền", DataPropertyName = nameof(RecordRow.TotalText), ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 9 });
        _gridRecords.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Chuộc", DataPropertyName = nameof(RecordRow.RedeemedText), ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 7 });
        _gridRecords.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ngày cầm", DataPropertyName = nameof(RecordRow.DatePawnText), ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 8 });
        _gridRecords.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ghi chú", DataPropertyName = nameof(RecordRow.RecordNote), ReadOnly = false, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 14 });
        _gridRecords.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ngày tạo", DataPropertyName = nameof(RecordRow.CreatedAtText), ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 9 });
        _gridRecords.DataSource = _recordRows;
        _gridRecords.CellBeginEdit += (_, e) => OnRecordCellBeginEdit(e.RowIndex, e.ColumnIndex);
        _gridRecords.CellEndEdit += async (_, e) => await OnRecordCellEndEditAsync(e.RowIndex, e.ColumnIndex);
        _gridRecords.CellFormatting += (_, e) => OnRecordsCellFormatting(e.RowIndex, e.ColumnIndex, e);
        _gridRecords.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            if (_gridRecords.Rows[e.RowIndex].DataBoundItem is not RecordRow row)
            {
                return;
            }
            var col = e.ColumnIndex < 0 ? null : _gridRecords.Columns[e.ColumnIndex];
            if (col != null && string.Equals(col.DataPropertyName, nameof(RecordRow.RedeemedText), StringComparison.Ordinal))
            {
                try
                {
                    SetListEnabled(false);
                    var items = await AppServices.Pawn.GetItemsByRecordIdAsync(row.Id);
                    var shouldRedeem = items.Any(x => !x.IsRedeemed);
                    var updates = items.Select(x => (x.ItemId, shouldRedeem)).ToList();
                    await AppServices.Pawn.UpdateItemsRedeemedAsync(row.Id, updates);
                    await LoadPageAsync(_pageIndex);
                }
                catch (Exception ex)
                {
                    AppServices.Logger.Error("PawnControl.ToggleRedeemFromList failed", ex);
                    MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    SetListEnabled(true);
                }

                return;
            }

            using var dlg = new RedeemForm(row.Id, row.CustomerName);
            dlg.ShowDialog(FindForm());
            await LoadPageAsync(_pageIndex);
        };

        root.Controls.Add(_gridRecords, 0, 2);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _btnPrev = new Button { Text = "Trước", Width = 100, Height = 40 };
        _btnPrev.Click += async (_, __) => await LoadPageAsync(Math.Max(0, _pageIndex - 1));
        _btnNext = new Button { Text = "Sau", Width = 100, Height = 40 };
        _btnNext.Click += async (_, __) => await LoadPageAsync(_pageIndex + 1);
        _lblPage = new Label { AutoSize = true, Padding = new Padding(12, 10, 0, 0) };
        _btnDeleteRecord = new Button { Text = "Xóa phiếu", Width = 130, Height = 40 };
        _btnDeleteRecord.UseVisualStyleBackColor = false;
        _btnDeleteRecord.BackColor = Color.FromArgb(220, 38, 38);
        _btnDeleteRecord.ForeColor = Color.White;
        _btnDeleteRecord.FlatStyle = FlatStyle.Flat;
        _btnDeleteRecord.Click += async (_, __) => await DeleteSelectedRecordAsync();

        bottom.Controls.Add(_btnPrev);
        bottom.Controls.Add(_btnNext);
        bottom.Controls.Add(_lblPage);
        bottom.Controls.Add(new Label { Width = 30 });
        bottom.Controls.Add(_btnDeleteRecord);

        root.Controls.Add(bottom, 0, 3);

        Controls.Add(root);

        _pageSizeTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _pageSizeTimer.Tick += async (_, __) =>
        {
            _pageSizeTimer.Stop();
            if (!_loaded)
            {
                return;
            }

            if (_loadingPage)
            {
                _pageSizeTimer.Start();
                return;
            }

            var newSize = CalculatePageSize();
            if (newSize == _pageSize)
            {
                return;
            }

            _pageSize = newSize;
            await LoadPageAsync(0);
        };

        SizeChanged += (_, __) => SchedulePageSizeRecalc();
        _gridRecords.SizeChanged += (_, __) => SchedulePageSizeRecalc();

        Load += async (_, __) =>
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;

            _pageSize = CalculatePageSize();
            _dtPawn.Value = DateTime.Today;
            _dtTo.Value = DateTime.Today;
            _dtFrom.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            _entryItems.Add(new EntryItemRow());
            var loadPageTask = LoadPageAsync(0);
            var loadCatalogTask = LoadCatalogAsync();
            await loadPageTask;
            await loadCatalogTask;
        };
    }

    private int CalculatePageSize()
    {
        var rowHeight = _gridRecords.RowTemplate.Height <= 0 ? 32 : _gridRecords.RowTemplate.Height;
        var headerHeight = _gridRecords.ColumnHeadersVisible ? _gridRecords.ColumnHeadersHeight : 0;
        var available = _gridRecords.ClientSize.Height - headerHeight;
        var approx = available <= 0 ? 0 : available / rowHeight;
        return Math.Clamp(approx, 10, 200);
    }

    private void SchedulePageSizeRecalc()
    {
        if (!_loaded)
        {
            return;
        }

        _pageSizeTimer.Stop();
        _pageSizeTimer.Start();
    }

    private async Task LoadCatalogAsync()
    {
        try
        {
            var items = await AppServices.Catalog.GetAllAsync();
            _catalogItems = items.ToList();
            _cmbCatalogQuick.DataSource = _catalogItems;
            _cmbCatalogQuick.DisplayMember = nameof(PawnCatalogItem.ItemName);
            _cmbCatalogQuick.ValueMember = nameof(PawnCatalogItem.Id);
        }
        catch (Exception ex)
        {
            AppServices.Logger.Error("PawnControl.LoadCatalogAsync failed", ex);
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SaveRecordAsync()
    {
        var name = ToTitleCaseName(_txtCustomerName.Text);
        _txtCustomerName.Text = name;
        var cccd = (_txtCccd.Text ?? "").Trim();
        var totalText = (_txtTotalAmount.Text ?? "").Trim();
        var datePawn = DateOnly.FromDateTime(_dtPawn.Value.Date);
        var recordNote = (_txtRecordNote.Text ?? "").Trim();

        if (name.Length == 0 || cccd.Length == 0)
        {
            MessageBox.Show("Vui lòng nhập tên khách hàng và CCCD.", "Thiếu dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!InputParsers.TryParseMoneyVnd(totalText, out var total))
        {
            MessageBox.Show("Tổng tiền không hợp lệ. Ví dụ: 15000 hoặc 15k.", "Sai định dạng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var items = new List<PawnItemInput>();
        foreach (var row in _entryItems.ToList())
        {
            var itemName = (row.ItemName ?? "").Trim();
            if (itemName.Length == 0)
            {
                continue;
            }

            if (!long.TryParse((row.Qty ?? "").Trim(), out var qty) || qty <= 0)
            {
                MessageBox.Show("Số lượng không hợp lệ.", "Sai dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var weightText = (row.WeightChi ?? "").Trim().Replace(",", ".");
            if (!double.TryParse(weightText, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight) || weight < 0)
            {
                MessageBox.Show("Trọng lượng không hợp lệ.", "Sai dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var note = (row.Note ?? "").Trim();
            items.Add(new PawnItemInput(qty, itemName, weight, note));
        }

        if (items.Count == 0)
        {
            MessageBox.Show("Vui lòng nhập ít nhất 1 món hàng cầm.", "Thiếu dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var focusNameAfterSave = false;
        try
        {
            SetEntryEnabled(false);
            await AppServices.Pawn.CreateRecordAsync(name, cccd, total, datePawn, recordNote, items);
            ClearEntry();
            await LoadPageAsync(0);
            focusNameAfterSave = true;
        }
        catch (Exception ex)
        {
            AppServices.Logger.Error("PawnControl.SaveRecordAsync failed", ex);
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetEntryEnabled(true);
            if (focusNameAfterSave)
            {
                _txtCustomerName.Focus();
                _txtCustomerName.SelectAll();
            }
        }
    }

    private void ClearEntry()
    {
        _txtCustomerName.Text = "";
        _txtCccd.Text = "";
        _txtTotalAmount.Text = "";
        _dtPawn.Value = DateTime.Today;
        _txtRecordNote.Text = "";
        _entryItems.Clear();
        _entryItems.Add(new EntryItemRow());
        _txtCustomerName.Focus();
    }

    private PawnRepository.PawnFilter BuildFilter()
    {
        var from = DateOnly.FromDateTime(_dtFrom.Value.Date);
        var to = DateOnly.FromDateTime(_dtTo.Value.Date);
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var field = _cmbSearchField.SelectedIndex switch
        {
            1 => "cccd",
            2 => "item",
            3 => "amount",
            _ => "name"
        };

        return new PawnRepository.PawnFilter(_txtSearch.Text ?? "", field, from, to);
    }

    private async Task LoadPageAsync(int newPageIndex)
    {
        try
        {
            if (_loadingPage)
            {
                return;
            }
            _loadingPage = true;
            SetListEnabled(false);

            var filter = BuildFilter();
            var effectivePageIndex = newPageIndex;
            var page = await AppServices.Pawn.GetRecordsPageAsync(filter, effectivePageIndex, _pageSize);

            _totalCount = page.TotalCount;

            if (effectivePageIndex > 0 && _totalCount > 0 && page.Items.Count == 0)
            {
                var last = Math.Max(0, (_totalCount - 1) / _pageSize);
                if (last != effectivePageIndex)
                {
                    effectivePageIndex = last;
                    page = await AppServices.Pawn.GetRecordsPageAsync(filter, effectivePageIndex, _pageSize);
                }
            }
            _pageIndex = effectivePageIndex;

            _recordRows.Clear();
            foreach (var r in page.Items)
            {
                _recordRows.Add(new RecordRow
                {
                    Id = r.Id,
                    CustomerName = r.CustomerName,
                    Cccd = r.Cccd,
                    RecordNote = r.RecordNote ?? "",
                    ItemsSummary = r.ItemsSummary,
                    TotalAmountVnd = r.TotalAmountVnd,
                    ItemCount = r.ItemCount,
                    RedeemedCount = r.RedeemedCount,
                    DatePawnText = r.DatePawn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    CreatedAtText = r.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                });
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling(_totalCount / (double)_pageSize));
            _lblPage.Text = $"Trang {_pageIndex + 1}/{totalPages} - Tổng {_totalCount} - {_pageSize}/trang";

            _btnPrev.Enabled = _pageIndex > 0;
            _btnNext.Enabled = (_pageIndex + 1) * _pageSize < _totalCount;
        }
        catch (Exception ex)
        {
            AppServices.Logger.Error("PawnControl.LoadPageAsync failed", ex);
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetListEnabled(true);
            _loadingPage = false;
        }
    }

    private void OnRecordCellBeginEdit(int rowIndex, int colIndex)
    {
        if (rowIndex < 0 || colIndex < 0)
        {
            return;
        }

        var col = _gridRecords.Columns[colIndex];
        if (!string.Equals(col.DataPropertyName, nameof(RecordRow.RecordNote), StringComparison.Ordinal))
        {
            return;
        }

        if (_gridRecords.Rows[rowIndex].DataBoundItem is not RecordRow row)
        {
            return;
        }

        _recordNoteBeforeEdit[row.Id] = row.RecordNote ?? "";
    }

    private void OnRecordsCellFormatting(int rowIndex, int colIndex, DataGridViewCellFormattingEventArgs e)
    {
        if (rowIndex < 0 || colIndex < 0)
        {
            return;
        }

        var col = _gridRecords.Columns[colIndex];
        if (!string.Equals(col.DataPropertyName, nameof(RecordRow.RedeemedText), StringComparison.Ordinal))
        {
            return;
        }

        if (_gridRecords.Rows[rowIndex].DataBoundItem is not RecordRow row)
        {
            return;
        }

        var baseStyle = e.CellStyle ?? new DataGridViewCellStyle();
        var style = new DataGridViewCellStyle(baseStyle)
        {
            ForeColor = row.IsFullyRedeemed ? Color.FromArgb(22, 163, 74) : Color.FromArgb(220, 38, 38)
        };
        e.CellStyle = style;
    }

    private async Task OnRecordCellEndEditAsync(int rowIndex, int colIndex)
    {
        try
        {
            if (rowIndex < 0 || colIndex < 0)
            {
                return;
            }

            if (_gridRecords.Rows[rowIndex].DataBoundItem is not RecordRow row)
            {
                return;
            }

            var col = _gridRecords.Columns[colIndex];
            if (!string.Equals(col.DataPropertyName, nameof(RecordRow.RecordNote), StringComparison.Ordinal))
            {
                return;
            }

            var cellValue = _gridRecords.Rows[rowIndex].Cells[colIndex].Value;
            var newNote = (cellValue?.ToString() ?? "").Trim();
            var oldNote = _recordNoteBeforeEdit.TryGetValue(row.Id, out var old) ? old : "";
            if (string.Equals(newNote, oldNote, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                await AppServices.Pawn.UpdateRecordNoteAsync(row.Id, newNote);
                row.RecordNote = newNote;
                _recordNoteBeforeEdit.Remove(row.Id);
                _gridRecords.Refresh();
            }
            catch
            {
                row.RecordNote = oldNote;
                _gridRecords.Rows[rowIndex].Cells[colIndex].Value = oldNote;
                _recordNoteBeforeEdit.Remove(row.Id);
                _gridRecords.Refresh();
                throw;
            }
        }
        catch (Exception ex)
        {
            AppServices.Logger.Error("PawnControl.OnRecordCellEndEditAsync failed", ex);
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DeleteSelectedRecordAsync()
    {
        try
        {
            if (_gridRecords.CurrentRow?.DataBoundItem is not RecordRow row)
            {
                return;
            }

            var msg = $"Xóa phiếu cầm ID {row.Id} của khách \"{row.CustomerName}\"?\nHành động này không thể hoàn tác.";
            if (MessageBox.Show(msg, "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            SetListEnabled(false);
            await AppServices.Pawn.DeleteRecordAsync(row.Id);
            await LoadPageAsync(Math.Max(0, _pageIndex - 1));
        }
        catch (Exception ex)
        {
            AppServices.Logger.Error("PawnControl.DeleteSelectedRecordAsync failed", ex);
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetListEnabled(true);
        }
    }

    private async Task ExportAsync()
    {
        try
        {
            if (!VerifyExportPassword())
            {
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = $"PhieuCam_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };
            if (sfd.ShowDialog(FindForm()) != DialogResult.OK)
            {
                return;
            }

            _btnExport.Enabled = false;
            var filter = BuildFilter();
            var exporter = new PawnExcelExporter();
            await exporter.ExportAsync(sfd.FileName, filter);
            MessageBox.Show("Xuất Excel thành công.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppServices.Logger.Error("PawnControl.ExportAsync failed", ex);
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnExport.Enabled = true;
        }
    }

    private bool VerifyExportPassword()
    {
        using var dlg = new PasswordPromptForm();
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return false;
        }

        var password = (dlg.Password ?? "").Trim();
        if (!string.Equals(password, ExportExcelPassword, StringComparison.Ordinal))
        {
            MessageBox.Show("Mật khẩu không đúng.", "Từ chối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private void OpenDataFolder()
    {
        try
        {
            var dbPath = AppPaths.EffectiveDatabasePath;
            var dir = Path.GetDirectoryName(dbPath);
            if (string.IsNullOrWhiteSpace(dir))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppServices.Logger.Error("PawnControl.OpenDataFolder failed", ex);
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetEntryEnabled(bool enabled)
    {
        _btnSaveRecord.Enabled = enabled;
        _txtCustomerName.Enabled = enabled;
        _txtCccd.Enabled = enabled;
        _txtTotalAmount.Enabled = enabled;
        _dtPawn.Enabled = enabled;
        _gridEntryItems.Enabled = enabled;
        _txtRecordNote.Enabled = enabled;
        _btnAddItem.Enabled = enabled;
        _btnRemoveItem.Enabled = enabled;
        _cmbCatalogQuick.Enabled = enabled;
        _btnAddFromCatalog.Enabled = enabled;
    }

    private void SetListEnabled(bool enabled)
    {
        _btnApply.Enabled = enabled;
        _gridRecords.Enabled = enabled;
        _btnPrev.Enabled = enabled && _pageIndex > 0;
        _btnNext.Enabled = enabled && (_pageIndex + 1) * _pageSize < _totalCount;
        _btnDeleteRecord.Enabled = enabled;
        _cmbSearchField.Enabled = enabled;
        _txtSearch.Enabled = enabled;
        _dtFrom.Enabled = enabled;
        _dtTo.Enabled = enabled;
        _btnOpenData.Enabled = enabled;
    }

    private void FormatTotalAmount()
    {
        if (_formattingTotalAmount)
        {
            return;
        }

        try
        {
            _formattingTotalAmount = true;
            var text = _txtTotalAmount.Text ?? "";
            if (text.Trim().Length == 0)
            {
                return;
            }

            if (!InputParsers.TryParseMoneyVnd(text, out var value))
            {
                return;
            }

            var caret = _txtTotalAmount.SelectionStart;
            _txtTotalAmount.Text = InputParsers.FormatMoneyVnd(value);
            _txtTotalAmount.SelectionStart = Math.Min(_txtTotalAmount.Text.Length, caret);
        }
        finally
        {
            _formattingTotalAmount = false;
        }
    }

    private static string ToTitleCaseName(string? input)
    {
        input = (input ?? string.Empty).Trim();
        if (input.Length == 0)
        {
            return string.Empty;
        }

        input = System.Text.RegularExpressions.Regex.Replace(input, "\\s+", " ");
        var ti = CultureInfo.CurrentCulture.TextInfo;
        return ti.ToTitleCase(input.ToLower());
    }

    private sealed class EntryItemRow
    {
        public string? Qty { get; set; } = "1";
        public string? ItemName { get; set; } = "";
        public string? WeightChi { get; set; } = "0";
        public string? Note { get; set; } = "";
    }

    private sealed class RecordRow
    {
        public long Id { get; init; }
        public string CustomerName { get; init; } = "";
        public string Cccd { get; init; } = "";
        public string RecordNote { get; set; } = "";
        public string ItemsSummary { get; init; } = "";
        public long TotalAmountVnd { get; init; }
        public long ItemCount { get; init; }
        public long RedeemedCount { get; init; }
        public string DatePawnText { get; init; } = "";
        public string CreatedAtText { get; init; } = "";

        public string TotalText => InputParsers.FormatMoneyVnd(TotalAmountVnd);

        public bool IsFullyRedeemed => ItemCount > 0 && RedeemedCount >= ItemCount;

        public string RedeemedText => IsFullyRedeemed ? "Đã chuộc" : "Chưa chuộc";
    }
}
