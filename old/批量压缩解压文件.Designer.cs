namespace 批量压缩
{
    partial class Mainform
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Mainform));
            btnSource = new Button();
            tbSource = new TextBox();
            rtbSource = new RichTextBox();
            rtbOk = new RichTextBox();
            rtbCMD = new RichTextBox();
            tbSavePath = new TextBox();
            btnSavePath = new Button();
            btnRun = new Button();
            exTension = new TextBox();
            cbExist = new ComboBox();
            notifyIcon = new NotifyIcon(components);
            contextMenuStrip1 = new ContextMenuStrip(components);
            退出ToolStripMenuItem = new ToolStripMenuItem();
            btnSuccess = new Button();
            btnFail = new Button();
            rtbFail = new RichTextBox();
            btnAddFile = new Button();
            btnClearAll = new Button();
            btnClearSource = new Button();
            btnReset = new Button();
            label6 = new Label();
            enclosureList = new RichTextBox();
            label3 = new Label();
            tbPW = new TextBox();
            btnHide = new Button();
            btnRefresh = new Button();
            labelSourceCount = new Label();
            cbNotice = new CheckBox();
            tbNotice = new TextBox();
            lpw = new Label();
            btnPW = new Button();
            tbVolume = new TextBox();
            cbGMK = new ComboBox();
            cbAdd = new CheckBox();
            cbFrom = new ComboBox();
            cbRate = new ComboBox();
            label1 = new Label();
            label2 = new Label();
            tbRec = new TextBox();
            cbQucikOpen = new CheckBox();
            label4 = new Label();
            tbTmp = new TextBox();
            cbCheck = new CheckBox();
            cbDivide = new CheckBox();
            tbSize = new TextBox();
            label5 = new Label();
            lokCount = new Label();
            lfail = new Label();
            labeldecp = new Label();
            cbMoveSource = new CheckBox();
            cbShutdown = new CheckBox();
            cbYiYaSuo = new CheckBox();
            Cbpw = new CheckBox();
            lOKsize = new Label();
            lRate = new Label();
            tbgetpw = new TextBox();
            btngetpw = new Button();
            tbFileName = new TextBox();
            label8 = new Label();
            CbDel = new CheckBox();
            Btndepress = new Button();
            CbSolid = new CheckBox();
            lMoveFailNum = new Label();
            btnZoom = new Button();
            btnAsSource = new Button();
            btnCancel = new Button();
            lbStatus = new Label();
            lbCurrentFile = new Label();
            labelSourceSize = new Label();
            contextMenuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // btnSource
            // 
            btnSource.Cursor = Cursors.Hand;
            btnSource.Font = new Font("微软雅黑", 14.25F);
            btnSource.ForeColor = Color.Green;
            btnSource.Location = new Point(572, 15);
            btnSource.Name = "btnSource";
            btnSource.Size = new Size(141, 39);
            btnSource.TabIndex = 0;
            btnSource.Text = "来源";
            btnSource.UseVisualStyleBackColor = true;
            btnSource.Click += ButtonFrom_Click;
            // 
            // tbSource
            // 
            tbSource.Font = new Font("宋体", 12F);
            tbSource.Location = new Point(274, 21);
            tbSource.Name = "tbSource";
            tbSource.Size = new Size(292, 26);
            tbSource.TabIndex = 0;
            tbSource.TextChanged += TbSource_TextChanged;
            // 
            // rtbSource
            // 
            rtbSource.BorderStyle = BorderStyle.None;
            rtbSource.Font = new Font("宋体", 12F);
            rtbSource.Location = new Point(379, 296);
            rtbSource.Name = "rtbSource";
            rtbSource.Size = new Size(332, 497);
            rtbSource.TabIndex = 2;
            rtbSource.Text = "";
            // 
            // rtbOk
            // 
            rtbOk.BorderStyle = BorderStyle.None;
            rtbOk.Font = new Font("宋体", 12F);
            rtbOk.Location = new Point(737, 296);
            rtbOk.Name = "rtbOk";
            rtbOk.Size = new Size(332, 497);
            rtbOk.TabIndex = 3;
            rtbOk.Text = "";
            // 
            // rtbCMD
            // 
            rtbCMD.BorderStyle = BorderStyle.None;
            rtbCMD.Font = new Font("宋体", 12F);
            rtbCMD.Location = new Point(14, 296);
            rtbCMD.Name = "rtbCMD";
            rtbCMD.Size = new Size(332, 497);
            rtbCMD.TabIndex = 4;
            rtbCMD.Text = "";
            // 
            // tbSavePath
            // 
            tbSavePath.Font = new Font("宋体", 12F);
            tbSavePath.Location = new Point(274, 71);
            tbSavePath.Name = "tbSavePath";
            tbSavePath.Size = new Size(292, 26);
            tbSavePath.TabIndex = 6;
            tbSavePath.TextChanged += TbSavePath_TextChanged;
            // 
            // btnSavePath
            // 
            btnSavePath.Cursor = Cursors.Hand;
            btnSavePath.Font = new Font("微软雅黑", 14.25F);
            btnSavePath.ForeColor = Color.Blue;
            btnSavePath.Location = new Point(572, 66);
            btnSavePath.Name = "btnSavePath";
            btnSavePath.Size = new Size(141, 39);
            btnSavePath.TabIndex = 8;
            btnSavePath.Text = "目的地";
            btnSavePath.UseVisualStyleBackColor = true;
            btnSavePath.Click += Btn2_Click;
            // 
            // btnRun
            // 
            btnRun.Cursor = Cursors.Hand;
            btnRun.Font = new Font("楷体", 26.25F, FontStyle.Bold);
            btnRun.ForeColor = Color.Red;
            btnRun.Location = new Point(572, 186);
            btnRun.Name = "btnRun";
            btnRun.Size = new Size(141, 63);
            btnRun.TabIndex = 2;
            btnRun.Text = "压缩";
            btnRun.UseVisualStyleBackColor = true;
            btnRun.Click += BtnRun_Click;
            // 
            // exTension
            // 
            exTension.Font = new Font("宋体", 12F);
            exTension.Location = new Point(1377, 223);
            exTension.Name = "exTension";
            exTension.Size = new Size(49, 26);
            exTension.TabIndex = 12;
            exTension.Text = "rar";
            exTension.TextAlign = HorizontalAlignment.Center;
            exTension.TextChanged += exTension_TextChanged;
            // 
            // cbExist
            // 
            cbExist.Font = new Font("宋体", 12F);
            cbExist.FormattingEnabled = true;
            cbExist.Items.AddRange(new object[] { "跳过", "添加并更新", "替换" });
            cbExist.Location = new Point(14, 222);
            cbExist.Name = "cbExist";
            cbExist.Size = new Size(153, 24);
            cbExist.TabIndex = 19;
            // 
            // notifyIcon
            // 
            notifyIcon.ContextMenuStrip = contextMenuStrip1;
            notifyIcon.Icon = (Icon)resources.GetObject("notifyIcon.Icon");
            notifyIcon.Text = "批量压缩";
            notifyIcon.Visible = true;
            notifyIcon.MouseClick += NotifyIcon1_MouseDoubleClick;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.ImageScalingSize = new Size(20, 20);
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { 退出ToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(107, 28);
            // 
            // 退出ToolStripMenuItem
            // 
            退出ToolStripMenuItem.Name = "退出ToolStripMenuItem";
            退出ToolStripMenuItem.Size = new Size(106, 24);
            退出ToolStripMenuItem.Text = "退出";
            退出ToolStripMenuItem.Click += 退出ToolStripMenuItem_Click;
            // 
            // btnSuccess
            // 
            btnSuccess.Font = new Font("微软雅黑", 14.25F);
            btnSuccess.Location = new Point(737, 257);
            btnSuccess.Name = "btnSuccess";
            btnSuccess.Size = new Size(140, 33);
            btnSuccess.TabIndex = 23;
            btnSuccess.Text = "成功的项目↓";
            btnSuccess.UseVisualStyleBackColor = true;
            btnSuccess.Click += Button2_Click;
            // 
            // btnFail
            // 
            btnFail.Font = new Font("微软雅黑", 14.25F);
            btnFail.Location = new Point(1094, 257);
            btnFail.Name = "btnFail";
            btnFail.Size = new Size(142, 33);
            btnFail.TabIndex = 24;
            btnFail.Text = "失败的项目↓";
            btnFail.UseVisualStyleBackColor = true;
            btnFail.Click += BtnFail_Click;
            // 
            // rtbFail
            // 
            rtbFail.BorderStyle = BorderStyle.None;
            rtbFail.Font = new Font("宋体", 12F);
            rtbFail.Location = new Point(1094, 296);
            rtbFail.Name = "rtbFail";
            rtbFail.Size = new Size(332, 497);
            rtbFail.TabIndex = 30;
            rtbFail.Text = "";
            // 
            // btnAddFile
            // 
            btnAddFile.Cursor = Cursors.Hand;
            btnAddFile.Font = new Font("微软雅黑", 14.25F);
            btnAddFile.Location = new Point(1001, 11);
            btnAddFile.Name = "btnAddFile";
            btnAddFile.Size = new Size(68, 33);
            btnAddFile.TabIndex = 33;
            btnAddFile.Text = "浏览";
            btnAddFile.UseVisualStyleBackColor = true;
            btnAddFile.Click += BtnAddFile_Click;
            // 
            // btnClearAll
            // 
            btnClearAll.Font = new Font("微软雅黑", 14.25F);
            btnClearAll.Location = new Point(14, 257);
            btnClearAll.Name = "btnClearAll";
            btnClearAll.Size = new Size(153, 33);
            btnClearAll.TabIndex = 36;
            btnClearAll.Text = "清空↓";
            btnClearAll.UseVisualStyleBackColor = true;
            btnClearAll.Click += BtnClearAll_Click;
            // 
            // btnClearSource
            // 
            btnClearSource.Font = new Font("微软雅黑", 14.25F);
            btnClearSource.Location = new Point(491, 257);
            btnClearSource.Name = "btnClearSource";
            btnClearSource.Size = new Size(75, 33);
            btnClearSource.TabIndex = 37;
            btnClearSource.Text = "清空↓";
            btnClearSource.UseVisualStyleBackColor = true;
            btnClearSource.Click += BtnClearSource_Click;
            // 
            // btnReset
            // 
            btnReset.Font = new Font("微软雅黑", 14.25F);
            btnReset.ForeColor = Color.Purple;
            btnReset.Location = new Point(177, 257);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(169, 33);
            btnReset.TabIndex = 26;
            btnReset.Text = "清空下面四个";
            btnReset.UseVisualStyleBackColor = true;
            btnReset.Click += Btnreset_Click;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("宋体", 12F);
            label6.Location = new Point(177, 226);
            label6.Name = "label6";
            label6.Size = new Size(103, 16);
            label6.TabIndex = 38;
            label6.Text = "已存在的文件";
            // 
            // enclosureList
            // 
            enclosureList.BorderStyle = BorderStyle.None;
            enclosureList.Font = new Font("宋体", 12F);
            enclosureList.Location = new Point(736, 50);
            enclosureList.Name = "enclosureList";
            enclosureList.Size = new Size(333, 95);
            enclosureList.TabIndex = 40;
            enclosureList.Text = "";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("宋体", 12F);
            label3.Location = new Point(1292, 230);
            label3.Name = "label3";
            label3.Size = new Size(79, 16);
            label3.TabIndex = 41;
            label3.Text = "文件格式:";
            // 
            // tbPW
            // 
            tbPW.Font = new Font("宋体", 12F);
            tbPW.Location = new Point(71, 185);
            tbPW.Name = "tbPW";
            tbPW.Size = new Size(185, 26);
            tbPW.TabIndex = 45;
            tbPW.TextAlign = HorizontalAlignment.Center;
            // 
            // btnHide
            // 
            btnHide.AutoSize = true;
            btnHide.BackColor = SystemColors.Control;
            btnHide.BackgroundImageLayout = ImageLayout.None;
            btnHide.Cursor = Cursors.Hand;
            btnHide.FlatAppearance.BorderColor = SystemColors.Control;
            btnHide.FlatAppearance.BorderSize = 0;
            btnHide.Font = new Font("微软雅黑", 18F);
            btnHide.ForeColor = Color.Black;
            btnHide.Location = new Point(988, 185);
            btnHide.Margin = new Padding(0);
            btnHide.Name = "btnHide";
            btnHide.Size = new Size(81, 61);
            btnHide.TabIndex = 48;
            btnHide.Text = "隐藏";
            btnHide.UseVisualStyleBackColor = true;
            btnHide.Click += BtnHide_Click;
            // 
            // btnRefresh
            // 
            btnRefresh.Font = new Font("微软雅黑", 14.25F);
            btnRefresh.Location = new Point(572, 257);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(140, 33);
            btnRefresh.TabIndex = 50;
            btnRefresh.Text = "更新";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += BtnrRefresh_Click;
            // 
            // labelSourceCount
            // 
            labelSourceCount.Font = new Font("宋体", 12F);
            labelSourceCount.Location = new Point(380, 233);
            labelSourceCount.Name = "labelSourceCount";
            labelSourceCount.Size = new Size(71, 16);
            labelSourceCount.TabIndex = 51;
            labelSourceCount.Text = "总文件数";
            labelSourceCount.TextAlign = ContentAlignment.MiddleLeft;
            labelSourceCount.Click += Lsum_Click;
            // 
            // cbNotice
            // 
            cbNotice.AutoSize = true;
            cbNotice.Checked = true;
            cbNotice.CheckState = CheckState.Checked;
            cbNotice.Font = new Font("宋体", 12F);
            cbNotice.Location = new Point(1100, 193);
            cbNotice.Name = "cbNotice";
            cbNotice.Size = new Size(58, 20);
            cbNotice.TabIndex = 52;
            cbNotice.Text = "注释";
            cbNotice.UseVisualStyleBackColor = true;
            // 
            // tbNotice
            // 
            tbNotice.Font = new Font("宋体", 12F);
            tbNotice.Location = new Point(1159, 190);
            tbNotice.Name = "tbNotice";
            tbNotice.Size = new Size(266, 26);
            tbNotice.TabIndex = 53;
            // 
            // lpw
            // 
            lpw.AutoSize = true;
            lpw.Font = new Font("宋体", 12F);
            lpw.Location = new Point(14, 190);
            lpw.Name = "lpw";
            lpw.Size = new Size(55, 16);
            lpw.TabIndex = 54;
            lpw.Text = "密码：";
            // 
            // btnPW
            // 
            btnPW.AutoSize = true;
            btnPW.BackColor = SystemColors.Control;
            btnPW.BackgroundImageLayout = ImageLayout.None;
            btnPW.Cursor = Cursors.Hand;
            btnPW.FlatAppearance.BorderColor = SystemColors.Control;
            btnPW.FlatAppearance.BorderSize = 0;
            btnPW.Font = new Font("微软雅黑", 14.25F);
            btnPW.ForeColor = Color.Black;
            btnPW.Location = new Point(274, 179);
            btnPW.Margin = new Padding(0);
            btnPW.Name = "btnPW";
            btnPW.Size = new Size(72, 35);
            btnPW.TabIndex = 55;
            btnPW.Text = "确认";
            btnPW.UseVisualStyleBackColor = true;
            btnPW.Click += BtnPW_Click;
            // 
            // tbVolume
            // 
            tbVolume.Font = new Font("宋体", 12F);
            tbVolume.Location = new Point(1159, 223);
            tbVolume.Name = "tbVolume";
            tbVolume.Size = new Size(77, 26);
            tbVolume.TabIndex = 57;
            tbVolume.Text = "300";
            tbVolume.TextAlign = HorizontalAlignment.Center;
            // 
            // cbGMK
            // 
            cbGMK.Font = new Font("宋体", 12F);
            cbGMK.FormattingEnabled = true;
            cbGMK.Items.AddRange(new object[] { "g", "m", "k" });
            cbGMK.Location = new Point(1242, 224);
            cbGMK.Name = "cbGMK";
            cbGMK.Size = new Size(39, 24);
            cbGMK.TabIndex = 58;
            // 
            // cbAdd
            // 
            cbAdd.AutoSize = true;
            cbAdd.Checked = true;
            cbAdd.CheckState = CheckState.Checked;
            cbAdd.Font = new Font("宋体", 12F);
            cbAdd.Location = new Point(736, 18);
            cbAdd.Name = "cbAdd";
            cbAdd.Size = new Size(258, 20);
            cbAdd.TabIndex = 59;
            cbAdd.Text = "添加以下文件/夹至每个压缩文件";
            cbAdd.UseVisualStyleBackColor = true;
            // 
            // cbFrom
            // 
            cbFrom.Font = new Font("宋体", 12F);
            cbFrom.FormattingEnabled = true;
            cbFrom.Items.AddRange(new object[] { "从txt读取要解压的文件：", "压缩此文件夹内所有文件：" });
            cbFrom.Location = new Point(14, 20);
            cbFrom.Name = "cbFrom";
            cbFrom.Size = new Size(242, 24);
            cbFrom.TabIndex = 60;
            cbFrom.SelectedIndexChanged += CbFrom_SelectedIndexChanged;
            // 
            // cbRate
            // 
            cbRate.Font = new Font("宋体", 12F);
            cbRate.FormattingEnabled = true;
            cbRate.Items.AddRange(new object[] { "不压缩（最快）", "轻度压缩（推荐）", "均衡", "极限压缩（极慢，慎选）" });
            cbRate.Location = new Point(1159, 50);
            cbRate.Name = "cbRate";
            cbRate.Size = new Size(204, 24);
            cbRate.TabIndex = 61;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("宋体", 12F);
            label1.Location = new Point(1097, 157);
            label1.Name = "label1";
            label1.Size = new Size(159, 16);
            label1.TabIndex = 62;
            label1.Text = "为了降低损坏率,增大";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("宋体", 12F);
            label2.Location = new Point(1307, 157);
            label2.Name = "label2";
            label2.Size = new Size(79, 16);
            label2.TabIndex = 63;
            label2.Text = "%文件大小";
            // 
            // tbRec
            // 
            tbRec.Font = new Font("宋体", 12F);
            tbRec.Location = new Point(1257, 151);
            tbRec.Name = "tbRec";
            tbRec.Size = new Size(48, 26);
            tbRec.TabIndex = 64;
            tbRec.Text = "0";
            tbRec.TextAlign = HorizontalAlignment.Center;
            // 
            // cbQucikOpen
            // 
            cbQucikOpen.AutoSize = true;
            cbQucikOpen.Checked = true;
            cbQucikOpen.CheckState = CheckState.Checked;
            cbQucikOpen.Font = new Font("宋体", 12F);
            cbQucikOpen.Location = new Point(1100, 89);
            cbQucikOpen.Name = "cbQucikOpen";
            cbQucikOpen.Size = new Size(218, 20);
            cbQucikOpen.TabIndex = 65;
            cbQucikOpen.Text = "添加快速打开和防损坏信息";
            cbQucikOpen.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("宋体", 12F);
            label4.Location = new Point(1097, 13);
            label4.Name = "label4";
            label4.Size = new Size(87, 16);
            label4.TabIndex = 67;
            label4.Text = "临时文件夹";
            // 
            // tbTmp
            // 
            tbTmp.Font = new Font("宋体", 12F);
            tbTmp.Location = new Point(1197, 10);
            tbTmp.Name = "tbTmp";
            tbTmp.Size = new Size(229, 26);
            tbTmp.TabIndex = 66;
            // 
            // cbCheck
            // 
            cbCheck.AutoSize = true;
            cbCheck.Font = new Font("宋体", 12F);
            cbCheck.Location = new Point(1368, 123);
            cbCheck.Name = "cbCheck";
            cbCheck.Size = new Size(58, 20);
            cbCheck.TabIndex = 68;
            cbCheck.Text = "校验";
            cbCheck.UseVisualStyleBackColor = true;
            // 
            // cbDivide
            // 
            cbDivide.AutoSize = true;
            cbDivide.Checked = true;
            cbDivide.CheckState = CheckState.Checked;
            cbDivide.Font = new Font("宋体", 12F);
            cbDivide.Location = new Point(1100, 229);
            cbDivide.Name = "cbDivide";
            cbDivide.Size = new Size(58, 20);
            cbDivide.TabIndex = 69;
            cbDivide.Text = "分卷";
            cbDivide.UseVisualStyleBackColor = true;
            // 
            // tbSize
            // 
            tbSize.Font = new Font("宋体", 12F);
            tbSize.Location = new Point(810, 151);
            tbSize.Name = "tbSize";
            tbSize.Size = new Size(93, 26);
            tbSize.TabIndex = 70;
            tbSize.TextAlign = HorizontalAlignment.Center;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("宋体", 12F);
            label5.Location = new Point(736, 157);
            label5.Name = "label5";
            label5.Size = new Size(71, 16);
            label5.TabIndex = 71;
            label5.Text = "每次压缩";
            // 
            // lokCount
            // 
            lokCount.Font = new Font("新宋体", 12F);
            lokCount.Location = new Point(883, 267);
            lokCount.Name = "lokCount";
            lokCount.Size = new Size(100, 16);
            lokCount.TabIndex = 72;
            lokCount.Text = "0个文件";
            lokCount.TextAlign = ContentAlignment.MiddleRight;
            // 
            // lfail
            // 
            lfail.AutoSize = true;
            lfail.Font = new Font("新宋体", 12F);
            lfail.Location = new Point(1242, 265);
            lfail.Name = "lfail";
            lfail.Size = new Size(63, 16);
            lfail.TabIndex = 73;
            lfail.Text = "0个文件";
            lfail.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // labeldecp
            // 
            labeldecp.AutoSize = true;
            labeldecp.Font = new Font("宋体", 12F);
            labeldecp.Location = new Point(907, 158);
            labeldecp.Name = "labeldecp";
            labeldecp.Size = new Size(55, 16);
            labeldecp.TabIndex = 75;
            labeldecp.Text = "GB文件";
            // 
            // cbMoveSource
            // 
            cbMoveSource.AutoSize = true;
            cbMoveSource.Checked = true;
            cbMoveSource.CheckState = CheckState.Checked;
            cbMoveSource.Font = new Font("宋体", 12F);
            cbMoveSource.Location = new Point(380, 159);
            cbMoveSource.Name = "cbMoveSource";
            cbMoveSource.Size = new Size(298, 20);
            cbMoveSource.TabIndex = 76;
            cbMoveSource.Text = "完成后移动源文件到【已压缩】文件夹";
            cbMoveSource.UseVisualStyleBackColor = true;
            cbMoveSource.Click += cbMoveSource_CheckedChanged;
            // 
            // cbShutdown
            // 
            cbShutdown.AutoSize = true;
            cbShutdown.Font = new Font("宋体", 12F);
            cbShutdown.Location = new Point(1320, 89);
            cbShutdown.Name = "cbShutdown";
            cbShutdown.Size = new Size(106, 20);
            cbShutdown.TabIndex = 77;
            cbShutdown.Text = "完成后关机";
            cbShutdown.UseVisualStyleBackColor = true;
            // 
            // cbYiYaSuo
            // 
            cbYiYaSuo.AutoSize = true;
            cbYiYaSuo.Checked = true;
            cbYiYaSuo.CheckState = CheckState.Checked;
            cbYiYaSuo.Font = new Font("宋体", 12F);
            cbYiYaSuo.Location = new Point(1100, 123);
            cbYiYaSuo.Name = "cbYiYaSuo";
            cbYiYaSuo.Size = new Size(250, 20);
            cbYiYaSuo.TabIndex = 79;
            cbYiYaSuo.Text = "不压缩名称含【已压缩】的文件";
            cbYiYaSuo.UseVisualStyleBackColor = true;
            // 
            // Cbpw
            // 
            Cbpw.AutoSize = true;
            Cbpw.Font = new Font("宋体", 12F);
            Cbpw.Location = new Point(14, 154);
            Cbpw.Name = "Cbpw";
            Cbpw.Size = new Size(346, 20);
            Cbpw.TabIndex = 80;
            Cbpw.Text = "每个文件用不同随机密码加密（牛逼的功能）";
            Cbpw.UseVisualStyleBackColor = true;
            Cbpw.CheckedChanged += Cbpw_CheckedChanged;
            // 
            // lOKsize
            // 
            lOKsize.Font = new Font("新宋体", 12F);
            lOKsize.Location = new Point(989, 267);
            lOKsize.Name = "lOKsize";
            lOKsize.Size = new Size(80, 16);
            lOKsize.TabIndex = 81;
            lOKsize.Text = "0 GB";
            lOKsize.TextAlign = ContentAlignment.MiddleRight;
            // 
            // lRate
            // 
            lRate.AutoSize = true;
            lRate.Font = new Font("宋体", 12F);
            lRate.Location = new Point(1098, 53);
            lRate.Name = "lRate";
            lRate.Size = new Size(55, 16);
            lRate.TabIndex = 82;
            lRate.Text = "压缩率";
            // 
            // tbgetpw
            // 
            tbgetpw.Font = new Font("宋体", 12F);
            tbgetpw.Location = new Point(274, 121);
            tbgetpw.Name = "tbgetpw";
            tbgetpw.Size = new Size(292, 26);
            tbgetpw.TabIndex = 83;
            // 
            // btngetpw
            // 
            btngetpw.Cursor = Cursors.Hand;
            btngetpw.Font = new Font("微软雅黑", 14.25F);
            btngetpw.ForeColor = Color.FromArgb(192, 0, 192);
            btngetpw.Location = new Point(572, 116);
            btngetpw.Name = "btngetpw";
            btngetpw.Size = new Size(140, 37);
            btngetpw.TabIndex = 84;
            btngetpw.Text = "查询密码";
            btngetpw.UseVisualStyleBackColor = true;
            btngetpw.Click += Btngetpw_Click;
            // 
            // tbFileName
            // 
            tbFileName.Font = new Font("宋体", 12F);
            tbFileName.Location = new Point(14, 121);
            tbFileName.Name = "tbFileName";
            tbFileName.Size = new Size(242, 26);
            tbFileName.TabIndex = 85;
            tbFileName.TextChanged += tbFileName_TextChanged;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Font = new Font("宋体", 12F);
            label8.Location = new Point(14, 75);
            label8.Name = "label8";
            label8.Size = new Size(135, 16);
            label8.TabIndex = 87;
            label8.Text = "保存到此文件夹：";
            // 
            // CbDel
            // 
            CbDel.AutoSize = true;
            CbDel.Font = new Font("宋体", 12F);
            CbDel.Location = new Point(380, 190);
            CbDel.Name = "CbDel";
            CbDel.Size = new Size(154, 20);
            CbDel.TabIndex = 90;
            CbDel.Text = "完成后删除源文件";
            CbDel.UseVisualStyleBackColor = true;
            CbDel.Click += CbDel_CheckedChanged;
            // 
            // Btndepress
            // 
            Btndepress.Cursor = Cursors.Hand;
            Btndepress.Font = new Font("楷体", 26.25F, FontStyle.Bold);
            Btndepress.ForeColor = Color.Red;
            Btndepress.Location = new Point(736, 185);
            Btndepress.Name = "Btndepress";
            Btndepress.Size = new Size(141, 63);
            Btndepress.TabIndex = 91;
            Btndepress.Text = "解压";
            Btndepress.UseVisualStyleBackColor = true;
            Btndepress.Click += Btndepress_Click;
            // 
            // CbSolid
            // 
            CbSolid.AutoSize = true;
            CbSolid.Checked = true;
            CbSolid.CheckState = CheckState.Checked;
            CbSolid.Font = new Font("宋体", 12F);
            CbSolid.Location = new Point(1368, 49);
            CbSolid.Name = "CbSolid";
            CbSolid.Size = new Size(58, 20);
            CbSolid.TabIndex = 92;
            CbSolid.Text = "固实";
            CbSolid.TextAlign = ContentAlignment.MiddleCenter;
            CbSolid.UseVisualStyleBackColor = true;
            CbSolid.CheckedChanged += CbSolid_CheckedChanged;
            // 
            // lMoveFailNum
            // 
            lMoveFailNum.AutoSize = true;
            lMoveFailNum.Font = new Font("新宋体", 12F);
            lMoveFailNum.Location = new Point(1317, 265);
            lMoveFailNum.Name = "lMoveFailNum";
            lMoveFailNum.Size = new Size(103, 16);
            lMoveFailNum.TabIndex = 93;
            lMoveFailNum.Text = "移动失败数:0";
            lMoveFailNum.TextAlign = ContentAlignment.MiddleRight;
            // 
            // btnZoom
            // 
            btnZoom.AutoSize = true;
            btnZoom.BackColor = SystemColors.Control;
            btnZoom.BackgroundImageLayout = ImageLayout.None;
            btnZoom.Cursor = Cursors.Hand;
            btnZoom.FlatAppearance.BorderColor = SystemColors.Control;
            btnZoom.FlatAppearance.BorderSize = 0;
            btnZoom.Font = new Font("微软雅黑", 18F);
            btnZoom.ForeColor = Color.Black;
            btnZoom.Location = new Point(883, 186);
            btnZoom.Margin = new Padding(0);
            btnZoom.Name = "btnZoom";
            btnZoom.Size = new Size(100, 61);
            btnZoom.TabIndex = 94;
            btnZoom.Text = "放大";
            btnZoom.UseVisualStyleBackColor = true;
            btnZoom.Click += BtnSize_Click;
            // 
            // btnAsSource
            // 
            btnAsSource.AutoSize = true;
            btnAsSource.BackColor = SystemColors.Control;
            btnAsSource.BackgroundImageLayout = ImageLayout.None;
            btnAsSource.Cursor = Cursors.Hand;
            btnAsSource.FlatAppearance.BorderColor = SystemColors.Control;
            btnAsSource.FlatAppearance.BorderSize = 0;
            btnAsSource.Font = new Font("微软雅黑", 14.25F);
            btnAsSource.ForeColor = Color.Black;
            btnAsSource.Location = new Point(177, 66);
            btnAsSource.Margin = new Padding(0);
            btnAsSource.Name = "btnAsSource";
            btnAsSource.Size = new Size(79, 35);
            btnAsSource.TabIndex = 96;
            btnAsSource.Text = "同上";
            btnAsSource.UseVisualStyleBackColor = true;
            btnAsSource.Click += btnAsSource_Click;
            // 
            // btnCancel
            // 
            btnCancel.Font = new Font("宋体", 10.8F, FontStyle.Bold, GraphicsUnit.Point, 134);
            btnCancel.ForeColor = Color.Red;
            btnCancel.Location = new Point(988, 151);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(81, 28);
            btnCancel.TabIndex = 40;
            btnCancel.Text = "中止";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += BtnCancel_Click;
            // 
            // lbStatus
            // 
            lbStatus.AutoSize = true;
            lbStatus.Font = new Font("微软雅黑", 10.8F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lbStatus.Location = new Point(14, 98);
            lbStatus.Name = "lbStatus";
            lbStatus.Size = new Size(39, 20);
            lbStatus.TabIndex = 41;
            lbStatus.Text = "就绪";
            // 
            // lbCurrentFile
            // 
            lbCurrentFile.AutoSize = true;
            lbCurrentFile.Font = new Font("微软雅黑", 10.8F, FontStyle.Regular, GraphicsUnit.Point, 134);
            lbCurrentFile.Location = new Point(177, 98);
            lbCurrentFile.Name = "lbCurrentFile";
            lbCurrentFile.Size = new Size(69, 20);
            lbCurrentFile.TabIndex = 42;
            lbCurrentFile.Text = "当前文件";
            // 
            // labelSourceSize
            // 
            labelSourceSize.Font = new Font("宋体", 12F);
            labelSourceSize.Location = new Point(379, 267);
            labelSourceSize.Name = "labelSourceSize";
            labelSourceSize.Size = new Size(87, 16);
            labelSourceSize.TabIndex = 97;
            labelSourceSize.Text = "总文件大小";
            labelSourceSize.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // Mainform
            // 
            AllowDrop = true;
            AutoScaleDimensions = new SizeF(6F, 12F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = SystemColors.AppWorkspace;
            ClientSize = new Size(1448, 804);
            Controls.Add(labelSourceSize);
            Controls.Add(btnAsSource);
            Controls.Add(btnZoom);
            Controls.Add(lMoveFailNum);
            Controls.Add(CbSolid);
            Controls.Add(Btndepress);
            Controls.Add(CbDel);
            Controls.Add(label8);
            Controls.Add(tbFileName);
            Controls.Add(btngetpw);
            Controls.Add(tbgetpw);
            Controls.Add(lRate);
            Controls.Add(lOKsize);
            Controls.Add(Cbpw);
            Controls.Add(cbYiYaSuo);
            Controls.Add(cbShutdown);
            Controls.Add(cbMoveSource);
            Controls.Add(labeldecp);
            Controls.Add(lfail);
            Controls.Add(lokCount);
            Controls.Add(label5);
            Controls.Add(tbSize);
            Controls.Add(cbDivide);
            Controls.Add(cbCheck);
            Controls.Add(label4);
            Controls.Add(tbTmp);
            Controls.Add(cbQucikOpen);
            Controls.Add(tbRec);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(cbRate);
            Controls.Add(cbFrom);
            Controls.Add(cbAdd);
            Controls.Add(cbGMK);
            Controls.Add(tbVolume);
            Controls.Add(btnPW);
            Controls.Add(lpw);
            Controls.Add(tbNotice);
            Controls.Add(cbNotice);
            Controls.Add(labelSourceCount);
            Controls.Add(btnRefresh);
            Controls.Add(btnHide);
            Controls.Add(tbPW);
            Controls.Add(label3);
            Controls.Add(exTension);
            Controls.Add(enclosureList);
            Controls.Add(label6);
            Controls.Add(btnClearSource);
            Controls.Add(btnClearAll);
            Controls.Add(btnAddFile);
            Controls.Add(rtbFail);
            Controls.Add(btnReset);
            Controls.Add(btnFail);
            Controls.Add(btnSuccess);
            Controls.Add(cbExist);
            Controls.Add(btnRun);
            Controls.Add(btnSavePath);
            Controls.Add(tbSavePath);
            Controls.Add(rtbCMD);
            Controls.Add(rtbOk);
            Controls.Add(rtbSource);
            Controls.Add(tbSource);
            Controls.Add(btnSource);
            Controls.Add(btnCancel);
            Controls.Add(lbStatus);
            Controls.Add(lbCurrentFile);
            Font = new Font("宋体", 9F);
            Icon = (Icon)resources.GetObject("$this.Icon");
            KeyPreview = true;
            Name = "Mainform";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "RAR批量压缩解压——鼠标放在按钮上查看说明";
            Load += Mainform_Load;
            KeyDown += Mainform_KeyDown;
            contextMenuStrip1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button btnSource;
        private System.Windows.Forms.TextBox tbSource;
        private System.Windows.Forms.RichTextBox rtbSource;
        private System.Windows.Forms.RichTextBox rtbOk;
        private System.Windows.Forms.RichTextBox rtbCMD;
        private System.Windows.Forms.TextBox tbSavePath;
        private System.Windows.Forms.Button btnSavePath;
        private System.Windows.Forms.Button btnRun;
        private System.Windows.Forms.TextBox exTension;
        private System.Windows.Forms.ComboBox cbExist;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 退出ToolStripMenuItem;
        private System.Windows.Forms.Button btnSuccess;
        private System.Windows.Forms.Button btnFail;
        private System.Windows.Forms.RichTextBox rtbFail;
        private System.Windows.Forms.Button btnAddFile;
        private System.Windows.Forms.Button btnClearAll;
        private System.Windows.Forms.Button btnClearSource;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.RichTextBox enclosureList;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbPW;
        private System.Windows.Forms.Button btnHide;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Label labelSourceCount;
        private System.Windows.Forms.CheckBox cbNotice;
        private System.Windows.Forms.TextBox tbNotice;
        private System.Windows.Forms.Label lpw;
        private System.Windows.Forms.Button btnPW;
        private System.Windows.Forms.ComboBox cbGMK;
        private System.Windows.Forms.CheckBox cbAdd;
        private System.Windows.Forms.ComboBox cbFrom;
        private System.Windows.Forms.ComboBox cbRate;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbRec;
        private System.Windows.Forms.CheckBox cbQucikOpen;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbTmp;
        private System.Windows.Forms.CheckBox cbCheck;
        private System.Windows.Forms.CheckBox cbDivide;
        private System.Windows.Forms.TextBox tbSize;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label lokCount;
        private System.Windows.Forms.Label lfail;
        private System.Windows.Forms.Label labeldecp;
        private System.Windows.Forms.CheckBox cbMoveSource;
        private System.Windows.Forms.CheckBox cbShutdown;
        private System.Windows.Forms.CheckBox cbYiYaSuo;
        private System.Windows.Forms.CheckBox Cbpw;
        private System.Windows.Forms.Label lOKsize;
        private System.Windows.Forms.Label lRate;
        private System.Windows.Forms.TextBox tbVolume;
        private System.Windows.Forms.TextBox tbgetpw;
        private System.Windows.Forms.Button btngetpw;
        private System.Windows.Forms.TextBox tbFileName;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.CheckBox CbDel;
        private System.Windows.Forms.Button Btndepress;
        private System.Windows.Forms.CheckBox CbSolid;
        private System.Windows.Forms.Label lMoveFailNum;
        private System.Windows.Forms.Button btnZoom;
        private System.Windows.Forms.Button btnAsSource;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lbStatus;
        private System.Windows.Forms.Label lbCurrentFile;
        private Label labelSourceSize;
    }
}

