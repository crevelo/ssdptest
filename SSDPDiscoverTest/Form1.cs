using System;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using NativeWifi;

namespace SSDPDiscoverTest
{
    public partial class Form1 : Form
    {
        public class FoundNetwork
        {
            public string profileName;
            public WlanClient.WlanInterface wLanInterface;
            private Wlan.WlanAvailableNetwork _network;
            public Wlan.WlanAvailableNetwork network
            {
                get { return _network; }
                set
                {
                    _network = value;
                    profileName = Encoding.ASCII.GetString(_network.dot11Ssid.SSID).Trim(new char[] { '\0' });
                }
            }
        }

        public class SSDP
        {
            public string ip;
            public string man;
            public string st;
            public int port;
            public int mx;
        }

        bool isFindingWifi;
        bool isListening;
        Thread listenerThread;
        SSDP ssdp;
        string search;
        string selectedProfilePassword;
        byte[] sbytes;
        List<FoundNetwork> availableConnections;
        List<Wlan.WlanProfileInfo> profiles;
        WlanClient wlan;

        int sendcnt = 0;
        int rcvcnt = 0;

        public Form1()
        {
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

            isFindingWifi = false;
            availableConnections = new List<FoundNetwork>();
            profiles = new List<Wlan.WlanProfileInfo>();
            wlan = new WlanClient();

            InitializeComponent();
        }

        private void StartListener(object obj)
        {
            while (isListening)
            {
                try
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error at Form1.cs/StartListener: " + ex.Message);
                    isListening = false;
                }
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            //if (!isListening) 
            //{
            //    Thread discoverThread = new Thread(Discover);
            //    discoverThread.Start();
            //}
            if (!isFindingWifi)
            {
                Thread findWifiThread = new Thread(FindWifi);
                findWifiThread.Start();
            }
        }

        private void FindWifi()
        {
            isFindingWifi = true;
            try
            {
                availableConnections.Clear();
                profiles.Clear();
                foreach (WlanClient.WlanInterface wlanInterface in wlan.Interfaces)
                {
                    Wlan.WlanAvailableNetwork[] networks = wlanInterface.GetAvailableNetworkList(Wlan.WlanGetAvailableNetworkFlags.IncludeAllAdhocProfiles);

                    foreach (Wlan.WlanAvailableNetwork network in networks)
                    {
                        FoundNetwork conn = new FoundNetwork();
                        conn.wLanInterface = wlanInterface;
                        conn.network = network;
                        if (conn.profileName.IndexOf("DIRECT") > -1) availableConnections.Add(conn);
                    }
                    foreach (Wlan.WlanProfileInfo profile in wlanInterface.GetProfiles())
                    {
                        profiles.Add(profile);
                    }
                    //Wlan.WlanBssEntry[] wlanBssEntries = wlanInterface.GetNetworkBssList();
                    //foreach (Wlan.WlanBssEntry bssEntry in wlanBssEntries)
                    //{
                    //    int rss = bssEntry.rssi;
                    //    byte[] macAddr = bssEntry.dot11Bssid;
                    //    string tMac = "";
                    //    for (int i = 0; i < bssEntry.dot11Bssid.Length; i++)
                    //    {
                    //        tMac += macAddr[i].ToString("X2").PadLeft(2, '0').ToUpper();
                    //    }
                    //    StringBuilder sb = new StringBuilder();
                    //    sb.Append(String.Format("SSID: {0}.\r\r", System.Text.ASCIIEncoding.ASCII.GetString(bssEntry.dot11Ssid.SSID).ToString()));
                    //    sb.Append(String.Format("Signal: {0}%.\r\n", bssEntry.linkQuality));
                    //    sb.Append(String.Format("BSS Type: {0}.\r\n", bssEntry.dot11BssType));
                    //    sb.Append(String.Format("MAC: {0}\r\n.", tMac));
                    //    sb.Append(String.Format("RSSID:{0}\r\n\r\n", rss.ToString()));
                    //}
                }
                if (availableConnections.Count > 0) UpdateComboBox(comboBox1, availableConnections.ToArray());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error at SSDPDiscoverTest.cs/Form1/FindWifi: " + ex.Message);
            }
            isFindingWifi = false;
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

        private delegate void UpdateComboBoxDelegate(ComboBox cb, FoundNetwork[] items);
        private void UpdateComboBox(ComboBox cb, FoundNetwork[] items)
        {
            if (!cb.InvokeRequired)
            {
                cb.SuspendLayout();
                bool isFound = false;
                cb.Items.Clear();
                for (int i = 0; i < items.Length; i++)
                {
                    isFound = false;
                    for (int j = 0; j < cb.Items.Count; j++)
                    {
                        if (items[i].profileName == cb.Items[j].ToString())
                        {
                            isFound = true;
                            break;
                        }
                    }
                    if (!isFound) cb.Items.Add(items[i].profileName);
                }
                cb.ResumeLayout();
                if (cb.Items.Count > 0 && cb.SelectedIndex < 0 && cb.Items.Count > 0) cb.SelectedIndex = 0;
            }
            else cb.Invoke(new UpdateComboBoxDelegate(UpdateComboBox), new object[] { cb, items });
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

        private void button1_Click(object sender, EventArgs e)
        {
            // connect to wifi
            FoundNetwork conn = availableConnections[comboBox1.SelectedIndex];
            string profileName = conn.profileName;
            int profileIdx = -1;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i].profileName == profileName)
                {
                    profileIdx = i;
                    break;
                }
            }

