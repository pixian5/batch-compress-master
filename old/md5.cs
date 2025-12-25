using System.Security.Cryptography;
using System.Text;
//由于刚开始编码使用了系统默认，导致不同版本的MD5值不一样，.net4.8使用了936编码。若密码不正确，将编码由utf8切换为936 gbk GB2312编码试试
namespace 批量压缩
{
    class MyMd5
    {
        /// <summary>
        /// MD5字符串加密 注意编码
        /// </summary>
        /// <param name="txt"></param>
        /// <returns>加密后字符串</returns>
        public static string MD5UTF878(string txt)
        {
            using MD5 mi = MD5.Create();
            byte[] buffer = Encoding.UTF8.GetBytes(txt);
            //开始加密
            byte[] newBuffer = mi.ComputeHash(buffer);
            StringBuilder sb = new();
            for (int i = 0; i < newBuffer.Length; i++)
            {
                sb.Append(newBuffer[i].ToString("x2"));
            }
            //对 完整文件名（如1.zip）进行MD5加密后，从第7位开始取8位
            return  sb.ToString().Substring(7, 8);            
        }
        public static string MD5UTF874(string txt)
        {
            using MD5 mi = MD5.Create();
            byte[] buffer = Encoding.UTF8.GetBytes(txt);
            //开始加密
            byte[] newBuffer = mi.ComputeHash(buffer);
            StringBuilder sb = new();
            for (int i = 0; i < newBuffer.Length; i++)
            {
                sb.Append(newBuffer[i].ToString("x2"));
            }
            //对 完整文件名（如1.zip）进行MD5加密后，从第7位开始取4位
            return sb.ToString().Substring(7, 4);
        }
        public static string MD5gb2312(string txt)
        {
            using MD5 mi = MD5.Create();
            //切换为中文编码
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            byte[] buffer = Encoding.GetEncoding("gb2312").GetBytes(txt);
            
            //开始加密
            byte[] newBuffer = mi.ComputeHash(buffer);
            StringBuilder sb = new();
            for (int i = 0; i < newBuffer.Length; i++)
            {
                sb.Append(newBuffer[i].ToString("x2"));
            }
            //对 完整文件名（如1.zip）进行MD5加密后，从第7位开始取4位
            return sb.ToString().Substring(7, 4);
        }
    }
}
