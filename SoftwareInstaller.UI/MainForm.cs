using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.Json;
using SoftwareInstaller.Core;
using SoftwareInstaller.Models;
using SoftwareInstaller.Utils;

namespace SoftwareInstaller.UI
{
    public partial class MainForm : Form
    {
        private readonly List<Control> _bottomControls = new List<Control>();
        private readonly SoftwareManager? _softwareManager;
        private readonly SchemeHandler? _schemeHandler;
        private const string AllSoftwareNodeText = "所有软件";
        private bool isUpdatingByCode = false;
        private string _scanDirectory = "../soft/";

        public MainForm()
        {
            InitializeComponent();

            // 读取配置文件
            LoadConfiguration();

            // 设置应用程序图标
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                Icon? loadedIcon = null;
                
                if (File.Exists(iconPath))
                {
                    using (var stream = new FileStream(iconPath, FileMode.Open, FileAccess.Read))
                    {
                        loadedIcon = new Icon(stream);
                    }
                }
                else
                {
                    System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    using (Stream stream = assembly.GetManifestResourceStream("SoftwareInstaller.UI.app.ico"))
                    {
                        if (stream != null)
                        {
                            loadedIcon = new Icon(stream);
                        }
                    }
                }

                if (loadedIcon != null)
                {
                    this.Icon = loadedIcon;
                    this.ShowIcon = true;
                    this.ShowInTaskbar = true;
                }
            }
            catch { /* 图标加载失败，使用默认图标 */ }

            try
            {
                _softwareManager = new SoftwareManager();
                _schemeHandler = new SchemeHandler();
            }
            catch (Exception ex)
            {
                ErrorHandler.ShowError($"应用程序初始化失败，即将退出。\n错误: {ex.Message}");
                this.Load += (s, e) => this.Close();
                return;
            }

            InitializeCustomComponents();
            InitializeSchemeManagement();
            RebuildTreeView(); 
        }

