using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace CmnLocalLib
{
    /// <summary>
    /// Utilityクラス
    /// </summary>
    public static class UtilLib
    {
        private const string _passcode = "passcode";
        private const string _salt = "passsalt";
        static private RijndaelManaged _rijindeal;

        static private object mLockObj = new object();

        static UtilLib()
        {
            _rijindeal = new RijndaelManaged();
            _rijindeal.KeySize = 128;
            _rijindeal.BlockSize = 128;

            byte[] bsalt = Encoding.UTF8.GetBytes(_salt);
            Rfc2898DeriveBytes deriveBytes = new Rfc2898DeriveBytes(_passcode, bsalt);
            deriveBytes.IterationCount = 1000;

            _rijindeal.Key = deriveBytes.GetBytes(_rijindeal.KeySize / 8);
            _rijindeal.IV = deriveBytes.GetBytes(_rijindeal.BlockSize / 8);
        }


        #region <<XML関係>>
        /// <summary>
        /// XMLデシリアライズ
        /// </summary>
        /// <param name="strPath">ロードするXMLファイル</param>
        /// <param name="tObj">読み込むクラスオブジェクト</param>
        /// <returns>読み込んだクラスオブジェクト</returns>
        public static T LoadXml<T>(string strPath)
            where T : class
        {
            T tObj = null;

            if (File.Exists(strPath) == false)
            {
                //例外を投げる
                throw new FileNotFoundException();
            }

            XmlSerializer serializer = new XmlSerializer(typeof(T));

            //usingでfinallyも包括
            using (FileStream fs = new FileStream(strPath, System.IO.FileMode.Open))
            {
                //as演算子による型変換は変換可能かどうかのチェックのみ
                //例外が発生する可能性がある場合、キャストすると遅くなるのでasで変換
                //失敗したらnullになる
                tObj = serializer.Deserialize(fs) as T;
            }

            return tObj;
        }

        /// <summary>
        /// XMLシリアライズ
        /// </summary>
        /// <param name="filePath">書き込むXMLファイルパス
        /// <param name="obj">シリアライズするオブジェクト
        /// <param name="type">シリアライズするオブジェクトの型
        public static int SaveXML(string filePath, object obj, Type type)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(type, "");

                // 書き込む書式の設定
                XmlWriterSettings settings = new XmlWriterSettings();

                //名前空間の定義：xsi=XXX
                XmlSerializerNamespaces namesp = new XmlSerializerNamespaces();
                namesp.Add(String.Empty, String.Empty);

                //XML宣言を書き込む:falseに設定
                settings.OmitXmlDeclaration = false;

                settings.Indent = true;
                settings.IndentChars = "    ";

                // ファイルへオブジェクトを書き込み（シリアライズ）
                using (XmlWriter writer = XmlWriter.Create(filePath, settings))
                {
                    serializer.Serialize(writer, obj, namesp);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                return -1;
            }
            return 0;
        }

        public static T EncryptLoad<T>(string strPath, string strPass)
            where T : class
        {
            T tObj = null;

            if (File.Exists(strPath) == false)
            {
                //例外を投げる
                throw new FileNotFoundException();
            }

            XmlSerializer serializer = new XmlSerializer(typeof(T));

            //usingでfinallyも包括
            using (FileStream fs = new FileStream(strPath, System.IO.FileMode.Open))
            {
                using (MemoryStream memstream = new MemoryStream())
                {
                    const int size = 4096;
                    byte[] buffer = new byte[size];
                    int numBytes;

                    //読み込んだデータをメモリストリームに書き出し
                    while ((numBytes = fs.Read(buffer, 0, size)) > 0)
                    {
                        memstream.Write(buffer, 0, numBytes);
                    }

                    //変換
                    byte[] source = memstream.ToArray();
                    source = DeAESlize(source);

                    //メモリストリームにバイナリデータを読み込みデシリアライズ
                    using (MemoryStream memStream2 = new MemoryStream(source))
                    {
                        BinaryFormatter formatter = new BinaryFormatter();
                        tObj = formatter.Deserialize(memStream2) as T;

                        Console.WriteLine("Loaded.");
                    }
                }
                ////as演算子による型変換は変換可能かどうかのチェックのみ
                ////例外が発生する可能性がある場合、キャストすると遅くなるのでasで変換
                ////失敗したらnullになる
                //tObj = serializer.Deserialize(fs) as T;
            }

            return tObj;
        }


        public static int EncryptSave(string filePath, object obj, Type type, string strPass)
        {
            try
            {
                using (var difs = new MemoryStream())
                {
                    //オブジェクトをバイナリ変換
                    BinaryFormatter binformatter = new BinaryFormatter();
                    binformatter.Serialize(difs, obj);

                    byte[] source = difs.ToArray();

                    source = AESlize(source);

                    using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        fileStream.Write(source, 0, source.Length);
                    }

                    Console.WriteLine("Done [" + obj.ToString() + "]");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                return -1;
            }
            return 0;
        }

        static private byte[] AESlize(byte[] data)
        {
            ICryptoTransform encryptor = _rijindeal.CreateEncryptor();
            byte[] encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

            encryptor.Dispose();

            return encrypted;
        }

        static private byte[] DeAESlize(byte[] data)
        {
            ICryptoTransform decryptor = _rijindeal.CreateDecryptor();
            byte[] plain = decryptor.TransformFinalBlock(data, 0, data.Length);

            return plain;
        }


        /// <summary>
        /// XML読込
        /// 要素単位で処理したい時などに使用
        /// </summary>
        /// <param name="strPath"></param>
        /// <returns></returns>
        public static int ReadXML(string strPath)
        {
            int nResult = 0;

            XDocument xmlDoc = XDocument.Load(strPath);
            IEnumerable<XElement> eleRoot = xmlDoc.Elements("");

            return nResult;
        }

        #endregion


        /// <summary>
        /// CSV出力
        /// ログ出力向け
        /// </summary>
        /// <param name="strPath">出力ファイル</param>
        /// <param name="lstLog">文字列配列</param>
        /// <param name="append">true:追記 false:新規</param>
        /// <returns></returns>
        public static int OutCsv(string strPath, List<string> lstLog, bool append, bool outtime, string strCode)
        {
            int nResult = 0;
            int iFldCnt = lstLog.Count;
            if (iFldCnt == 0) return 0;
            string strFmt = "yyyy/MM/dd HH:mm:ss";

            lock(mLockObj)
            {
                using (StreamWriter sw = new StreamWriter(strPath, append, Encoding.GetEncoding("Shift_JIS")))
                {
                    string strOut = lstLog[0] + ",";
                    for (int i = 1; i < (iFldCnt - 1); i++) strOut += lstLog[i] + ",";
                    strOut += lstLog[iFldCnt - 1];

                    if (outtime == true)
                    {
                        DateTime nowTime = DateTime.Now;
                        strOut += "," + nowTime.ToString(strFmt);
                    }
                    sw.WriteLine(strOut);
                }
            }

            return nResult;
        }

        public static int OutString(string strPath, string strSetLog, bool append, bool outtime, string strCode)
        {
            int nResult = 0;

            string strFmt = "yyyy/MM/dd HH:mm:ss";

            lock (mLockObj)
            {
                using (StreamWriter sw = new StreamWriter(strPath, append, Encoding.GetEncoding("Shift_JIS")))
                {
                    string strOut = strSetLog;

                    if (outtime == true)
                    {
                        DateTime nowTime = DateTime.Now;
                        strOut = nowTime.ToString(strFmt) + "," + strSetLog;
                    }
                    sw.WriteLine(strOut);
                }
            }


            return nResult;
        }

        public static string GetStartUpPath()
        {
            string exePath = Environment.GetCommandLineArgs()[0];
            string exeFullPath = System.IO.Path.GetFullPath(exePath);

            return exeFullPath;
        }

        public static string GetRootDir()
        {
            return Path.GetDirectoryName(GetStartUpPath());
        }

    }

    public static class FileEncryptor
    {
        /// <summary>
        /// Key Length
        /// </summary>
        public const int KeyLength = 16;

        /// <summary>
        /// Generate Byte Key from Password
        /// </summary>
        /// <returns>Byte Key</returns>
        /// <param name="password">Password</param>
        private static byte[] GenerateByteKey(string password)
        {
            /* Get Bytes of Password */
            var bytesPassword = Encoding.UTF8.GetBytes(password);
            /* Create Bytes Key Array */
            var bytesKey = new byte[KeyLength];

            /* Loop to Key Length */
            for (int i = 0; i < KeyLength; i++)
            {
                /* Copy Password Byte Array and fill remain bytes by 0 */
                bytesKey[i] = (i < bytesPassword.Length) ? bytesPassword[i] : (byte)0;
            }

            return bytesKey;
        }

        /// <summary>
        /// Encrypt File
        /// </summary>
        /// <param name="ifs">Input File Stream</param>
        /// <param name="ofs">Output File Stream</param>
        /// <param name="password">Password</param>
        public static void Encrypt(Stream ifs, Stream ofs, string password)
        {
            /* Generate Byte Key from Password */
            var bytesKey = FileEncryptor.GenerateByteKey(password);

            /* Create AES Crypto Service Provider */
            var aes = new AesCryptoServiceProvider()
            {
                BlockSize = 128,
                KeySize = 128,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                Key = bytesKey,
            };

            /* Generate Random Initialization Vector(IV) */
            aes.GenerateIV();

            /* Get IV */
            var bytesIV = aes.IV;

            /* Write IV to File (128bit=16bytes) */
            ofs.Write(bytesIV, 0, 16);

            /* Create AES Encryptor */
            using (var encrypt = aes.CreateEncryptor())
            {
                /* Create Crypto Stream */
                using (var cs = new CryptoStream(ofs, encrypt, CryptoStreamMode.Write))
                {
                    /* Inifity Loop */
                    while (true)
                    {
                        /* Create Write Buffer */
                        var buffer = new byte[1024];
                        /* Read File to Buffer */
                        var len = ifs.Read(buffer, 0, buffer.Length);

                        /* Read Data Size is larger than 0 */
                        if (len > 0)
                        {
                            /* Write Read Data to Crypto Stream */
                            cs.Write(buffer, 0, len);
                        }
                        else
                        {
                            /* End of Input File */
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Decrypt File
        /// </summary>
        /// <param name="ifs">Input File Stream</param>
        /// <param name="ofs">Output File Stream</param>
        /// <param name="password">Password</param>
        public static void Decrypt(Stream ifs, Stream ofs, string password)
        {
            /* Generate Byte Key from Password */
            var bytesKey = FileEncryptor.GenerateByteKey(password);

            /* Create AES Crypto Service Provider */
            var aes = new AesCryptoServiceProvider()
            {
                BlockSize = 128,
                KeySize = 128,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                Key = bytesKey,
            };

            /* Create IV Buffer */
            var bytesIV = new byte[KeyLength];

            /* Read IV from Input File */
            ifs.Read(bytesIV, 0, KeyLength);

            /* Set Read IV */
            aes.IV = bytesIV;

            /* Create AES Decryptor */
            using (var encrypt = aes.CreateDecryptor())
            {
                /* Create Crypto Stream */
                using (var cs = new CryptoStream(ofs, encrypt, CryptoStreamMode.Write))
                {
                    /* Inifinity Loop */
                    while (true)
                    {
                        /* Create Read Buffer */
                        var buffer = new byte[1024];

                        /* Read Input File to Buffer */
                        var len = ifs.Read(buffer, 0, buffer.Length);

                        /* Read Data Size is larger than 0 */
                        if (len > 0)
                        {
                            /* Write Read Data to Crypto Stream */
                            cs.Write(buffer, 0, len);
                        }
                        else
                        {
                            /* End of Data */
                            break;
                        }
                    }
                }
            }
        }
    }

    public class MyLogging
    {
        public string LogRootPath { get; set; }
        public bool UseTrace { get; set; }
        public string TypeName { get; set; } = "no name";
        public int DeleteDaySpan { get; set; } = 30;
        //private DateTime LastTime;
        /// <summary>
        /// 排他ロック用オブジェクト
        /// </summary>
        private object mLockObj = new object();
        private object mLockDelete = new object();
        private DateTime LastDate = DateTime.Parse("1900/1/1");

        public MyLogging(string strRoot)
        {
            LogRootPath = strRoot;
            Directory.CreateDirectory(strRoot);
        }

        public void LogOutput(string strLog)
        {
            string strPath = Path.Combine(LogRootPath, string.Format("{0}.txt",DateTime.Now.ToString("yyyyMMdd")));
            lock (mLockObj)
            {
                using (StreamWriter sw = new StreamWriter(strPath, true, Encoding.GetEncoding("Shift_JIS")))
                {
                    string strOut = $"[{DateTime.Now.ToString("HH:mm:ss")}],{strLog}";

                    sw.WriteLine(strOut);
                }
            }

            //年月日のいずれかに差異がある場合は削除確認
            if((DateTime.Now.Year != LastDate.Year) ||
                (DateTime.Now.Month != LastDate.Month) ||
                (DateTime.Now.Day != LastDate.Day))
            {
                DeleteLogFile();
                LastDate = DateTime.Now;
            }

        }

        public void DeleteLogFile()
        {
            Task.Run(() =>
            {
                //指定日数分マイナスした値を取得
                DateTime delDateTime = DateTime.Now.AddDays(-1 * DeleteDaySpan);
                //時間部分を初期化した値を生成
                DateTime delDate = new DateTime(delDateTime.Year, delDateTime.Month, delDateTime.Day);

                //フォルダ内のファイルを取得
                DirectoryInfo dirInfo = new DirectoryInfo(LogRootPath);
                FileInfo[] fileInfos = dirInfo.GetFiles();

                //最終更新日が指定日以前のファイルをリストで取得
                List<FileInfo> delInfos = fileInfos.Where(x => x.LastWriteTime <= delDate).ToList();

                //対象のファイルを削除
                foreach (FileInfo info in delInfos)
                {
                    try
                    {
                        File.Delete(info.FullName);
                    }
                    catch (Exception ex)
                    {
                        LogOutput(string.Format("ログファイル削除 例外:{0}", ex.Message));
                    }
                }
            });
        }
    }
}
