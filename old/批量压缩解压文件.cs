using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks; // 添加Task支持
using System.Threading; // 添加线程支持
//如何以管理员权限运行vs：右键点击【devenv.exe】，点击【兼容性疑难解答】，点击【需要更多权限】
//压缩时的密码带扩展名，如1.jpg
//解压时需要去掉.rar，如1.jpg.rar变成1.jpg
namespace 批量压缩
{
    public partial class Mainform : Form
    {
        public Mainform()
        {
            InitializeComponent();
        }
        private void Mainform_Load(object sender, EventArgs e)
        {
            lOKsize.Text = "0.0GB";
            btnZoom.Text = "缩小";
            Font = new Font("宋体", 9f);//11.25f
            WindowState = FormWindowState.Maximized;
            Win32Utility.SetCueText(tbSource, "待解压文件们所在文件夹");
            tbSource.Text = @"I:\";
            tbSavePath.Text = @"H:\其它课程\";
            //tbFileName.Text = @"d:\00\";
            //tbSavePath.Text = tbSource.Text;
            tbTmp.Text = tbSavePath.Text;
            tbNotice.Text = ".\\注释.txt";
            tbSize.Text = "666";
            cbFrom.SelectedIndex = 1;       //从TXT还是路径读取文件
            //tbSource.Text = @"B:\0smwr\0desktop\文件名密码.txt";
            cbGMK.SelectedIndex = 0;        //分卷大小单位是g、m、k 对应0 1 2
            cbExist.SelectedIndex = 2;      //压缩时就存在的文件默认【覆盖】
            //cbSave.SelectedIndex = 1;     //压缩文件保存到哪里
            //tbSavePath.Visible = false;
            //btnSavePath.Visible = false;
            cbYiYaSuo.Checked = true;//不压缩名字带【已压缩】的文件/文件夹
            cbRate.SelectedIndex = 1;//压缩率
            Cbpw.Checked = true;//随机密码
            CbSolid.Checked = true;//固实
            //cbMoveSource.Checked = true;//压缩后移动源文件到"【已压缩】"

            IDataObject iData = Clipboard.GetDataObject();
            if (iData.GetDataPresent(DataFormats.Text))
            {
                string cb = (String)iData.GetData(DataFormats.Text);
                if (Directory.Exists(cb))
                {
                    tbSource.Text = cb;
                }
            }
            lbCurrentFile.Text = "info";
            string jy = "c:\\【解压密码】";
            enclosureList.AppendText(jy + "发邮件给 qgkc520@Gmail.com\n");
            enclosureList.AppendText(jy + "微信号：i17269637581\n");
            enclosureList.AppendText(jy + "QQ号：2027123419\n");
            enclosureList.AppendText(jy + "微信号可能会改名，如果搜不到，请通过邮箱联系\n");

            this.AllowDrop = true; // 允许控件接受拖放操作
            ToolTip ttpSettings = new() // 气球提示设置，鼠标指向控件时显示提示信息  
            {
                InitialDelay = 100,
                AutoPopDelay = 1000000,
                ReshowDelay = 100,
                ShowAlways = true,
                IsBalloon = true
            };
            string tipbtnSource = "如果想选择从txt读取密码，先输入【待解压文件们所在文件夹】，如'D:\\压缩文件夹'\ntxt格式为:奇数行是不带【路径】、【后缀】的文件名，偶数行是密码。如：\n文件1\n密码1\n文件2\n密码2\n\n点击【选txt】按钮后，富文本框最终将变成：\nD:\\文件1.rar\n密码1\nD:\\文件2.rar\n密码2\n\n最后点【压缩】或【解压】即可";
            ttpSettings.SetToolTip(cbFrom, tipbtnSource); //cb改成哪个控件，哪个控件就弹提示
            ttpSettings.SetToolTip(btnSource, tipbtnSource); //来源按钮的提示
            string tipOverwrite = "不压缩模式不可进行【固实】，其他都可以。\n若勾选【固实】，部分网盘不提供在线解压功能";
            ttpSettings.SetToolTip(CbSolid, tipOverwrite); //固实的提示
            string rateTXT = "压缩率选得越高，占用资源越多，但是文件大小一般不会明显减小\n推荐选择【不压缩】或【轻度压缩】\n【不压缩】：\n把待压缩文件单纯打包成一个文件，相当于复制。勾选加密后仍可进行加密";
            ttpSettings.SetToolTip(cbRate, rateTXT);
            string btngetpwTip = "1、在这一行第一个输入框输入文件名（不要带后缀，会自动加上右侧的拓展名）\n2、点击【查询密码】，自动计算出密码，同时复制到剪切板";
            ttpSettings.SetToolTip(btngetpw, btngetpwTip);
            string tbTXTTip = "如果选择了从txt获取文件名和密码，那么这个框输入txt的路径，如D:\\密码本.txt";
            ttpSettings.SetToolTip(tbFileName, tbTXTTip);
            Win32Utility.SetCueText(tbSource, "鼠标指向按钮查看说明");
        }
        // 添加取消操作的支持
        private CancellationTokenSource _cancellationTokenSource;

        // 添加进度报告的支持
        private IProgress<CompressionProgressInfo> _progressReporter;

        // 定义进度信息类
        private class CompressionProgressInfo
        {
            public string CurrentFile { get; set; }
            public int SuccessCount { get; set; }
            public int FailCount { get; set; }
            public int IgnoreCount { get; set; }
            public int NonExistCount { get; set; }
            public double CompressedSize { get; set; }
            public string Message { get; set; }
            public bool IsError { get; set; }
        }

        //开始压缩！
        private async void BtnRun_Click(object sender, EventArgs e)
        {
            // 禁用压缩和解压按钮，防止重复点击
            Btndepress.Enabled = false;
            btnRun.Enabled = false;

            try
            {
                // 创建取消令牌源
                _cancellationTokenSource = new CancellationTokenSource();

                // 创建进度报告器
                _progressReporter = new Progress<CompressionProgressInfo>(ReportProgress);

                // 显示后台工作指示
                lbStatus.Text = "压缩中...";

                // 异步执行压缩操作
                await Task.Run(() => Compression(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                // 压缩完成后的处理
                lbStatus.Text = "压缩完成!";
            }
            catch (OperationCanceledException)
            {
                lbStatus.Text = "操作已取消";
            }
            catch (Exception ex)
            {
                lbStatus.Text = "发生错误";
                MessageBox.Show($"压缩过程中发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // 清理资源
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }

                // 重新启用按钮
                btnRun.Enabled = true;
                Btndepress.Enabled = true;
            }
        }

        // 进度报告处理方法
        private void ReportProgress(CompressionProgressInfo info)
        {
            // 更新UI
            if (info.CurrentFile != null)
            {
                lbCurrentFile.Text = info.CurrentFile;
            }

            lokCount.Text = info.SuccessCount.ToString() + "个文件";
            lfail.Text = info.FailCount.ToString();

            if (info.Message != null)
            {
                if (info.IsError)
                {
                    rtbFail.AppendText(info.Message + "\n");
                    rtbFail.ScrollToCaret();
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        rtbOk.AppendText(info.Message + "\n");
                        rtbOk.ScrollToCaret();
                    });
                }
            }

            if (info.CompressedSize > 0)
            {
                lOKsize.Text = info.CompressedSize.ToString("F2") + "GB";
                notifyIcon.ShowBalloonTip(3000, "已压缩 " + info.CompressedSize.ToString("F2") + " GB", lokCount.Text, ToolTipIcon.Info);
            }
        }

        bool isAdvancedEnabled;
        TimeSpan ts1;
        TimeSpan ts2;
        TimeSpan ts3;
        DateTime dt1;
        DateTime dt2;
        DateTime dtEach;
        string dateDiff;
        // 异步压缩方法
        private void Compression(CancellationToken cancellationToken)
        {
            ts1 = new(DateTime.Now.Ticks);
            dt1 = DateTime.Now;
            double beginSize = 0;//压缩前保存压缩文件的文件夹有多大文件了。
            if (Directory.Exists(tbSavePath.Text.Trim()))
            {
                beginSize = TotalSize(tbSavePath.Text.Trim());
            }

            // 在UI线程更新初始大小
            this.Invoke((MethodInvoker)delegate
            {
                lOKsize.Text = TotalSize(tbSavePath.Text.Trim()).ToString("F1") + "GB";
            });

            int fileNum = 0;//待压缩文件总数
            int successFile = 0;//成功缩文件数
            int failFile = 0;//失败压缩文件数
            int nonFile = 0;//不存在压缩文件数
            int ignoreFile = 0;//忽略压缩文件数

            string savePath = (tbSavePath.Text.Trim() + "\\").Replace("\\\\", "\\");
            string extension = "." + exTension.Text.Trim();//把扩展名加上点
            string volumeText = tbVolume.Text.Trim();
            string sourcePath = tbSource.Text.Trim();
            string noticePath = tbNotice.Text.Trim();
            string password = tbPW.Text.Trim();
            string recoveryRecord = tbRec.Text.Trim();
            string tempPath = tbTmp.Text.Trim();


            // 获取所有文件行
            string[] fileLines = null;
            this.Invoke((MethodInvoker)delegate
            {
                fileLines = rtbSource.Lines;
            });
            //读取富文本框里的所有行，每行就是每个要压缩的文件/夹
            foreach (string oldFullName in fileLines)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;// 检查是否请求取消操作
                }
                if (string.IsNullOrEmpty(oldFullName))//要压缩的文件路径这一行为空就跳过
                {
                    continue;
                }