        private void LoadConfiguration()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    string jsonContent = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<Config>(jsonContent);
                    if (config != null && !string.IsNullOrEmpty(config.ScanDirectory))
                    {
                        _scanDirectory = config.ScanDirectory;
                    }
                }
                else
                {
                    // 配置文件不存在，生成默认配置文件
                    var defaultConfig = new Config();
                    string defaultJsonContent = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(configPath, defaultJsonContent);
                    System.Diagnostics.Debug.WriteLine("生成默认配置文件: " + configPath);
                    _scanDirectory = defaultConfig.ScanDirectory;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取配置文件失败: {ex.Message}");
                // 使用默认值
                _scanDirectory = "..\\常用软件";
            }
        }

        private class Config
        {
            public string ScanDirectory { get; set; } = "..\\常用软件";
        }

        private void InitializeCustomComponents()
        {
            manualInstallButton = new Button { Text = "▶️ 手动安装", Size = new System.Drawing.Size(100, 25) };
            autoInstallButton = new Button { Text = "⚡ 自动安装", Size = new System.Drawing.Size(100, 25) };
            schemeComboBox = new ComboBox { Size = new System.Drawing.Size(120, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            saveSchemeButton = new Button { Text = "保存方案", Size = new System.Drawing.Size(80, 25) };
            deleteSchemeButton = new Button { Text = "删除方案", Size = new System.Drawing.Size(80, 25) };
            aboutButton = new Button { Text = "关于", Size = new System.Drawing.Size(80, 25) };
            
            var utilityButtons = new List<Button> { addCategoryButton, deleteCategoryButton, addSoftwareButton, deleteSoftwareButton };

            Action<Button, bool> styleButton = (btn, isPrimary) =>
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = Color.Gainsboro;
                btn.ForeColor = isPrimary ? Color.White : Color.Black;
                btn.BackColor = isPrimary ? Color.DodgerBlue : Color.FromArgb(240, 240, 240);
                
                var hoverColor = isPrimary ? Color.FromArgb(0, 102, 204) : Color.FromArgb(220, 220, 220);
                var originalColor = btn.BackColor;
                btn.MouseEnter += (s, e) => btn.BackColor = hoverColor;
                btn.MouseLeave += (s, e) => btn.BackColor = originalColor;
            };

            utilityButtons.ForEach(btn => styleButton(btn, false));
            styleButton(saveSchemeButton, false);
            styleButton(deleteSchemeButton, false);
            styleButton(aboutButton, false);
            styleButton(manualInstallButton, true);
            styleButton(autoInstallButton, true);

            _bottomControls.AddRange(new Control[] { manualInstallButton, autoInstallButton, schemeComboBox, saveSchemeButton, deleteSchemeButton, aboutButton });

            this.Load += OnMainFormLoad;
            bottomPanel.Paint += BottomPanel_Paint;
            bottomPanel.Resize += OnBottomPanelResize;

            categoryTreeView.AfterCheck += CategoryTreeView_AfterCheck;
            categoryTreeView.NodeMouseClick += CategoryTreeView_NodeMouseClick;
            categoryTreeView.NodeMouseDoubleClick += CategoryTreeView_NodeMouseDoubleClick;

            categoryTreeView.AllowDrop = true;
            categoryTreeView.DragEnter += CategoryTreeView_DragEnter;
            categoryTreeView.DragDrop += CategoryTreeView_DragDrop;
            categoryTreeView.DragOver += CategoryTreeView_DragOver;

            searchTextBox.TextChanged += SearchTextBox_TextChanged;

            addCategoryButton.Click += AddCategoryButton_Click;
            deleteCategoryButton.Click += DeleteCategoryButton_Click;
            addSoftwareButton.Click += AddSoftwareButton_Click;
            deleteSoftwareButton.Click += DeleteSoftwareButton_Click;
            scanSoftButton.Click += ScanSoftButton_Click;
            manualInstallButton.Click += ManualInstallButton_Click;
            autoInstallButton.Click += AutoInstallButton_Click;
            schemeComboBox.SelectedIndexChanged += SchemeComboBox_SelectedIndexChanged;
            saveSchemeButton.Click += SaveSchemeButton_Click;
            deleteSchemeButton.Click += DeleteSchemeButton_Click;
            aboutButton.Click += AboutButton_Click;

            this.FormClosing += MainForm_FormClosing;
        }

        private void OnMainFormLoad(object? sender, EventArgs e)
        {
            foreach (var control in _bottomControls)
            {
                bottomPanel.Controls.Add(control);
            }
            LayoutBottomControls();
        }

        private void OnBottomPanelResize(object? sender, EventArgs e)
        {
            LayoutBottomControls();
        }

        private void LayoutBottomControls()
        {
            if (!_bottomControls.Any()) return;

            int totalWidth = _bottomControls.Sum(c => c.Width) + (_bottomControls.Count - 1) * 10;
            int currentX = (bottomPanel.ClientSize.Width - totalWidth) / 2;
            int controlY = (bottomPanel.ClientSize.Height - manualInstallButton.Height) / 2;

            foreach (var control in _bottomControls)
            {
                int currentY = controlY + (manualInstallButton.Height - control.Height) / 2;
                control.Location = new System.Drawing.Point(currentX, currentY);
                currentX += control.Width + 10;
            }
        }

        private void BottomPanel_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.DrawLine(new Pen(Color.Gainsboro, 1), 0, 0, bottomPanel.Width, 0);
        }

        private void InitializeSchemeManagement()
        {
            PopulateSchemeComboBox();
        }

        private void RebuildTreeView()
        {
            categoryTreeView.Nodes.Clear();
            TreeNode allSoftwareNode = new TreeNode(AllSoftwareNodeText);
            categoryTreeView.Nodes.Add(allSoftwareNode);

            if (_softwareManager == null) return;

            var categoryNodes = _softwareManager.ActiveSoftwareItems.Select(i => i.Category).Distinct()
                .ToDictionary(name => name, name => new TreeNode(name));

            foreach (var node in categoryNodes.Values.OrderBy(n => n.Text))
            {
                allSoftwareNode.Nodes.Add(node);
            }
            
            foreach (var item in _softwareManager.ActiveSoftwareItems)
            {
                if (categoryNodes.TryGetValue(item.Category, out var categoryNode))
                {
                    TreeNode softwareNode = new TreeNode(item.Name) { Tag = item, Checked = item.IsSelected };
                    categoryNode.Nodes.Add(softwareNode);
                }
            }

            foreach(TreeNode categoryNode in allSoftwareNode.Nodes)
            {
                UpdateParentNodeCheckState(categoryNode);
            }
            UpdateParentNodeCheckState(allSoftwareNode);

            categoryTreeView.ExpandAll();
        }

        private void CategoryTreeView_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (isUpdatingByCode || e.Node == null) return;

            categoryTreeView.AfterCheck -= CategoryTreeView_AfterCheck;

            try
            {
                SetChildNodeCheckedState(e.Node, e.Node.Checked);
                
                TreeNode? parent = e.Node.Parent;
                while (parent != null)
                {
                    UpdateParentNodeCheckState(parent);
                    parent = parent.Parent;
                }

                SyncModelFromTree(categoryTreeView.Nodes);
                UpdateSoftwareListView();
            }
            finally
            {
                categoryTreeView.AfterCheck += CategoryTreeView_AfterCheck;
            }
        }

        private void SetChildNodeCheckedState(TreeNode node, bool isChecked)
        {
            foreach (TreeNode childNode in node.Nodes)
            {
                childNode.Checked = isChecked;
                if (childNode.Nodes.Count > 0)
                {
                    SetChildNodeCheckedState(childNode, isChecked);
                }
            }
        }

        private void UpdateParentNodeCheckState(TreeNode parentNode)
        {
            bool allChildrenChecked = parentNode.Nodes.Cast<TreeNode>().All(n => n.Checked);
            parentNode.Checked = allChildrenChecked;
        }

        private void UpdateSoftwareListView()
        {
            softwareListView.Items.Clear();
            int itemIndex = 1;

            void FindCheckedSoftware(TreeNodeCollection nodes)
            {
                foreach (TreeNode node in nodes)
                {
                    if (node.Tag is SoftwareItem item && node.Checked)
                    {
                        ListViewItem lvi = new ListViewItem(itemIndex.ToString());
                        lvi.SubItems.Add(item.Name);
                        lvi.SubItems.Add(item.Version);
                        lvi.SubItems.Add(item.FilePath ?? string.Empty);
                        lvi.SubItems.Add(item.Description);
                        lvi.SubItems.Add(item.SilentInstallArgs ?? string.Empty);
                        lvi.Tag = item;
                        softwareListView.Items.Add(lvi);
                        itemIndex++;
                    }
                    if (node.Nodes.Count > 0)
                    {
                        FindCheckedSoftware(node.Nodes);
                    }
                }
            }
            FindCheckedSoftware(categoryTreeView.Nodes);
        }

        private void CategoryTreeView_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node == null) return;

            if (e.Button == MouseButtons.Left)
            {
                var hitTest = e.Node.TreeView?.HitTest(e.Location);
                if (hitTest != null && hitTest.Location == TreeViewHitTestLocations.Label)
                {
                    e.Node.Checked = !e.Node.Checked;
                }
            }
        }

        private void CategoryTreeView_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node?.Tag is SoftwareItem itemToEdit)
            {
                using (var form = new SoftwareEditForm(itemToEdit))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        RebuildTreeView();
                        UpdateSoftwareListView();
                    }
                }
            }
        }

        private void AddCategoryButton_Click(object? sender, EventArgs e)
        {
            using (var dialog = new InputDialog("增加分类", "请输入新的分类名称:"))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string? categoryName = dialog.InputText;
                    if (!string.IsNullOrWhiteSpace(categoryName) && categoryName != AllSoftwareNodeText)
                    {
                        TreeNode? allSoftwareNode = categoryTreeView.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == AllSoftwareNodeText);
                        if (allSoftwareNode != null)
                        {
                            if (allSoftwareNode.Nodes.Cast<TreeNode>().Any(n => n.Text == categoryName))
                            {
                                MessageBox.Show("该分类已存在！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                            TreeNode newCategoryNode = new TreeNode(categoryName);
                            allSoftwareNode.Nodes.Add(newCategoryNode);
                            allSoftwareNode.Expand();
                        }
                    }
                }
            }
        }

        private void DeleteCategoryButton_Click(object? sender, EventArgs e)
        {
            if (_softwareManager == null) return;
            TreeNode? selectedNode = categoryTreeView.SelectedNode;
            if (selectedNode != null && selectedNode.Parent != null && selectedNode.Parent.Text == AllSoftwareNodeText)
            {
                if (MessageBox.Show($"确定要删除分类 '{selectedNode.Text}' 吗?\n这将同时删除该分类下的所有软件。", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    _softwareManager.ActiveSoftwareItems.RemoveAll(item => item.Category == selectedNode.Text);
                    RebuildTreeView();
                    UpdateSoftwareListView();
                }
            }
            else
            {
                MessageBox.Show("请先选择一个要删除的分类.\n（注意：不能删除'所有软件'根分类）", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void AddSoftwareButton_Click(object? sender, EventArgs e)
        {
            if (_softwareManager == null) return;
            TreeNode? selectedNode = categoryTreeView.SelectedNode;
            string categoryName;

            // 校验：确保用户没有选择根节点
            if (selectedNode != null && selectedNode.Text == AllSoftwareNodeText)
            {
                MessageBox.Show("不能直接在'所有软件'根分类下添加软件，请选择一个具体的分类，或创建新分类。", "操作无效", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (selectedNode == null)
            {
                categoryName = "未分类";
            }
            else if (selectedNode.Tag is SoftwareItem item)
            {
                categoryName = item.Category;
            }
            else
            {
                categoryName = selectedNode.Text;
            }

            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                openFileDialog.Title = "请选择软件安装包";

                if (openFileDialog.ShowDialog(this) == DialogResult.OK)
                {
                    var newSoftware = new SoftwareItem
                    {
                        FilePath = openFileDialog.FileName,
                        Name = string.Empty,
                        Version = string.Empty,
                        Size = string.Empty,
                        Description = string.Empty,
                        Category = categoryName
                    };

                    try
                    {
                        var fileInfo = new FileInfo(newSoftware.FilePath);
                        var versionInfo = FileVersionInfo.GetVersionInfo(newSoftware.FilePath);
                        newSoftware.Name = (versionInfo.ProductName ?? Path.GetFileNameWithoutExtension(fileInfo.Name)).Trim();
                        newSoftware.Version = (versionInfo.FileVersion ?? "N/A").Trim();
                        newSoftware.Size = $"{(fileInfo.Length / 1024.0 / 1024.0):F2} MB";
                        newSoftware.Description = (versionInfo.FileDescription ?? string.Empty).Trim();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"无法读取文件信息: {ex.Message}");
                        newSoftware.Name = Path.GetFileNameWithoutExtension(newSoftware.FilePath);
                    }

                    using (var form = new SoftwareEditForm(newSoftware))
                    {
                        form.Text = "新增软件";
                        if (form.ShowDialog(this) == DialogResult.OK)
                        {
                            var finalSoftware = form.SoftwareItem;
                            finalSoftware.Category = categoryName;
                            finalSoftware.IsSelected = true;

                            _softwareManager.AddSoftwareToActiveList(finalSoftware);

                            RebuildTreeView();
                            UpdateSoftwareListView();
                        }
                    }
                }
            }
        }

        private void DeleteSoftwareButton_Click(object? sender, EventArgs e)
        {
            if (_softwareManager == null) return;
            List<SoftwareItem> itemsToRemove = GetSelectedSoftwareItems();

            if (itemsToRemove.Count == 0)
            {
                MessageBox.Show("请先勾选需要删除的软件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string message = $"您确定要从当前列表中移除选中的 {itemsToRemove.Count} 款软件吗?\n(注意：这仅影响当前视图，不会从您的永久配置中删除它)";
            var confirmResult = MessageBox.Show(message, "确认移除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirmResult == DialogResult.Yes)
            {
                foreach (var item in itemsToRemove)
                {
                    _softwareManager.ActiveSoftwareItems.Remove(item);
                }

                RebuildTreeView();
                UpdateSoftwareListView();
            }
        }

        private void FindNodesBySoftwareItems(TreeNodeCollection nodes, List<SoftwareItem> itemsToFind, List<TreeNode> foundNodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is SoftwareItem item && itemsToFind.Contains(item))
                {
                    foundNodes.Add(node);
                }

                if (node.Nodes.Count > 0)
                {
                    FindNodesBySoftwareItems(node.Nodes, itemsToFind, foundNodes);
                }
            }
        }

        private void ManualInstallButton_Click(object? sender, EventArgs e)
        {
            List<SoftwareItem> selectedSoftware = GetSelectedSoftwareItems();
            if (selectedSoftware.Count == 0)
            {
                MessageBox.Show("没有选择任何软件进行安装。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var item in selectedSoftware)
            {
                ProcessRunner.ExecuteInstallation(item, false);
            }
        }

        private async void AutoInstallButton_Click(object? sender, EventArgs e)
        {
            if (_softwareManager == null) return;
            List<SoftwareItem> selectedSoftware = GetSelectedSoftwareItems();
            if (selectedSoftware.Count == 0)
            {
                MessageBox.Show("没有选择任何软件进行安装。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            autoInstallButton.Enabled = false;
            this.UseWaitCursor = true;

            foreach (var item in selectedSoftware)
            {
                if (string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
                {
                    MessageBox.Show($"软件 '{item.Name}' 的安装文件路径无效或不存在，已跳过。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }

                Func<Task<bool>> confirmInstallation;
                
                // 如果存在静默参数，直接返回成功，不需要用户确认
                if (!string.IsNullOrEmpty(item.SilentInstallArgs))
                {
                    confirmInstallation = async () =>
                    {
                        return await Task.Run(() => true);
                    };
                }
                else
                {
                    confirmInstallation = async () =>
                    {
                        return await Task.Run(() =>
                        {
                            string message = $"安装程序 '{item.Name}' 的一个静默模式似乎已执行完毕。\n\n请您确认软件是否已成功安装到系统中？\n\n- 点击 '是'，程序将保存当前有效的静默参数。\n- 点击 '否'，程序将尝试下一个静默参数。";
                            var result = MessageBox.Show(message,
                                "请确认安装结果",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);
                            return result == DialogResult.Yes;
                        });
                    };
                }

                var (success, usedArgs) = await _softwareManager.InstallSoftwareIntelligently(item, confirmInstallation);

                if (!success && string.IsNullOrEmpty(item.SilentInstallArgs))
                {
                    string failMessage = $"无法为软件 '{item.Name}' 找到有效的静默安装参数。\n所有尝试都失败了，或者您取消了确认。\n建议您使用“手动安装”。";
                    MessageBox.Show(failMessage, "自动安装失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            autoInstallButton.Enabled = true;
            this.UseWaitCursor = false;
        }

        private List<SoftwareItem> GetSelectedSoftwareItems()
        {
            if (_softwareManager == null) return new List<SoftwareItem>();
            return _softwareManager.ActiveSoftwareItems.Where(item => item.IsSelected).ToList();
        }

        private void SaveSchemeButton_Click(object? sender, EventArgs e)
        {
            if (_schemeHandler == null || _softwareManager == null) return;
            using (var dialog = new InputDialog("保存方案", "请输入方案名称:"))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string? schemeName = dialog.InputText;
                    if (string.IsNullOrWhiteSpace(schemeName)) return;

                    if (_schemeHandler.SchemeExists(schemeName))
                    {
                        if (MessageBox.Show("该方案名称已存在，要覆盖吗?", "确认覆盖", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        {
                            return;
                        }
                    }

                    _schemeHandler.SaveScheme(schemeName, _softwareManager.ActiveSoftwareItems);
                    PopulateSchemeComboBox();
                    schemeComboBox.SelectedItem = schemeName;
                }
            }
        }

        private void DeleteSchemeButton_Click(object? sender, EventArgs e)
        {
            if (_schemeHandler == null) return;
            if (schemeComboBox.SelectedItem is string selectedScheme && !string.IsNullOrEmpty(selectedScheme))
            {
                if (MessageBox.Show($"确定要删除方案 '{selectedScheme}' 吗?", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    _schemeHandler.DeleteScheme(selectedScheme);
                    PopulateSchemeComboBox();
                }
            }
            else
            {
                MessageBox.Show("请先从下拉框中选择一个要删除的方案。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void AboutButton_Click(object? sender, EventArgs e)
        {
            using (var aboutForm = new Form())
            {
                aboutForm.Text = "关于";
                aboutForm.Size = new System.Drawing.Size(350, 180);
                aboutForm.StartPosition = FormStartPosition.CenterParent;
                aboutForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                aboutForm.MaximizeBox = false;
                aboutForm.MinimizeBox = false;
                aboutForm.BackColor = Color.White;

                var label = new Label
                {
                    Text = "还没想好写什么",
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    Font = new System.Drawing.Font("Microsoft YaHei", 12F)
                };

                var closeButton = new Button
                {
                    Text = "关闭",
                    Size = new System.Drawing.Size(80, 30),
                    DialogResult = DialogResult.OK,
                    FlatStyle = FlatStyle.Flat
                };

                var buttonPanel = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 50
                };
                buttonPanel.Controls.Add(closeButton);
                closeButton.Location = new Point((buttonPanel.Width - closeButton.Width) / 2, 10);
                closeButton.Anchor = AnchorStyles.None;

                aboutForm.Controls.Add(label);
                aboutForm.Controls.Add(buttonPanel);

                aboutForm.ShowDialog(this);
            }
        }

        private void SchemeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_schemeHandler == null || _softwareManager == null) return;
            if (schemeComboBox.SelectedItem is string selectedScheme && _schemeHandler.GetScheme(selectedScheme) is List<SoftwareItem> loadedItems)
            {
                _softwareManager.ActiveSoftwareItems.Clear();
                foreach (var item in loadedItems)
                {
                    _softwareManager.AddSoftwareToActiveList(item);
                }

                isUpdatingByCode = true;
                RebuildTreeView();
                isUpdatingByCode = false;

                UpdateSoftwareListView();
            }
        }

        private void SyncModelFromTree(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is SoftwareItem item)
                {
                    item.IsSelected = node.Checked;
                }
                if (node.Nodes.Count > 0)
                {
                    SyncModelFromTree(node.Nodes);
                }
            }
        }

        private void PopulateSchemeComboBox()
        {
            if (_schemeHandler == null) return;
            var currentSelection = schemeComboBox.SelectedItem;
            schemeComboBox.Items.Clear();
            foreach (var schemeName in _schemeHandler.GetSchemeNames())
            {
                schemeComboBox.Items.Add(schemeName);
            }
            if (currentSelection != null && schemeComboBox.Items.Contains(currentSelection))
            {
                schemeComboBox.SelectedItem = currentSelection;
            }
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            _softwareManager?.SaveSoftwareList();
        }

        private void CategoryTreeView_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void CategoryTreeView_DragOver(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                TreeView treeView = (TreeView)sender!;
                Point clientPoint = treeView.PointToClient(new Point(e.X, e.Y));
                TreeNode? node = treeView.GetNodeAt(clientPoint);

                if (node != null)
                {
                    treeView.SelectedNode = node;
                }
            }
        }

        private void CategoryTreeView_DragDrop(object? sender, DragEventArgs e)
        {
            if (_softwareManager == null) return;

            string[]? files = e.Data?.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;

            TreeView treeView = (TreeView)sender!;
            Point clientPoint = treeView.PointToClient(new Point(e.X, e.Y));
            TreeNode? targetNode = treeView.GetNodeAt(clientPoint);

            string categoryName;
            if (targetNode != null && targetNode.Text != AllSoftwareNodeText)
            {
                if (targetNode.Tag is SoftwareItem item)
                {
                    categoryName = item.Category;
                }
                else
                {
                    categoryName = targetNode.Text;
                }
            }
            else
            {
                categoryName = "未分类";
            }

            TreeNode? allSoftwareNode = categoryTreeView.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == AllSoftwareNodeText);
            TreeNode? categoryNode = allSoftwareNode?.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == categoryName);

            if (categoryNode == null && allSoftwareNode != null)
            {
                categoryNode = new TreeNode(categoryName);
                allSoftwareNode.Nodes.Add(categoryNode);
            }

            int addedCount = 0;
            string finalCategoryName = categoryName;

            foreach (string path in files)
            {
                if (Directory.Exists(path))
                {
                    // 如果是拖拽到"未分类"，使用文件夹名作为分类名
                    string folderCategoryName = categoryName;
                    if (categoryName == "未分类")
                    {
                        folderCategoryName = Path.GetFileName(path);
                        finalCategoryName = folderCategoryName;

                        // 更新或创建分类节点
                        if (allSoftwareNode != null)
                        {
                            categoryNode = allSoftwareNode.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == folderCategoryName);
                            if (categoryNode == null)
                            {
                                categoryNode = new TreeNode(folderCategoryName);
                                allSoftwareNode.Nodes.Add(categoryNode);
                            }
                        }
                    }

                    var installers = InstallerScanner.ScanDirectory(path);
                    foreach (var installer in installers)
                    {
                        var newSoftware = CreateSoftwareItemFromInstaller(installer, folderCategoryName);
                        _softwareManager.AddSoftwareToActiveList(newSoftware);
                        addedCount++;
                    }
                }
                else if (File.Exists(path))
                {
                    var extension = Path.GetExtension(path).ToLowerInvariant();
                    if (extension == ".exe" || extension == ".msi")
                    {
                        var fileInfo = new FileInfo(path);
                        var installer = new InstallerInfo
                        {
                            FilePath = path,
                            FileName = Path.GetFileName(path),
                            Name = Path.GetFileNameWithoutExtension(path),
                            Size = $"{(fileInfo.Length / 1024.0 / 1024.0):F2} MB"
                        };
                        var newSoftware = CreateSoftwareItemFromInstaller(installer, categoryName);
                        _softwareManager.AddSoftwareToActiveList(newSoftware);
                        addedCount++;
                    }
                }
            }

            if (addedCount > 0)
            {
                RebuildTreeView();
                UpdateSoftwareListView();
                MessageBox.Show($"成功添加 {addedCount} 个软件到分类 '{finalCategoryName}'", "添加成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("未找到有效的安装程序文件。\n支持的格式: .exe, .msi", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private SoftwareItem CreateSoftwareItemFromInstaller(InstallerInfo installer, string categoryName)
        {
            var newSoftware = new SoftwareItem
            {
                FilePath = installer.FilePath,
                Name = installer.Name,
                Version = "",
                Size = installer.Size,
                Description = installer.Description, // 使用readme.txt中的描述
                Category = categoryName,
                IsSelected = true
            };

            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(installer.FilePath);
                if (!string.IsNullOrEmpty(versionInfo.ProductName))
                {
                    newSoftware.Name = versionInfo.ProductName.Trim();
                }
                if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                {
                    newSoftware.Version = versionInfo.FileVersion.Trim();
                }
                // 只有当readme.txt中没有描述时，才使用版本信息中的描述
                if (string.IsNullOrEmpty(newSoftware.Description) && !string.IsNullOrEmpty(versionInfo.FileDescription))
                {
                    newSoftware.Description = versionInfo.FileDescription.Trim();
                }
            }
            catch
            {
            }

            return newSoftware;
        }

        private void SearchTextBox_TextChanged(object? sender, EventArgs e)
        {
            if (_softwareManager == null) return;

            string searchText = searchTextBox.Text?.Trim().ToLowerInvariant() ?? "";

            if (string.IsNullOrEmpty(searchText))
            {
                UpdateSoftwareListView();
                return;
            }

            softwareListView.Items.Clear();
            int itemIndex = 1;

            var filteredItems = _softwareManager.ActiveSoftwareItems.Where(item =>
                item.Name.ToLowerInvariant().Contains(searchText) ||
                item.Description.ToLowerInvariant().Contains(searchText) ||
                item.Category.ToLowerInvariant().Contains(searchText) ||
                item.Version.ToLowerInvariant().Contains(searchText)
            );

            foreach (var item in filteredItems)
            {
                ListViewItem lvi = new ListViewItem(itemIndex.ToString());
                lvi.SubItems.Add(item.Name);
                lvi.SubItems.Add(item.Version);
                lvi.SubItems.Add(item.Size);
                lvi.SubItems.Add(item.Description);
                lvi.Tag = item;
                softwareListView.Items.Add(lvi);
                itemIndex++;
            }
        }

        private void ScanSoftButton_Click(object? sender, EventArgs e)
        {
            if (_softwareManager == null) return;

            // 获取扫描目录路径
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string softDirectory = _scanDirectory;
            
            // 如果是相对路径，转换为绝对路径
            if (!Path.IsPathRooted(softDirectory))
            {
                softDirectory = Path.Combine(Path.GetDirectoryName(appDirectory.TrimEnd(Path.DirectorySeparatorChar))!, softDirectory);
            }

            if (!Directory.Exists(softDirectory))
            {
                MessageBox.Show($"未找到扫描目录：{softDirectory}\n\n请确保软件放置在正确的目录中，\n且目录路径已在appsettings.json中正确配置。", "目录不存在", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var detectedSoftware = new List<SoftwareItem>();

            try
            {
                // 首先读取soft目录根目录下的readme.txt文件
                var rootReadmePath = Path.Combine(softDirectory, "readme.txt");
                var rootDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                if (File.Exists(rootReadmePath))
                {
                    try
                    {
                        // 读取根目录readme.txt
                        var content = File.ReadAllText(rootReadmePath);
                        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                        
                        for (int i = 0; i < lines.Length - 1; i++)
                        {
                            var fileNameLine = lines[i].Trim().Trim('\'', '"');
                            if (string.IsNullOrEmpty(fileNameLine))
                                continue;

                            var descriptionLine = lines[i + 1].Trim();
                            if (string.IsNullOrEmpty(descriptionLine))
                                continue;

                            rootDescriptions[fileNameLine] = descriptionLine;
                            // 添加空格和下划线的变体
                            var fileNameWithUnderscore = fileNameLine.Replace(' ', '_');
                            if (fileNameWithUnderscore != fileNameLine)
                            {
                                rootDescriptions[fileNameWithUnderscore] = descriptionLine;
                            }
                            var fileNameWithSpaces = fileNameLine.Replace('_', ' ');
                            if (fileNameWithSpaces != fileNameLine)
                            {
                                rootDescriptions[fileNameWithSpaces] = descriptionLine;
                            }
                            i++;
                        }
                        System.Diagnostics.Debug.WriteLine($"从根目录readme.txt读取了{rootDescriptions.Count}个描述");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"读取根目录readme.txt失败: {ex.Message}");
                    }
                }

                // 扫描Soft目录下的所有子目录（分类）
                var categoryDirectories = Directory.GetDirectories(softDirectory);

                foreach (var categoryDir in categoryDirectories)
                {
                    string categoryName = Path.GetFileName(categoryDir);
                    var installers = InstallerScanner.ScanDirectory(categoryDir);

                    foreach (var installer in installers)
                    {
                        // 首先尝试使用子目录readme.txt中的描述
                        // 如果没有，尝试使用根目录readme.txt中的描述
                        if (string.IsNullOrEmpty(installer.Description) && 
                            rootDescriptions.TryGetValue(installer.FileName, out var rootDescription))
                        {
                            installer.Description = rootDescription;
                            System.Diagnostics.Debug.WriteLine($"使用根目录readme.txt中的描述: {installer.FileName} -> {rootDescription}");
                        }

                        // 检查是否已存在于活动列表中
                        bool existsInActive = _softwareManager.ActiveSoftwareItems.Any(s =>
                            s.FilePath?.Equals(installer.FilePath, StringComparison.OrdinalIgnoreCase) == true);

                        // 检查是否已存在于本次检测列表中（防止同一目录下有重复文件）
                        bool existsInDetected = detectedSoftware.Any(s =>
                            s.FilePath?.Equals(installer.FilePath, StringComparison.OrdinalIgnoreCase) == true);

                        if (!existsInActive && !existsInDetected)
                        {
                            var newSoftware = CreateSoftwareItemFromInstaller(installer, categoryName);
                            detectedSoftware.Add(newSoftware);
                        }
                    }
                }

                if (detectedSoftware.Count == 0)
                {
                    MessageBox.Show("Soft目录下没有找到新的安装程序。", "扫描完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 显示审核弹窗
                using (var reviewForm = new SoftwareReviewForm(detectedSoftware))
                {
                    if (reviewForm.ShowDialog(this) == DialogResult.OK)
                    {
                        int addedCount = 0;
                        foreach (var software in reviewForm.SelectedSoftware)
                        {
                            _softwareManager.AddSoftwareToActiveList(software);
                            addedCount++;
                        }

                        if (addedCount > 0)
                        {
                            RebuildTreeView();
                            UpdateSoftwareListView();
                            MessageBox.Show($"成功添加 {addedCount} 个软件到列表中。", "添加成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.ShowError($"扫描Soft目录时发生错误：\n{ex.Message}");
            }
        }

        private void SoftwareListView_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (_softwareManager == null) return;
            var hitTestInfo = softwareListView.HitTest(e.Location);
            if (hitTestInfo.Item != null && hitTestInfo.SubItem != null)
            {
                var listItem = hitTestInfo.Item;
                var softwareItem = (SoftwareItem)listItem.Tag!;
                int subItemIndex = listItem.SubItems.IndexOf(hitTestInfo.SubItem);

                // 双击的不是序号列
                if (subItemIndex > 0)
                {
                    string currentValue = hitTestInfo.SubItem.Text;
                    string newValue = string.Empty;

                    switch (subItemIndex)
                    {
                        case 1: // 软件名称
                            newValue = PromptDialog.Show("编辑软件名称", "请输入软件名称：", currentValue);
                            if (!string.IsNullOrEmpty(newValue))
                            {
                                softwareItem.Name = newValue.Trim();
                                listItem.SubItems[subItemIndex].Text = newValue.Trim();
                            }
                            break;
                        case 2: // 版本
                            newValue = PromptDialog.Show("编辑版本", "请输入版本号：", currentValue);
                            if (!string.IsNullOrEmpty(newValue))
                            {
                                softwareItem.Version = newValue.Trim();
                                listItem.SubItems[subItemIndex].Text = newValue.Trim();
                            }
                            break;
                        case 3: // 安装路径
                            newValue = PromptDialog.Show("编辑安装路径", "请输入安装路径：", currentValue);
                            if (!string.IsNullOrEmpty(newValue))
                            {
                                softwareItem.FilePath = newValue.Trim();
                                listItem.SubItems[subItemIndex].Text = newValue.Trim();
                            }
                            break;
                        case 4: // 说明
                            newValue = PromptDialog.Show("编辑说明", "请输入软件说明：", currentValue);
                            if (!string.IsNullOrEmpty(newValue))
                            {
                                softwareItem.Description = newValue.Trim();
                                listItem.SubItems[subItemIndex].Text = newValue.Trim();
                            }
                            break;
                        case 5: // 静默参数
                            newValue = PromptDialog.Show("编辑静默安装参数", "请输入静默安装参数：", currentValue);
                            if (!string.IsNullOrEmpty(newValue))
                            {
                                softwareItem.SilentInstallArgs = newValue.Trim();
                                listItem.SubItems[subItemIndex].Text = newValue.Trim();
                            }
                            break;
                    }

                    // 保存更改
                    _softwareManager.SaveSoftwareList();
                }
            }
        }

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
        }
    }
}
