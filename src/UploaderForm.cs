#if WINDOWS
using System.Text.Json;
using System.Windows.Forms;

namespace ModUploader;

public class UploaderForm : Form
{
    private sealed class LanguageOption
    {
        public required string Code { get; init; }
        public required string DisplayName { get; init; }
        public override string ToString() => $"{DisplayName} ({Code})";
    }

    private sealed class LanguageTagOption
    {
        public required string Value { get; init; }
        public required string DisplayName { get; init; }
    }

    private static readonly string[] DefaultTags =
    [
        "Acts",
        "Ancients",
        "Audio",
        "Cards",
        "Characters",
        "Cosmetics",
        "Events",
        "Expansion",
        "Extensions",
        "Humor",
        "Modifiers",
        "Monsters",
        "Potions",
        "QoL",
        "Relics",
        "Rooms",
        "Tools & APIs",
        "Utility",
        "Misc"
    ];

    private static readonly LanguageTagOption[] DefaultLanguageTags =
    [
        new() { Value = "english", DisplayName = "English" },
        new() { Value = "schinese", DisplayName = "Simplified Chinese" },
        new() { Value = "tchinese", DisplayName = "Traditional Chinese" },
        new() { Value = "japanese", DisplayName = "Japanese" },
        new() { Value = "koreana", DisplayName = "Korean" },
        new() { Value = "french", DisplayName = "French" },
        new() { Value = "german", DisplayName = "German" },
        new() { Value = "spanish", DisplayName = "Spanish - Spain" },
        new() { Value = "russian", DisplayName = "Russian" },
        new() { Value = "portuguese", DisplayName = "Portuguese - Portugal" },
        new() { Value = "brazilian", DisplayName = "Portuguese - Brazil" },
        new() { Value = "italian", DisplayName = "Italian" },
        new() { Value = "polish", DisplayName = "Polish" },
        new() { Value = "thai", DisplayName = "Thai" }
    ];