                // 创建进度信息
                var progressInfo = new CompressionProgressInfo
                {
                    CurrentFile = oldFullName,
                    SuccessCount = successFile,
                    FailCount = failFile,
                    IgnoreCount = ignoreFile,
                    NonExistCount = nonFile
                };

                //如果这一行不存在，提示用户并跳过
                if (!File.Exists(oldFullName) && !Directory.Exists(oldFullName))
                {
                    progressInfo.Message = "不存在： " + oldFullName;
                    progressInfo.IsError = true;
                    _progressReporter.Report(progressInfo);
                    nonFile++;
                    continue;
                }

                if (cbYiYaSuo.Checked && Path.GetFileNameWithoutExtension(oldFullName).Contains("【已压缩】"))
                {
                    continue;//如果勾选了【不解压含"【已解压】"的文件】且文件名包含"【已解压】"，跳过它
                }

                fileNum++;

                // 如果要压缩的是文件夹就在里面创建文件夹作为联系方式
                string[] enclosureLines = null;
                bool shouldAddEnclosure = false;
                bool isRandomPassword = false;
                int existAction = 0;

                isAdvancedEnabled = false;

                this.Invoke((MethodInvoker)delegate
                {
                    enclosureLines = enclosureList.Lines;
                    shouldAddEnclosure = cbAdd.Checked && enclosureList.Text != "";
                    isRandomPassword = Cbpw.Checked;
                    existAction = cbExist.SelectedIndex;
                    isAdvancedEnabled = !tbNotice.ReadOnly;
                });

                if (Directory.Exists(oldFullName) && shouldAddEnclosure)
                {
                    if (enclosureLines.Length > 0 && !string.IsNullOrEmpty(enclosureLines[0]))
                    {
                        foreach (var item in enclosureLines)
                        {
                            string mail = oldFullName + "\\" + Path.GetFileName(item);
                            if (!Directory.Exists(mail))
                            {
                                Directory.CreateDirectory(mail);
                            }
                        }
                    }
                }
                //压缩到指定文件夹，如果保存压缩文件的文件夹不存在就创建
                string newDir = savePath;
                if (!Directory.Exists(newDir))
                {
                    Directory.CreateDirectory(newDir);
                }

                //压缩后的纯文件名+后缀，如"1.jpg"压缩后-》"1.jpg.rar"。
                string name = Path.GetFileName(oldFullName) + extension;
                //压缩出的文件的绝对路径+文件名
                string saveRarFullName = (newDir + @"\" + name).Replace("\\\\", "\\");

                if (File.Exists(saveRarFullName))
                {
                    if (existAction == 0)//如果要压缩到的文件存在且选中【跳过】
                    {
                        progressInfo.Message = "已跳过： " + saveRarFullName;
                        progressInfo.IsError = true;
                        _progressReporter.Report(progressInfo);
                        ignoreFile++;
                        progressInfo.IgnoreCount = ignoreFile;
                        continue;
                    }
                    else if (existAction == 2)//如果要压缩到的文件存在且选中【替换】
                    {
                        File.Delete(saveRarFullName);
                    }
                }

                // 准备压缩参数
                string enclosure = "";//在压缩文件中附加文件
                string mm_p = "";//解压密码参数
                string volume = "300";//分卷大小
                string tmp = tempPath;//临时文件夹
                string raRate = "-m";//压缩率
                string quickOpen = "";//添加快速打开信息
                string rec = "0";//添加恢复记录大小
                string notice = "";//注释
                string check = "";//校验
                int compressionLevel = 0;
                bool isSolid = false;
                bool isDivide = false;
                bool isQuickOpen = false;
                bool isCheckEnabled = false;
                bool isNoticeEnabled = false;
                string divideSize = "";

                // 从UI获取设置
                this.Invoke((MethodInvoker)delegate
                {
                    compressionLevel = cbRate.SelectedIndex;
                    isSolid = CbSolid.Checked;
                    isDivide = cbDivide.Checked;
                    isQuickOpen = cbQucikOpen.Checked;
                    isCheckEnabled = cbCheck.Checked;
                    isNoticeEnabled = cbNotice.Checked;
                    divideSize = tbVolume.Text;
                });

                // 不压缩、直接保存的文件类型
                string exception = " -ms7z;ace;arj;bz2;cab;gz;mp4;mkv;rm;rmvb;flv;mov;lha;lz;lzh;mp3;rar;taz;tgz;xz;z;zip;zipx";
                string cite = "-oi:50000000";//将50mb以上的文件保存为引用
                string solid = "";//是否固实：-s。锁定： -k
                string shouldSkip = "-o-";

                if (isSolid)
                {
                    solid = " -s -md32 -k ";//字典大小32mb
                }

                // 处理密码
                if (!isRandomPassword && !string.IsNullOrEmpty(password))
                {
                    password = password;
                }

                // 高级功能
                if (isAdvancedEnabled)
                {
                    // 随机密码
                    if (isRandomPassword)
                    {
                        // 在UI线程更新失败列表
                        //this.Invoke((MethodInvoker)delegate
                        //{
                        //    rtbFail.AppendText(name);
                        //});

                        password = MyMd5.MD5UTF878(name + "592ptt1314") + MyMd5.MD5UTF878(name + "592pnn1314");
                        
                    }

                    // 分卷大小
                    if (isDivide && !string.IsNullOrEmpty(divideSize))
                    {
                        string selectedUnit = "";

                        this.Invoke((MethodInvoker)delegate
                        {
                            selectedUnit = cbGMK.SelectedItem.ToString();
                        });

                        volume = "-v" + divideSize + selectedUnit;
                    }

                    // 压缩率
                    raRate += compressionLevel;

                    // 添加恢复记录大小
                    if (IsNum(recoveryRecord))
                    {
                        rec = recoveryRecord;
                    }

                    // 添加快速打开信息
                    if (isQuickOpen)
                    {
                        quickOpen = "-qo+";
                    }

                    // 如果临时文件夹不存在
                    if (!string.IsNullOrEmpty(tmp) && !Directory.Exists(tmp))
                    {
                        Directory.CreateDirectory(tmp);
                    }

                    tmp = "-w\"" + tmp + "\"";

                    // 注释文件
                    if (isNoticeEnabled && File.Exists(noticePath))
                    {
                        notice = "-z\"" + noticePath + "\"";
                    }

                    // 压缩完是否校验
                    if (isCheckEnabled)
                    {
                        check = "-t";
                    }
                    if (cbExist.SelectedIndex == 2)
                    {
                        shouldSkip = "-o+";//覆盖
                    }
                    else if (cbExist.SelectedIndex == 1)
                    {
                        shouldSkip = "-u";//更新
                    }
                }

                // 执行的rar命令
                string shellArguments = $@"A -ep1 -IBCK {shouldSkip} -SCf ""{saveRarFullName}"" ""{oldFullName}"" {enclosure} -p""{password}"" {notice} {volume} {raRate} -rr{rec} {quickOpen} {tmp} {check} {exception} {solid} {cite}";

                // 在UI线程更新命令显示
                this.Invoke((MethodInvoker)delegate
                {
                    rtbCMD.AppendText(shellArguments + "\n\n");
                    rtbCMD.ScrollToCaret();
                });

                // 执行压缩操作，获取rar返回码
                int dcr = API.CompressByRar(shellArguments);

                if (dcr == 0 || dcr == 1) // 成功
                {
                    progressInfo.Message = saveRarFullName;
                    progressInfo.IsError = false;
                    successFile++;
                    progressInfo.SuccessCount = successFile;
                    _progressReporter.Report(progressInfo);

                    // 压缩后处理源文件
                    bool shouldDelete = false;
                    bool shouldMove = false;

                    this.Invoke((MethodInvoker)delegate
                    {
                        shouldDelete = CbDel.Checked;
                        shouldMove = cbMoveSource.Checked;
                    });

                    // 压缩后删除源文件
                    if (shouldDelete)
                    {
                        if (Directory.Exists(oldFullName))
                        {
                            Directory.Delete(oldFullName, true);
                        }
                        else if (File.Exists(oldFullName))
                        {
                            File.Delete(oldFullName);
                        }
                        this.Invoke((MethodInvoker)delegate
                        {
                            rtbOk.AppendText("已删除");
                        });
                    }
                    // 压缩后移动源文件
                    else if (shouldMove)
                    {
                        string oldpath = "";
                        if (Directory.Exists(oldFullName)) // 如果是文件夹
                        {
                            DirectoryInfo directoryInfo = new(oldFullName);
                            try
                            {
                                if (directoryInfo.Parent != null)
                                {
                                    oldpath = directoryInfo.Parent.FullName;
                                }
                            }
                            catch (Exception ex)
                            {
                                string errorMessage = ex + "\n未找到父目录，请核对:" + oldFullName;
                                // 在UI线程显示错误
                                this.Invoke((MethodInvoker)delegate
                                {
                                    MessageBox.Show(errorMessage);
                                });
                            }
                        }
                        else if (File.Exists(oldFullName)) // 如果是文件，获取文件所在的目录
                        {
                            FileInfo fileInfo = new(oldFullName);
                            oldpath = fileInfo.DirectoryName;
                        }
                        else
                        {
                            progressInfo.Message = "\n识别文件或文件夹失败:" + oldFullName;
                            progressInfo.IsError = true;
                            _progressReporter.Report(progressInfo);
                            continue;
                        }

                        if (!Directory.Exists(oldpath + "\\【已压缩】\\"))
                        {
                            Directory.CreateDirectory(oldpath + "\\【已压缩】\\");
                        }

                        string newFullName = (oldpath + "\\【已压缩】\\" + Path.GetFileName(oldFullName)).Replace("\\\\", "\\");
                        if (!Directory.Exists(newFullName) && !File.Exists(newFullName))
                        {
                            try
                            {
                                Directory.Move(oldFullName, newFullName); // 重命名
                                this.Invoke((MethodInvoker)delegate
                                {
                                    rtbOk.AppendText("已移动");
                                });
                            }
                            catch (Exception ex)
                            {
                                progressInfo.Message = ex + " 行代码出现问题\n" + oldFullName + "\n" + newFullName;
                                progressInfo.IsError = true;
                                _progressReporter.Report(progressInfo);
                            }
                        }
                        else
                        {
                            progressInfo.Message = $"移动/重命名失败，文件已存在：{newFullName}\n";
                            progressInfo.IsError = true;
                            _progressReporter.Report(progressInfo);
                        }
                    }
                }
                else if (dcr == -2)
                {
                    // 在UI线程显示错误
                    this.Invoke((MethodInvoker)delegate
                    {
                        MessageBox.Show("请安装 WinRAR 5.0 以上版本");
                    });
                    break;
                }
                else // 压缩失败
                {
                    progressInfo.Message = $"555，压缩失败了：{saveRarFullName}\n{RarTips(dcr)}\n";
                    progressInfo.IsError = true;
                    failFile++;
                    progressInfo.FailCount = failFile;
                    _progressReporter.Report(progressInfo);
                }

                // 报告当前压缩大小
                double yiYaSuoSize = TotalSize(savePath) - beginSize;
                progressInfo.CompressedSize = yiYaSuoSize;
                _progressReporter.Report(progressInfo);

                // 更新托盘图标
                this.Invoke((MethodInvoker)delegate
                {
                    notifyIcon.Text = yiYaSuoSize.ToString("F3") + "GB";
                });

                // 检查是否达到目标大小
                double targetSize = 0;
                bool shouldCheckSize = false;
                this.Invoke((MethodInvoker)delegate
                {
                    if (double.TryParse(tbSize.Text, out targetSize))
                    {
                        shouldCheckSize = true;
                    }
                });

                if (shouldCheckSize && yiYaSuoSize > targetSize)
                {
                    ts2 = new(DateTime.Now.Ticks);
                    ts3 = ts1.Subtract(ts2).Duration();
                    dateDiff = ts3.Hours.ToString() + "小时" + ts3.Minutes.ToString() + "分钟" + ts3.Seconds.ToString() + "秒";
                    dt2 = DateTime.Now;

                    string summaryMessage = $"达到文件大小\n" +
                        $"用时：{dateDiff}\n" +
                        $"开始时间：{dt1.ToShortTimeString()}\n" +
                        $"结束时间：{dt2.ToShortTimeString()}\n" +
                        $"压缩成功：{successFile}\n" +
                        $"压缩失败：{failFile}\n" +
                        $"不存在的文件：{nonFile}\n" +
                        $"忽略文件：{ignoreFile}";

                    // 在UI线程更新结果
                    this.Invoke((MethodInvoker)delegate
                    {
                        rtbFail.AppendText(summaryMessage);
                    });
                    break;
                }
            }

