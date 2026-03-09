using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SoftwareInstaller.Models;
using SoftwareInstaller.Utils;

namespace SoftwareInstaller.UI
{
    public partial class SoftwareReviewForm : Form
    {
        private ListView _listView = null!;
        private Button _btnSelectAll = null!;
        private Button _btnDeselectAll = null!;
        private Button _btnConfirm = null!;
        private Button _btnCancel = null!;
        private Label _lblInfo = null!;

        private List<SoftwareItem> _detectedSoftware = new List<SoftwareItem>();
        private const int MaxDescriptionLength = 500;

        public List<SoftwareItem> SelectedSoftware { get; private set; } = new List<SoftwareItem>();

        public SoftwareReviewForm(List<SoftwareItem> detectedSoftware)
        {
            _detectedSoftware = detectedSoftware.Select(s => s.Clone()).ToList();
            InitializeComponent();
            PopulateList();
        }

        private void InitializeComponent()
        {
            this.Text = "软件审核 - 请选择要添加的软件并编辑描述";
            this.Size = new Size(900, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.MinimumSize = new Size(800, 600);

            // 信息标签
            _lblInfo = new Label
            {
                Text = "请勾选要添加的软件，选中软件后可在下方编辑描述：",
                Location = new Point(20, 15),
                Size = new Size(860, 25),
                Font = new Font("Segoe UI", 10F, FontStyle.Regular)
            };

            // 列表视图
            _listView = new ListView
            {
                Location = new Point(20, 45),
                Size = new Size(860, 280),
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _listView.Columns.Add("选择", 50);
            _listView.Columns.Add("分类", 100);
            _listView.Columns.Add("软件名称", 150);
            _listView.Columns.Add("版本", 80);
            _listView.Columns.Add("大小", 70);
            _listView.Columns.Add("说明", 200);
            _listView.Columns.Add("安装路径", 200);
            _listView.Columns.Add("静默参数", 150);

            _listView.ItemChecked += ListView_ItemChecked;
            _listView.MouseDoubleClick += ListView_MouseDoubleClick;
            _listView.MouseDown += ListView_MouseDown;

            // 按钮
            int buttonY = 340;
            int buttonWidth = 100;
            int buttonHeight = 30;
            int spacing = 10;

            _btnSelectAll = new Button
            {
                Text = "全选",
                Location = new Point(20, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 240, 240)
            };
            _btnSelectAll.Click += (s, e) => SetAllChecked(true);

            _btnDeselectAll = new Button
            {
                Text = "全不选",
                Location = new Point(20 + buttonWidth + spacing, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 240, 240)
            };
            _btnDeselectAll.Click += (s, e) => SetAllChecked(false);

            _btnConfirm = new Button
            {
                Text = "确认添加",
                Location = new Point(680, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.DodgerBlue,
                ForeColor = Color.White
            };
            _btnConfirm.Click += BtnConfirm_Click;

            _btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(680 + buttonWidth + spacing, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 240, 240),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(_lblInfo);
            this.Controls.Add(_listView);
            this.Controls.Add(_btnSelectAll);
            this.Controls.Add(_btnDeselectAll);
            this.Controls.Add(_btnConfirm);
            this.Controls.Add(_btnCancel);

            this.CancelButton = _btnCancel;
        }

        private void PopulateList()
        {
            _listView.Items.Clear();
            var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in _detectedSoftware)
            {
                // 使用安装路径作为唯一索引，防止重复
                if (!string.IsNullOrEmpty(item.FilePath))
                {
                    if (addedPaths.Contains(item.FilePath))
                    {
                        // 已存在相同路径的软件，跳过
                        continue;
                    }
                    addedPaths.Add(item.FilePath);
                }

                var listItem = new ListViewItem("");
                listItem.SubItems.Add(item.Category);
                listItem.SubItems.Add(item.Name);
                listItem.SubItems.Add(item.Version);
                listItem.SubItems.Add(item.Size);
                listItem.SubItems.Add(item.Description);
                listItem.SubItems.Add(item.FilePath);
                listItem.SubItems.Add(item.SilentInstallArgs ?? "");
                listItem.Tag = item;
                listItem.Checked = true;

                _listView.Items.Add(listItem);
            }

            UpdateInfoLabel();
        }

        private void ListView_ItemChecked(object? sender, ItemCheckedEventArgs e)
        {
            UpdateInfoLabel();
        }

        private void SetAllChecked(bool isChecked)
        {
            foreach (ListViewItem item in _listView.Items)
            {
                item.Checked = isChecked;
            }
            UpdateInfoLabel();
        }

        private void UpdateInfoLabel()
        {
            int selectedCount = _listView.Items.Cast<ListViewItem>().Count(i => i.Checked);
            int totalCount = _listView.Items.Count;
            _lblInfo.Text = $"检测到 {totalCount} 个软件，已选择 {selectedCount} 个";
        }

        private void BtnConfirm_Click(object? sender, EventArgs e)
        {
            SelectedSoftware = _listView.Items.Cast<ListViewItem>()
                .Where(i => i.Checked)
                .Select(i => (SoftwareItem)i.Tag!)
                .Where(item => item != null)
                .ToList();

            if (SelectedSoftware.Count == 0)
            {
                MessageBox.Show("请至少选择一个软件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void ListView_MouseDown(object? sender, MouseEventArgs e)
        {
            var hitTestInfo = _listView.HitTest(e.Location);
            if (hitTestInfo.Item != null && hitTestInfo.SubItem != null)
            {
                int subItemIndex = hitTestInfo.Item.SubItems.IndexOf(hitTestInfo.SubItem);
                // 如果点击的不是选择列，取消选择操作
                if (subItemIndex > 0)
                {
                    // 取消点击事件，防止修改选择状态
                    _listView.Enabled = false;
                    _listView.Enabled = true;
                }
            }
        }

        private void ListView_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            var hitTestInfo = _listView.HitTest(e.Location);
            if (hitTestInfo.Item != null && hitTestInfo.SubItem != null)
            {
                var listItem = hitTestInfo.Item;
                var softwareItem = (SoftwareItem)listItem.Tag!;
                int subItemIndex = listItem.SubItems.IndexOf(hitTestInfo.SubItem);

                // 双击的不是选择列
                if (subItemIndex > 0)
                {
                    string currentValue = hitTestInfo.SubItem.Text;
                    string columnName = _listView.Columns[subItemIndex].Text;
                    string newValue = string.Empty;

                    switch (subItemIndex)
                    {
                        case 1: // 分类
                            newValue = PromptDialog.Show("编辑分类", "请输入分类名称：", currentValue);
                            if (!string.IsNullOrEmpty(newValue))
                            {
                                softwareItem.Category = newValue.Trim();
                                listItem.SubItems[subItemIndex].Text = newValue.Trim();
                            }
                            break;
                        case 2: // 软件名称
                            newValue = PromptDialog.Show("编辑软件名称", "请输入软件名称：", currentValue);
                            if (!string.IsNullOrEmpty(newValue))
                            {
                                softwareItem.Name = newValue.Trim();
                                listItem.SubItems[subItemIndex].Text = newValue.Trim();
                            }
                            break;
                        case 3: // 版本
                            newValue = PromptDialog.Show("编辑版本号", "请输入版本号：", currentValue);
                            if (!string.IsNullOrEmpty(newValue))
                            {
                                softwareItem.Version = newValue.Trim();
                                listItem.SubItems[subItemIndex].Text = newValue.Trim();
                            }
                            break;
                        case 5: // 说明
                            using (var descriptionForm = new DescriptionEditForm(currentValue, MaxDescriptionLength))
                            {
                                if (descriptionForm.ShowDialog() == DialogResult.OK)
                                {
                                    string description = descriptionForm.Description;
                                    softwareItem.Description = description;
                                    listItem.SubItems[subItemIndex].Text = description;
                                }
                            }
                            break;
                        case 6: // 安装路径
                            using (var openFileDialog = new OpenFileDialog())
                            {
                                openFileDialog.Title = "选择安装程序";
                                openFileDialog.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                                openFileDialog.InitialDirectory = Path.GetDirectoryName(currentValue);
                                if (openFileDialog.ShowDialog() == DialogResult.OK)
                                {
                                    string filePath = openFileDialog.FileName;
                                    softwareItem.FilePath = filePath;
                                    listItem.SubItems[subItemIndex].Text = filePath;
                                }
                            }
                            break;
                        case 7: // 静默安装参数
                            newValue = PromptDialog.Show("编辑静默安装参数", "请输入静默安装参数：", currentValue);
                            if (!string.IsNullOrEmpty(newValue))
                            {
                                softwareItem.SilentInstallArgs = newValue.Trim();
                                listItem.SubItems[subItemIndex].Text = newValue.Trim();
                            }
                            break;
                    }
                }
            }
        }

        // 简单的提示对话框
        private class PromptDialog : Form
        {
            private TextBox _textBox = new TextBox();
            private Button _btnOK = new Button();
            private Button _btnCancel = new Button();

            public string Result { get; private set; } = string.Empty;

            private PromptDialog(string title, string label, string defaultValue)
            {
                this.Text = title;
                this.Size = new Size(400, 150);
                this.StartPosition = FormStartPosition.CenterParent;
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.MaximizeBox = false;
                this.MinimizeBox = false;

                var lblPrompt = new Label
                {
                    Text = label,
                    Location = new Point(20, 20),
                    Size = new Size(360, 20)
                };

                _textBox = new TextBox
                {
                    Location = new Point(20, 45),
                    Size = new Size(360, 20),
                    Text = defaultValue
                };

                _btnOK = new Button
                {
                    Text = "确定",
                    Location = new Point(200, 80),
                    Size = new Size(80, 25),
                    DialogResult = DialogResult.OK
                };

                _btnCancel = new Button
                {
                    Text = "取消",
                    Location = new Point(290, 80),
                    Size = new Size(80, 25),
                    DialogResult = DialogResult.Cancel
                };

                this.Controls.Add(lblPrompt);
                this.Controls.Add(_textBox);
                this.Controls.Add(_btnOK);
                this.Controls.Add(_btnCancel);

                this.AcceptButton = _btnOK;
                this.CancelButton = _btnCancel;
            }

            public static string Show(string title, string label, string defaultValue)
            {
                using (var dialog = new PromptDialog(title, label, defaultValue))
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        return dialog._textBox.Text;
                    }
                    return string.Empty;
                }
            }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                if (this.DialogResult == DialogResult.OK)
                {
                    Result = _textBox.Text;
                }
                base.OnFormClosing(e);
            }
        }

        // 描述编辑对话框
        private class DescriptionEditForm : Form
        {
            private TextBox _textBox = new TextBox();
            private Label _lblCharCount = new Label();
            private Button _btnOK = new Button();
            private Button _btnCancel = new Button();
            private int _maxLength;

            public string Description { get; private set; } = string.Empty;

            public DescriptionEditForm(string currentDescription, int maxLength)
            {
                _maxLength = maxLength;
                this.Text = "编辑软件描述";
                this.Size = new Size(500, 250);
                this.StartPosition = FormStartPosition.CenterParent;
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.MaximizeBox = false;
                this.MinimizeBox = false;

                var lblPrompt = new Label
                {
                    Text = "软件描述（最多500字符）：",
                    Location = new Point(20, 20),
                    Size = new Size(460, 20)
                };

                _textBox = new TextBox
                {
                    Location = new Point(20, 45),
                    Size = new Size(460, 100),
                    Text = currentDescription,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    MaxLength = maxLength
                };
                _textBox.TextChanged += TextBox_TextChanged;

                _lblCharCount = new Label
                {
                    Text = $"{currentDescription.Length}/{maxLength}",
                    Location = new Point(380, 20),
                    Size = new Size(100, 20),
                    TextAlign = ContentAlignment.MiddleRight
                };

                _btnOK = new Button
                {
                    Text = "确定",
                    Location = new Point(260, 160),
                    Size = new Size(80, 25),
                    DialogResult = DialogResult.OK
                };

                _btnCancel = new Button
                {
                    Text = "取消",
                    Location = new Point(350, 160),
                    Size = new Size(80, 25),
                    DialogResult = DialogResult.Cancel
                };

                this.Controls.Add(lblPrompt);
                this.Controls.Add(_textBox);
                this.Controls.Add(_lblCharCount);
                this.Controls.Add(_btnOK);
                this.Controls.Add(_btnCancel);

                this.AcceptButton = _btnOK;
                this.CancelButton = _btnCancel;
            }

            private void TextBox_TextChanged(object? sender, EventArgs e)
            {
                int length = _textBox.Text.Length;
                _lblCharCount.Text = $"{length}/{_maxLength}";

                if (length >= _maxLength)
                {
                    _lblCharCount.ForeColor = Color.Red;
                }
                else if (length >= _maxLength * 0.8)
                {
                    _lblCharCount.ForeColor = Color.Orange;
                }
                else
                {
                    _lblCharCount.ForeColor = Color.Gray;
                }
            }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                if (this.DialogResult == DialogResult.OK)
                {
                    Description = _textBox.Text.Trim();
                }
                base.OnFormClosing(e);
            }
        }
    }
}
