#define USESENDCTRL
#define USERECVCTRL
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;

namespace CmnLocalLib
{
    public class CmdSendCtrl
    {
        public const int SEND_OK = 0;
        public const int SEND_NG = -1;
        public const int SEND_ERR = -2;

        public int PortNo { get; set; }
        public string IPAdd { get; set; }

        public string MonName { get; set; }

        public int RetryMax { get; set; } = 3;

        public CmdSendCtrl()
        {

        }

        /// <summary>
        /// TCPIP接続処理
        /// 接続のみ行う
        /// </summary>
        /// <returns></returns>
        public int Connect()
        {
            string strIP = IPAdd;
            int nPortNo = PortNo;
            try
            {
                using (TcpClient client = new TcpClient(strIP, nPortNo))
                {
                    client.Close();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                return -1;
            }
            return 0;
        }

        public int DisConnect()
        {
            //通信時のみ接続し切断するため切断処理は特になし
            return 0;
        }

        /// <summary>
        /// TCPIP送信処理
        /// 接続⇒送信⇒応答受信⇒切断
        /// </summary>
        /// <param name="sendBuff"></param>
        /// <param name="recvBuff"></param>
        /// <returns></returns>
        public virtual int SendClient(byte[] sendBuff, out byte[] recvBuff)
        {
            string strIP = IPAdd;
            int nPortNo = PortNo;

            recvBuff = new byte[4096];
#if USESENDCTRL
            try
            {
                using (TcpClient client = new TcpClient(strIP, nPortNo))
                {
                    NetworkStream ns = client.GetStream();

                    ns.Write(sendBuff, 0, sendBuff.Length);

                    ns.Read(recvBuff, 0, 4096);

                    client.Close();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                return SEND_ERR;
            }

            char[] chrTrim = { (char)0x00 };
            string strRcv = Encoding.UTF8.GetString(recvBuff).Trim(chrTrim);

            int nResult = SEND_OK;
            switch (strRcv)
            {
                case "ACK":
                    nResult = SEND_OK;
                    break;
                case "NAK":
                    nResult = SEND_NG;
                    break;
                case "ERR":
                    nResult = SEND_ERR;
                    break;
            }

            return nResult;
#else
            //Thread.Sleep(3000);
            return SEND_OK;
#endif
        }

        public virtual int SendCmd(string strCmd)
        {
            //改行コードを追加
            string sendCmd = strCmd + "\r\n";

            //エンコードを指定してバッファに格納
            byte[] cmdBuff = Encoding.UTF8.GetBytes(strCmd);
            //byte[] cmdBuff = Encoding.ASCII.GetBytes(strCmd);
            byte[] rcvBuff;

            return this.SendClient(cmdBuff, out rcvBuff);
        }

        public int TaskSendCmd(string strCmd)
        {
            //改行コードを追加
            string sendCmd = strCmd + "\r\n";

            //エンコードを指定してバッファに格納
            byte[] cmdBuff = Encoding.UTF8.GetBytes(sendCmd);
            //byte[] cmdBuff = Encoding.ASCII.GetBytes(strCmd);
            byte[] rcvBuff;

            if (this.SendClient(cmdBuff, out rcvBuff) == 0)
            {
                return 0;
            }

            for (int i = 0; i < RetryMax; i++)
            {
                if (this.SendClient(cmdBuff, out rcvBuff) == 0)
                {
                    return 0;
                }
            }

            return -1;
        }
    }
}
