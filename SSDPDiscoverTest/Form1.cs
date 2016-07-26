using System;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SSDPDiscoverTest
{
    public partial class Form1 : Form
    {
        public class SSDP
        {
            public string ip;
            public string man;
            public string st;
            public int port;
            public int mx;
        }
        
        bool isListening;
        Thread listenerThread;
        SSDP ssdp;
        string search;
        byte[] sbytes;

        int sendcnt = 0;
        int rcvcnt = 0;

        public Form1()
        {
            InitializeComponent();

            ssdp = new SSDP();
            ssdp.ip = "239.255.255.250";
            ssdp.port = 1900;
            ssdp.man = "ssdp:discover";
            ssdp.mx = 1;
            ssdp.st = "urn:schemas-sony-com:service:ScalarWebAPI:1";

            StringBuilder sb = new StringBuilder();
            sb.Append("M-SEARCH * HTTP/1.1\r\n");
            sb.Append(String.Format("HOST: {0}:{1}\r\n", ssdp.ip, ssdp.port));
            sb.Append(String.Format("MAN: \"{0}\"\r\n", ssdp.man));
            sb.Append(String.Format("MX: {0}\r\n", ssdp.mx.ToString()));
            sb.Append(String.Format("ST: {0}\r\n\r\n\r\n", ssdp.st));
            search = sb.ToString();
            sbytes = Encoding.UTF8.GetBytes(search);
        }

        private void StartListener(object obj)
        {
            while (isListening)
            {
                Socket socket = (Socket)obj;
                //using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    //System.Diagnostics.Debug.WriteLine(socket.Available);

                    if (socket.Available > 0)
                    {
                        byte[] buffer = new byte[8192];
                        EndPoint ep = new IPEndPoint(IPAddress.Any, ssdp.port);
                        int len = socket.ReceiveFrom(buffer, ref ep);
                        string str = Encoding.UTF8.GetString(buffer, 0, len);

                        System.Diagnostics.Debug.WriteLine(str);
                        UpdateControl(textBox1, str);
                        rcvcnt++;
                        if (rcvcnt > 3)
                        {
                            rcvcnt = 0;
                            ClearControl(textBox1);
                        }
                    }
                }
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!isListening) 
            {
                Thread thread = new Thread(Discover);
                thread.Start();
            }
        }

        private void Discover()
        {   
            // send discover
            IPAddress multicastAddress = IPAddress.Parse(ssdp.ip);
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multicastAddress, IPAddress.Any));
                socket.SendTo(sbytes, 0, sbytes.Length, SocketFlags.None, new IPEndPoint(multicastAddress, ssdp.port));

                System.Diagnostics.Debug.WriteLine(search);
                UpdateControl(textBox2, search);

                isListening = true;
                listenerThread = new Thread(StartListener);
                listenerThread.Start(socket);

                int time = 0;
                while (isListening)
                {
                    Thread.Sleep(10);
                    time += 10;
                    if (time > 5000) isListening = false;
                }
                socket.Close();
                sendcnt++;
                if (sendcnt > 3)
                {
                    sendcnt = 0;
                    ClearControl(textBox2);
                } 
            }
        }

        private delegate void UpdateControlDelegate(Control control, string text);
        private void UpdateControl(Control control, string text)
        {
            if (!control.InvokeRequired) control.Text += text;
            else control.Invoke(new UpdateControlDelegate(UpdateControl), new object[] { control, text });
        }

        private delegate void ClearControlDelegate(Control control);
        private void ClearControl(Control control)
        {
            if (!control.InvokeRequired) control.Text = "";
            else control.Invoke(new ClearControlDelegate(ClearControl), new object[] { control });
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            isListening = false;    // ends listener
        }
    }
}
