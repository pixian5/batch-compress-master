# 压缩选项和后处理选项放到同一列

已完成修改，将压缩选项和后处理选项合并到同一列。

## 修改内容

1. **UI布局优化**：将原来的两个独立Border（压缩选项和后处理选项）合并为一个Border，使用Grid的两列布局
   - 左列：压缩选项（扩展名、压缩率、分卷、注释等）
   - 右列：后处理选项（删除源、移动源、添加附件、关机等）

2. **默认值调整**：
   - 注释默认勾选
   - 分卷默认勾选
   - 分卷大小默认20GB
   - GB单位下拉列表宽度从60增加到80

## 修改文件

- [MainWindow.axaml](file:///Users/x/code/trae/compress/Views/MainWindow.axaml)：UI布局调整
- [MainWindowViewModel.cs](file:///Users/x/code/trae/compress/ViewModels/MainWindowViewModel.cs)：默认值设置

## Git提交

已提交到git：1226-0215-优化UI布局：将压缩选项和后处理选项合并到同一列，调整默认勾选项和分卷大小

程序已重新构建并运行，您可以在应用程序窗口中看到更新后的布局。