    private static readonly Dictionary<string, string> LanguageTagAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["english"] = "english",
        ["simplified chinese"] = "schinese",
        ["schinese"] = "schinese",
        ["traditional chinese"] = "tchinese",
        ["tchinese"] = "tchinese",
        ["japanese"] = "japanese",
        ["korean"] = "koreana",
        ["koreana"] = "koreana",
        ["french"] = "french",
        ["german"] = "german",
        ["spanish - spain"] = "spanish",
        ["spanish"] = "spanish",
        ["russian"] = "russian",
        ["portuguese - portugal"] = "portuguese",
        ["portuguese"] = "portuguese",
        ["portuguese - brazil"] = "brazilian",
        ["brazilian"] = "brazilian",
        ["italian"] = "italian",
        ["polish"] = "polish",
        ["thai"] = "thai"
    };

    private static readonly LanguageOption[] CommonLanguageOptions =
    [
        new() { Code = "english", DisplayName = "English" },
        new() { Code = "schinese", DisplayName = "简体中文" },
        new() { Code = "tchinese", DisplayName = "繁體中文" },
        new() { Code = "japanese", DisplayName = "日本語" },
        new() { Code = "koreana", DisplayName = "한국어" },
        new() { Code = "french", DisplayName = "Français" },
        new() { Code = "german", DisplayName = "Deutsch" },
        new() { Code = "spanish", DisplayName = "Español" },
        new() { Code = "russian", DisplayName = "Русский" },
        new() { Code = "portuguese", DisplayName = "Português" },
        new() { Code = "brazilian", DisplayName = "Português (Brasil)" },
        new() { Code = "italian", DisplayName = "Italiano" },
        new() { Code = "polish", DisplayName = "Polski" },
        new() { Code = "thai", DisplayName = "ไทย" }
    ];

    private readonly TextBox _workspacePathTextBox = new() { ReadOnly = true };
    private readonly TextBox _itemIdTextBox = new();
    private readonly TextBox _titleTextBox = new();
    private readonly CheckBox _updateDetailsCheckBox = new() { Text = "更新名称与描述", Checked = true };
    private readonly TextBox _descriptionTextBox = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly CheckBox _updateLocalizedDetailsCheckBox = new() { Text = "更新多语言标题与描述", Checked = false };
    private readonly TextBox _localizedDetailsTextBox = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly ComboBox _languageCodeComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _insertLanguageTemplateButton = new() { Text = "插入语言模板" };
    private readonly TextBox _changeNoteTextBox = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly CheckBox _updateChangeNoteCheckBox = new() { Text = "更新说明", Checked = true };
    private readonly ComboBox _visibilityComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly FlowLayoutPanel _tagsFlowPanel = new()
    {
        AutoScroll = true,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        Margin = new Padding(0),
        Padding = new Padding(4, 4, 4, 4)
    };
    private readonly List<CheckBox> _tagCheckBoxes = [];
    private readonly FlowLayoutPanel _languageTagsFlowPanel = new()
    {
        AutoScroll = false,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        Margin = new Padding(0),
        Padding = new Padding(2, 2, 2, 2)
    };
    private readonly List<CheckBox> _languageTagCheckBoxes = [];
    private readonly CheckBox _updateTagsCheckBox = new() { Text = "更新标签", Checked = true };
    private readonly TextBox _customTagsTextBox = new();
    private readonly CheckBox _updateDependenciesCheckBox = new() { Text = "更新依赖", Checked = false };
    private readonly TextBox _dependenciesTextBox = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _statusTextBox = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label _dropHintLabel = new();

    private string? _workspacePath;
    private bool _uploading;

    public UploaderForm()
    {
        Text = "STS2 Mod Uploader";
        Width = 920;
        Height = 860;
        MinimumSize = new Size(860, 760);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;

        _visibilityComboBox.Items.AddRange(["private", "friends_only", "unlisted", "public"]);
        _visibilityComboBox.SelectedIndex = 0;

        foreach (string tag in DefaultTags)
        {
            CheckBox tagCheckBox = new()
            {
                Text = tag,
                AutoSize = false,
                Width = 155,
                Height = 28,
                Margin = new Padding(4, 2, 8, 2)
            };
            _tagCheckBoxes.Add(tagCheckBox);
            _tagsFlowPanel.Controls.Add(tagCheckBox);
        }

        foreach (LanguageTagOption languageTag in DefaultLanguageTags)
        {
            CheckBox languageTagCheckBox = new()
            {
                Text = languageTag.DisplayName,
                Tag = languageTag.Value,
                AutoSize = false,
                Width = 120,
                Height = 24,
                Margin = new Padding(2, 1, 8, 1)
            };
            _languageTagCheckBoxes.Add(languageTagCheckBox);
            _languageTagsFlowPanel.Controls.Add(languageTagCheckBox);
        }

        _updateDetailsCheckBox.CheckedChanged += (_, _) => UpdateSectionEnabledState();
        _updateLocalizedDetailsCheckBox.CheckedChanged += (_, _) => UpdateSectionEnabledState();
        _updateChangeNoteCheckBox.CheckedChanged += (_, _) => UpdateSectionEnabledState();
        _updateTagsCheckBox.CheckedChanged += (_, _) => UpdateSectionEnabledState();
        _updateDependenciesCheckBox.CheckedChanged += (_, _) => UpdateSectionEnabledState();

        _languageCodeComboBox.Items.AddRange(CommonLanguageOptions);
        if (_languageCodeComboBox.Items.Count > 0)
        {
            _languageCodeComboBox.SelectedIndex = 0;
        }
        _insertLanguageTemplateButton.Click += (_, _) => InsertLanguageTemplate();

        _dropHintLabel.Text = "将 Mod 工作区文件夹拖拽到此窗口，或点击“选择文件夹”";
        _dropHintLabel.Dock = DockStyle.Top;
        _dropHintLabel.Height = 34;
        _dropHintLabel.TextAlign = ContentAlignment.MiddleCenter;
        _dropHintLabel.BackColor = Color.FromArgb(245, 245, 245);

        Controls.Add(BuildLayout());
        Controls.Add(_dropHintLabel);

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        Log.MessageLogged += HandleLogMessage;
        FormClosed += (_, _) => Log.MessageLogged -= HandleLogMessage;
    }

    private Control BuildLayout()
    {
        TableLayoutPanel table = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 12),
            ColumnCount = 4,
            RowCount = 10
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // 0: 工作区
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // 1: Item ID
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // 2: 标题
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // 3: 可见性
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 130)); // 4: 描述
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // 5: 多语言
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));  // 6: 更新说明
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 190)); // 7: Tag
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));  // 8: 依赖
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 9: 日志

        Button chooseWorkspaceButton = new()
        {
            Text = "选择文件夹",
            Dock = DockStyle.Fill
        };
        chooseWorkspaceButton.Click += (_, _) => SelectWorkspace();

        Button reloadWorkspaceButton = new()
        {
            Text = "重新读取配置",
            Dock = DockStyle.Fill
        };
        reloadWorkspaceButton.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_workspacePath))
            {
                LoadWorkspace(_workspacePath);
            }
        };

        Button saveConfigButton = new()
        {
            Text = "保存 workshop.json",
            Dock = DockStyle.Fill
        };
        saveConfigButton.Click += (_, _) => SaveWorkshopConfig(showSuccessMessage: true);

        Button uploadButton = new()
        {
            Text = "上传到创意工坊",
            Dock = DockStyle.Fill
        };
        uploadButton.Click += async (_, _) => await UploadAsync();

        AddRow(table, 0, "工作区", _workspacePathTextBox, chooseWorkspaceButton);
        AddRow(table, 1, "Item ID(可选)", _itemIdTextBox, reloadWorkspaceButton);
        AddRow(table, 2, "标题", _titleTextBox, _updateDetailsCheckBox);
        AddRow(table, 3, "可见性", _visibilityComboBox, saveConfigButton);
        AddRow(table, 4, "描述", _descriptionTextBox, null);

        Label localizedLabel = new()
        {
            Text = "多语言(每行: 语言|标题|描述)",
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        };
        table.Controls.Add(localizedLabel, 0, 5);
        _localizedDetailsTextBox.Dock = DockStyle.Fill;
        table.Controls.Add(_localizedDetailsTextBox, 1, 5);

        TableLayoutPanel localizedActionPanel = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0)
        };
        localizedActionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        localizedActionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        localizedActionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _updateLocalizedDetailsCheckBox.Dock = DockStyle.Fill;
        localizedActionPanel.Controls.Add(_updateLocalizedDetailsCheckBox, 0, 0);

        TableLayoutPanel localizedHelperPanel = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        localizedHelperPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        localizedHelperPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        _languageCodeComboBox.Dock = DockStyle.Fill;
        localizedHelperPanel.Controls.Add(_languageCodeComboBox, 0, 0);
        _insertLanguageTemplateButton.Dock = DockStyle.Fill;
        localizedHelperPanel.Controls.Add(_insertLanguageTemplateButton, 1, 0);
        localizedActionPanel.Controls.Add(localizedHelperPanel, 0, 1);

        Label localizedHintLabel = new()
        {
            Text = "每行: 语言|标题|描述。描述换行请写 \\n（会自动转成真实换行）",
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 24
        };
        localizedActionPanel.Controls.Add(localizedHintLabel, 0, 2);
        table.Controls.Add(localizedActionPanel, 2, 5);
        table.SetColumnSpan(localizedActionPanel, 2);

        Label changeNoteLabel = new()
        {
            Text = "更新说明",
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        };
        table.Controls.Add(changeNoteLabel, 0, 6);
        _changeNoteTextBox.Dock = DockStyle.Fill;
        table.Controls.Add(_changeNoteTextBox, 1, 6);

        TableLayoutPanel noteActionPanel = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0)
        };
        noteActionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        noteActionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        noteActionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        _updateChangeNoteCheckBox.Dock = DockStyle.Fill;
        noteActionPanel.Controls.Add(_updateChangeNoteCheckBox, 0, 0);
        uploadButton.Text = "发布到创意工坊";
        uploadButton.Dock = DockStyle.Fill;
        noteActionPanel.Controls.Add(uploadButton, 0, 2);
        table.Controls.Add(noteActionPanel, 2, 6);
        table.SetColumnSpan(noteActionPanel, 2);

        Label tagsLabel = new() { Text = "Tag 选择", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        table.Controls.Add(tagsLabel, 0, 7);
        table.SetColumnSpan(tagsLabel, 1);

        TableLayoutPanel tagsPanel = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0)
        };
        tagsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        tagsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        tagsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        TableLayoutPanel tagsHeaderPanel = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0)
        };
        tagsHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        tagsHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        tagsHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _updateTagsCheckBox.Dock = DockStyle.Fill;
        tagsHeaderPanel.Controls.Add(_updateTagsCheckBox, 0, 0);
        Label customTagsLabel = new() { Text = "自定义Tag(逗号分隔)", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        tagsHeaderPanel.Controls.Add(customTagsLabel, 1, 0);
        _customTagsTextBox.Dock = DockStyle.Fill;
        tagsHeaderPanel.Controls.Add(_customTagsTextBox, 2, 0);

        _languageTagsFlowPanel.Dock = DockStyle.Fill;
        _tagsFlowPanel.Dock = DockStyle.Fill;
        tagsPanel.Controls.Add(tagsHeaderPanel, 0, 0);
        tagsPanel.Controls.Add(_languageTagsFlowPanel, 0, 1);
        tagsPanel.Controls.Add(_tagsFlowPanel, 0, 2);
        table.Controls.Add(tagsPanel, 1, 7);
        table.SetColumnSpan(tagsPanel, 3);

        Label dependenciesLabel = new() { Text = "依赖ID(逗号/换行分隔)", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        table.Controls.Add(dependenciesLabel, 0, 8);
        table.SetColumnSpan(dependenciesLabel, 1);
        table.Controls.Add(_dependenciesTextBox, 1, 8);
        table.SetColumnSpan(_dependenciesTextBox, 2);
        _dependenciesTextBox.Dock = DockStyle.Fill;
        _updateDependenciesCheckBox.Dock = DockStyle.Fill;
        table.Controls.Add(_updateDependenciesCheckBox, 3, 8);

        Label statusLabel = new() { Text = "上传日志", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        table.Controls.Add(statusLabel, 0, 9);
        _statusTextBox.Dock = DockStyle.Fill;
        table.Controls.Add(_statusTextBox, 1, 9);
        table.SetColumnSpan(_statusTextBox, 3);

        return table;
    }

    private static void AddRow(TableLayoutPanel table, int row, string labelText, Control inputControl, Control? actionControl)
    {
        Label label = new()
        {
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        };

        inputControl.Dock = DockStyle.Fill;
        table.Controls.Add(label, 0, row);
        table.Controls.Add(inputControl, 1, row);

        if (actionControl != null)
        {
            actionControl.Dock = DockStyle.Fill;
            table.Controls.Add(actionControl, 2, row);
            table.SetColumnSpan(actionControl, 2);
        }
        else
        {
            // 没有操作按钮时，让输入区域占满右侧所有列，便于编辑长文本。
            table.SetColumnSpan(inputControl, 3);
        }
    }

    private void SelectWorkspace()
    {
        using FolderBrowserDialog dialog = new();
        dialog.Description = "选择 Mod 工作区文件夹";

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LoadWorkspace(dialog.SelectedPath);
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] drops && drops.Length > 0)
        {
            string first = drops[0];
            if (Directory.Exists(first))
            {
                LoadWorkspace(first);
            }
        }
    }

    private void LoadWorkspace(string workspacePath)
    {
        if (!Directory.Exists(workspacePath))
        {
            MessageBox.Show("目录不存在。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _workspacePath = workspacePath;
        _workspacePathTextBox.Text = workspacePath;
        AppendStatus($"已选择工作区: {workspacePath}");

        string configPath = Path.Combine(workspacePath, "workshop.json");
        if (!File.Exists(configPath))
        {
            ClearEditorFields();
            AppendStatus("未找到 workshop.json，已加载空模板。");
            return;
        }

        try
        {
            string json = File.ReadAllText(configPath);
            ModConfig? config = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.ModConfig);
            ApplyConfigToForm(config ?? new ModConfig());
            AppendStatus("已读取 workshop.json");
        }
        catch (Exception ex)
        {
            AppendStatus($"读取 workshop.json 失败: {ex.Message}");
            MessageBox.Show("workshop.json 解析失败，请检查格式。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        string modIdPath = Path.Combine(workspacePath, "mod_id.txt");
        if (File.Exists(modIdPath))
        {
            _itemIdTextBox.Text = File.ReadAllText(modIdPath).Trim();
        }
    }

    private void ClearEditorFields()
    {
        _itemIdTextBox.Text = string.Empty;
        _titleTextBox.Text = string.Empty;
        _descriptionTextBox.Text = string.Empty;
        _localizedDetailsTextBox.Text = string.Empty;
        _changeNoteTextBox.Text = string.Empty;
        _visibilityComboBox.SelectedItem = "private";
        _customTagsTextBox.Text = string.Empty;
        _dependenciesTextBox.Text = string.Empty;

        foreach (CheckBox tagCheckBox in _tagCheckBoxes)
        {
            tagCheckBox.Checked = false;
        }
        foreach (CheckBox languageTagCheckBox in _languageTagCheckBoxes)
        {
            languageTagCheckBox.Checked = false;
        }

        _updateDetailsCheckBox.Checked = true;
        _updateLocalizedDetailsCheckBox.Checked = false;
        _updateChangeNoteCheckBox.Checked = true;
        _updateTagsCheckBox.Checked = true;
        _updateDependenciesCheckBox.Checked = false;
        UpdateSectionEnabledState();
    }

    private void ApplyConfigToForm(ModConfig config)
    {
        ClearEditorFields();
        _titleTextBox.Text = config.title ?? string.Empty;
        _descriptionTextBox.Text = config.description ?? string.Empty;
        _changeNoteTextBox.Text = config.changeNote ?? string.Empty;
        _updateDetailsCheckBox.Checked = config.title != null || config.description != null;
        _updateLocalizedDetailsCheckBox.Checked =
            (config.localizedTitles?.Count ?? 0) > 0 || (config.localizedDescriptions?.Count ?? 0) > 0;
        _updateChangeNoteCheckBox.Checked = config.changeNote != null;
        _updateTagsCheckBox.Checked = config.tags != null;
        _updateDependenciesCheckBox.Checked = config.dependencies != null;

        if (!string.IsNullOrWhiteSpace(config.visibility) && _visibilityComboBox.Items.Contains(config.visibility))
        {
            _visibilityComboBox.SelectedItem = config.visibility;
        }

        List<string> customTags = [];
        foreach (string tag in config.tags ?? [])
        {
            string normalizedTag = NormalizeLanguageTag(tag);
            CheckBox? languageTagCheckBox = _languageTagCheckBoxes.FirstOrDefault(
                c => string.Equals(c.Tag as string, normalizedTag, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(c.Text, tag, StringComparison.OrdinalIgnoreCase));
            if (languageTagCheckBox != null)
            {
                languageTagCheckBox.Checked = true;
                continue;
            }

            CheckBox? tagCheckBox = _tagCheckBoxes.FirstOrDefault(
                c => string.Equals(c.Text, tag, StringComparison.OrdinalIgnoreCase));
            if (tagCheckBox != null)
            {
                tagCheckBox.Checked = true;
            }
            else
            {
                customTags.Add(tag);
            }
        }

        _customTagsTextBox.Text = string.Join(", ", customTags);
        _dependenciesTextBox.Text = string.Join(Environment.NewLine, config.dependencies ?? []);
        _localizedDetailsTextBox.Text = BuildLocalizedDetailsText(config.localizedTitles, config.localizedDescriptions);
        UpdateSectionEnabledState();
    }

    private bool SaveWorkshopConfig(bool showSuccessMessage)
    {
        if (string.IsNullOrWhiteSpace(_workspacePath))
        {
            MessageBox.Show("请先选择工作区文件夹。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        if (!TryParseLocalizedDetails(
                _localizedDetailsTextBox.Text,
                out Dictionary<string, string> localizedTitles,
                out Dictionary<string, string> localizedDescriptions,
                out string? parseError))
        {
            MessageBox.Show(parseError ?? "多语言格式不正确。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        ModConfig config = new()
        {
            title = _updateDetailsCheckBox.Checked ? EmptyToNull(_titleTextBox.Text) : null,
            description = _updateDetailsCheckBox.Checked ? EmptyToNull(_descriptionTextBox.Text) : null,
            localizedTitles = _updateLocalizedDetailsCheckBox.Checked ? localizedTitles : null,
            localizedDescriptions = _updateLocalizedDetailsCheckBox.Checked ? localizedDescriptions : null,
            changeNote = _updateChangeNoteCheckBox.Checked ? EmptyToNull(_changeNoteTextBox.Text) : null,
            visibility = _visibilityComboBox.SelectedItem?.ToString() ?? "private",
            tags = _updateTagsCheckBox.Checked ? BuildTags() : null,
            dependencies = _updateDependenciesCheckBox.Checked ? BuildDependencies() : null
        };

        string configPath = Path.Combine(_workspacePath, "workshop.json");
        string json = JsonSerializer.Serialize(config, SourceGenerationContext.Default.ModConfig);
        File.WriteAllText(configPath, json);
        AppendStatus($"已保存: {configPath}");

        if (showSuccessMessage)
        {
            MessageBox.Show("配置已保存。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        return true;
    }

    private async Task UploadAsync()
    {
        if (_uploading)
        {
            return;
        }

        if (!SaveWorkshopConfig(showSuccessMessage: false))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_workspacePath))
        {
            return;
        }

        if (!TryParseItemId(_itemIdTextBox.Text, out ulong? itemId))
        {
            MessageBox.Show("Item ID 格式无效，必须是数字。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _uploading = true;
        AppendStatus("开始上传...");

        try
        {
            int code = await UploadCommand.UploadWorkspace(new DirectoryInfo(_workspacePath), itemId);
            if (code == 0)
            {
                AppendStatus("上传完成。");
            }
            else
            {
                AppendStatus($"上传失败，退出码: {code}");
            }
        }
        catch (Exception ex)
        {
            AppendStatus($"上传异常: {ex.Message}");
            MessageBox.Show(ex.Message, "上传失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _uploading = false;
        }
    }

    private static bool TryParseItemId(string input, out ulong? itemId)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            itemId = null;
            return true;
        }

        if (ulong.TryParse(input.Trim(), out ulong parsed))
        {
            itemId = parsed;
            return true;
        }

        itemId = null;
        return false;
    }

    private List<string> BuildTags()
    {
        List<string> tags = [];
        foreach (CheckBox tagCheckBox in _tagCheckBoxes)
        {
            if (!tagCheckBox.Checked)
            {
                continue;
            }

            string value = tagCheckBox.Text ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value))
            {
                tags.Add(value.Trim());
            }
        }

        foreach (CheckBox languageTagCheckBox in _languageTagCheckBoxes)
        {
            if (!languageTagCheckBox.Checked)
            {
                continue;
            }

            string value = languageTagCheckBox.Tag as string ?? languageTagCheckBox.Text ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value) &&
                !tags.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(value.Trim());
            }
        }

        string[] customTags = _customTagsTextBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string tag in customTags)
        {
            if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(tag);
            }
        }

        return tags;
    }

    private List<ulong> BuildDependencies()
    {
        List<ulong> dependencies = [];
        string[] tokens = _dependenciesTextBox.Text.Split(
            [',', '\r', '\n', '\t', ' '],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string token in tokens)
        {
            if (ulong.TryParse(token, out ulong dependency))
            {
                dependencies.Add(dependency);
            }
        }

        return dependencies;
    }

    private static string? EmptyToNull(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static string NormalizeLanguageTag(string value)
    {
        string trimmed = value.Trim();
        return LanguageTagAliases.TryGetValue(trimmed, out string? normalized) ? normalized : trimmed;
    }

    private static string BuildLocalizedDetailsText(
        Dictionary<string, string>? localizedTitles,
        Dictionary<string, string>? localizedDescriptions)
    {
        HashSet<string> languages = [];
        if (localizedTitles != null)
        {
            foreach (string language in localizedTitles.Keys)
            {
                if (!string.IsNullOrWhiteSpace(language))
                {
                    languages.Add(language);
                }
            }
        }

        if (localizedDescriptions != null)
        {
            foreach (string language in localizedDescriptions.Keys)
            {
                if (!string.IsNullOrWhiteSpace(language))
                {
                    languages.Add(language);
                }
            }
        }

        if (languages.Count == 0)
        {
            return string.Empty;
        }

        List<string> lines = [];
        foreach (string language in languages.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            string title = localizedTitles != null && localizedTitles.TryGetValue(language, out string? t) ? t : "";
            string description =
                localizedDescriptions != null && localizedDescriptions.TryGetValue(language, out string? d) ? d : "";
            lines.Add($"{language}|{EscapeInlineValue(title)}|{EscapeInlineValue(description)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryParseLocalizedDetails(
        string rawText,
        out Dictionary<string, string> localizedTitles,
        out Dictionary<string, string> localizedDescriptions,
        out string? error)
    {
        localizedTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        localizedDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = null;

        string[] lines = rawText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] parts = line.Split('|', 3);
            if (parts.Length != 3)
            {
                error = $"多语言第 {i + 1} 行格式错误。应为: 语言|标题|描述";
                return false;
            }

            string language = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(language))
            {
                error = $"多语言第 {i + 1} 行语言代码为空。";
                return false;
            }

            string title = parts[1].Trim();
            string description = parts[2].Trim();

            if (!string.IsNullOrWhiteSpace(title))
            {
                localizedTitles[language] = UnescapeInlineValue(title);
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                localizedDescriptions[language] = UnescapeInlineValue(description);
            }
        }

        return true;
    }

    private static string EscapeInlineValue(string input)
    {
        return input
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string UnescapeInlineValue(string input)
    {
        return input
            .Replace("\\n", Environment.NewLine, StringComparison.Ordinal)
            .Replace("\\|", "|", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private void InsertLanguageTemplate()
    {
        if (_languageCodeComboBox.SelectedItem is not LanguageOption option)
        {
            return;
        }

        string code = option.Code;
        string[] lines = _localizedDetailsTextBox.Text
            .Split([Environment.NewLine], StringSplitOptions.None);

        bool exists = lines.Any(line => line.StartsWith(code + "|", StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            MessageBox.Show($"语言 {code} 已存在。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string templateLine = $"{code}||";
        if (string.IsNullOrWhiteSpace(_localizedDetailsTextBox.Text))
        {
            _localizedDetailsTextBox.Text = templateLine;
        }
        else
        {
            _localizedDetailsTextBox.AppendText(Environment.NewLine + templateLine);
        }

        _localizedDetailsTextBox.Focus();
    }

    private void UpdateSectionEnabledState()
    {
        bool updateDetails = _updateDetailsCheckBox.Checked;
        bool updateLocalizedDetails = _updateLocalizedDetailsCheckBox.Checked;
        bool updateChangeNote = _updateChangeNoteCheckBox.Checked;
        bool updateTags = _updateTagsCheckBox.Checked;
        bool updateDependencies = _updateDependenciesCheckBox.Checked;

        _titleTextBox.Enabled = updateDetails;
        _descriptionTextBox.Enabled = updateDetails;
        _localizedDetailsTextBox.Enabled = updateLocalizedDetails;

        _changeNoteTextBox.Enabled = updateChangeNote;

        _languageCodeComboBox.Enabled = updateLocalizedDetails;
        _insertLanguageTemplateButton.Enabled = updateLocalizedDetails;
        foreach (CheckBox tagCheckBox in _tagCheckBoxes)
        {
            tagCheckBox.Enabled = updateTags;
        }
        foreach (CheckBox languageTagCheckBox in _languageTagCheckBoxes)
        {
            languageTagCheckBox.Enabled = updateTags;
        }
        _customTagsTextBox.Enabled = updateTags;
        _dependenciesTextBox.Enabled = updateDependencies;
    }

    private void HandleLogMessage(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => AppendStatus(message));
        }
        else
        {
            AppendStatus(message);
        }
    }

    private void AppendStatus(string text)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {text}";
        _statusTextBox.AppendText(line + Environment.NewLine);
    }
}
#endif