            if (profileIdx > -1)    // profiles is found
            {
                string profileXml = conn.wLanInterface.GetProfileXml(profiles[profileIdx].profileName);
                conn.wLanInterface.SetProfile(Wlan.WlanProfileFlags.AllUser, profileXml, true);
                conn.wLanInterface.Connect(Wlan.WlanConnectionMode.Profile, Wlan.Dot11BssType.Any, conn.network.profileName);
            }
            else
            {
                FPasswordPrompt prompt = new FPasswordPrompt();
                if (prompt.ShowDialog() == DialogResult.OK)
                {
                    string mac = "";
                    Wlan.WlanBssEntry[] bssList = conn.wLanInterface.GetNetworkBssList();
                    foreach (Wlan.WlanBssEntry bss in bssList)
                    {
                        string ssid = Encoding.ASCII.GetString(bss.dot11Ssid.SSID);
                        if (ssid == conn.network.profileName)
                        {
                            for (int i = 0; i < bss.dot11Bssid.Length; i++)
                            {
                                mac += bss.dot11Bssid[i].ToString("X2").PadLeft(2, '0').ToUpper();
                            }
                            break;
                        }
                    }
                    
                    string key = selectedProfilePassword;
                    string profileXml = string.Format("<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>{0}</name><SSIDConfig><SSID><hex>{1}</hex><name>{0}</name></SSID></SSIDConfig><connectionType>ESS</connectionType><MSM><security><authEncryption><authentication>open</authentication><encryption>WEP</encryption><useOneX>false</useOneX></authEncryption><sharedKey><keyType>networkKey</keyType><protected>false</protected><keyMaterial>{2}</keyMaterial></sharedKey><keyIndex>0</keyIndex></security></MSM></WLANProfile>", profileName, mac, key);
                    conn.wLanInterface.SetProfile(Wlan.WlanProfileFlags.AllUser, profileName, true);
                    conn.wLanInterface.Connect(Wlan.WlanConnectionMode.Auto, Wlan.Dot11BssType.Any, profileName);
                }
            }
        }

        private void PasswordPromptEnd(object sender, FPasswordPrompt.PasswordPromptEventArgs e)
        {
            selectedProfilePassword = e.Password;
        }
    }
}
