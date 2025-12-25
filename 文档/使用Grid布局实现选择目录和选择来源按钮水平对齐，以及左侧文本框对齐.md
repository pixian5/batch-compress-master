# 使用Grid布局实现选择目录和选择来源按钮水平对齐，以及左侧文本框对齐

## 问题描述
使用 Grid 布局替代 StackPanel，确保"选择目录"和"选择来源"按钮水平对齐，以及左侧的文本框也水平对齐。

## 修改内容

### 文件修改
- **文件路径**: [Views/MainWindow.axaml](file:///Users/x/code/trae/compress/Views/MainWindow.axaml)
- **修改位置**: 第23-51行

### 修改详情

**修改前的布局**（使用 StackPanel）:
```xml
<StackPanel Orientation="Horizontal" Spacing="10">
    <ComboBox Width="200"/>
    <TextBox Width="400"/>
    <Button Width="100"/>
</StackPanel>
<StackPanel Orientation="Horizontal" Spacing="10">
    <TextBlock Width="100"/>
    <Button Width="60"/>
    <TextBox Width="400"/>
    <Button Width="100"/>
</StackPanel>
```

**修改后的布局**（使用 Grid）:
```xml
<Grid ColumnDefinitions="200,60,400,100,Auto" RowDefinitions="Auto,Auto,Auto">
    <!-- Row 0: Source Mode Selection -->
    <ComboBox Grid.Row="0" Grid.Column="0"/>
    <TextBox Grid.Row="0" Grid.Column="2"/>
    <Button Grid.Row="0" Grid.Column="3"/>
    
    <!-- Row 1: Text File Path -->
    <TextBlock Grid.Row="1" Grid.Column="0"/>
    <Button Grid.Row="1" Grid.Column="1"/>
    <TextBox Grid.Row="1" Grid.Column="2"/>
    
    <!-- Row 2: Output Path -->
    <TextBlock Grid.Row="2" Grid.Column="0"/>
    <Button Grid.Row="2" Grid.Column="1"/>
    <TextBox Grid.Row="2" Grid.Column="2"/>
    <Button Grid.Row="2" Grid.Column="3"/>
    <TextBlock Grid.Row="2" Grid.Column="4"/>
</Grid>
```

### Grid 列定义
- **Column 0**: 200 - 用于下拉框和标签
- **Column 1**: 60 - 用于"同上"和"选择TXT"按钮
- **Column 2**: 400 - 用于文本框（来源路径、TXT路径、输出路径）
- **Column 3**: 100 - 用于"选择来源"和"选择目录"按钮
- **Column 4**: Auto - 用于输出大小显示

### 对齐效果

**按钮对齐**:
- "选择来源"按钮（Row 0, Column 3）与"选择目录"按钮（Row 2, Column 3）在同一列，实现水平对齐

**文本框对齐**:
- 来源路径文本框（Row 0, Column 2）
- TXT路径文本框（Row 1, Column 2）
- 输出路径文本框（Row 2, Column 2）
三个文本框在同一列，实现水平对齐

## 验证结果
- ✅ 构建成功，无错误
- ✅ 仅有1个警告（与本次修改无关，是SystemIntegrationService.cs中的过时API警告）

## Git提交
- **提交信息**: 1226-1507 使用Grid布局实现选择目录和选择来源按钮水平对齐，以及左侧文本框对齐
- **提交哈希**: 974effb
- **已推送到**: origin/temp_branch

## 优势说明

使用 Grid 布局相比 StackPanel 的优势：
1. **精确对齐**: 通过列定义确保控件精确对齐
2. **统一宽度**: 同一列的控件宽度一致，视觉效果更好
3. **易于维护**: 调整布局时只需修改列定义，不需要逐个调整控件
4. **响应式**: 可以轻松调整列宽，适应不同屏幕尺寸
