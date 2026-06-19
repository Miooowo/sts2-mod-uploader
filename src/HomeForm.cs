#if WINDOWS
using System.Windows.Forms;

namespace ModUploader;

public class HomeForm : Form
{
    public HomeForm()
    {
        Text = "STS2 Mod Uploader";
        Width = 620;
        Height = 360;
        MinimumSize = new Size(560, 320);
        StartPosition = FormStartPosition.CenterScreen;

        Label titleLabel = new()
        {
            Text = "欢迎使用 STS2 Mod Uploader",
            Dock = DockStyle.Top,
            Height = 70,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold)
        };

        Label hintLabel = new()
        {
            Text = "选择你要进行的操作（新建默认输出到当前目录/NewModWorkspace）",
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        };

        TableLayoutPanel actionsPanel = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(40, 20, 40, 28),
            ColumnCount = 2,
            RowCount = 1
        };
        actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actionsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Button newWorkspaceButton = new()
        {
            Text = "新建工作区文件夹",
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 12, FontStyle.Regular),
            Margin = new Padding(10)
        };
        newWorkspaceButton.Click += (_, _) => CreateWorkspace();

        Button publishButton = new()
        {
            Text = "发布 Mod",
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 12, FontStyle.Regular),
            Margin = new Padding(10)
        };
        publishButton.Click += (_, _) => OpenUploader();

        actionsPanel.Controls.Add(newWorkspaceButton, 0, 0);
        actionsPanel.Controls.Add(publishButton, 1, 0);

        Controls.Add(actionsPanel);
        Controls.Add(hintLabel);
        Controls.Add(titleLabel);
    }

    private void CreateWorkspace()
    {
        string targetPath = Path.Combine(Environment.CurrentDirectory, "NewModWorkspace");
        DirectoryInfo targetDirectory = new(targetPath);

        if (targetDirectory.Exists)
        {
            MessageBox.Show(
                $"目标文件夹已存在：{targetPath}\n请先删除或重命名后重试。",
                "创建失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            NewCommand.CreateNewWorkspace(targetDirectory).GetAwaiter().GetResult();
            MessageBox.Show(
                $"已创建工作区：{targetPath}",
                "创建成功",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"创建失败：{ex.Message}",
                "错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OpenUploader()
    {
        using UploaderForm uploader = new();
        Hide();
        uploader.ShowDialog(this);
        Show();
    }
}
#endif