            // 处理关机操作
            bool shouldShutdown = false;
            isAdvancedEnabled = false;

            this.Invoke((MethodInvoker)delegate
            {
                shouldShutdown = cbShutdown.Checked;
                isAdvancedEnabled = !tbNotice.ReadOnly;
            });

            if (shouldShutdown && isAdvancedEnabled) // 关机
            {
                Process.Start("c:/windows/system32/shutdown.exe", "-s");

                // 在UI线程执行询问操作
                this.Invoke((MethodInvoker)delegate
                {
                    MessageBoxButtons messButton = MessageBoxButtons.OKCancel;
                    DialogResult dr = MessageBox.Show("取消关机吗?", "即将关机", messButton);
                    if (dr == DialogResult.OK) // 如果点击"确定"按钮
                    {
                        Process.Start("c:/windows/system32/shutdown.exe", "-a");
                    }
                });
            }

            // 最终统计
            dt2 = DateTime.Now;
            ts2 = new(DateTime.Now.Ticks);
            ts3 = ts1.Subtract(ts2).Duration();
            dateDiff = ts3.Hours.ToString() + "小时" + ts3.Minutes.ToString() + "分钟" + ts3.Seconds.ToString() + "秒";

            // 在UI线程更新结果
            this.Invoke((MethodInvoker)delegate
            {
                notifyIcon.Text = "完成";

                if (fileNum == 0)
                {
                    rtbOk.AppendText("没有压缩任何文件\n");
                }
                else
                {
                    if (failFile + nonFile == 0)
                    {
                        rtbOk.AppendText("开始时间：" + dt1.ToShortTimeString().ToString() + "\n结束时间" + dt2.ToShortTimeString().ToString()
                            + "\n用时：" + dateDiff + "\n压缩全部成功！o(*￣▽￣*)ブ\n文件总数：" + successFile + "\n忽略文件：" + ignoreFile);
                    }
                    else
                    {
                        rtbOk.AppendText("开始时间：" + dt1.ToShortTimeString().ToString() + "\n结束时间" + dt2.ToShortTimeString().ToString()
                            + "\n用时：" + dateDiff + "\n压缩成功：" + successFile + "\n压缩失败：" + failFile
                            + "\n不存在的文件：" + nonFile + "\n忽略文件：" + ignoreFile);
                    }
                    rtbOk.ScrollToCaret();
                }
            });
        }

