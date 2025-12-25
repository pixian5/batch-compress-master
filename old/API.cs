using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace 批量压缩
{
    public class API
    {

        //获取rar路径
        public static string RarPath()
        {
            RegistryKey regkey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\winrar.exe");
            if (regkey == null)
            {
                return null;
            }
            string strkey = regkey.GetValue("Path").ToString();
            regkey.Close();
            return strkey;
        }
        //压缩主程序
        public static int CompressByRar(string shellArguments)
        {
            string rarexe = RarPath();
            if (!string.IsNullOrEmpty(rarexe))
            {
                rarexe += @"\winrar.exe";
                if (!File.Exists(rarexe))
                {
                    return -2;//没安装rar就退出
                }
            }
            try
            {
               using Process unrar = new()
                {
                    EnableRaisingEvents = true//引发exited事件，获取rar返回码    
                };
                ProcessStartInfo processStartInfo = new()
                {
                    FileName = rarexe,
                    Arguments = shellArguments,               //设置命令参数  
                    //WindowStyle = ProcessWindowStyle.Minimized  //如果想隐藏 rar 窗口，就把 Normal 设置为 Hidden 
                };
                ProcessStartInfo startinfo = processStartInfo;
                unrar.StartInfo = startinfo;
                unrar.Start();
                unrar.WaitForExit();//等待执行完成  
                //执行压缩后写入日志，暂时不让他写了，想加的时候，去掉注释就行了：
                //savelog(newpath, name, mm, unrar.ExitCode);
                    return unrar.ExitCode;
            }
            catch (Exception)
            {
                return -3;
            }
        }
        
        //异步压缩方法
        public static async Task<int> CompressByRarAsync(string shellArguments)
        {
            return await Task.Run(() => CompressByRar(shellArguments));
        }
        /*日志记录
         public static void Savelog(string newpath, string name, string mm, int exitCode)
        {
            if (mm == "")
            {
                mm = "{无密码}";
            }
            string day = DateTime.Now.ToString("yyyy-MM-dd");
            string log;
            if (exitCode == 0 || exitCode == 1)
            {
                log = day + "成功的文件.txt";
            }
            else
            {
                log = day + "失败的文件.txt";
            }
            if (!File.Exists(newpath + log))
            {
                FileStream fs1 = new(newpath + log, FileMode.Create);
                fs1.Close();
            }
            using StreamWriter file = new(newpath + log, true);
            file.WriteLine(name);//文件名
            file.WriteLine(mm);//密码
            file.WriteLine(DateTime.Now.ToShortDateString().ToString() + " " + DateTime.Now.ToLongTimeString().ToString() + RarTips(exitCode));//退出码
        }
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
                10 => "没有找到与指定的掩码和选项匹配的文件。",
                11 => "密码错误。",
                255 => "用户主动中断操作",
                _ => "我不知道错哪了",
            };
            return rarcode;
        }
         * */
    }
}
