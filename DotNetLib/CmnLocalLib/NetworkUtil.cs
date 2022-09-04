using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CmnLocalLib
{
    public class NetworkUtil
    {
        #region 定数定義
        public const int PING_NG = -1;      //PING NG
        public const int PING_OK = 0;       //PING OK
        #endregion

        /// <summary>
        /// IPアドレス
        /// </summary>
        public string IPAdd { get; set; }

        /// <summary>
        /// Ping実施回数
        /// </summary>
        public int PingCount { get; set; }
        /// <summary>
        /// Ping間隔[msec]
        /// </summary>
        public int WaitTime { get; set; }
        /// <summary>
        /// Pingタイムアウト[msec]
        /// </summary>
        public int TimeOut { get; set; }
        /// <summary>
        /// Ping送信成功判定数
        /// </summary>
        public int PassCount { get; set; }

        public NetworkUtil()
        {
            IPAdd = "127.0.0.1";
            PingCount = 3;
            WaitTime = 500;
            TimeOut = 1000;
            PassCount = 3;
        }

        /// <summary>
        /// PING確認
        /// </summary>
        /// <param name="strIP"></param>
        /// <returns></returns>
        public int PingCheck()
        {
            Ping sender = new Ping();

            int nOKCount = 0;
            try
            {
                //ping実行中にネットワークの変更を行うと例外が発生するため
                //例外をキャッチする
                for (int i = 0; i < PingCount; i++)
                {
                    PingReply reply = sender.Send(IPAdd, TimeOut);
                    if (reply.Status == IPStatus.Success)
                    {
                        Trace.WriteLine(string.Format("Reply from {0}: bytes={1} time={2}ms TTL={3}",
                            reply.Address,
                            reply.Buffer.Length,
                            reply.RoundtripTime,
                            reply.Options.Ttl));
                        nOKCount++;
                    }
                    else
                    {
                        Trace.WriteLine(reply.Status);
                    }
                    Thread.Sleep(WaitTime);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }

            if (nOKCount < PassCount)
            {
                return PING_NG;
            }
            return PING_OK;
        }

    }
}
