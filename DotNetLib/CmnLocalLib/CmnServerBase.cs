using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CmnLocalLib
{
    public class CmnServerBase:IDisposable
    {
        private int mPortNo;
        private bool mCancelFlag;
        private string mStrType;

        public int nReadTimeout { get; set; }
        public int nWriteTimeout { get; set; }

        public int nMaxRecvSize { get; set; }

        private bool bRunFlag { get; set; }



        TcpListener mListener;

        public CmnServerBase(int nPort, string strType,int nMaxSize)
        {
            mPortNo = nPort;
            mCancelFlag = false;
            mStrType = strType;
            nReadTimeout = 5000;
            nWriteTimeout = 5000;
            nMaxRecvSize = nMaxSize;
        }

        public int Start()
        {

            bRunFlag = true;
            //TcpListenerオブジェクトを作成する
            mListener = new TcpListener(IPAddress.Any, mPortNo);
            Socket sock = mListener.Server;
            sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            //mListener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.ReuseAddress,true);

            //Listenを開始する
            try
            {
                mListener.Start();
            }
            catch (Exception ex)
            {
                ErrorLog(string.Format("Listen 例外発生:{0}",ex.Message));
                mListener.Stop();
                mListener.Server.Close();
                return -1;
            }

            //Trace.WriteLine(string.Format("Listenを開始しました({0}:{1})。",
            //    ((System.Net.IPEndPoint)mListener.LocalEndpoint).Address,
            //    ((System.Net.IPEndPoint)mListener.LocalEndpoint).Port));

            int nResult = 0;
            while (!mCancelFlag)
            {

                try
                {
                    if (mCancelFlag) break;

                    //Trace.WriteLine("接続待ち...");
                    //LogOutPut("接続待ち");

                    using (TcpClient client = mListener.AcceptTcpClient())
                    {
                        TraceLog(string.Format("クライアント接続({0}:{1})",
                            ((System.Net.IPEndPoint)client.Client.RemoteEndPoint).Address,
                            ((System.Net.IPEndPoint)client.Client.RemoteEndPoint).Port));

                        //NetworkStreamを取得
                        using (NetworkStream ns = client.GetStream())
                        {
                            //読み取り、書き込みのタイムアウトの設定
                            ns.ReadTimeout = nReadTimeout;
                            ns.WriteTimeout = nWriteTimeout;

                            nResult = Recv(ns);

                            ns.Close();
                        }

                        client.Close();
                    }
                }
                catch (Exception ex)
                {
                    mListener.Stop();
                    ErrorLog(string.Format("Recv 例外発生:{0}",ex.Message));
                    //Trace.WriteLine(ex.Message);
                    //LogOutPut("例外発生");
                    break;
                }
            }

            bRunFlag = false;

            TraceLog("Start 終了");
            //Trace.WriteLine("Start 終了");
            //リスナを閉じる
            mListener.Stop();
            mListener.Server.Close();
            return 0;
        }

        public void Exit()
        {
            mCancelFlag = true;
            DateTime nowTime = DateTime.Now;
            TraceLog(string.Format("サーバー終了確認 >> 開始{0}", nowTime.ToString("yyyy/MM/dd HH:mm:ss")));
            try
            {
                //念のためリスナーを停止
                mListener.Stop();
                mListener.Server.Close();

                while (bRunFlag)
                {
                    TimeSpan chkSpan = DateTime.Now - nowTime;

                    if (chkSpan.TotalSeconds > 5)
                    {
                        ErrorLog(string.Format("サーバー終了確認 >> タイムアウト"));
                        break;
                    }
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                ErrorLog("Stop 例外発生：{0}" + ex.Message);
            }
            TraceLog("Exit要求 終了");

        }

        /// <summary>
        /// 受信処理
        /// 文字コードの処理等は後処理にて行う
        /// </summary>
        /// <param name="ns"></param>
        /// <returns></returns>
        public int Recv(NetworkStream ns)
        {
            int nRcvSize = 0;

            using (MemoryStream ms = new System.IO.MemoryStream())
            {
                byte[] resBytes = new byte[4096];
                int resSize = 0;

                while (!mCancelFlag)
                {
                    //データの一部を受信する
                    try
                    {
                        resSize = ns.Read(resBytes, 0, resBytes.Length);
                        nRcvSize += resSize;
                    }
                    catch (Exception ex)
                    {
                        resSize = -1;
                        ErrorLog(string.Format("Read 例外発生:{0}",ex.Message));
                    }

                    //Readが0を返した時はクライアントが切断したと判断
                    if (resSize == 0)
                    {
                        TraceLog("クライアント切断");
                        break;
                    }

                    //タイムアウトした場合
                    if (resSize < 0)
                    {
                        ErrorLog("受信タイムアウト");
                        return -1;
                    }


                    //受信したデータを蓄積する
                    ms.Write(resBytes, 0, resSize);

                    //受信データに残りがあれば再受信
                    if (ns.DataAvailable) continue;

                    if (ms.Length > 0)
                    {
                        //受信したデータを文字列に変換
                        byte[] recvBuff = ms.GetBuffer();

                        //受信データサイズをクリア
                        nRcvSize = 0;

                        //受信データ（コマンド）に応じた処理
                        RunCmd(ns, recvBuff);
                    }
                }
            }
            return 0;
        }

        //コマンド受信後の処理
        //中身は使用側で実装する
        public virtual int RunCmd(NetworkStream ns,byte[] recvBuff)
        {
            return 0;
        }

        public virtual void TraceLog(string strLog)
        {
            string strPath = Path.Combine(UtilLib.GetRootDir(), "Log", "trace");
            if (!Directory.Exists(strPath))
            {
                Directory.CreateDirectory(strPath);
            }
            strPath += string.Format("Log{0}-{1}.csv", DateTime.Now.ToString("yyyyMMdd"), mStrType);

            UtilLib.OutString(strPath, strLog, true, true, "");
        }

        public virtual void ErrorLog(string strLog)
        {
            string strPath = Path.Combine(UtilLib.GetRootDir(), "Log", "error");
            if (!Directory.Exists(strPath))
            {
                Directory.CreateDirectory(strPath);
            }
            strPath += string.Format("Log{0}-{1}.csv", DateTime.Now.ToString("yyyyMMdd"), mStrType);

            UtilLib.OutString(strPath, strLog, true, true, "");
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                    this.Exit();
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        // ~CmnServerBase()
        // {
        //   // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
        //   Dispose(false);
        // }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
