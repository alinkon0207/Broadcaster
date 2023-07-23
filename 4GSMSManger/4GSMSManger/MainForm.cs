using _4GSMSManger.Properties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace _4GSMSManger
{
    public partial class MainForm : Form
    {
        public const int FRAME_HDR_SIZE = 4;

        public class StateObject
        {
            public const int BUFFER_SIZE = 0x100000;
            public Socket workSocket = null;
            public byte[] buffer = new byte[BUFFER_SIZE];
            public byte[] remainBuffer = new byte[BUFFER_SIZE];
            public int offset = 0;
            public int frameLength = 0;
        }

        TcpListener m_server = null;
        Hashtable m_clientsHash = new Hashtable();
        UdpClient m_udpNode = null;
        bool m_running = false;
        int m_rf_tag = 0;
        int m_pci_num = 0;
        bool m_rf_status = false;

        string buf_4g_msg = "";
        string buf_2g_msg = "";

        const string ENDPOINT_4G_SWEEP_MODULAR = "192.168.178.3:30791";
        const string ENDPOINT_2G_SWEEP_MODULAR = "192.168.178.3:30792";
        const string ENDPOINT_4G_1 = "192.168.178.213:31790";
        const string ENDPOINT_4G_2 = "192.168.178.215:31790";
        const string ENDPOINT_4G_3 = "192.168.178.216:31790";
        const string ENDPOINT_4G_4 = "192.168.178.217:31790";
        const string ENDPOINT_GSM1 = "192.168.178.222:5558";
        const string ENDPOINT_GSM2 = "192.168.178.223:5558";

        const string FRAME_MARKER = "\r\n";

        public MainForm()
        {
            InitializeComponent();
        }

        private void HeartbeatThread()
        {
            int lastTick = Environment.TickCount;

            while (m_running)
            {
                if (Environment.TickCount - lastTick >= 4500)
                {
                    lock (this)
                    {
                        try
                        {
                            foreach (TcpClient client in m_clientsHash.Values)
                            {
                                if (client.Connected)
                                {
                                    byte[] heartbeat_cmd = new byte[] { 1, 3, 0, 0 };
                                    client.Client.Send(heartbeat_cmd, 0, heartbeat_cmd.Length, SocketFlags.None);
                                    Console.WriteLine("Sent HEARTBEAT command.");
                                }
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }

                    lastTick = Environment.TickCount;
                }

                Thread.Sleep(100);
            }
        }

        private void HandleIncomingConnection(IAsyncResult result)
        {
            TcpClient client = null;

            if (m_server == null)
                return;

            try
            {
                lock (this)
                {
                    if (m_server != null)
                    {
                        client = m_server.EndAcceptTcpClient(result);

                        m_clientsHash[client.Client.RemoteEndPoint.ToString()] = client;

                        Thread clientThread = new Thread(() => TcpReceiveThread(client));
                        clientThread.Start();
                        AppendLog(string.Format("Client connected: {0}", client.Client.RemoteEndPoint));

                        m_server.BeginAcceptTcpClient(new AsyncCallback(HandleIncomingConnection), null);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                lock (this)
                {
                    if (client != null)
                    {
                        string str = client.Client?.RemoteEndPoint?.ToString();
                        DisconClient(str);
                        client.Close();
                    }
                }
            }
        }

        public void ConClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                stream.ReadTimeout = 500;
                EndPoint endPoint = client.Client.RemoteEndPoint;

                switch (endPoint.ToString())
                {
                    case ENDPOINT_4G_1:
                        {
                            AppendLog(string.Format("4G_1 ({0}) has been connected.", endPoint.ToString()));

                            InvokeUI(() =>
                            {
                                lblStatus_lte1rf.Text = "Connected";
                            });

                            int syncFreq = Utils.ToInt32(txt_syncfreq.Text);
                            SendDeviceConfig(syncFreq.ToString(), endPoint, stream);
                        }
                        break;
                    case ENDPOINT_4G_2:
                        {
                            AppendLog(string.Format("4G_2 ({0}) has been connected.", endPoint.ToString()));

                            InvokeUI(() =>
                            {
                                lblStatus_lte2rf.Text = "Connected";
                            });

                            SendDeviceConfig("9480", endPoint, stream);
                        }
                        break;
                    case ENDPOINT_4G_3:
                        {
                            AppendLog(string.Format("4G_3 ({0}) has been connected.", endPoint.ToString()));

                            InvokeUI(() =>
                            {
                                lblStatus_lte3rf.Text = "Connected";
                            });

                            SendDeviceConfig("100", endPoint, stream);
                        }
                        break;
                    case ENDPOINT_4G_4:
                        {
                            AppendLog(string.Format("4G_4 ({0}) has been connected.", endPoint.ToString()));

                            InvokeUI(() =>
                            {
                                lblStatus_lte4rf.Text = "Connected";
                            });

                            SendDeviceConfig("1650", endPoint, stream);
                        }
                        break;
                    case ENDPOINT_4G_SWEEP_MODULAR:
                        AppendLog(string.Format("4G Sweep Modular ({0}) has been connected.", endPoint.ToString()));
                        break;
                    case ENDPOINT_2G_SWEEP_MODULAR:
                        AppendLog(string.Format("2G Sweep Modular ({0}) has been connected.", endPoint.ToString()));
                        break;
                }
            }
            catch (Exception ex)
            {
            }
        }

        public void DisconClient(string endPointStr)
        {
            try
            {
                switch (endPointStr)
                {
                    case ENDPOINT_4G_1:
                        {
                            AppendLog(string.Format("4G_1 ({0}) has been disconnected.", endPointStr));

                            InvokeUI(() =>
                            {
                                lblStatus_lte1rf.Text = "Disconnected";
                            });
                        }
                        break;
                    case ENDPOINT_4G_2:
                        {
                            AppendLog(string.Format("4G_2 ({0}) has been disconnected.", endPointStr));

                            InvokeUI(() =>
                            {
                                lblStatus_lte2rf.Text = "Disconnected";
                            });
                        }
                        break;
                    case ENDPOINT_4G_3:
                        {
                            AppendLog(string.Format("4G_3 ({0}) has been disconnected.", endPointStr));

                            InvokeUI(() =>
                            {
                                lblStatus_lte3rf.Text = "Disconnected";
                            });
                        }
                        break;
                    case ENDPOINT_4G_4:
                        {
                            AppendLog(string.Format("4G_4 ({0}) has been disconnected.", endPointStr));

                            InvokeUI(() =>
                            {
                                lblStatus_lte4rf.Text = "Disconnected";
                            });
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
            }
        }

        public void TcpReceiveThread(TcpClient client)
        {
            try
            {
                ConClient(client);
                //while (m_running)
                //{
                //    byte[] buffer = new byte[1024];
                //    int bytesRead = stream.Read(buffer, 0, 1024);

                //    Thread.Sleep(1);
                //}

                StateObject so2 = new StateObject();
                so2.workSocket = client.Client;
                client.Client.BeginReceive(so2.buffer, 0, StateObject.BUFFER_SIZE, 0,
                                      new AsyncCallback(TcpRead_Callback), so2);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing client messages: " + ex.Message);

                // Close the client connection when finished
                lock (this)
                {
                    if (client.Client != null)
                    {
                        string endPointStr = client.Client.RemoteEndPoint.ToString();
                        if (m_clientsHash.Contains(endPointStr))
                            m_clientsHash.Remove(endPointStr);

                        DisconClient(endPointStr);
                    }

                    client.Close();
                }

                Console.WriteLine("Client disconnected");
            }
        }

        public void TcpRead_Callback(IAsyncResult ar)
        {
            try
            {
                StateObject so = (StateObject)ar.AsyncState;
                Socket s = so.workSocket;

                if (!s.Connected)
                    return;

                EndPoint remotePoint = s.RemoteEndPoint;
                string remoteEndPointStr = remotePoint.ToString();
                int read = s.EndReceive(ar);
                if (read > 0)
                {
                    try
                    {
                        Console.WriteLine("received buffer: ");
                        if (so.buffer[0] == 1 || so.buffer[0] == 2) // protocol message
                        {
                            for (int i = 0; i < read; i++)
                                Console.Write("{0:X} ", so.buffer[i]);
                            Console.WriteLine("");
                        }
                        else
                            Console.WriteLine(Encoding.Default.GetString(so.buffer, 0, read));

                        ParseTcpMessage(remotePoint, so.buffer, read);
                    }
                    catch (Exception ex)
                    {
                        AppendLog(ex.Message);
                    }

                    s.BeginReceive(so.buffer, 0, StateObject.BUFFER_SIZE, 0,
                                             new AsyncCallback(TcpRead_Callback), so);
                }
                else
                {
                    if (m_clientsHash.Contains(remoteEndPointStr))
                        m_clientsHash.Remove(remoteEndPointStr);

                    DisconClient(remoteEndPointStr);

                    s.Close();
                }
            }
            catch (Exception ex)
            {
                AppendLog(ex.Message);
            }
        }

        void ParseTcpMessage(EndPoint endPoint, byte[] buffer, int length)
        {
            switch (endPoint.ToString())
            {
                case ENDPOINT_4G_SWEEP_MODULAR:
                    {
                        string msg = Encoding.Default.GetString(buffer, 0, length);
                        WriteLog(msg);

                        if (buffer[0] == 1 || buffer[0] == 2)
                        {
                            if (buf_4g_msg != "")
                            {
                                Thread thread2 = new Thread(new ParameterizedThreadStart(this.PciScan));
                                thread2.Start(buf_4g_msg);
                                thread2.Join();
                                thread2.Abort();

                                buf_4g_msg = "";
                            }
                        }
                        else // AT command
                        {
                            string atCmd;
                            int startIdx;
                            int lastIdx;

                            buf_4g_msg += msg;

                            startIdx = buf_4g_msg.IndexOf(FRAME_MARKER);
                            lastIdx = buf_4g_msg.LastIndexOf(FRAME_MARKER);
                            if (startIdx < lastIdx)
                            {
                                atCmd = buf_4g_msg.Substring(startIdx, lastIdx - startIdx);

                                if (atCmd.Contains("+PCISCAN:"))
                                {
                                    Thread thread2 = new Thread(new ParameterizedThreadStart(this.PciScan));
                                    thread2.Start(atCmd);
                                    thread2.Join();
                                    thread2.Abort();
                                }
//                                 if (atCmd.Contains("+GETSIB"))
//                                 {
//                                     Thread thread2 = new Thread(new ParameterizedThreadStart(this.GetSib5));
//                                     thread2.Start(atCmd);
//                                     thread2.Join();
//                                     thread2.Abort();
//                                 }

                                buf_4g_msg = buf_4g_msg.Substring(lastIdx);
                            }
                        }
                    }
                    break;
                case ENDPOINT_2G_SWEEP_MODULAR:
                    {
                        string msg = Encoding.Default.GetString(buffer, 0, length);

                        WriteLog(msg);

                        if (buffer[0] == 1 || buffer[0] == 2)   // General Message
                        {
                            if (buf_2g_msg != "")
                            {
                                Thread thread3 = new Thread(new ParameterizedThreadStart(this.GSMScan));
                                thread3.Start(buf_2g_msg);
                                thread3.Join();
                                thread3.Abort();

                                buf_2g_msg = "";
                            }

                            Thread thread4 = new Thread(new ParameterizedThreadStart(this.GSMScan));
                            thread4.Start(msg);
                            thread4.Join();
                            thread4.Abort();
                        }
                        else    // AT command
                        {
                            string atCmd;
                            int startIdx;
                            int lastIdx;

                            buf_2g_msg += msg;

                            startIdx = buf_2g_msg.IndexOf(FRAME_MARKER);
                            lastIdx = buf_2g_msg.LastIndexOf(FRAME_MARKER);
                            if (startIdx < lastIdx)
                            {
                                atCmd = buf_2g_msg.Substring(startIdx, lastIdx - startIdx);

                                Thread thread3 = new Thread(new ParameterizedThreadStart(this.GSMScan));
                                thread3.Start(atCmd);
                                thread3.Join();
                                thread3.Abort();

                                buf_2g_msg = buf_2g_msg.Substring(lastIdx);
                            }
                        }
                    }
                    break;
                case ENDPOINT_4G_1:
                case ENDPOINT_4G_2:
                case ENDPOINT_4G_3:
                case ENDPOINT_4G_4:
                    Lte_imsi(buffer, length);
                    break;
            }
        }

        public static void WriteLog(string msg)
        {
            try
            {
                StreamWriter writer = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "Log" + DateTime.Now.ToString("yyyyMMdd") + ".txt", true);
                writer.WriteLine("\r\n" + "Date:" + DateTime.Now.ToString() + "  " + msg);
                writer.Close();
            }
            catch (Exception)
            {
            }
        }

        public void GSMScan(object str)
        {
            string[] strArray = ((string)str).Split(new char[] { ':', ',', '\r', '\n' });
            int index = 0;

            for (int i = 0; i < strArray.Length; i++)
            {
                InvokeUI(() =>
                {
                    if (strArray[i] == "Operator")
                    {
                        int num3 = this.gridSweep_2.Rows.Add();
                        this.gridSweep_2.Rows[num3].Cells[4].Value = strArray[i + 1];
                        index = num3;
                    }
                    else if (strArray[i] == "Arfcn")
                    {
                        this.gridSweep_2.Rows[index].Cells[0].Value = strArray[i + 1];
                    }
                    else if (strArray[i] == "MCC")
                    {
                        this.gridSweep_2.Rows[index].Cells[1].Value = strArray[i + 1];
                    }
                    else if (strArray[i] == "MNC")
                    {
                        this.gridSweep_2.Rows[index].Cells[2].Value = strArray[i + 1];
                    }
                    else if (strArray[i] == "Rxlev")
                    {
                        int num2 = Convert.ToInt32(strArray[i + 1]);
                        this.gridSweep_2.Rows[index].Cells[3].Value = (num2 - 140).ToString();
                    }
                });
            }
        }

        public void GetSib5(object str)
        {
            string[] strArray = ((string)str).Split(new char[] { '\r', ':', '\n', ' ' });

            for (int i = 0; i < strArray.Length; i++)
            {
                if (strArray[i] == "dl_CarrierFreq")
                {
                    int num2 = this.gridSweep_1.Rows.Add();
                    this.gridSweep_1.Rows[num2].Cells[0].Value = strArray[i + 2];
                }
            }
        }

        public void PciScan(object str)
        {
            InvokeUI(() =>
            {
                string[] strArray = ((string)str).Split(new char[] { '\r', ':', '\n', ' ' });
                int num = 0;

                for (int i = 0; i < strArray.Length; i++)
                {
                    if (strArray[i] == "FREQ")
                    {
                        int num3 = this.gridSweep_1.Rows.Add();
                        this.gridSweep_1.Rows[num3].Cells[0].Value = strArray[i + 2];
                        num = num3;
                        this.m_pci_num = num3;
                    }
                    else if (strArray[i] == "PLMN[0]")
                    {
                        this.gridSweep_1.Rows[num].Cells[1].Value = strArray[i + 9];
                        this.gridSweep_1.Rows[num].Cells[2].Value = (strArray[i + 0x11].Length >= 2) ? strArray[i + 0x11] : ("0" + strArray[i + 0x11]);
                    }
                    else if (strArray[i] == "PLMN[1]")
                    {
                        this.gridSweep_1.Rows[num].Cells[3].Value = strArray[i + 9];
                        this.gridSweep_1.Rows[num].Cells[4].Value = strArray[i + 0x11];
                    }
                }
            });
        }

        public void Lte_imsi(byte[] data, int length)
        {
            if (data.Length < 15)
                return;

            if (data[1] == 0x13)
            {
                for (int i = 0; i < data[14]; i++)
                {
                    int count = (i * 30) + 0x12;
                    byte[] bytes = data.Skip<byte>(count).Take<byte>(15).ToArray<byte>();
                    string parameter = Encoding.Default.GetString(bytes);
                    Thread thread = new Thread(new ParameterizedThreadStart(this.DateWrite_4G));
                    thread.Start(parameter);
                    thread.Join();
                    thread.Abort();
                }
            }
        }

        public void DateWrite_4G(object str)
        {
            InvokeUI(() =>
            {
                string str2 = (string)str;
                int count = this.IMSI_4G.Rows.Count;
                bool flag = true;
                int num2 = 0;

                while (true)
                {
                    if (num2 < count)
                    {
                        if (this.IMSI_4G.Rows[num2].Cells[1].Value.ToString() != str2)
                        {
                            num2++;
                            continue;
                        }
                        this.IMSI_4G.Rows[num2].Cells[3].Value = (Utils.ToInt32(this.IMSI_4G.Rows[num2].Cells[3].Value) + 1).ToString();
                        flag = false;
                    }

                    if (flag)
                    {
                        int num4 = this.IMSI_4G.Rows.Add();
                        this.IMSI_4G.FirstDisplayedScrollingRowIndex = num4;
                        this.IMSI_4G.Rows[num4].Cells[0].Value = num4;
                        this.IMSI_4G.Rows[num4].Cells[1].Value = str2;
                        this.IMSI_4G.Rows[num4].Cells[2].Value = DateTime.Now.ToString();
                        this.IMSI_4G.Rows[num4].Cells[3].Value = "1";
                        this.IMSI_4G.Rows[num4].Cells[4].Value = "4G";
                    }
                    return;
                }
            });
        }

        public void SendDeviceConfig(string earfcn, EndPoint endPoint, NetworkStream stream)
        {
            byte[] init_notif_rsp = new byte[] { 1, 2, 0, 7, 1, 0, 4, 0, 0, 0, 0 };
            byte[] ue_redirec_rsp = new byte[] {
                2, 0x1c, 0, 0x22, 0x1b, 0, 1, 2, 0x2e, 0, 4, 0, 0, 0, 0, 14,
                0, 1, 0, 1, 0, 4, 0, 0, 0, 0, 0x2f, 0, 1, 0, 50, 0,
                1, 3, 0x4a, 0, 1, 0
            };
            byte[] init_notif = new byte[] {
                2, 1, 0, 0x12, 1, 0, 4, 0, 0, 0, 1, 3, 0, 8, 0, 0,
                0, 0, 0x5b, 100, 3, 0x60
            };
            byte[] lte_scan_rsp = new byte[] {
                2, 30, 0, 0x10, 0x1d, 0, 2, 0xff, 130, 30, 0, 1, 1, 1, 0, 4,
                0, 0, 0, 2
            };
            byte[] sniffer_start = new byte[] {
                1, 5, 0, 0x1f, 1, 0, 4, 0, 0, 0, 5, 0x1a, 0, 1, 0, 2,
                0, 1, 0, 4, 0, 1, 1, 5, 0, 4, 7, 0x21, 7, 0x21, 13, 0,
                2, 0, 60
            };
            byte[] dl_earfcn_bytes = BitConverter.GetBytes(Convert.ToInt16(earfcn));
            byte[] ul_earfcn_bytes = BitConverter.GetBytes((int)(Convert.ToInt16(earfcn) + 18000)); // For 1920~1980MHz
            byte[] cell_config = new byte[] {
                1, 15, 0, 0x3a, 8, 0, 2, 6, 0x72, 0x1f, 0, 2, 0x4c, 0xc2, 9, 0,
                2, 0, 150, 14, 0, 2, 0x24, 0x72, 0x22, 0, 4, 0, 0, 0, 1, 0x17,
                0, 4, 0, 70, 0, 0x1f, 2, 0, 1, 0, 7, 0, 1, 1, 0x18, 0,
                2, 1, 0xf4, 0x21, 0, 1, 0, 1, 0, 4, 0, 0, 0, 8
            };
            cell_config[7] = dl_earfcn_bytes[1];
            cell_config[8] = dl_earfcn_bytes[0];
            cell_config[12] = ul_earfcn_bytes[1];
            cell_config[13] = ul_earfcn_bytes[0];
            byte[] tx_power_dbm_config = new byte[] { 2, 0x2f, 0, 11, 1, 0, 4, 0, 0, 0, 3, 0x3a, 0, 1, 0x7f };

            stream.Write(init_notif_rsp, 0, init_notif_rsp.Length);
            Thread.Sleep(2000);
            stream.Write(ue_redirec_rsp, 0, ue_redirec_rsp.Length);
            Thread.Sleep(1000);
            stream.Write(init_notif, 0, init_notif.Length);
            Thread.Sleep(1000);
            stream.Write(tx_power_dbm_config, 0, tx_power_dbm_config.Length);
            Thread.Sleep(1000);
            stream.Write(lte_scan_rsp, 0, lte_scan_rsp.Length);
            Thread.Sleep(1000);
            stream.Write(sniffer_start, 0, sniffer_start.Length);
            Thread.Sleep(10000);
            stream.Write(cell_config, 0, cell_config.Length);
            Thread.Sleep(2000);

            stream.Write(init_notif_rsp, 0, init_notif_rsp.Length);
            Thread.Sleep(2000);
            stream.Write(ue_redirec_rsp, 0, ue_redirec_rsp.Length);
            Thread.Sleep(1000);
            stream.Write(init_notif, 0, init_notif.Length);
            Thread.Sleep(1000);
            stream.Write(tx_power_dbm_config, 0, tx_power_dbm_config.Length);
            Thread.Sleep(1000);
            stream.Write(lte_scan_rsp, 0, lte_scan_rsp.Length);
            Thread.Sleep(1000);
            stream.Write(sniffer_start, 0, sniffer_start.Length);
            Thread.Sleep(10000);
            stream.Write(cell_config, 0, cell_config.Length);
            Thread.Sleep(2000);

            if (m_rf_tag == 0)
                Lte_OpenRF("127", endPoint.ToString());
            else if ((m_rf_tag == 1) || (m_rf_tag == 2))
                Lte_OpenRF("43", endPoint.ToString());

            AppendLog("4G_FDD Configuration complete");
        }

        public void Lte_OpenRF(string dbm, string endPoint)
        {
            if (!m_clientsHash.Contains(endPoint))
                return;

            TcpClient client = (TcpClient)m_clientsHash[endPoint];

            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[] { 2, 0x2f, 0, 11, 1, 0, 4, 0, 0, 0, 0x10, 0x3a, 0, 1, 40 };
            buffer[14] = BitConverter.GetBytes(Convert.ToInt16(dbm))[0];

            stream.Write(buffer, 0, buffer.Length);
            stream.Flush();

            switch (endPoint)
            {
                case ENDPOINT_4G_1:
                    lblStatus_lte1rf.Text = (dbm == "43" ? "open" : "close");
                    break;
                case ENDPOINT_4G_2:
                    lblStatus_lte2rf.Text = (dbm == "43" ? "open" : "close");
                    break;
                case ENDPOINT_4G_3:
                    lblStatus_lte3rf.Text = (dbm == "43" ? "open" : "close");
                    break;
                case ENDPOINT_4G_4:
                    lblStatus_lte4rf.Text = (dbm == "43" ? "open" : "close");
                    break;
            }
        }

        private void AppendLog(string log)
        {
            InvokeUI(() =>
            {
                var item = lvLogs.Items.Add(DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss"));
                item.SubItems.Add(log);
                item.EnsureVisible();
            });
        }


        private void btnStart_Click(object sender, EventArgs e)
        {
            string ip = cmbIPAddress.Text;

            AppendLog("Waiting for connection ...");

            try
            {
                lock (this)
                {
                    m_server = new TcpListener(ip == "Any" ? IPAddress.Any : IPAddress.Parse(ip), 32790);
                    m_server.Start();
                    m_server.BeginAcceptTcpClient(new AsyncCallback(HandleIncomingConnection), null);

                    AppendLog("Creating Heartbeat thread...");
                    new Thread(HeartbeatThread).Start();
                }
            }
            catch (Exception ex)
            {
                return;
            }

            try
            {

                lock (this)
                {
                    m_udpNode = new UdpClient(new IPEndPoint(IPAddress.Any, 5557));
                    m_udpNode.BeginReceive(new AsyncCallback(UdpReceiveCallback), null);
                }
                //new Thread(new ParameterizedThreadStart(UdpReceiveThread)).Start(m_udpNode);
            }
            catch (Exception ex)
            {
                return;
            }

            m_running = true;
            ControlMainButtonEnables(true);
        }

        public void UdpReceiveCallback(IAsyncResult result)
        {
            try
            {
                IPEndPoint dataFrom = null;
                byte[] buffer = null;

                lock (this)
                {
                    if (m_udpNode != null)
                        buffer = m_udpNode.EndReceive(result, ref dataFrom);
                }

                if (buffer != null)
                {
                    string str = BitConverter.ToString(buffer, 0, buffer.Length);
                    /*AppendLog($"{dataFrom.ToString()}[{str}]");*/

                    switch (dataFrom.ToString())
                    {
                        case ENDPOINT_GSM1:
                            Gsm1Receiveparameter(buffer);
                            break;
                        case ENDPOINT_GSM2:
                            Gsm2Receiveparameter(buffer);
                            break;
                    }
                }

                if (m_running)
                {
                    lock (this)
                    {
                        if (m_udpNode != null)
                            m_udpNode.BeginReceive(new AsyncCallback(UdpReceiveCallback), null);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void Gsm1Receiveparameter(byte[] arryHex)
        {
            if (arryHex[5] == 0x11)
            {
                for (int i = 0; i < arryHex.Length; i++)
                {
                    if ((arryHex[i] == 80) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_arfcn1.Text = Encoding.Default.GetString(bytes);
                        });

                    }
                    else if ((arryHex[i] == 1) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_mcc_1.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 2) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(2).ToArray<byte>();
                        InvokeUI(() =>
                        {
                            txt_mnc_1.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 0x51) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();
                        InvokeUI(() =>
                        {
                            txt_gsm1att0.Text = Encoding.Default.GetString(bytes);
                            AppendLog("GSM1_ATT:" + this.txt_gsm1att0.Text);
                        });
                    }
                    else if ((arryHex[i] == 1) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 1))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();
                        InvokeUI(() =>
                        {
                            txt_mcc_2.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 2) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 1))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();
                        InvokeUI(() =>
                        {
                            txt_mnc_2.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 3) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(8).ToArray<byte>();
                        InvokeUI(() =>
                        {
                            txt_gsm1lac0.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 3) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 1))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(8).ToArray<byte>();
                        InvokeUI(() =>
                        {
                            txt_gsm1lac1.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 4) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(8).ToArray<byte>();
                        InvokeUI(() =>
                        {
                            txt_gsm1cid0.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 4) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 1))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(8).ToArray<byte>();
                        InvokeUI(() =>
                        {
                            txt_gsm1cid1.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 6) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_gsm1cro0.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 6) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 1))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_gsm1cro1.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                }
            }
            if (arryHex[5] == 0x18)
            {
                string str = "";
                string str2 = "";

                for (int i = 0; i < arryHex.Length; i++)
                {
                    if ((arryHex[i] == 0x18) && ((arryHex[i + 2] == 0x11) && (arryHex[i + 3] == 2)))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 3)).Take<byte>(0x10).ToArray<byte>();
                        str = Encoding.Default.GetString(bytes);
                    }
                    else if ((arryHex[i] == 0x18) && ((arryHex[i + 2] == 0x12) && (arryHex[i + 3] == 2)))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 3)).Take<byte>(0x10).ToArray<byte>();
                        str2 = Encoding.Default.GetString(bytes);
                    }
                    else if ((arryHex[i] == 6) && ((arryHex[i + 2] == 0x15) && (arryHex[i + 3] == 2)))
                    {
                        Thread thread = new Thread(new ParameterizedThreadStart(this.DateWrite));
                        thread.Start(string.Concat(new object[] { str, ",", str2, ",", (Convert.ToInt16(arryHex[i + 4]) - 0xff).ToString(), ",GSM1" }));
                        thread.Join();
                        thread.Abort();

                    }
                }
            }
            if (arryHex[5] == 0x3a)
            {
                for (int i = 0; i < arryHex.Length; i++)
                {
                    if ((arryHex[i] == 2) && (arryHex[i + 1] == 0))
                    {
                        if ((arryHex[i + 2] == 0x1c) || (arryHex[i + 2] == 60))
                        {
                            InvokeUI(() =>
                            {
                                lblStatus_gsm1rf.Text = "Open";
                            });
                        }
                        else if (arryHex[i + 2] == 12)
                        {
                            InvokeUI(() =>
                            {
                                lblStatus_gsm1rf.Text = "Close";
                            });
                        }
                    }
                }
            }
        }

        public void InvokeUI(Action action)
        {
            if (InvokeRequired)
            {
                this.Invoke(action);
            }
            else
            {
                action();
            }
        }

        public void DateWrite(object str)
        {
            InvokeUI(() =>
            {
                string[] strArray = ((string)str).Split(new char[] { ',' });
                int count = this.imsiimei.Rows.Count;
                bool flag = true;
                int num2 = 0;

                while (true)
                {
                    if (num2 < count)
                    {
                        if (this.imsiimei.Rows[num2].Cells[1].Value.ToString() != strArray[0].ToString())
                        {
                            num2++;
                            continue;
                        }
                        this.imsiimei.Rows[num2].Cells[6].Value = (Convert.ToInt32(this.imsiimei.Rows[num2].Cells[6].Value) + 1).ToString();
                        this.imsiimei.Rows[num2].Cells[4].Value = strArray[2];
                        this.imsiimei.Rows[num2].Cells[5].Value = DateTime.Now.ToString();
                        this.imsiimei.Rows[num2].Cells[7].Value = strArray[3];
                        flag = false;
                    }
                    if (flag)
                    {
                        int num4 = this.imsiimei.Rows.Add();
                        this.imsiimei.FirstDisplayedScrollingRowIndex = num4;
                        this.imsiimei.Rows[num4].Cells[0].Value = num4;
                        this.imsiimei.Rows[num4].Cells[1].Value = strArray[0];
                        this.imsiimei.Rows[num4].Cells[2].Value = strArray[1];
                        this.imsiimei.Rows[num4].Cells[3].Value = "OK";
                        this.imsiimei.Rows[num4].Cells[4].Value = strArray[2];
                        this.imsiimei.Rows[num4].Cells[5].Value = DateTime.Now.ToString();
                        this.imsiimei.Rows[num4].Cells[6].Value = "1";
                        this.imsiimei.Rows[num4].Cells[7].Value = strArray[3];
                    }
                    return;
                }
            });
        }

        private void Gsm2Receiveparameter(byte[] arryHex)
        {
            if (arryHex[5] == 0x11)
            {
                for (int i = 0; i < arryHex.Length; i++)
                {
                    if ((arryHex[i] == 80) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_arfcn3.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 1) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_mcc_3.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 2) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(2).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_mnc_3.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 0x51) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_gsm2att0.Text = Encoding.Default.GetString(bytes);
                            AppendLog("GSM2_ATT:" + txt_gsm2att0.Text);
                        });
                    }
                    else if ((arryHex[i] == 1) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 1))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_mcc_4.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 2) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 1))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_mnc_4.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 3) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(8).ToArray<byte>();
                        InvokeUI(() =>
                        {
                            txt_gsm2lac0.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 3) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 1))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(8).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_gsm2lac1.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 4) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(8).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_gsm2cid0.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 4) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 1))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(8).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_gsm2cid1.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 6) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 0))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_gsm2cro0.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                    else if ((arryHex[i] == 6) && ((arryHex[i + 1] == 1) && ((arryHex[5] == 0x11) && (arryHex[6] == 1))))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 2)).Take<byte>(3).ToArray<byte>();

                        InvokeUI(() =>
                        {
                            txt_gsm2cro1.Text = Encoding.Default.GetString(bytes);
                        });
                    }
                }
            }
            if (arryHex[5] == 0x18)
            {
                string str = "";
                string str2 = "";

                for (int i = 0; i < arryHex.Length; i++)
                {
                    if ((arryHex[i] == 0x18) && ((arryHex[i + 2] == 0x11) && (arryHex[i + 3] == 2)))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 3)).Take<byte>(15).ToArray<byte>();
                        str = Encoding.Default.GetString(bytes);
                    }
                    else if ((arryHex[i] == 0x18) && ((arryHex[i + 2] == 0x12) && (arryHex[i + 3] == 2)))
                    {
                        byte[] bytes = arryHex.Skip<byte>((i + 3)).Take<byte>(15).ToArray<byte>();
                        str2 = Encoding.Default.GetString(bytes);
                    }
                    else if ((arryHex[i] == 6) && ((arryHex[i + 2] == 0x15) && (arryHex[i + 3] == 2)))
                    {
                        Thread thread = new Thread(new ParameterizedThreadStart(this.DateWrite));
                        thread.Start(string.Concat(new object[] { str, ",", str2, ",", (Convert.ToInt16(arryHex[i + 4]) - 0xff).ToString(), ",GSM2" }));
                        thread.Join();
                        thread.Abort();
                    }
                }
            }
            if (arryHex[5] == 0x3a)
            {
                for (int i = 0; i < arryHex.Length; i++)
                {
                    if ((arryHex[i] == 2) && (arryHex[i + 1] == 0))
                    {
                        if ((arryHex[i + 2] == 0x1c) || (arryHex[i + 2] == 60))
                        {
                            InvokeUI(() =>
                            {
                                lblStatus_gsm2rf.Text = "Open";
                            });
                        }
                        else if (arryHex[i + 2] == 12)
                        {
                            InvokeUI(() =>
                            {
                                lblStatus_gsm2rf.Text = "Close";
                            });
                        }
                    }
                }
            }
        }

        void ControlMainButtonEnables(bool running)
        {
            btnStart.Enabled = !running;
            btnStop.Enabled = running;

            InvokeUI(() =>
            {
            });
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            IPAddress[] localIPs = Dns.GetHostEntry(Dns.GetHostName()).AddressList;

            cmbIPAddress.Items.Add("Any");
            foreach (IPAddress addr in localIPs)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    Console.WriteLine("IPv4 Address: " + addr);
                    cmbIPAddress.Items.Add(addr);
                }
                else if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    Console.WriteLine("IPv6 Address: " + addr);
                }
            }

            cmbIPAddress.SelectedIndex = 0;

            ControlMainButtonEnables(false);

            txt_syncfreq.Text = "0";

            lblStatus_lte2rf.Text = "Disconnected";
            lblStatus_lte3rf.Text = "Disconnected";
            lblStatus_lte4rf.Text = "Disconnected";
            lblStatus_lte1rf.Text = "Disconnected";

            this.txt_earfcn4g1.Text = Settings.Default.LTE1_EARFCN;
            this.txt_plmn4g1.Text = Settings.Default.LTE1_PLMN;
            this.txt_earfcn4g2.Text = Settings.Default.LTE2_EARFCN;
            this.txt_plmn4g2.Text = Settings.Default.LTE2_PLMN;
            this.txt_earfcn4g3.Text = Settings.Default.LTE3_EARFCN;
            this.txt_plmn4g3.Text = Settings.Default.LTE3_PLMN;
            this.txt_earfcn4g4.Text = Settings.Default.LTE4_EARFCN;
            this.txt_plmn4g4.Text = Settings.Default.LTE4_PLMN;
            this.txtSMS_sender_1.Text = Settings.Default.SENDER1;
            this.txtSMS_SMS_1.Text = Settings.Default.SMS1;
            this.txtSMS_sender_2.Text = Settings.Default.SENDER2;
            this.txtSMS_SMS_2.Text = Settings.Default.SMS2;
            this.txt_syncfreq.Text = Settings.Default.SYNC_FREQ;
            this.txt_arfcn1.Text = Settings.Default.GSM_F0;
            this.txt_arfcn3.Text = Settings.Default.GSM_F3;
            this.txt_mcc_1.Text = Settings.Default.GSM_MCC1;
            this.txt_mcc_2.Text = Settings.Default.GSM_MCC2;
            this.txt_mnc_1.Text = Settings.Default.GSM_MNC1;
            this.txt_mnc_2.Text = Settings.Default.GSM_MNC2;
            this.txt_mcc_3.Text = Settings.Default.GSM_MCC3;
            this.txt_mcc_4.Text = Settings.Default.GSM_MCC4;
            this.txt_mnc_3.Text = Settings.Default.GSM_MNC3;
            this.txt_mnc_4.Text = Settings.Default.GSM_MNC4;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            m_running = false;

            lock (this)
            {
                if (m_server != null)
                {
                    m_server.Stop();
                    m_server = null;
                    AppendLog("Server has been stopped.");
                }

                m_udpNode.Close();
                m_udpNode = null;
            }

            //InvokeUI(() =>
            //{
            //    lblStatus_lte2rf.Text = "Disconnected";
            //    lblStatus_lte3rf.Text = "Disconnected";
            //    lblStatus_lte4rf.Text = "Disconnected";
            //    lblStatus_lte1rf.Text = "Disconnected";
            //});

            //ControlMainButtonEnables(false);

            Application.Exit();
        }

        private void GetGsm1Parameter()
        {
            byte[] str = new byte[] {
                0x2c, 0, 0, 0, 0, 1, 0, 0, 12, 0, 80, 1, 0, 0, 0, 0,
                0, 0, 0, 0, 12, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                12, 0, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0
            };
            byte[] buffer2 = new byte[] {
                0x2c, 0, 0, 0, 0, 1, 1, 0, 12, 0, 80, 1, 0, 0, 0, 0,
                0, 0, 0, 0, 12, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                12, 0, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0
            };
            byte[] buffer3 = new byte[] {
                20, 0, 0, 0, 0, 1, 0, 0, 12, 0, 0x51, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };
            byte[] buffer4 = new byte[] {
                20, 0, 0, 0, 0, 1, 0, 0, 12, 0, 6, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };
            byte[] buffer5 = new byte[] {
                20, 0, 0, 0, 0, 1, 1, 0, 12, 0, 6, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };
            byte[] buffer6 = new byte[] {
                20, 0, 0, 0, 0, 1, 0, 0, 12, 0, 3, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };
            byte[] buffer7 = new byte[] {
                20, 0, 0, 0, 0, 1, 1, 0, 12, 0, 3, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };
            byte[] buffer8 = new byte[] {
                20, 0, 0, 0, 0, 1, 0, 0, 12, 0, 4, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };
            byte[] buffer9 = new byte[] {
                20, 0, 0, 0, 0, 1, 1, 0, 12, 0, 4, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };


            UdpSendTo(str, ENDPOINT_GSM1);
            UdpSendTo(buffer3, ENDPOINT_GSM1);
            Thread.Sleep(1000);
            UdpSendTo(buffer2, ENDPOINT_GSM1);
            UdpSendTo(buffer4, ENDPOINT_GSM1);
            UdpSendTo(buffer5, ENDPOINT_GSM1);
            UdpSendTo(buffer6, ENDPOINT_GSM1);
            UdpSendTo(buffer7, ENDPOINT_GSM1);
            UdpSendTo(buffer8, ENDPOINT_GSM1);
            UdpSendTo(buffer9, ENDPOINT_GSM1);
        }


        public void UdpSendTo(byte[] data, string dest)
        {
            lock (this)
            {
                if (m_udpNode != null)
                {
                    string[] param = dest.Split(":".ToCharArray());

                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(param[0]), Utils.ToInt32(param[1]));
                    m_udpNode.Send(data, data.Length, endPoint);
                }
            }
        }

        private void GetGsm2Parameter()
        {
            byte[] str = new byte[] {
                0x2c, 0, 0, 0, 0, 1, 0, 0, 12, 0, 80, 1, 0, 0, 0, 0,
                0, 0, 0, 0, 12, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                12, 0, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0
            };
            byte[] buffer2 = new byte[] {
                0x2c, 0, 0, 0, 0, 1, 1, 0, 12, 0, 80, 1, 0, 0, 0, 0,
                0, 0, 0, 0, 12, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                12, 0, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0
            };
            byte[] buffer3 = new byte[] {
                20, 0, 0, 0, 0, 1, 0, 0, 12, 0, 0x51, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };
            byte[] buffer4 = new byte[] {
                20, 0, 0, 0, 0, 1, 0, 0, 12, 0, 6, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };
            byte[] buffer5 = new byte[] {
                20, 0, 0, 0, 0, 1, 1, 0, 12, 0, 6, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };
            byte[] buffer6 = new byte[] {
                20, 0, 0, 0, 0, 1, 0, 0, 12, 0, 3, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };
            byte[] buffer7 = new byte[] {
                20, 0, 0, 0, 0, 1, 1, 0, 12, 0, 3, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };
            byte[] buffer8 = new byte[] {
                20, 0, 0, 0, 0, 1, 0, 0, 12, 0, 4, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };
            byte[] buffer9 = new byte[] {
                20, 0, 0, 0, 0, 1, 1, 0, 12, 0, 4, 1, 0, 0, 0, 0,
                0, 0, 0, 0
            };

            UdpSendTo(str, ENDPOINT_GSM2);
            UdpSendTo(buffer3, ENDPOINT_GSM2);
            Thread.Sleep(1000);
            UdpSendTo(buffer2, ENDPOINT_GSM2);
            UdpSendTo(buffer4, ENDPOINT_GSM2);
            UdpSendTo(buffer5, ENDPOINT_GSM2);
            UdpSendTo(buffer6, ENDPOINT_GSM2);
            UdpSendTo(buffer7, ENDPOINT_GSM2);
            UdpSendTo(buffer8, ENDPOINT_GSM2);
            UdpSendTo(buffer9, ENDPOINT_GSM2);
        }

        private void btn2G_getgsm1_Click(object sender, EventArgs e)
        {
            GetGsm1Parameter();
        }

        bool Check2GValidationInput_GSM1()
        {
            string errorMsg = "Kindly, enter parameter.";
            if (string.IsNullOrEmpty(txt_arfcn1.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_arfcn1.Focus();
                return false;
            }
            if (string.IsNullOrEmpty(txt_gsm1att0.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm1att0.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_mcc_1.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_mcc_1.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_mnc_1.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_mnc_1.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_gsm1cro0.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm1cro0.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_mcc_2.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_mcc_2.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_mnc_2.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_mnc_2.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_gsm1cro1.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm1cro1.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_gsm1lac0.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm1lac0.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_gsm1lac1.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm1lac1.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_gsm1cid0.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm1cid0.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_gsm1cid1.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm1cid1.Focus();
                return false;
            }

            return true;
        }

        bool Check2GValidationInput_GSM2()
        {
            string errorMsg = "Kindly, enter parameter.";
            if (string.IsNullOrEmpty(txt_arfcn3.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_arfcn3.Focus();
                return false;
            }
            if (string.IsNullOrEmpty(txt_gsm2att0.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm2att0.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_mcc_3.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_mcc_3.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_mnc_3.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_mnc_3.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_gsm2cro0.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm2cro0.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_mcc_4.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_mcc_4.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_mnc_4.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_mnc_4.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_gsm2cro1.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm2cro1.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_gsm2lac0.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm2lac0.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_gsm2lac1.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm2lac1.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_gsm2cid0.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm2cid0.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(txt_gsm2cid1.Text))
            {
                //MessageBox.Show(errorMsg, "2G");
                txt_gsm2cid1.Focus();
                return false;
            }

            return true;
        }
        private void btn2G_setgsm1_Click(object sender, EventArgs e)
        {
            if (!Check2GValidationInput_GSM1())
                return;

            SetGsm1Parameter();
            Settings.Default.GSM_F0 = txt_arfcn1.Text;
            Settings.Default.GSM_MCC1 = txt_mcc_1.Text;
            Settings.Default.GSM_MNC1 = txt_mnc_1.Text;
            Settings.Default.GSM_MCC2 = txt_mcc_2.Text;
            Settings.Default.GSM_MNC2 = txt_mnc_2.Text;
        }

        private void btn2G_getgsm2_Click(object sender, EventArgs e)
        {
            GetGsm2Parameter();
        }

        private void SetGsm1Parameter()
        {
            byte[] buffer = new byte[] { 0x2c };
            byte[] buffer2 = new byte[] { 20 };
            byte[] buffer42 = new byte[4];
            buffer42[1] = 2;
            byte[] buffer3 = buffer42;
            byte[] buffer43 = new byte[4];
            buffer43[1] = 2;
            buffer43[2] = 1;
            byte[] buffer4 = buffer43;
            byte[] buffer5 = new byte[] { 12, 0, 80, 1, 0x34, 0x36, 0, 0, 0, 0, 0, 0 };
            byte[] buffer6 = new byte[] { 12, 0, 80, 1, 0x34, 0x36, 0, 0, 0, 0, 0, 0 };
            byte[] bytes = Encoding.Default.GetBytes(txt_arfcn1.Text);
            for (int i = 0; i < bytes.Length; i++)
            {
                buffer5[i + 4] = bytes[i];
            }
            byte[] buffer8 = new byte[] { 12, 0, 1, 1, 0x34, 0x36, 0x30, 0, 0, 0, 0, 0 };
            byte[] buffer9 = Encoding.Default.GetBytes(txt_mcc_1.Text);
            for (int j = 0; j < buffer9.Length; j++)
            {
                buffer8[j + 4] = buffer9[j];
            }
            byte[] buffer10 = new byte[] { 12, 0, 2, 1, 0x30, 0x31, 0, 0, 0, 0, 0, 0 };
            byte[] buffer11 = Encoding.Default.GetBytes(txt_mnc_1.Text);
            for (int k = 0; k < buffer11.Length; k++)
            {
                buffer10[k + 4] = buffer11[k];
            }
            byte[] buffer12 = Encoding.Default.GetBytes((Utils.ToInt32(txt_arfcn1.Text) + 5).ToString());
            for (int m = 0; m < buffer12.Length; m++)
            {
                buffer6[m + 4] = buffer12[m];
            }
            byte[] buffer13 = new byte[] { 12, 0, 1, 1, 0x34, 0x36, 0x30, 0, 0, 0, 0, 0 };
            byte[] buffer14 = Encoding.Default.GetBytes(txt_mcc_2.Text);
            for (int n = 0; n < buffer14.Length; n++)
            {
                buffer13[n + 4] = buffer14[n];
            }
            byte[] buffer15 = new byte[] { 12, 0, 2, 1, 0x30, 0x31, 0, 0, 0, 0, 0, 0 };
            byte[] buffer16 = Encoding.Default.GetBytes(txt_mnc_2.Text);
            for (int num6 = 0; num6 < buffer16.Length; num6++)
            {
                buffer15[num6 + 4] = buffer16[num6];
            }
            byte[] array = new byte[(((buffer.Length + buffer3.Length) + buffer5.Length) + buffer8.Length) + buffer10.Length];
            byte[] buffer18 = new byte[(((buffer.Length + buffer3.Length) + buffer5.Length) + buffer8.Length) + buffer10.Length];
            buffer.CopyTo(array, 0);
            buffer3.CopyTo(array, buffer.Length);
            buffer5.CopyTo(array, (int)(buffer.Length + buffer3.Length));
            buffer8.CopyTo(array, (int)((buffer.Length + buffer3.Length) + buffer5.Length));
            buffer10.CopyTo(array, (int)(((buffer.Length + buffer3.Length) + buffer5.Length) + buffer8.Length));
            UdpSendTo(array, ENDPOINT_GSM1);
            buffer.CopyTo(buffer18, 0);
            buffer4.CopyTo(buffer18, buffer.Length);
            buffer6.CopyTo(buffer18, (int)(buffer.Length + buffer4.Length));
            buffer13.CopyTo(buffer18, (int)((buffer.Length + buffer4.Length) + buffer6.Length));
            buffer15.CopyTo(buffer18, (int)(((buffer.Length + buffer4.Length) + buffer6.Length) + buffer15.Length));
            UdpSendTo(buffer18, ENDPOINT_GSM1);
            byte[] buffer19 = new byte[] { 12, 0, 0x51, 1, 0x33, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer20 = Encoding.Default.GetBytes(txt_gsm1att0.Text);
            byte[] buffer21 = new byte[(buffer2.Length + buffer3.Length) + buffer19.Length];
            for (int num7 = 0; num7 < buffer20.Length; num7++)
            {
                buffer19[num7 + 4] = buffer20[num7];
            }
            buffer2.CopyTo(buffer21, 0);
            buffer3.CopyTo(buffer21, buffer2.Length);
            buffer19.CopyTo(buffer21, (int)(buffer2.Length + buffer3.Length));
            byte[] buffer22 = new byte[] { 12, 0, 3, 1, 0x36, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer23 = new byte[] { 12, 0, 3, 1, 0x36, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer24 = new byte[(buffer2.Length + buffer3.Length) + buffer22.Length];
            byte[] buffer25 = new byte[(buffer2.Length + buffer4.Length) + buffer23.Length];
            byte[] buffer26 = Encoding.Default.GetBytes(txt_gsm1lac0.Text);
            byte[] buffer27 = Encoding.Default.GetBytes(txt_gsm1lac1.Text);
            for (int num8 = 0; num8 < buffer26.Length; num8++)
            {
                buffer22[num8 + 4] = buffer26[num8];
            }
            buffer2.CopyTo(buffer24, 0);
            buffer3.CopyTo(buffer24, buffer2.Length);
            buffer22.CopyTo(buffer24, (int)(buffer2.Length + buffer3.Length));
            UdpSendTo(buffer24, ENDPOINT_GSM1);
            for (int num9 = 0; num9 < buffer27.Length; num9++)
            {
                buffer23[num9 + 4] = buffer27[num9];
            }
            buffer2.CopyTo(buffer25, 0);
            buffer4.CopyTo(buffer25, buffer2.Length);
            buffer23.CopyTo(buffer25, (int)(buffer2.Length + buffer4.Length));
            UdpSendTo(buffer25, ENDPOINT_GSM1);
            byte[] buffer28 = new byte[] { 12, 0, 4, 1, 0x36, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer29 = new byte[] { 12, 0, 4, 1, 0x36, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer30 = Encoding.Default.GetBytes(txt_gsm1cid0.Text);
            byte[] buffer31 = Encoding.Default.GetBytes(txt_gsm1cid1.Text);
            byte[] buffer32 = new byte[(buffer2.Length + buffer3.Length) + buffer28.Length];
            byte[] buffer33 = new byte[(buffer2.Length + buffer4.Length) + buffer29.Length];
            for (int num10 = 0; num10 < buffer30.Length; num10++)
            {
                buffer28[num10 + 4] = buffer30[num10];
            }
            buffer2.CopyTo(buffer32, 0);
            buffer3.CopyTo(buffer32, buffer2.Length);
            buffer28.CopyTo(buffer32, (int)(buffer2.Length + buffer3.Length));
            UdpSendTo(buffer32, ENDPOINT_GSM1);
            for (int num11 = 0; num11 < buffer31.Length; num11++)
            {
                buffer29[num11 + 4] = buffer31[num11];
            }
            buffer2.CopyTo(buffer33, 0);
            buffer4.CopyTo(buffer33, buffer2.Length);
            buffer29.CopyTo(buffer33, (int)(buffer2.Length + buffer4.Length));
            UdpSendTo(buffer33, ENDPOINT_GSM1);
            byte[] buffer34 = new byte[] { 12, 0, 6, 1, 0x36, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer35 = new byte[] { 12, 0, 6, 1, 0x36, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer36 = Encoding.Default.GetBytes(txt_gsm1cro0.Text);
            byte[] buffer37 = Encoding.Default.GetBytes(txt_gsm1cro1.Text);
            byte[] buffer38 = new byte[(buffer.Length + buffer3.Length) + buffer34.Length];
            byte[] buffer39 = new byte[(buffer.Length + buffer4.Length) + buffer35.Length];
            for (int num12 = 0; num12 < buffer36.Length; num12++)
            {
                buffer34[num12 + 4] = buffer36[num12];
            }
            buffer2.CopyTo(buffer38, 0);
            buffer3.CopyTo(buffer38, buffer2.Length);
            buffer34.CopyTo(buffer38, (int)(buffer2.Length + buffer3.Length));
            UdpSendTo(buffer38, ENDPOINT_GSM1);
            for (int num13 = 0; num13 < buffer37.Length; num13++)
            {
                buffer35[num13 + 4] = buffer37[num13];
            }
            buffer2.CopyTo(buffer39, 0);
            buffer4.CopyTo(buffer39, buffer2.Length);
            buffer35.CopyTo(buffer39, (int)(buffer2.Length + buffer4.Length));
            UdpSendTo(buffer39, ENDPOINT_GSM1);
        }

        private void btn2G_setgsm2_Click(object sender, EventArgs e)
        {
            if (!Check2GValidationInput_GSM2())
                return;

            Settings.Default.GSM_F3 = txt_arfcn3.Text;
            Settings.Default.GSM_MCC3 = txt_mcc_3.Text;
            Settings.Default.GSM_MNC3 = txt_mnc_3.Text;
            Settings.Default.GSM_MCC4 = txt_mcc_4.Text;
            Settings.Default.GSM_MNC4 = txt_mnc_4.Text;
            SetGsm2Parameter();
        }

        private void SetGsm2Parameter()
        {
            byte[] buffer = new byte[] { 0x2c };
            byte[] buffer2 = new byte[] { 20 };
            byte[] buffer42 = new byte[4];
            buffer42[1] = 2;
            byte[] buffer3 = buffer42;
            byte[] buffer43 = new byte[4];
            buffer43[1] = 2;
            buffer43[2] = 1;
            byte[] buffer4 = buffer43;
            byte[] buffer5 = new byte[] { 12, 0, 80, 1, 0x34, 0x36, 0, 0, 0, 0, 0, 0 };
            byte[] buffer6 = new byte[] { 12, 0, 80, 1, 0x34, 0x36, 0, 0, 0, 0, 0, 0 };
            byte[] bytes = Encoding.Default.GetBytes(txt_arfcn3.Text);
            for (int i = 0; i < bytes.Length; i++)
            {
                buffer5[i + 4] = bytes[i];
            }
            byte[] buffer8 = new byte[] { 12, 0, 1, 1, 0x34, 0x36, 0x30, 0, 0, 0, 0, 0 };
            byte[] buffer9 = Encoding.Default.GetBytes(txt_mcc_3.Text);
            for (int j = 0; j < buffer9.Length; j++)
            {
                buffer8[j + 4] = buffer9[j];
            }
            byte[] buffer10 = new byte[] { 12, 0, 2, 1, 0x30, 0x31, 0, 0, 0, 0, 0, 0 };
            byte[] buffer11 = Encoding.Default.GetBytes(txt_mnc_3.Text);
            for (int k = 0; k < buffer11.Length; k++)
            {
                buffer10[k + 4] = buffer11[k];
            }
            byte[] buffer12 = Encoding.Default.GetBytes((Utils.ToInt32(txt_arfcn3.Text) + 5).ToString());
            for (int m = 0; m < buffer12.Length; m++)
            {
                buffer6[m + 4] = buffer12[m];
            }
            byte[] buffer13 = new byte[] { 12, 0, 1, 1, 0x34, 0x36, 0x30, 0, 0, 0, 0, 0 };
            byte[] buffer14 = Encoding.Default.GetBytes(txt_mcc_4.Text);
            for (int n = 0; n < buffer14.Length; n++)
            {
                buffer13[n + 4] = buffer14[n];
            }
            byte[] buffer15 = new byte[] { 12, 0, 2, 1, 0x30, 0x31, 0, 0, 0, 0, 0, 0 };
            byte[] buffer16 = Encoding.Default.GetBytes(txt_mnc_4.Text);
            for (int num6 = 0; num6 < buffer16.Length; num6++)
            {
                buffer15[num6 + 4] = buffer16[num6];
            }
            byte[] array = new byte[(((buffer.Length + buffer3.Length) + buffer5.Length) + buffer8.Length) + buffer10.Length];
            byte[] buffer18 = new byte[(((buffer.Length + buffer3.Length) + buffer5.Length) + buffer8.Length) + buffer10.Length];
            buffer.CopyTo(array, 0);
            buffer3.CopyTo(array, buffer.Length);
            buffer5.CopyTo(array, (int)(buffer.Length + buffer3.Length));
            buffer8.CopyTo(array, (int)((buffer.Length + buffer3.Length) + buffer5.Length));
            buffer10.CopyTo(array, (int)(((buffer.Length + buffer3.Length) + buffer5.Length) + buffer8.Length));
            UdpSendTo(array, ENDPOINT_GSM2);
            buffer.CopyTo(buffer18, 0);
            buffer4.CopyTo(buffer18, buffer.Length);
            buffer6.CopyTo(buffer18, (int)(buffer.Length + buffer4.Length));
            buffer13.CopyTo(buffer18, (int)((buffer.Length + buffer4.Length) + buffer6.Length));
            buffer15.CopyTo(buffer18, (int)(((buffer.Length + buffer4.Length) + buffer6.Length) + buffer15.Length));
            UdpSendTo(buffer18, ENDPOINT_GSM2);
            byte[] buffer19 = new byte[] { 12, 0, 0x51, 1, 0x33, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer20 = Encoding.Default.GetBytes(txt_gsm2att0.Text);
            byte[] buffer21 = new byte[(buffer2.Length + buffer3.Length) + buffer19.Length];
            for (int num7 = 0; num7 < buffer20.Length; num7++)
            {
                buffer19[num7 + 4] = buffer20[num7];
            }
            buffer2.CopyTo(buffer21, 0);
            buffer3.CopyTo(buffer21, buffer2.Length);
            buffer19.CopyTo(buffer21, (int)(buffer2.Length + buffer3.Length));
            byte[] buffer22 = new byte[] { 12, 0, 3, 1, 0x36, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer23 = new byte[] { 12, 0, 3, 1, 0x36, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer24 = Encoding.Default.GetBytes(txt_gsm2lac0.Text);
            byte[] buffer25 = Encoding.Default.GetBytes(txt_gsm2lac1.Text);
            byte[] buffer26 = new byte[(buffer2.Length + buffer3.Length) + buffer22.Length];
            byte[] buffer27 = new byte[(buffer2.Length + buffer4.Length) + buffer23.Length];
            for (int num8 = 0; num8 < buffer24.Length; num8++)
            {
                buffer22[num8 + 4] = buffer24[num8];
            }
            buffer2.CopyTo(buffer26, 0);
            buffer3.CopyTo(buffer26, buffer2.Length);
            buffer22.CopyTo(buffer26, (int)(buffer2.Length + buffer3.Length));
            this.UdpSendTo(buffer26, ENDPOINT_GSM2);
            for (int num9 = 0; num9 < buffer25.Length; num9++)
            {
                buffer23[num9 + 4] = buffer25[num9];
            }
            buffer2.CopyTo(buffer27, 0);
            buffer4.CopyTo(buffer27, buffer2.Length);
            buffer23.CopyTo(buffer27, (int)(buffer2.Length + buffer4.Length));
            this.UdpSendTo(buffer27, ENDPOINT_GSM2);
            byte[] buffer28 = new byte[] { 12, 0, 4, 1, 0x36, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer29 = new byte[] { 12, 0, 4, 1, 0x36, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer30 = Encoding.Default.GetBytes(txt_gsm2cid0.Text);
            byte[] buffer31 = Encoding.Default.GetBytes(txt_gsm2cid1.Text);
            byte[] buffer32 = new byte[(buffer2.Length + buffer3.Length) + buffer28.Length];
            byte[] buffer33 = new byte[(buffer2.Length + buffer4.Length) + buffer29.Length];
            for (int num10 = 0; num10 < buffer30.Length; num10++)
            {
                buffer28[num10 + 4] = buffer30[num10];
            }
            buffer2.CopyTo(buffer32, 0);
            buffer3.CopyTo(buffer32, buffer2.Length);
            buffer28.CopyTo(buffer32, (int)(buffer2.Length + buffer3.Length));
            UdpSendTo(buffer32, ENDPOINT_GSM2);
            for (int num11 = 0; num11 < buffer31.Length; num11++)
            {
                buffer29[num11 + 4] = buffer31[num11];
            }
            buffer2.CopyTo(buffer33, 0);
            buffer4.CopyTo(buffer33, buffer2.Length);
            buffer29.CopyTo(buffer33, (int)(buffer2.Length + buffer4.Length));
            UdpSendTo(buffer33, ENDPOINT_GSM2);
            byte[] buffer34 = new byte[] { 12, 0, 6, 1, 0x36, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer35 = new byte[] { 12, 0, 6, 1, 0x36, 0x30, 0, 0, 0, 0, 0, 0 };
            byte[] buffer36 = Encoding.Default.GetBytes(txt_gsm2cro0.Text);
            byte[] buffer37 = Encoding.Default.GetBytes(txt_gsm2cro1.Text);
            byte[] buffer38 = new byte[(buffer.Length + buffer3.Length) + buffer34.Length];
            byte[] buffer39 = new byte[(buffer.Length + buffer4.Length) + buffer35.Length];
            for (int num12 = 0; num12 < buffer36.Length; num12++)
            {
                buffer34[num12 + 4] = buffer36[num12];
            }
            buffer2.CopyTo(buffer38, 0);
            buffer3.CopyTo(buffer38, buffer2.Length);
            buffer34.CopyTo(buffer38, (int)(buffer2.Length + buffer3.Length));
            UdpSendTo(buffer38, ENDPOINT_GSM2);
            for (int num13 = 0; num13 < buffer37.Length; num13++)
            {
                buffer35[num13 + 4] = buffer37[num13];
            }
            buffer2.CopyTo(buffer39, 0);
            buffer4.CopyTo(buffer39, buffer2.Length);
            buffer35.CopyTo(buffer39, (int)(buffer2.Length + buffer4.Length));
            UdpSendTo(buffer39, ENDPOINT_GSM2);
        }

        private void btn4G_Save1_Click(object sender, EventArgs e)
        {
            Settings.Default.LTE1_EARFCN = txt_earfcn4g1.Text;
            Settings.Default.LTE1_PLMN = txt_plmn4g1.Text;
            Settings.Default.SYNC_FREQ = txt_syncfreq.Text;
            Settings.Default.Save();
            string[] strArray = txt_earfcn4g1.Text.Split(new char[] { ',' });
            if ((strArray[0] == "") && (txt_plmn4g1.Text == ""))
            {
                MessageBox.Show("LTE3 EARFCN SET EEROR !");
            }
            else
            {
                TDD_LteCellUpdate(txt_plmn4g1.Text, strArray[0], ENDPOINT_4G_1);
                Lte1EarfcnScan();
            }
        }

        public void TDD_LteCellUpdate(string plmn, string earfcn, string ipport)
        {
            byte[] buffer = new byte[] { 1, 0x11 };
            byte[] buffer2 = new byte[2];
            byte[] buffer3 = new byte[] { 0x22, 0, 4, 0, 0, 0, 3 };
            byte[] buffer4 = new byte[] { 0x17, 0, 4, 0, 70, 1, 0x1f };
            byte[] buffer5 = new byte[] { 14, 0, 2, 0x22, 0x47 };
            byte[] buffer6 = new byte[] { 8, 0, 2, 0, 100 };
            byte[] buffer7 = new byte[] { 0x1f, 0, 2, 70, 180 };
            byte[] buffer8 = new byte[] { 1, 0, 4, 0, 0, 0, 12 };
            string[] strArray = plmn.Split(new char[] { ',' });
            buffer3[6] = BitConverter.GetBytes(strArray.Length)[0];
            byte[] bytes = BitConverter.GetBytes(Convert.ToInt32(earfcn));
            buffer6[3] = bytes[1];
            buffer6[4] = bytes[0];
            byte[] buffer11 = BitConverter.GetBytes(Convert.ToInt32(earfcn));
            buffer7[3] = buffer11[1];
            buffer7[4] = buffer11[0];
            byte[] array = new byte[((((((buffer.Length + buffer2.Length) + buffer5.Length) + buffer6.Length) + buffer7.Length) + buffer8.Length) + buffer3.Length) + (buffer4.Length * strArray.Length)];
            byte[] buffer13 = BitConverter.GetBytes((int)(((((buffer5.Length + buffer6.Length) + buffer7.Length) + buffer8.Length) + buffer3.Length) + (buffer4.Length * strArray.Length)));
            buffer2[0] = buffer13[1];
            buffer2[1] = buffer13[0];
            buffer.CopyTo(array, 0);
            buffer2.CopyTo(array, buffer.Length);
            buffer5.CopyTo(array, (int)(buffer.Length + buffer2.Length));
            buffer6.CopyTo(array, (int)((buffer.Length + buffer2.Length) + buffer5.Length));
            buffer7.CopyTo(array, (int)(((buffer.Length + buffer2.Length) + buffer5.Length) + buffer6.Length));
            buffer8.CopyTo(array, (int)((((buffer.Length + buffer2.Length) + buffer5.Length) + buffer6.Length) + buffer7.Length));
            buffer3.CopyTo(array, (int)(((((buffer.Length + buffer2.Length) + buffer5.Length) + buffer6.Length) + buffer7.Length) + buffer8.Length));
            int index = 0;
            while (index < strArray.Length)
            {
                strArray[index] = strArray[index] + "f";
                string[] strArray2 = new string[3];
                int startIndex = 0;
                int num3 = 0;
                while (true)
                {
                    if (num3 >= 3)
                    {
                        buffer4.CopyTo(array, (int)(((((((buffer.Length + buffer2.Length) + buffer5.Length) + buffer6.Length) + buffer7.Length) + buffer8.Length) + buffer3.Length) + (index * buffer4.Length)));
                        index++;
                        break;
                    }
                    strArray2[num3] = strArray[index].Substring(startIndex, 2);
                    startIndex += 2;
                    buffer4[4 + num3] = Convert.ToByte(strArray2[num3], 0x10);
                    num3++;
                }
            }

            AppendLog($"{ipport}[{BitConverter.ToString(array, 0, array.Length)}]");
            TcpSend(array, ipport);
        }

        public void Lte1EarfcnScan()
        {
            byte[] buffer = new byte[] { 2, 0x33, 0, 0, 0x49, 0, 4, 0, 0, 0, 60, 0x48 };
            byte[] buffer2 = StringToHexbyte(txt_earfcn4g1.Text);
            byte[] buffer3 = new byte[2];
            buffer3[1] = BitConverter.GetBytes(buffer2.Length)[0];
            txt_earfcn4g1.Text.Split(new char[] { ',' });
            buffer[3] = BitConverter.GetBytes((int)(buffer2.Length + 10))[0];
            byte[] array = new byte[(buffer.Length + buffer2.Length) + buffer3.Length];
            buffer.CopyTo(array, 0);
            buffer3.CopyTo(array, buffer.Length);
            buffer2.CopyTo(array, (int)(buffer.Length + buffer3.Length));
            this.TcpSend(array, ENDPOINT_4G_1);
        }

        public void TcpSend(byte[] data, string ipport)
        {
            lock (this)
            {
                foreach (TcpClient client in m_clientsHash.Values)
                {
                    if (client.Connected && (client.Client.RemoteEndPoint.ToString() == ipport))
                    {
                        client.Client.Send(data, 0, data.Length, SocketFlags.None);
                    }
                }
            }
        }

        public static byte[] StringToHexbyte(string str)
        {
            string[] strArray = str.Split(new char[] { ',' });
            byte[] buffer = new byte[strArray.Length * 2];
            if (str != "")
            {
                for (int i = 0; i < strArray.Length; i++)
                {
                    int num2 = Convert.ToInt32(strArray[i]);
                    byte[] bytes = BitConverter.GetBytes(num2);
                    int index = i * 2;
                    buffer[index] = bytes[1];
                    buffer[index + 1] = bytes[0];
                }
            }
            return buffer;
        }

        private void btn4G_Save2_Click(object sender, EventArgs e)
        {
            Settings.Default.LTE2_EARFCN = txt_earfcn4g2.Text;
            Settings.Default.LTE2_PLMN = txt_plmn4g2.Text;
            Settings.Default.Save();
            string[] strArray = txt_earfcn4g2.Text.Split(new char[] { ',' });
            if ((strArray[0] == "") && (txt_plmn4g2.Text == ""))
            {
                MessageBox.Show("LTE5 EARFCN SET EEROR !");
            }
            else
            {
                FDD_LteCellUpdate(txt_plmn4g2.Text, strArray[0], ENDPOINT_4G_2);
                Lte2EarfcnScan();
            }
        }

        public void FDD_LteCellUpdate(string plmn, string earfcn, string ipport)
        {
            byte[] buffer = new byte[] { 1, 0x11 };
            byte[] buffer2 = new byte[2];
            byte[] buffer3 = new byte[] { 0x22, 0, 4, 0, 0, 0, 3 };
            byte[] buffer4 = new byte[] { 0x17, 0, 4, 0, 70, 1, 0x1f };
            byte[] buffer5 = new byte[] { 14, 0, 2, 0x22, 0x47 };
            byte[] buffer6 = new byte[] { 8, 0, 2, 0, 100 };
            byte[] buffer7 = new byte[] { 0x1f, 0, 2, 70, 180 };
            byte[] buffer8 = new byte[] { 1, 0, 4, 0, 0, 0, 12 };
            string[] strArray = plmn.Split(new char[] { ',' });
            buffer3[6] = BitConverter.GetBytes(strArray.Length)[0];
            byte[] bytes = BitConverter.GetBytes(Convert.ToInt32(earfcn));
            buffer6[3] = bytes[1];
            buffer6[4] = bytes[0];
            byte[] buffer11 = BitConverter.GetBytes((int)(Convert.ToInt32(earfcn) + 0x4650));
            buffer7[3] = buffer11[1];
            buffer7[4] = buffer11[0];
            byte[] array = new byte[((((((buffer.Length + buffer2.Length) + buffer5.Length) + buffer6.Length) + buffer7.Length) + buffer8.Length) + buffer3.Length) + (buffer4.Length * strArray.Length)];
            byte[] buffer13 = BitConverter.GetBytes((int)(((((buffer5.Length + buffer6.Length) + buffer7.Length) + buffer8.Length) + buffer3.Length) + (buffer4.Length * strArray.Length)));
            buffer2[0] = buffer13[1];
            buffer2[1] = buffer13[0];
            buffer.CopyTo(array, 0);
            buffer2.CopyTo(array, buffer.Length);
            buffer5.CopyTo(array, (int)(buffer.Length + buffer2.Length));
            buffer6.CopyTo(array, (int)((buffer.Length + buffer2.Length) + buffer5.Length));
            buffer7.CopyTo(array, (int)(((buffer.Length + buffer2.Length) + buffer5.Length) + buffer6.Length));
            buffer8.CopyTo(array, (int)((((buffer.Length + buffer2.Length) + buffer5.Length) + buffer6.Length) + buffer7.Length));
            buffer3.CopyTo(array, (int)(((((buffer.Length + buffer2.Length) + buffer5.Length) + buffer6.Length) + buffer7.Length) + buffer8.Length));
            int index = 0;
            while (index < strArray.Length)
            {
                strArray[index] = strArray[index] + "f";
                string[] strArray2 = new string[3];
                int startIndex = 0;
                int num3 = 0;
                while (true)
                {
                    if (num3 >= 3)
                    {
                        buffer4.CopyTo(array, (int)(((((((buffer.Length + buffer2.Length) + buffer5.Length) + buffer6.Length) + buffer7.Length) + buffer8.Length) + buffer3.Length) + (index * buffer4.Length)));
                        index++;
                        break;
                    }
                    strArray2[num3] = strArray[index].Substring(startIndex, 2);
                    startIndex += 2;
                    buffer4[4 + num3] = Convert.ToByte(strArray2[num3], 0x10);
                    num3++;
                }
            }
            AppendLog($"{ipport}[{BitConverter.ToString(array, 0, array.Length)}]");
            this.TcpSend(array, ipport);
        }

        public void Lte2EarfcnScan()
        {
            byte[] buffer = new byte[] { 2, 0x33, 0, 0, 0x49, 0, 4, 0, 0, 0, 60, 0x48 };
            byte[] buffer2 = StringToHexbyte(txt_earfcn4g2.Text);
            byte[] buffer3 = new byte[2];
            buffer3[1] = BitConverter.GetBytes(buffer2.Length)[0];
            txt_earfcn4g2.Text.Split(new char[] { ',' });
            buffer[3] = BitConverter.GetBytes((int)(buffer2.Length + 10))[0];
            byte[] array = new byte[(buffer.Length + buffer2.Length) + buffer3.Length];
            buffer.CopyTo(array, 0);
            buffer3.CopyTo(array, buffer.Length);
            buffer2.CopyTo(array, (int)(buffer.Length + buffer3.Length));
            this.TcpSend(array, ENDPOINT_4G_2);
        }

        private void btn4G_Save3_Click(object sender, EventArgs e)
        {
            Settings.Default.LTE3_EARFCN = txt_earfcn4g3.Text;
            Settings.Default.LTE3_PLMN = txt_plmn4g3.Text;
            Settings.Default.Save();
            string[] strArray = txt_earfcn4g3.Text.Split(new char[] { ',' });
            if ((strArray[0] == "") && (txt_plmn4g3.Text == ""))
            {
                MessageBox.Show("LTE6 EARFCN SET EEROR !");
            }
            else
            {
                FDD_LteCellUpdate(txt_plmn4g3.Text, strArray[0], ENDPOINT_4G_3);
                Lte3EarfcnScan();
            }
        }

        public void Lte3EarfcnScan()
        {
            byte[] buffer = new byte[] { 2, 0x33, 0, 0, 0x49, 0, 4, 0, 0, 0, 60, 0x48 };
            byte[] buffer2 = StringToHexbyte(txt_earfcn4g3.Text);
            byte[] buffer3 = new byte[2];
            buffer3[1] = BitConverter.GetBytes(buffer2.Length)[0];
            txt_earfcn4g3.Text.Split(new char[] { ',' });
            buffer[3] = BitConverter.GetBytes((int)(buffer2.Length + 10))[0];
            byte[] array = new byte[(buffer.Length + buffer2.Length) + buffer3.Length];
            buffer.CopyTo(array, 0);
            buffer3.CopyTo(array, buffer.Length);
            buffer2.CopyTo(array, (int)(buffer.Length + buffer3.Length));
            this.TcpSend(array, ENDPOINT_4G_3);
        }

        private void btn4G_Save4_Click(object sender, EventArgs e)
        {
            Settings.Default.LTE4_EARFCN = txt_earfcn4g4.Text;
            Settings.Default.LTE4_PLMN = txt_plmn4g4.Text;
            Settings.Default.Save();
            string[] strArray = txt_earfcn4g4.Text.Split(new char[] { ',' });
            if ((strArray[0] == "") && (txt_plmn4g4.Text == ""))
            {
                MessageBox.Show("LTE7 EARFCN SET EEROR !");
            }
            else
            {
                this.FDD_LteCellUpdate(txt_plmn4g4.Text, strArray[0], ENDPOINT_4G_4);
                this.Lte4EarfcnScan();
            }
        }

        public void Lte4EarfcnScan()
        {
            byte[] buffer = new byte[] { 2, 0x33, 0, 0, 0x49, 0, 4, 0, 0, 0, 60, 0x48 };
            byte[] buffer2 = StringToHexbyte(txt_earfcn4g4.Text);
            byte[] buffer3 = new byte[2];
            buffer3[1] = BitConverter.GetBytes(buffer2.Length)[0];
            txt_earfcn4g4.Text.Split(new char[] { ',' });
            buffer[3] = BitConverter.GetBytes((int)(buffer2.Length + 10))[0];
            byte[] array = new byte[(buffer.Length + buffer2.Length) + buffer3.Length];
            buffer.CopyTo(array, 0);
            buffer3.CopyTo(array, buffer.Length);
            buffer2.CopyTo(array, (int)(buffer.Length + buffer3.Length));
            this.TcpSend(array, ENDPOINT_4G_4);
        }

        private void btnSMS_send_1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txt_arfcn1.Text))
            {
                tabControl.SelectedTab = tabPage_2G;
                txt_arfcn1.Focus();
                return;
            }
            if (string.IsNullOrEmpty(txt_arfcn3.Text))
            {
                tabControl.SelectedTab = tabPage_2G;
                txt_arfcn3.Focus();
                return;
            }
            if (string.IsNullOrEmpty(txtSMS_sender_1.Text))
            {
                txtSMS_sender_1.Focus();
                return;
            }
            if (string.IsNullOrEmpty(txtSMS_SMS_1.Text))
            {
                txtSMS_SMS_1.Focus();
                return;
            }

            Settings.Default.SENDER1 = txtSMS_sender_1.Text;
            Settings.Default.SMS1 = txtSMS_SMS_1.Text;
            Settings.Default.Save();
            m_rf_status = true;

            byte[] str = new byte[8] { 8, 0, 0, 0, 0, 7, 0, 0 };
            byte[] str2 = new byte[8] { 8, 0, 0, 0, 0, 7, 1, 0 };
            byte[] str3 = new byte[7] { 7, 0, 0, 0, 0, 5, 0 };
            byte[] array = new byte[8] { 0, 45, 0, 1, 20, 0, 96, 1 };
            byte[] array2 = new byte[8] { 0, 45, 1, 1, 20, 0, 96, 1 };
            byte[] bytes = Encoding.Default.GetBytes(txtSMS_sender_1.Text);
            byte[] array3 = new byte[2];
            byte[] array4 = array3;
            byte[] array5 = new byte[2] { 97, 1 };
            if (chkSMS_unicode1.Checked)
            {
                byte[] bytes2 = Encoding.Unicode.GetBytes(txtSMS_SMS_1.Text);
                byte[] array6 = new byte[bytes2.Length];
                for (int i = 0; i < bytes2.Length; i += 2)
                {
                    array6[i] = bytes2[i + 1];
                    array6[i + 1] = bytes2[i];
                }
                array4 = BitConverter.GetBytes(bytes2.Length + array5.Length + 2);
                byte[] bytes3 = BitConverter.GetBytes(4 + array.Length + 16 + 4 + bytes2.Length);
                byte[] array7 = new byte[4 + array.Length + 16 + 4 + bytes2.Length];
                byte[] array8 = array4.Take(2).ToArray();
                bytes3.CopyTo(array7, 0);
                array2.CopyTo(array7, bytes3.Length);
                bytes.CopyTo(array7, bytes3.Length + array.Length);
                array8.CopyTo(array7, bytes3.Length + array.Length + 16);
                array5.CopyTo(array7, bytes3.Length + array.Length + 16 + array8.Length);
                array6.CopyTo(array7, bytes3.Length + array.Length + 16 + array8.Length + array5.Length);
                UdpSendTo(array7, ENDPOINT_GSM1);
                UdpSendTo(str, ENDPOINT_GSM1);
                UdpSendTo(str2, ENDPOINT_GSM1);
                UdpSendTo(array7, ENDPOINT_GSM1);
                UdpSendTo(array7, ENDPOINT_GSM2);
                UdpSendTo(str, ENDPOINT_GSM2);
                UdpSendTo(str2, ENDPOINT_GSM2);
                UdpSendTo(array7, ENDPOINT_GSM2);
                UdpSendTo(str3, ENDPOINT_GSM1);
                UdpSendTo(str3, ENDPOINT_GSM2);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_1);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_2);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_3);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_4);
                Lte_OpenRF("43", ENDPOINT_4G_4);
                Lte_OpenRF("43", ENDPOINT_4G_3);
                Lte_OpenRF("43", ENDPOINT_4G_2);
                Lte_OpenRF("43", ENDPOINT_4G_1);
                m_rf_tag = 1;
            }
            else
            {
                byte[] bytes4 = Encoding.Default.GetBytes(txtSMS_SMS_1.Text);
                array4 = BitConverter.GetBytes(bytes4.Length + array5.Length + 2);
                byte[] bytes5 = BitConverter.GetBytes(4 + array.Length + 16 + 4 + bytes4.Length);
                byte[] array9 = new byte[4 + array.Length + 16 + 4 + bytes4.Length];
                byte[] array10 = array4.Take(2).ToArray();
                bytes5.CopyTo(array9, 0);
                array.CopyTo(array9, bytes5.Length);
                bytes.CopyTo(array9, bytes5.Length + array.Length);
                array10.CopyTo(array9, bytes5.Length + array.Length + 16);
                array5.CopyTo(array9, bytes5.Length + array.Length + 16 + array10.Length);
                bytes4.CopyTo(array9, bytes5.Length + array.Length + 16 + array10.Length + array5.Length);
                UdpSendTo(array9, ENDPOINT_GSM1);
                UdpSendTo(str, ENDPOINT_GSM1);
                UdpSendTo(str2, ENDPOINT_GSM1);
                UdpSendTo(array9, ENDPOINT_GSM1);
                UdpSendTo(array9, ENDPOINT_GSM2);
                UdpSendTo(str, ENDPOINT_GSM2);
                UdpSendTo(str2, ENDPOINT_GSM2);
                UdpSendTo(array9, ENDPOINT_GSM2);
                UdpSendTo(str3, ENDPOINT_GSM1);
                UdpSendTo(str3, ENDPOINT_GSM2);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_1);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_2);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_3);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_4);
                Lte_OpenRF("43", ENDPOINT_4G_4);
                Lte_OpenRF("43", ENDPOINT_4G_3);
                Lte_OpenRF("43", ENDPOINT_4G_2);
                Lte_OpenRF("43", ENDPOINT_4G_1);
                m_rf_tag = 2;
            }
        }

        public void UE_REDIREC(string freq1, string freq2, string ipport)
        {
            byte[] buffer = new byte[] {
                1, 0x1b, 0, 0x22, 1, 0, 4, 0, 0, 0, 0x1b, 0x1d, 0, 1, 1, 0x19,
                0, 1, 1, 0x15, 0, 1, 0, 7, 0, 1, 4, 0x18, 0, 8
            };
            byte[] bytes = BitConverter.GetBytes(Convert.ToInt32(freq1));
            byte[] buffer3 = BitConverter.GetBytes((int)(Convert.ToInt32(freq1) + 5));
            byte[] buffer4 = BitConverter.GetBytes(Convert.ToInt32(freq2));
            byte[] buffer5 = BitConverter.GetBytes((int)(Convert.ToInt32(freq2) + 5));
            byte[] buffer6 = new byte[2];
            byte[] buffer7 = new byte[2];
            byte[] buffer8 = new byte[2];
            byte[] buffer9 = new byte[] { buffer5[1], buffer5[0] };
            buffer6[0] = bytes[1];
            buffer6[1] = bytes[0];
            buffer7[0] = buffer3[1];
            buffer7[1] = buffer3[0];
            buffer8[0] = buffer4[1];
            buffer8[1] = buffer4[0];
            byte[] array = new byte[buffer.Length + 8];
            buffer.CopyTo(array, 0);
            buffer6.CopyTo(array, buffer.Length);
            buffer7.CopyTo(array, (int)(buffer.Length + buffer6.Length));
            buffer8.CopyTo(array, (int)((buffer.Length + buffer6.Length) + buffer7.Length));
            buffer9.CopyTo(array, (int)(((buffer.Length + buffer6.Length) + buffer7.Length) + buffer8.Length));
            this.TcpSend(array, ipport);
        }


        private void btnSMS_send_2_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txt_arfcn1.Text))
            {
                tabControl.SelectedTab = tabPage_2G;
                txt_arfcn1.Focus();
                return;
            }
            if (string.IsNullOrEmpty(txt_arfcn3.Text))
            {
                tabControl.SelectedTab = tabPage_2G;
                txt_arfcn3.Focus();
                return;
            }
            if (string.IsNullOrEmpty(txtSMS_sender_2.Text))
            {
                txtSMS_sender_2.Focus();
                return;
            }
            if (string.IsNullOrEmpty(txtSMS_SMS_2.Text))
            {
                txtSMS_SMS_2.Focus();
                return;
            }

            Settings.Default.SENDER2 = txtSMS_sender_2.Text;
            Settings.Default.SMS2 = this.txtSMS_SMS_2.Text;
            Settings.Default.Save();
            m_rf_status = true;
            byte[] str = new byte[8] { 8, 0, 0, 0, 0, 7, 0, 0 };
            byte[] str2 = new byte[8] { 8, 0, 0, 0, 0, 7, 1, 0 };
            byte[] str3 = new byte[7] { 7, 0, 0, 0, 0, 5, 0 };
            byte[] array = new byte[8] { 0, 45, 0, 1, 20, 0, 96, 1 };
            byte[] array2 = new byte[8] { 0, 45, 1, 1, 20, 0, 96, 1 };
            byte[] bytes = Encoding.Default.GetBytes(txtSMS_sender_2.Text);
            byte[] array3 = new byte[2];
            byte[] array4 = array3;
            byte[] array5 = new byte[2] { 97, 1 };
            if (chkSMS_unicode2.Checked)
            {
                byte[] bytes2 = Encoding.Unicode.GetBytes(txtSMS_SMS_2.Text);
                byte[] array6 = new byte[bytes2.Length];
                for (int i = 0; i < bytes2.Length; i += 2)
                {
                    array6[i] = bytes2[i + 1];
                    array6[i + 1] = bytes2[i];
                }
                array4 = BitConverter.GetBytes(bytes2.Length + array5.Length + 2);
                byte[] bytes3 = BitConverter.GetBytes(4 + array.Length + 16 + 4 + bytes2.Length);
                byte[] array7 = new byte[4 + array.Length + 16 + 4 + bytes2.Length];
                byte[] array8 = array4.Take(2).ToArray();
                bytes3.CopyTo(array7, 0);
                array2.CopyTo(array7, bytes3.Length);
                bytes.CopyTo(array7, bytes3.Length + array.Length);
                array8.CopyTo(array7, bytes3.Length + array.Length + 16);
                array5.CopyTo(array7, bytes3.Length + array.Length + 16 + array8.Length);
                array6.CopyTo(array7, bytes3.Length + array.Length + 16 + array8.Length + array5.Length);
                UdpSendTo(array7, ENDPOINT_GSM1);
                UdpSendTo(str, ENDPOINT_GSM1);
                UdpSendTo(str2, ENDPOINT_GSM1);
                UdpSendTo(array7, ENDPOINT_GSM1);
                UdpSendTo(array7, ENDPOINT_GSM2);
                UdpSendTo(str, ENDPOINT_GSM2);
                UdpSendTo(str2, ENDPOINT_GSM2);
                UdpSendTo(array7, ENDPOINT_GSM2);
                UdpSendTo(str3, ENDPOINT_GSM1);
                UdpSendTo(str3, ENDPOINT_GSM2);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_1);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_2);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_3);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_4);
                Lte_OpenRF("43", ENDPOINT_4G_4);
                Lte_OpenRF("43", ENDPOINT_4G_3);
                Lte_OpenRF("43", ENDPOINT_4G_2);
                Lte_OpenRF("43", ENDPOINT_4G_1);
                m_rf_tag = 1;
            }
            else
            {
                byte[] bytes4 = Encoding.Default.GetBytes(txtSMS_SMS_2.Text);
                array4 = BitConverter.GetBytes(bytes4.Length + array5.Length + 2);
                byte[] bytes5 = BitConverter.GetBytes(4 + array.Length + 16 + 4 + bytes4.Length);
                byte[] array9 = new byte[4 + array.Length + 16 + 4 + bytes4.Length];
                byte[] array10 = array4.Take(2).ToArray();
                bytes5.CopyTo(array9, 0);
                array.CopyTo(array9, bytes5.Length);
                bytes.CopyTo(array9, bytes5.Length + array.Length);
                array10.CopyTo(array9, bytes5.Length + array.Length + 16);
                array5.CopyTo(array9, bytes5.Length + array.Length + 16 + array10.Length);
                bytes4.CopyTo(array9, bytes5.Length + array.Length + 16 + array10.Length + array5.Length);
                UdpSendTo(array9, ENDPOINT_GSM1);
                UdpSendTo(str, ENDPOINT_GSM1);
                UdpSendTo(str2, ENDPOINT_GSM1);
                UdpSendTo(array9, ENDPOINT_GSM1);
                UdpSendTo(array9, ENDPOINT_GSM2);
                UdpSendTo(str, ENDPOINT_GSM2);
                UdpSendTo(str2, ENDPOINT_GSM2);
                UdpSendTo(array9, ENDPOINT_GSM2);
                UdpSendTo(str3, ENDPOINT_GSM1);
                UdpSendTo(str3, ENDPOINT_GSM2);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_1);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_2);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_3);
                UE_REDIREC(txt_arfcn1.Text, txt_arfcn3.Text, ENDPOINT_4G_4);
                Lte_OpenRF("43", ENDPOINT_4G_4);
                Lte_OpenRF("43", ENDPOINT_4G_3);
                Lte_OpenRF("43", ENDPOINT_4G_2);
                Lte_OpenRF("43", ENDPOINT_4G_1);
                m_rf_tag = 2;
            }
        }

        private void btnSMS_off1_Click(object sender, EventArgs e)
        {
            byte[] str = new byte[7] { 7, 0, 0, 0, 0, 6, 0 };
            byte[] str2 = new byte[7] { 7, 0, 0, 0, 1, 6, 0 };

            UdpSendTo(str, ENDPOINT_GSM1);
            UdpSendTo(str2, ENDPOINT_GSM2);
            UdpSendTo(str2, ENDPOINT_GSM1);
            UdpSendTo(str, ENDPOINT_GSM2);
            Lte_OpenRF("127", ENDPOINT_4G_4);
            Lte_OpenRF("127", ENDPOINT_4G_3);
            Lte_OpenRF("127", ENDPOINT_4G_2);
            Lte_OpenRF("127", ENDPOINT_4G_1);

            m_rf_tag = 0;
        }

        private void btnSMS_off2_Click(object sender, EventArgs e)
        {
            byte[] str = new byte[7] { 7, 0, 0, 0, 0, 6, 0 };
            byte[] str2 = new byte[7] { 7, 0, 0, 0, 1, 6, 0 };

            UdpSendTo(str, ENDPOINT_GSM1);
            UdpSendTo(str2, ENDPOINT_GSM2);
            UdpSendTo(str2, ENDPOINT_GSM1);
            UdpSendTo(str, ENDPOINT_GSM2);
            Lte_OpenRF("127", ENDPOINT_4G_4);
            Lte_OpenRF("127", ENDPOINT_4G_3);
            Lte_OpenRF("127", ENDPOINT_4G_2);
            Lte_OpenRF("127", ENDPOINT_4G_1);
            m_rf_tag = 0;
        }

        public void Sweep2GSend(byte[] data)
        {
            lock (this)
            {
                if (!m_clientsHash.Contains(ENDPOINT_2G_SWEEP_MODULAR))
                    return;

                TcpClient client = (TcpClient)m_clientsHash[ENDPOINT_2G_SWEEP_MODULAR];
                if (client.Connected)
                {
                    client.Client.Send(data, 0, data.Length, SocketFlags.None);
                }
            }
        }

        public void SweepSend(byte[] data)
        {
            lock (this)
            {
                if (!m_clientsHash.Contains(ENDPOINT_4G_SWEEP_MODULAR))
                    return;

                TcpClient client = (TcpClient)m_clientsHash[ENDPOINT_4G_SWEEP_MODULAR];
                if (client.Connected)
                    client.Client.Send(data, 0, data.Length, SocketFlags.None);
            }
        }

        private void btnSweep_search4g_Click(object sender, EventArgs e)
        {
            this.btnSweep_config4g.Enabled = false;

            new Thread(() =>
            {
                byte[] bytes = Encoding.Default.GetBytes("AT+BNDLOCK=5,0\r\n");
                byte[] data = Encoding.Default.GetBytes("AT+LTEMODELOCK=1\r\n");
                byte[] buffer3 = Encoding.Default.GetBytes("AT+LTEMODELOCK=2\r\n");
                byte[] buffer4 = Encoding.Default.GetBytes("AT+PCISCAN=3\r\n");
                byte[] buffer5 = Encoding.Default.GetBytes("AT+PCISCAN=1\r\n");
                byte[] buffer6 = Encoding.Default.GetBytes("AT+PCISCAN=5\r\n");
                byte[] buffer7 = Encoding.Default.GetBytes("AT+PCISCAN=8\r\n");
                byte[] buffer8 = Encoding.Default.GetBytes("AT+PCISCAN=39\r\n");
                byte[] buffer9 = Encoding.Default.GetBytes("AT+PCISCAN=40\r\n");
                byte[] buffer10 = Encoding.Default.GetBytes("AT+PCISCAN=41\r\n");

                InvokeUI(() =>
                {
                    this.gridSweep_1.Rows.Clear();
                });

                this.SweepSend(bytes);
                Thread.Sleep(2000);
                this.SweepSend(data);

                AppendLog("SWEEP FDD");
                Thread.Sleep(8000);
                this.SweepSend(buffer4);
                Thread.Sleep(3000);
                this.SweepSend(buffer5);
                Thread.Sleep(3000);
                this.SweepSend(buffer6);
                Thread.Sleep(3000);
                this.SweepSend(buffer7);
                Thread.Sleep(3000);
                this.SweepSend(buffer3);

                AppendLog("SWEEP TDD");
                Thread.Sleep(8000);
                this.SweepSend(buffer9);
                Thread.Sleep(3000);
                this.SweepSend(buffer10);
                Thread.Sleep(3000);
                this.SweepSend(buffer8);
                this.LockFreqGetSib();
                Thread.Sleep(5000);

                InvokeUI(() =>
                {
                    MessageBox.Show("SWEEP Finish!");
                    this.btnSweep_config4g.Enabled = true;
                });

            }).Start();

        }

        public void LockFreqGetSib()
        {
            for (int i = 0; i < (this.m_pci_num - 1); i++)
            {
                if (this.gridSweep_1.Rows[i].Cells[0].Value != null)
                {
                    byte[] bytes = Encoding.Default.GetBytes("AT+LTEMODELOCK=1\r\n");
                    byte[] data = Encoding.Default.GetBytes("AT+LTEMODELOCK=2\r\n");
                    Encoding.Default.GetBytes("AT+SGCELLINFOEX\r\n");
                    byte[] buffer3 = Encoding.Default.GetBytes("AT+GETSIB=5\r\n");
                    object[] objArray = new object[] { "AT+EARFCNLOCK=", this.gridSweep_1.Rows[i].Cells[0].Value, ",", this.gridSweep_1.Rows[i].Cells[0].Value, "\r\n" };
                    byte[] buffer4 = Encoding.Default.GetBytes(string.Concat(objArray));
                    if (int.Parse(this.gridSweep_1.Rows[i].Cells[0].Value.ToString()) < 0x8d03)
                    {
                        this.SweepSend(bytes);
                        Thread.Sleep(2000);
                        this.SweepSend(buffer4);
                        Thread.Sleep(5000);
                        this.SweepSend(buffer3);
                    }
                    else
                    {
                        this.SweepSend(data);
                        Thread.Sleep(2000);
                        this.SweepSend(buffer4);
                        Thread.Sleep(5000);
                        this.SweepSend(buffer3);
                    }
                }
            }
        }

        private void btnSweep_config4g_Click(object sender, EventArgs e)
        {
            int rowCount = this.gridSweep_1.RowCount;

            for (int i = 0; i < (rowCount - 1); i++)
            {
                string str = "";
                int num3 = Convert.ToInt32(this.gridSweep_1.Rows[i].Cells[0].Value);
                if (this.gridSweep_1.Rows[i].Cells[1].Value != null)
                {
                    str = Utils.ToString(this.gridSweep_1.Rows[i].Cells[1].Value) + Utils.ToString(this.gridSweep_1.Rows[i].Cells[2].Value);
                    if (str.Length > 0 && !this.txt_plmn4g1.Text.Contains(str))
                    {
                        this.txt_plmn4g1.AppendText(str);
                        this.txt_plmn4g1.AppendText(",");
                        this.txt_plmn4g2.AppendText(str);
                        this.txt_plmn4g2.AppendText(",");
                        this.txt_plmn4g3.AppendText(str);
                        this.txt_plmn4g3.AppendText(",");
                    }
                }
                if (((num3 > 0) && (num3 < 0x257)) || ((num3 > 0xabe) && (num3 < 0xd79)))
                {
                    string temp = Utils.ToString(this.gridSweep_1.Rows[i].Cells[0].Value);
                    if (temp.Length > 0 && !this.txt_earfcn4g2.Text.Contains(temp))
                    {
                        this.txt_earfcn4g2.AppendText(Utils.ToString(this.gridSweep_1.Rows[i].Cells[0].Value));
                        this.txt_earfcn4g2.AppendText(",");
                    }
                }
                else if ((num3 > 0x4b0) && (num3 < 0x79d))
                {
                    string temp = Utils.ToString(this.gridSweep_1.Rows[i].Cells[0].Value);
                    if (temp.Length > 0 && !this.txt_earfcn4g3.Text.Contains(temp))
                    {
                        this.txt_earfcn4g3.AppendText(Utils.ToString(this.gridSweep_1.Rows[i].Cells[0].Value));
                        this.txt_earfcn4g3.AppendText(",");
                    }
                }
                else if ((((num3 > 0x23fa) && (num3 < 0x25bb)) || ((num3 > 0xd7a) && (num3 < 0xed7))) && !this.txt_earfcn4g1.Text.Contains(Utils.ToString(this.gridSweep_1.Rows[i].Cells[0].Value)))
                {
                    this.txt_earfcn4g1.AppendText(Utils.ToString(this.gridSweep_1.Rows[i].Cells[0].Value));
                    this.txt_earfcn4g1.AppendText(",");
                }
            }
        }

        private void btnSweep_search2g_Click(object sender, EventArgs e)
        {
            this.config2g.Enabled = false;

            new Thread(() =>
            {
                byte[] bytes1 = Encoding.Default.GetBytes("AT+CBAND=DCS_MODE\r\n");
                byte[] bytes2 = Encoding.Default.GetBytes("AT+CNETSCAN\r\n");

                InvokeUI(() =>
                {
                    this.gridSweep_1.Rows.Clear();
                });

                this.Sweep2GSend(bytes1);
                Thread.Sleep(2 * 1000);
                this.Sweep2GSend(bytes2);

                InvokeUI(() =>
                {
                    AppendLog("Wait for 20 seconds.............");
                });

                Thread.Sleep(20 * 1000);

                InvokeUI(() =>
                {
                    MessageBox.Show("SWEEP Finish!");
                    this.config2g.Enabled = true;
                });
            }).Start();
        }

        private void config2g_Click(object sender, EventArgs e)
        {
            if (this.gridSweep_2.RowCount == 0)
                return;

            int rowCount = this.gridSweep_2.RowCount;
            this.txt_mcc_1.Text = Utils.ToString(this.gridSweep_2.Rows[0].Cells[1].Value);
            this.txt_mnc_1.Text = Utils.ToString(this.gridSweep_2.Rows[0].Cells[2].Value);
            this.txt_arfcn1.Text = Utils.ToString(this.gridSweep_2.Rows[0].Cells[0].Value);

            for (int i = 1; i < rowCount; i++)
            {
                string currentMnc = Utils.ToString(this.gridSweep_2.Rows[i].Cells[2].Value);
                if (this.txt_mnc_1.Text.Equals(currentMnc))
                    continue;

                this.txt_mcc_2.Text = Utils.ToString(this.gridSweep_2.Rows[i].Cells[1].Value);
                this.txt_mnc_2.Text = currentMnc;

                for (int j = 1; j < rowCount; j++)
                {
                    string mnc2 = Utils.ToString(this.gridSweep_2.Rows[j].Cells[2].Value);
                    if (this.txt_mnc_1.Text.Equals(mnc2) || this.txt_mnc_2.Text.Equals(mnc2))
                        continue;

                    this.txt_mcc_3.Text = Utils.ToString(this.gridSweep_2.Rows[j].Cells[1].Value);
                    this.txt_mnc_3.Text = mnc2;
                    this.txt_arfcn3.Text = Utils.ToString(this.gridSweep_2.Rows[j].Cells[0].Value);

                    for (int k = 1; k < rowCount; k++)
                    {
                        string mnc3 = Utils.ToString(this.gridSweep_2.Rows[k].Cells[2].Value);
                        if (this.txt_mnc_1.Text.Equals(mnc3) ||
                            this.txt_mnc_2.Text.Equals(mnc3) ||
                            this.txt_mnc_3.Text.Equals(mnc3))
                            continue;

                        this.txt_mcc_4.Text = Utils.ToString(this.gridSweep_2.Rows[k].Cells[1].Value);
                        this.txt_mnc_4.Text = mnc3;

                        MessageBox.Show("2G Configuration complete");
                        return;
                    }
                }
            }
        }
    }
}