        // 添加取消压缩的功能
        private void BtnCancel_Click(object sender, EventArgs e)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                lbStatus.Text = "正在取消，等待当前文件解压完成...";
            }
        }

        //开始解压！
        private async void Btndepress_Click(object sender, EventArgs e)
        {
            // 禁用压缩和解压按钮，防止重复点击
            Btndepress.Enabled = false;
            btnRun.Enabled = false;
            try
            {
                // 创建取消令牌源
                _cancellationTokenSource = new CancellationTokenSource();

                // 创建进度报告器
                _progressReporter = new Progress<CompressionProgressInfo>(ReportProgress);

                // 显示后台工作指示
                lbStatus.Text = "解压中...";

                // 异步执行解压操作
                await Task.Run(() => Decompression(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                // 解压完成后的处理
                lbStatus.Text = "解压完成!";
            }
            catch (OperationCanceledException)
            {
                lbStatus.Text = "操作已取消";
            }
            catch (Exception ex)
            {
                lbStatus.Text = "发生错误";
                MessageBox.Show($"解压过程中发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // 清理资源
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }

                // 重新启用按钮
                Btndepress.Enabled = true;
                btnRun.Enabled = true;
            }
        }

        // 异步解压方法
        private void Decompression(CancellationToken cancellationToken)
        {
            // 这里实现解压缩逻辑，类似于Compression方法
            // 注意处理取消操作并使用进度报告器更新UI
            dt1 = DateTime.Now;
            double yiJieYa = 0;//已解压多少字节
            double sourceSize = 0;//待解压总大小
            int sourceFileCount = 0;//待解压文件数
            int successFile = 0;//成功解压文件数
            int failFile = 0;//失败解压文件数
            int failSum = 0;//失败移动文件数
            int nonFile = 0;//不存在解压文件数

            // 从UI获取设置
            string savePath = "";
            string sourcePath = "";
            string extension = ".";
            bool shouldCheckSize = false;
            double targetSize = 0;
            int txtOrRtb = 1;
            string[] rtbSourceLines = [];
            // 获取所有要解压的文件
            //如果选择从txt读取文件，则每行文件名和密码交替，即读取文件名的下一行作为密码
            bool readPwFromNextLine = false;
            // 在UI线程获取设置
            this.Invoke((MethodInvoker)delegate
            {
                txtOrRtb = cbFrom.SelectedIndex;
                readPwFromNextLine = txtOrRtb == 0;
                savePath = (tbSavePath.Text.Trim() + "\\").Replace("\\\\", "\\");
                rtbSourceLines = rtbSource.Lines;
                if (tbSavePath.Text == null)
                {
                    MessageBox.Show("请输入保存解压后的文件的路径");
                    return;
                }
                sourcePath = tbSource.Text.Trim();
                extension += exTension.Text.Trim();

                // 检查大小限制
                if (double.TryParse(tbSize.Text, out targetSize))
                {
                    shouldCheckSize = true;
                }
            });

            // 计算总解压文件大小和数量，如果从txt读取密码，则每行文件名和密码交替，间隔的奇数行才是文件名
            for (int i = 0; i < rtbSourceLines.Length; i++)
            {
                string fullNamei = rtbSourceLines[i];
                this.Invoke((MethodInvoker)delegate
                {
                    rtbOk.AppendText($"待解压文件：{fullNamei}\n\n");
                });
                //跳过空行和非rar文件
                if (string.IsNullOrEmpty(fullNamei) || Path.GetExtension(fullNamei) != extension)
                {
                    //this.Invoke((MethodInvoker)delegate
                    //{
                    //    rtbOk.AppendText($"错误！fullNamei{fullNamei}\n空行{string.IsNullOrEmpty(fullNamei)}\n文件是否存在{Path.GetExtension(fullNamei)}\n");
                    //}); 
                    continue;
                }
                if (File.Exists(fullNamei))
                {
                    FileInfo fileInfo = new(fullNamei);
                    string[] rarPart = RarPart(fileInfo, extension);
                    for (int j = 1; j < rarPart.Length; j++)
                    {
                        FileInfo fInfo = new(rarPart[j]);// c:\a.part[j].rar
                        sourceFileCount++;//待解压总文件数
                    }
                    sourceSize += Convert.ToInt64(rarPart[0]);//RarPart的第一个元素保存了压缩文件的总大小

                    this.Invoke((MethodInvoker)delegate
                    {
                        rtbOk.AppendText($"待解压文件size：{sourceSize}\n文件数量{sourceFileCount}\n");
                    });
                }
            }

            // 更新UI显示总数
            string sourceSizeGB = $"{(sourceSize / 1024 / 1024 / 1024):f3}GB";
            this.Invoke((MethodInvoker)delegate
            {
                //rtbFail.AppendText("待解压总大小：" + sourceSizeGB);
                labelSourceCount.Text = sourceFileCount.ToString();
                labelSourceSize.Text = $"{sourceSizeGB}";
            });

            // 逐个处理文件
            for (int i = 0; i < rtbSourceLines.Length; i++)
            {
                dtEach = DateTime.Now;
                // 检查是否请求取消操作
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // 获取文件和密码
                string file = rtbSourceLines[i];
                if (string.IsNullOrEmpty(file)) continue;

                if (Path.GetExtension(file) != extension) // 不是指定类型的压缩文件就不解压
                {
                    continue;
                }

                // 创建进度信息
                var progressInfo = new CompressionProgressInfo
                {
                    CurrentFile = file,
                    SuccessCount = successFile,
                    FailCount = failFile
                };

                // 检查文件是否存在
                if (!File.Exists(file))
                {
                    // 防止解压了第一卷后将以后所有的文件移动到"已解压"，下次找不到的误报
                    if (!file.Contains(".part"))
                    {
                        progressInfo.Message = "不存在以下文件：\n" + file;
                        progressInfo.IsError = true;
                        _progressReporter.Report(progressInfo);
                    }
                    continue;
                }

                FileInfo fileInfo = new(file);
                string name = fileInfo.Name;//不包含路径，如a.rar

                // 如果勾选了【不解压含“【已解压】”的文件】且文件名包含“【已解压】”，跳过它
                bool shouldSkipDecompressed = false;

                this.Invoke((MethodInvoker)delegate
                {
                    shouldSkipDecompressed = cbYiYaSuo.Checked && name.Contains("【已解压】");
                });

                if (shouldSkipDecompressed)
                {
                    continue;
                }

                if (readPwFromNextLine)
                {
                    if (i + 1 >= rtbSourceLines.Length) break;
                    i++; // 跳到下一行获取密码
                }

                string password = readPwFromNextLine ? rtbSourceLines[i] : "密码获取失败";

                if (name.Contains(".part"))     //判断是不是分卷文件，给予不同密码
                {
                    if (!(name.Contains(extension)))
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            rtbOk.AppendText($"-是分卷文件，但不是分卷的第一卷： {name}\n");
                        });
                        continue;               //如果不是分卷文件的第一个文件，跳过它
                    }
                    else
                    {
                        //把 a.part01.rar 变成 a.rar，这个地方决定了密码是以a.rar（+ extension）还是a（不加+ extension）为准
                        string namePart = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(name)) + extension;

                        this.Invoke((MethodInvoker)delegate
                        {
                            rtbOk.AppendText($"\n-是分卷文件的第一卷：{name}\n{namePart}\n");
                        });
                        if ((txtOrRtb == 1) && Cbpw.Checked)         //随机密码
                        {
                            password = MyMd5.MD5UTF878(namePart + "592ptt1314") + MyMd5.MD5UTF878(namePart + "592pnn1314");
                            this.Invoke((MethodInvoker)delegate
                            {
                                rtbOk.AppendText(namePart + "\n" + password + "\n");
                            });
                        }
                    }
                }
                else if ((txtOrRtb == 1) && Cbpw.Checked) //不是分卷文件，启用随机密码
                {
                    password = MyMd5.MD5UTF878(name + "592ptt1314") + MyMd5.MD5UTF878(name + "592pnn1314");
                    this.Invoke((MethodInvoker)delegate
                    {
                        rtbOk.AppendText(name + "\n" + password + "\n");
                    });
                }

                // 准备解压参数
                string existFile = "-o-"; // 默认跳过已存在文件
                this.Invoke((MethodInvoker)delegate
                {
                    if (cbExist.SelectedIndex == 2)//如果解压后的文件存在且选中【替换】
                    {
                        existFile = "-o+";
                    }
                });

                // 执行的rar命令， -y表示全部确认
                //$ 符号启用字符串内插，允许你在字符串中嵌入表达式，用 {} 包裹。
                //@ 符号创建逐字字符串文本，使得字符串中的反斜杠 \ 不再作为转义字符，而是被视为普通字符。唯一的例外是双引号 "，你需要使用两个连续的双引号 "" 来表示一个实际的双引号字符。
                string shellArguments = string.Format(@$"-IBCK x {existFile} -p""{password}"" ""{file}"" ""{savePath}""");

                // 在UI线程更新命令显示
                this.Invoke((MethodInvoker)delegate
                {
                    rtbCMD.AppendText(shellArguments + "\n\n");
                    rtbCMD.ScrollToCaret();
                });

                // 执行解压操作，获取rar返回码
                int dcr = API.CompressByRar(shellArguments);

                if (dcr == 1 || dcr == 0) // 解压成功
                {
                    progressInfo.SuccessCount = successFile;
                    progressInfo.Message = "解压成功";
                    progressInfo.IsError = false;
                    _progressReporter.Report(progressInfo);

                    // 处理解压后的文件
                    bool shouldDeleteSource = false;
                    bool shouldMoveSource = false;

                    this.Invoke((MethodInvoker)delegate
                    {
                        shouldDeleteSource = CbDel.Checked;
                        shouldMoveSource = cbMoveSource.Checked;
                    });

                    string[] rarPart = RarPart(fileInfo, extension);
                    yiJieYa += Convert.ToInt64(rarPart[0]);
                    successFile++;//解压成功的文件数
                    int okSum = 0;//移动成功的文件数
                    // 解压后移动源文件
                    if (shouldMoveSource)
                    {
                        string newDir = fileInfo.DirectoryName + @"\【已解压】\";
                        string newFullFile = newDir + name;
                        if (File.Exists(newFullFile))
                        {
                            progressInfo.Message = "错误！没有进行移动文件,因为文件已存在：" + newFullFile;
                            progressInfo.IsError = true;
                            _progressReporter.Report(progressInfo);
                            failSum++;

                            this.Invoke((MethodInvoker)delegate
                            {
                                lMoveFailNum.Text = failSum.ToString();
                            });
                        }
                        else//如果新文件不存在，就移动
                        {
                            try
                            {
                                if (!Directory.Exists(newDir))
                                {
                                    Directory.CreateDirectory(newDir); //创建目录c:\【已解压】
                                }
                                for (int j = 1; j < rarPart.Length; j++)
                                {
                                    string fullNamej = rarPart[j]; // c:\a.part[j].rar
                                    string namej = Path.GetFileName(fullNamej); // a.part[j].rar
                                    string newFullName = newDir + namej; // c:\【已解压】\a.part[j].rar

                                    FileInfo fInfo = new(fullNamej);
                                    //yiJieYa += fInfo.Length;
                                    fInfo.MoveTo(newFullName);
                                    okSum++;//移动成功的文件数

                                    progressInfo.Message = Path.GetFileName(fullNamej) + "\n移动完成\n";
                                    progressInfo.IsError = false;
                                    _progressReporter.Report(progressInfo);
                                }
                            }
                            catch (Exception ex)
                            {
                                progressInfo.Message = $"移动或删除文件异常：{ex.ToString()}";
                                progressInfo.IsError = true;
                                _progressReporter.Report(progressInfo);
                            }
                        }
                    }

                    // 解压后删除源文件
                    if (shouldDeleteSource)
                    {
                        if (Directory.Exists(file))
                        {
                            Directory.Delete(file, true);
                            okSum++;
                        }
                        else if (File.Exists(file))
                        {
                            File.Delete(file);
                            okSum++;
                        }

                    }

                    // 更新进度信息
                    double remainingSizeGB = (sourceSize - yiJieYa) / 1024 / 1024 / 1024; // 剩余未解压 GB
                    string remainingSizeGBStr = remainingSizeGB.ToString("F3").PadLeft(7, '=');
                    double decompressedMB = yiJieYa / 1024 / 1024; // 已解压 MB
                    double decompressedGB = decompressedMB / 1024;
                    string decompressedGBStr = (decompressedMB / 1024).ToString("F3").PadLeft(7, '=');
                    string remainingCount = (sourceFileCount - successFile).ToString().PadLeft(3, '=');
                    dt2 = DateTime.Now;
                    double elapsedTime = dt2.Subtract(dt1).TotalSeconds;//总耗时
                    double eachTime = dt2.Subtract(dtEach).TotalSeconds;//每个文件耗时
                    double speedMBPS = decompressedMB / eachTime;//每个文件的解压速度

                    string showTitle = $"已解压{decompressedGBStr} GB，{successFile.ToString().PadLeft(3, '=')} 个文件\n还剩余{remainingSizeGBStr} GB，{remainingCount} 个文件";
                    string showText = $"耗时{elapsedTime:F0} 秒，该文件每秒解压 {speedMBPS:F1} MB";
                    // 更新UI
                    this.Invoke((MethodInvoker)delegate
                    {
                        rtbOk.AppendText(sourceSize + "\n" + remainingSizeGB + "\n");
                        notifyIcon.ShowBalloonTip(4000, showTitle, showText, ToolTipIcon.Info);
                        notifyIcon.Text = showTitle; // 任务栏图标的名称
                        lOKsize.Text = $"{decompressedGBStr} GB";
                        rtbFail.AppendText(showTitle + "\n" + showText + "\n");
                        rtbFail.ScrollToCaret();
                    });

                    // 检查是否达到目标大小
                    if (shouldCheckSize && decompressedGB > targetSize)
                    {
                        DateTime dt3 = DateTime.Now;
                        ts3 = dt1.Subtract(dt3).Duration();
                        dateDiff = ts3.Hours.ToString() + "小时" + ts3.Minutes.ToString() + "分钟" + ts3.Seconds.ToString() + "秒";

                        string summaryMessage = "达到文件大小\n用时：" + dateDiff
                            + "\n开始时间：" + dt1.ToShortTimeString().ToString()
                            + "\n结束时间" + dt3.ToShortTimeString().ToString()
                            + "\n成功：" + successFile + "\n失败：" + failFile;

                        this.Invoke((MethodInvoker)delegate
                        {
                            rtbCMD.AppendText(summaryMessage);
                            rtbCMD.ScrollToCaret();
                        });

                        break;
                    }
                }
                else if (dcr == -2)// 解压失败
                {
                    // 在UI线程显示错误
                    this.Invoke((MethodInvoker)delegate
                    {
                        MessageBox.Show("请安装 WinRAR 5.0 以上版本");
                    });
                    return;
                }
                else
                {
                    progressInfo.Message = "555，解压失败了：\n" + file + "\n" + password + "\n" + RarTips(dcr);
                    progressInfo.IsError = true;
                    failFile++;
                    progressInfo.FailCount = failFile;
                    _progressReporter.Report(progressInfo);
                }
            }

            // 处理关机操作
            bool shouldShutdown = false;
            isAdvancedEnabled = false;

            this.Invoke((MethodInvoker)delegate
            {
                shouldShutdown = cbShutdown.Checked;
                isAdvancedEnabled = !tbNotice.ReadOnly;//注释
            });

            if (shouldShutdown && isAdvancedEnabled) // 关机
            {
                Process.Start("c:/windows/system32/shutdown.exe", "-s");

                // 在UI线程执行询问操作
                this.Invoke((MethodInvoker)delegate
                {
                    MessageBoxButtons messButton = MessageBoxButtons.OKCancel;
                    DialogResult dr = MessageBox.Show("取消关机吗?", "即将关机", messButton);
                    if (dr == DialogResult.OK) // 如果点击"确定"按钮
                    {
                        Process.Start("c:/windows/system32/shutdown.exe", "-a");
                    }
                });
            }

            // 最终统计
            DateTime dtEnd = DateTime.Now;
            TimeSpan tsTotal = dtEnd.Subtract(dt1);
            string totalTime = tsTotal.Hours.ToString() + "小时" + tsTotal.Minutes.ToString() + "分钟" + tsTotal.Seconds.ToString() + "秒";

            // 在UI线程更新结果
            this.Invoke((MethodInvoker)delegate
            {
                notifyIcon.Text = "完成";

                string summaryMessage = $"解压完成！\n"
                        + $"开始时间：{dt1.ToShortTimeString()}\n"
                        + $"结束时间：{dtEnd.ToShortTimeString()}\n"
                        + $"总用时：{totalTime}\n"
                        + $"成功解压：{successFile} 个文件\n"
                        + $"解压失败：{failFile} 个文件\n"
                        + $"总大小：{(double)yiJieYa / 1024 / 1024 / 1024:F2} GB\n";

                rtbOk.AppendText(summaryMessage);
                rtbOk.ScrollToCaret();
            });
        }

        //选择要压缩的源文件
        private void ButtonFrom_Click(object sender, EventArgs e)
        {
            if (cbFrom.SelectedIndex == 1)
            {
                FolderBrowserDialog dialog = new()
                {
                    Description = "请先把所有要压缩的东西放在一个文件夹\n现在，请选择这个文件夹"
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string foldPath = dialog.SelectedPath; tbSource.Text = foldPath;
                    AddFileToListFromPath();
                }
            }
            else
            {
                OpenFileDialog fileDialog = new()
                {
                    Filter = "(*.txt)|*.txt"
                };
                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filepath = fileDialog.FileName;//返回文件的完整路径      
                    string extension = Path.GetExtension(filepath);//获取用户选择文件的后缀名
                    string[] str = new string[] { ".txt" };//声明允许的后缀名
                    if (!((IList)str).Contains(extension))
                    {
                        MessageBox.Show("仅能读取文本文件");
                    }
                    else
                    {
                        /*获取用户选择的文件，并判断文件大小不能超过20K，fileInfo.Length是以字节为单位的
                        FileInfo fileInfo = new FileInfo(fileDialog.FileName);
                        if (fileInfo.Length > 20480)                        {MessageBox.Show("上传的图片不能大于20K");}
                        else//在这里就可以写获取到正确文件后的代码了                        {}                        
                        */
                        tbFileName.Text = filepath;
                        AddFileToListFromTxt();
                    }
                }
            }
        }

        private void Btn2_Click(object sender, EventArgs e)//压缩完存放到哪个文件夹
        {
            FolderBrowserDialog dialog = new()
            {
                Description = "请选择文件路径"
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string foldPath = dialog.SelectedPath;
                //MessageBox.Show("已选择文件夹:" + foldPath, "选择文件夹提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                tbSavePath.Text = foldPath;
                tbTmp.Text = foldPath;
            }
        }

        private void 退出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        //这2个是打开日志
        private void Button2_Click(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", tbSavePath.Text);//打开文件夹
            //string strT = tbSavePath.Text + "\\" + DateTime.Now.ToString("yyyy-MM-dd") + "成功的文件.txt";
            //if (File.Exists(strT))
            //{
            //    Process.Start(strT);
            //}
            //else
            //{
            //    MessageBox.Show("没有记录");
            //}
        }

        private void BtnFail_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(tbSource.Text))
            {
                Process.Start("explorer.exe", tbSource.Text);//打开文件夹
            }
            //string strT = DateTime.Now.ToString("yyyy-MM-dd");
            //if (File.Exists(tbSavePath.Text.Trim() + "\\" + strT + "失败的文件.txt"))
            //{
            //    Process.Start(tbSavePath.Text.Trim() + "\\" + strT + "失败的文件.txt");
            //}
            //else
            //{
            //    MessageBox.Show("没有记录");
            //}
        }

        //帮助
        private void Btnhelp_Click(object sender, EventArgs e)
        {
            string help1 = "使用说明：\n本程序使用【WinRAR 7.0】进行压缩，请先安装。\n\n" +
                "1\n【从哪儿来】有两个选择：\n" +
                "【从此txt读取要压缩的文件：】：点此按钮读取您指定的txt，此TXT应保存 待压缩文件 的绝对路径 ，如c:\\文件1.avi、c:\\文件夹1\\文件2.mp4，每行一个。\n" +
                "【压缩此文件夹内所有文件：】：读取一个文件夹下所有文件/夹，这些文件都将被独立压缩在以各自命名的压缩文件中。\n\n" +
                "【目的地】：要把文件压缩到哪里\n\n点击【压缩！】即可开始压缩，如非必要，其他选项使用默认即可\n\n";

            string help2 = "2\n默认使用【随机密码】保护您的文件，每个文件的密码各不相同。\n" +
                "也可选择手动输入，那么所有文件的密码将完全相同。\n留空不加密。\n" +
                "为保证解压兼容性，密码中的空格将被删除\n\n";

            string help3 = "3\n压缩文件的类型可选 rar/7z/zip 等 WinRAR 支持的全部格式\n压缩完成后，默认什么也不干。\n" +
                "可选：将已经压缩的文件的文件名前面添加【【已压缩】】几个字，与未压缩的文件区别。\n" +
                "可选：如果文件路径含有【【已压缩】】,压缩的时候就不压缩它；同理，如果如果文件路径含有【【已解压】】,解压的时候就不解压它\n\n";

            string help4 = "4\n百度网盘不能在线解压固实的压缩文件。如果想在线解压，取消勾选【固实】，或【压缩率】选择【不压缩】";
            rtbCMD.AppendText(help1 + help2 + help3 + help4);
        }
        //清除所有框的内容
        private void Btnreset_Click(object sender, EventArgs e)
        {
            this.Invoke((MethodInvoker)delegate
            {
                rtbSource.Clear();
                rtbOk.Clear();
                rtbCMD.Clear();
                rtbFail.Clear();
                //tbPW.Text = "sbbd";//默认密码sbbd
            });
        }
        //添加附件
        private void BtnAddFile_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog2 = new()
            {
                Description = "---添加以下文件/夹至压缩文件---"
            };
            if (dialog2.ShowDialog() == DialogResult.OK)
            {
                enclosureList.AppendText(dialog2.SelectedPath);
            }
        }
        /*
        public static long GetDirectoryLength(string dirPath)//获取文件夹大小以判断是否分卷压缩
        {
            //判断给定的路径是否存在,如果不存在则退出
            if (!Directory.Exists(dirPath))
                return 0;
            long len = 0;
            //定义一个DirectoryInfo对象
            DirectoryInfo di = new DirectoryInfo(dirPath);
            //通过GetFiles方法,获取di目录中的所有文件的大小
            foreach (FileInfo fi in di.GetFiles())
            {
                len += fi.Length;
            }
            //获取di中所有的文件夹,并存到一个新的对象数组中,以进行递归
            DirectoryInfo[] dis = di.GetDirectories();
            if (dis.Length > 0)
            {
                for (int i = 0; i < dis.Length; i++)
                {
                    len += GetDirectoryLength(dis[i].FullName);
                }
            }
            return len;
        }
        */
        //清空命令列表
        private void BtnClearAll_Click(object sender, EventArgs e)
        {
            rtbCMD.Clear();
        }

        private void BtnClearSource_Click(object sender, EventArgs e)//清空待压缩项目
        {
            rtbSource.Clear();
        }
        //隐藏窗口
        private void BtnHide_Click(object sender, EventArgs e)
        {
            Visible = false;
        }
        //判断是否是数字
        private static bool IsNum(string str)
        {
            if (str == null || str.Length == 0)    //验证这个参数是否为空
                return false;                           //是，就返回False
            ASCIIEncoding ascii = new();//new ASCIIEncoding 的实例
            byte[] bytestr = ascii.GetBytes(str);         //把string类型的参数保存到数组里

            foreach (byte c in bytestr)                   //遍历这个数组里的内容
            {
                if (c < 48 || c > 57)                          //判断是否为数字
                {
                    return false;                              //不是，就返回False
                }
            }
            return true;                                        //是，就返回True
        }

        //将选中txt的所有内容添加到待压缩列表
        private void AddFileToListFromTxt()
        {
            //rtbCMD.Clear();
            rtbSource.Clear();
            using StreamReader txtList = new(tbFileName.Text);
            int fileSum = 0;
            int pwSum = 0;
            string fileNameFromTxt;
            string txtPW;
            double fileSize = 0;
            string fileExisted = "";
            string extension = '.' + exTension.Text.Trim();
            while ((fileNameFromTxt = txtList.ReadLine()) != null)
            {
                //如果不是分卷文件，把压缩文件路径最后添加上"\"，如果已经有了"\"，就把两个"\"换成1个"\"
                string name = (tbSource.Text.Trim() + "\\").Replace(@"\\", "\\") + fileNameFromTxt + "." + exTension.Text;
                //如果是分卷文件，分卷压缩后的第一卷文件名：
                string partPath = (tbSource.Text.Trim() + "\\").Replace(@"\\", "\\") + fileNameFromTxt + ".part";
                string partExtension = "1" + extension;
                bool partExist = false;
                string zeroSum = "";
                //判断是不是分卷文件，如文件a压缩后可能是a.part01.rar或者a.part001.rar
                for (int i = 0; i < 9; i++, zeroSum += '0')
                {
                    if (File.Exists(partPath + zeroSum + partExtension))
                    {
                        name = partPath + zeroSum + partExtension;
                        partExist = true;
                    }
                }
                //如果文件不存在，跳过下一行的密码。所以txt一定要严格按照第一行文件名，第二行密码的格式
                if (!File.Exists(name) && !partExist)
                {
                    //rtbFailList.AppendText(name + "\n");
                    txtList.ReadLine();
                    continue;
                }
                FileInfo fi = new(name);
                if (fileSize < Convert.ToDouble(tbSize.Text) * 1000000000)//如果总解压的文件大小小于指定大小
                {
                    fileSum++;
                }
                fileExisted += Path.GetFileNameWithoutExtension(name) + "\n" + (Convert.ToDouble(fi.Length) / 1073741824).ToString("F3") + " Gb\n";
                fileSize += fi.Length;

                //如果文件重复，连密码一起跳过
                bool boolFind = false;
                foreach (string linei in rtbSource.Lines)
                {
                    if (name == linei)
                    {
                        boolFind = true;
                        txtList.ReadLine();
                        break;
                    }
                }
                if (boolFind)
                {
                    continue;
                }
                rtbSource.AppendText(name + "\n");

                //录入密码，即文件名的下一行
                while ((txtPW = txtList.ReadLine()) != null)
                {
                    rtbSource.AppendText(txtPW + "\n");
                    pwSum++;
                    break;
                }
            }
            rtbSource.ScrollToCaret();
            lOKsize.Text = (fileSize / 1073741824).ToString("F3");//转换成Gb
            //lsum.Text = "文件数：" + filesum + "密码数：" + pwsum;
            labelSourceCount.Text = fileSum.ToString();
            if (fileExisted != "")
            {
                rtbCMD.AppendText("-----------------------------------------\n");
                rtbCMD.AppendText(fileExisted);
                rtbCMD.AppendText("\n-----------------------------------------\n");
            }
            //统计该文件夹下没找到密码的文件数
            string savefiles = tbSource.Text;
            if (!Directory.Exists(savefiles))
            {
                rtbCMD.AppendText("-----------------------------------------\n");
                rtbCMD.AppendText("\n文件夹不存在：" + savefiles + "\n");
                rtbCMD.AppendText("\n-----------------------------------------\n");
                return;
            }
            string[] files = Directory.GetFiles(savefiles);
            string notpwFile = "";
            int notpwfilesum = 0;
            string fenJuan = "";
            int notfenJuansum = 0;
            foreach (string file in files) //列出不存在文件和分卷文件
            {
                if (Path.GetExtension(file).Contains(extension))
                {
                    bool boolFind = false;
                    foreach (string ii in rtbSource.Lines)
                    {
                        if (file == ii)
                        {
                            boolFind = true;
                            break;
                        }
                    }
                    if (!boolFind)
                    {
                        if (file.Contains(".part"))
                        {
                            fenJuan += Path.GetFileNameWithoutExtension(file) + "\n";
                            notfenJuansum++;
                        }
                        else
                        {
                            notpwFile += Path.GetFileNameWithoutExtension(file) + "\n";
                            notpwfilesum++;
                        }
                    }
                }
            }
            if (notpwFile != "")
            {
                rtbCMD.AppendText("-----------------------------------------\n");
                rtbCMD.AppendText("\n以下文件未在密码本找到，请检查\n文件个数：" + notpwfilesum + "\n");
                rtbCMD.AppendText(notpwFile);
                rtbCMD.AppendText("\n-----------------------------------------\n");
            }
            if (fenJuan != "")
            {
                rtbCMD.AppendText("-----------------------------------------\n");
                rtbCMD.AppendText("\n以下文件疑似分卷文件，请注意甄别\n文件个数：" + notfenJuansum + "\n");
                rtbCMD.AppendText(fenJuan);
                rtbCMD.AppendText(notpwFile);
                rtbCMD.AppendText("\n-----------------------------------------\n");
            }
            rtbCMD.ScrollToCaret();

            string showTitle = lOKsize.Text + " GB，\n" + labelSourceCount.Text + " 个文件\n";
            double speedtime = Convert.ToDouble(lOKsize.Text) * 25;
            string showtxt = "预计耗时 " + speedtime.ToString("F1") + " 秒\n（假设每秒解压40mb）";
            notifyIcon.ShowBalloonTip(4000, showTitle, showtxt, ToolTipIcon.Info);
            notifyIcon.Text = showTitle + showtxt;//任务栏图标的名称
        }

        //将选中文件夹下的所有内容添加到待压缩列表
        private void AddFileToListFromPath()
        {
            //把c: 变成c:\，在最后添上\
            tbSource.Text = (tbSource.Text.Trim() + "\\").Replace("\\\\", "\\");
            tbSavePath.Text = (tbSavePath.Text.Trim() + "\\").Replace("\\\\", "\\");
            rtbSource.Clear();
            string sourcepath = tbSource.Text;
            if (!Directory.Exists(sourcepath))
            {
                return;
            }
            int foldsum = 0;
            int filesum = 0;
            string[] folds = Directory.GetDirectories(sourcepath); //获取该文件夹下面的所有一级文件夹
            foreach (string fold in folds) //列出每一个文件夹
            {
                if (Path.GetFileName(fold).Contains(".BIN") || Path.GetFileNameWithoutExtension(fold).Contains("System Volume Information") || Path.GetFileName(fold).Contains(".ecloud"))
                {
                    continue;
                }
                if (cbYiYaSuo.Checked && (Path.GetFileNameWithoutExtension(fold).Contains("【已压缩】") || Path.GetFileNameWithoutExtension(fold).Contains("【已解压】")))
                {
                    continue;
                }
                rtbSource.AppendText(fold + "\n");
                foldsum++;
            }
            string[] files = Directory.GetFiles(sourcepath);
            foreach (string file in files) //列出每一个文件
            {
                if (file.Contains(".ini") || file.Contains(".tmp") || file.Contains(".DS"))
                {
                    continue;
                }
                if (cbYiYaSuo.Checked && Path.GetFileNameWithoutExtension(file).Contains("【已压缩】") || Path.GetFileNameWithoutExtension(file).Contains("【已解压】"))
                {
                    continue;
                }
                //FileInfo fileInfo = new(file);
                rtbSource.AppendText(file + "\n");
                filesum++;
            }
            rtbSource.ScrollToCaret();
            //labelSourceCount.Text = "文件：" + filesum + "/文件夹：" + foldsum;
        }
        //更新按钮
        private void BtnrRefresh_Click(object sender, EventArgs e)
        {
            if (cbFrom.SelectedIndex == 0)
            {
                AddFileToListFromTxt();
            }
            else if (cbFrom.SelectedIndex == 1)
            {
                AddFileToListFromPath();
            }
            TotalSize(tbSavePath.Text.Trim());
        }
        //文件总数
        private void Lsum_Click(object sender, EventArgs e)
        {
            int nonfile = 0;
            int foldsum = 0;
            int filesum = 0;
            foreach (var fullpath in rtbSource.Lines)
            {
                if (fullpath == "")
                {
                    continue;
                }
                if (Directory.Exists(fullpath))
                {
                    foldsum++;
                }
                else if (File.Exists(fullpath))
                {
                    filesum++;
                }
                else
                {
                    nonfile++;
                }
            }
            labelSourceCount.Text = "文件/文件夹/无效： " + filesum + "/" + foldsum + "/" + nonfile;
        }

        //解锁高级功能
        private void BtnPW_Click(object sender, EventArgs e)
        {
            string unlock = MyMd5.MD5UTF874(DateTime.Now.DayOfYear.ToString() + "我爱") + MyMd5.MD5UTF874(DateTime.Now.DayOfYear.ToString() + "胖田田");
            if (tbPW.Text.Trim() == unlock)
            {
                tbNotice.ReadOnly = false;
                exTension.ReadOnly = false;
                tbVolume.ReadOnly = false;
                enclosureList.ReadOnly = false;
                enclosureList.Clear();
            }
            else
            {
                MessageBox.Show("密码设置成功");
            }
        }
        //这两个是从哪儿来、到哪儿去的源文件夹还是指定文件夹
        private void CbFrom_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbFrom.SelectedIndex == 0)
            {
                btnSource.Text = "选txt";
            }
            else
            {
                btnSource.Text = "从哪儿来";
            }
        }


        //随机密码还是自定义密码
        private void Cbpw_CheckedChanged(object sender, EventArgs e)
        {
            if (Cbpw.Checked)
            {
                lpw.Hide();
                tbPW.Hide();
                btnPW.Hide();
            }
            else
            {
                lpw.Show();
                tbPW.Show();
                btnPW.Show();
            }
        }
        //统计指定文件夹一共有多少gb压缩文件
        private double TotalSize(string path)
        {
            //this.Invoke((MethodInvoker)delegate
            //{
            //    rtbOk.Clear();
            //});
            string savefiles = path;
            double filesizesum = 0;
            if (Directory.Exists(savefiles))
            {
                string[] files = Directory.GetFiles(savefiles);
                foreach (string file in files) //列出每一个文件
                {
                    //若要压缩为rar，那么计算保存文件夹的所有rar的大小，用压缩完的大小减压缩前的大小，即为已压缩的大小
                    //rtbFailList.AppendText(Path.GetExtension(file) + "\n" );
                    if (Path.GetExtension(file).Contains(exTension.Text))
                    {
                        FileInfo fileInfo = new(file);
                        filesizesum += fileInfo.Length;
                        double filesize = fileInfo.Length;
                        //rtbOk.AppendText(Path.GetFileNameWithoutExtension(file) + "\n" + (filesize / 1073741824).ToString("f3") + " GB\n");
                    }
                }
                //rtbOk.ScrollToCaret();
                filesizesum /= 1073741824;//把字节转化成GB
            }
            return filesizesum;
        }

        private void TbSavePath_TextChanged(object sender, EventArgs e)
        {
            tbSavePath.Text = tbSavePath.Text.Trim().Replace(@"\\", @"\");

            lOKsize.Text = TotalSize(tbSavePath.Text.Trim()).ToString("F3") + "GB";

            //tbSavePath.Text=tbSource.Text.Trim();
            tbTmp.Text = tbSavePath.Text;
        }

        private void Btngetpw_Click(object sender, EventArgs e)
        {
            string name = tbFileName.Text;
            name += ".rar";
            string pw = MyMd5.MD5gb2312(name + "5") + "@" + MyMd5.MD5gb2312(name + "2") + ".com#" + MyMd5.MD5gb2312(name + "tt");
            rtbCMD.AppendText(name + "的密码是：\nMD5gb2312 5 2 tt\n" + pw + "\n");

            pw = MyMd5.MD5gb2312(name + "592") + "@" + MyMd5.MD5gb2312(name + "ptt") + ".com#" + MyMd5.MD5gb2312(name + "1314");
            rtbCMD.AppendText("gb2312 592 ptt 1314\n" + pw + "\n");

            pw = MyMd5.MD5UTF874(name + "5") + "@" + MyMd5.MD5UTF874(name + "2") + ".com#" + MyMd5.MD5UTF874(name + "tt");
            rtbCMD.AppendText("UTF874 5 2 tt\n" + pw + "\n");

            pw = MyMd5.MD5UTF874(name + "592") + "@" + MyMd5.MD5UTF874(name + "ptt") + ".com#" + MyMd5.MD5UTF874(name + "1314");
            rtbCMD.AppendText("UTF874 592 ptt 1314\n" + pw + "\n");

            pw = MyMd5.MD5UTF878(name + "592ptt1314") + MyMd5.MD5UTF878(name + "592pnn1314 7,8");
            rtbCMD.AppendText("UTF878 592ptt1314 592pnn1314 7,8\n" + pw + "\n");
            rtbCMD.ScrollToCaret();
            pw = MyMd5.MD5UTF878(name + "592ptt1314") + MyMd5.MD5UTF878(name + "592pnn1314");
            tbgetpw.Text = pw;
            Clipboard.SetText(pw);
        }

        private void AddDeRarFileFromrtbSourceList()
        {
            //把c: 变成c:\，在最后添上\
            tbSource.Text = (tbSource.Text.Trim() + "\\").Replace("\\\\", "\\");
            tbSavePath.Text = (tbSavePath.Text.Trim() + "\\").Replace("\\\\", "\\");
            rtbSource.Clear();
            string sourcepath = tbSource.Text;
            if (!Directory.Exists(sourcepath))//如果不存在来源目录，退出
            {
                return;
            }
            int filesum = 0;
            string[] files = Directory.GetFiles(sourcepath);
            foreach (string file in files) //列出每一个文件
            {
                if (Path.GetExtension(file) != "." + exTension.Text || (cbYiYaSuo.Checked && file.Contains("【已解压】")))
                {
                    continue;
                }
                FileInfo fileInfo = new(file);
                rtbSource.AppendText(file + "\n");
                filesum++;
            }
            rtbSource.ScrollToCaret();
            labelSourceCount.Text = "总压缩文件数： " + filesum;
        }

        private void TbSource_TextChanged(object sender, EventArgs e)
        {
            tbSource.Text = tbSource.Text.Trim().Replace(@"\\", @"\");
            if (cbFrom.SelectedIndex == 0)
            {
                if (File.Exists(tbSource.Text) && Path.GetExtension(tbSource.Text) == ".txt")
                {
                    btnRefresh.PerformClick();
                }
            }
            else if (cbFrom.SelectedIndex == 1)
            {
                if (Directory.Exists(tbSource.Text))
                {
                    //btnAsSource.PerformClick();//同来源
                    btnRefresh.PerformClick();
                }
            }
            //tbSavePath.Text = tbSource.Text;
        }

        //调整窗体大小
        private void BtnSize_Click(object sender, EventArgs e)
        {
            if (btnZoom.Text == "缩小")
            {
                Font = new Font("宋体", 9f);
                //StartPosition = FormStartPosition.CenterScreen;
                WindowState = FormWindowState.Normal;
                btnZoom.Text = "放大";
            }
            else
            {
                WindowState = FormWindowState.Maximized;
                Font = new Font("宋体", 11.25f);
                btnZoom.Text = "缩小";
            }
        }

        //WinRAR错误代码
        private static string RarTips(int exitCode)//根据退出码获取解压、压缩完的提示
        {
            string rarcode = exitCode switch
            {
                0 => "成功(*^_^*)",
                1 => "警告。有个无伤大雅的小错~",
                2 => "发生致命错误！",
                3 => "数据损坏。",
                4 => "文件已锁定，不能被修改",
                5 => "不能写",
                6 => "打不开",
                7 => "命令行输错了",
                8 => "内存不够",
                9 => "创建不了文件",
                10 => "没解压，可能是文件已存在",
                11 => "密码错误。",
                255 => "用户主动中断操作",
                _ => "我不知道错哪了",
            };
            return rarcode;
        }

        private void NotifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (!Visible)
                {
                    Visible = true;
                    TopMost = true;
                    TopMost = false;
                }
                else
                {
                    Visible = false;
                }
            }
            else
            {
                //Application.Exit();
            }
        }
        //判断是不是分卷文件，如果是，获取rar所有分卷，第一个元素保存文件的大小，之后每个元素保存各个分卷文件的路径
        private string[] RarPart(FileInfo fileInfo, string extension)
        {
            if (!fileInfo.Name.Contains(".part"))//如果不是分卷文件，直接返回该文件的大小和路径
            {
                return new string[] { fileInfo.Length.ToString(), fileInfo.FullName };
            }
            List<string> fullNameList = new();
            int partMax = 0;
            //string partZeroSum = "";
            string pathHeadNamePart = "";
            long allPartSize = 0;
            int iZeroSum = 1;
            fullNameList.Add("");//用动态数组的第一个元素存储所有分卷文件的大小
            for (; iZeroSum < 9; iZeroSum++)
            {
                //rtbFailList.AppendText("rarZeroSum:"+ partZeroSum + "\n"+ fileInfo.Name + "\nfileInfo.Name\n");
                //.part001.rar
                if (fileInfo.Name.Contains(".part" + "1".PadLeft(iZeroSum, '0') + extension))
                {
                    //把c:\a.part0001.rar转换成c:\a.part
                    pathHeadNamePart = fileInfo.FullName.Replace("1".PadLeft(iZeroSum, '0') + extension, "");
                    //文件c:\a.part0001.rar到底有几位数，如这个例子是4位数
                    //int partLength =  fileInfo.FullName.Replace(pathHeadNamePart, "").Replace(extension, "").Length;
                    //rtbFailList.AppendText(sourceFullNameHead + "\nsourceFullNameHead\n");
                    //通过循环，将c:\a.part变成c:\a.part[j].rar，遍历所有可能的分卷文件
                    for (int partj = 1; partj < Math.Pow(10, iZeroSum + 1); partj++)
                    {
                        //文件末尾名[j].rar,如【0123.rar】。PadLeft(iZeroSum, '0')在字符串左边添加i个字符0
                        string rearNamej = partj.ToString().PadLeft(iZeroSum, '0') + extension;
                        string fullNamej = pathHeadNamePart + rearNamej; //c:\a.part[j].rar
                        //rtbOk.AppendText("\n存在文件名:\n" + sourceFullNamej + "\n" + "\nlen\n" + len + "\n");
                        if (File.Exists(fullNamej))
                        {
                            partMax++;
                            fullNameList.Add(fullNamej);
                            FileInfo fi = new(fullNamej);
                            allPartSize += fi.Length;
                            //rtbOk.AppendText("\n源文件名：" + sourceFullNamej + "\n" + "len值：" + len + "\n");
                        }
                        else if (!File.Exists(fullNamej))//该文件的所有分卷获取完毕
                        {
                            break;
                        }
                    }
                    fullNameList[0] = allPartSize.ToString();
                    if (partMax > 0)//假如001.rar找到了，就不必判断是不是0001.rar了
                    {
                        return fullNameList.ToArray();
                    }
                }
            }
            fullNameList[0] = "0";
            return fullNameList.ToArray();
        }

        private void Mainform_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F5:
                    btnRefresh.PerformClick();
                    break;
                default:
                    break;
            }
        }

        //同来源
        private void btnAsSourceFN()
        {
            if (cbFrom.SelectedIndex == 0)
            {
                return;//如果是选了txt文件，就不执行
            }
            if (Directory.Exists(tbSource.Text))
            {
                if (tbFileName.Text.Length == 1)
                {
                    tbSavePath.Text = tbFileName.Text + tbSource.Text.Trim().Remove(0, 1);
                }
                else if (tbFileName.Text.Length > 1)
                {
                    tbSavePath.Text = tbFileName.Text + tbSource.Text.Trim().Remove(0, 2);
                }
                else
                {
                    tbSavePath.Text = tbSource.Text.Trim();
                }
            }
        }
        private void btnAsSource_Click(object sender, EventArgs e)
        {
            btnAsSourceFN();
        }

        private void CbSolid_CheckedChanged(object sender, EventArgs e)
        {
            if (cbRate.SelectedIndex == 0 && CbSolid.Checked)
            {
                string tipOverwrite = "【压缩率】选择【不压缩】时【固实】无效，其他都有效。\n启用【固实】后部分网盘不提供在线解压功能";
                MessageBox.Show(tipOverwrite);
                CbSolid.Checked = false;
            }
        }

        private void tbFileName_TextChanged(object sender, EventArgs e)
        {
            //btnAsSourceFN();
        }

        private void cbMoveSource_CheckedChanged(object sender, EventArgs e)
        {
            CbDel.Checked = false;
        }

        private void CbDel_CheckedChanged(object sender, EventArgs e)
        {
            cbMoveSource.Checked = false;
        }

        private void exTension_TextChanged(object sender, EventArgs e)
        {
            string extension = exTension.Text.Trim();
            // 如果选择 7z 分卷压缩，提示用户（在UI线程执行）
            if (extension == "7z")
            {
                var result = DialogResult.Cancel;

                MessageBoxButtons messButton = MessageBoxButtons.OKCancel;
                result = MessageBox.Show("我注意到您选择了【7z】，遗憾的是 WinRAR 不支持压缩【7z】格式（但能解压7z）\n点击【确定】将更改文件类型为rar，点【取消】将改为zip", "错误！", messButton);

                if (result == DialogResult.OK)
                {
                    extension = ".rar";
                    exTension.Text = "rar";
                }
                else
                {
                    extension = ".zip";
                    exTension.Text = "zip";
                }
            }
        }
    }
}
//密码 5 2 tt ;592 ptt 1314；592ptt1314 592pnn1314
//注意，密码是否带后缀，解压时是将"1.rar"作为密码，还是"1"