using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using rractiveLib;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;
using System.Collections.ObjectModel;

namespace InOutDemo
{
    /// <summary>
    /// MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Interface to race result USB Timing Box
        RRActiveConnector rrActiveUsb;
        // We will poll for new passings using a background worker
        BackgroundWorker bw;

        // Observable collections which XAML binds to
        public ObservableCollection<String> Transponders { get; private set; }

        // List of possible serial ports to which a USB Timing Box is connected
        public List<String> ComPorts { get; private set; }
        // True, iff additional loop boxes are used to detect whether 
        public Boolean MultiLoop { get; set; }
              
        public MainWindow()
        {
            //List available USB com port adapters
            ComPorts = Tools.ListComPorts(403, 6001);

            // Create required objects
            rrActiveUsb = new RRActiveConnector();
            MultiLoop = false;
            
            bw = new BackgroundWorker();
            bw.WorkerSupportsCancellation = true;
            bw.DoWork += bw_DoWork;
            bw.RunWorkerCompleted += bw_RunWorkerCompleted;

            Transponders = new ObservableCollection<String>();

            InitializeComponent();

            cbxComPort.SelectedIndex = 0;
        }

        // Background worker polls for passing until cancelled
        void bw_DoWork(object sender, DoWorkEventArgs e)
        {            
            while (!bw.CancellationPending)
            {
                // New passings available? 
                while (rrActiveUsb.UnreadPassings > 0)
                {
                    // Get passing
                    var passing = rrActiveUsb.GetNextPassing(); 
                    Trace.WriteLine(string.Format("New Passing: Transponder {0}@{1} - time: {2}", passing.TransponderCode, passing.LoopID, passing.TimeStamp), Tools.TRACE_CATEGORY_INFO);

                    // Add to collection
                   this.Dispatcher.InvokeAsync(new Action(() => { Transponders.Add(passing.TransponderCode + "@" + passing.TimeStamp); }));
                }
                Thread.Sleep(100);
            }
        }

        // After disconnecting, the user can connect again
        void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            rrActiveUsb.Disconnect();
            btnConnect.Content = "Connect";
            lblDeviceInfo.Content = "";
            cbxComPort.IsEnabled = true;
            btnReset.IsEnabled = false;
        }

        /// <summary>
        /// Given a COM port, connect to USB Timing Box
        /// </summary>
        /// <param name="comPort">COM port to connect to</param>
        /// <returns>true, iff the connection attempt succeeds</returns>
        private bool Connect(string comPort)
        {
            byte port = byte.Parse(comPort.Substring(3));
            var res = rrActiveUsb.Connect(port);

            if (res)
            {
                Trace.WriteLine(string.Format("Successfully connected to race|result USB Timing Box @COM{0}.", port), Tools.TRACE_CATEGORY_INFO);
                Trace.WriteLine(string.Format("ID: {0}, HW: {1}, FW: {2}", rrActiveUsb.DecoderID, (float)rrActiveUsb.DecoderHardwareVersion / 10, (float)rrActiveUsb.DecoderFirmwareVersion / 10), Tools.TRACE_CATEGORY_INFO);                
                rrActiveUsb.ChannelID = 1;              

                return true;
            }
            else
            {
                Trace.WriteLine(string.Format("Could not connect to race|result USB Timing Box @COM{0}.", port), Tools.TRACE_CATEGORY_ERROR);
                return false;
            }
        }

        // if already running, stop - else start
        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (bw.IsBusy)
            {
                bw.CancelAsync();
            }
            else
            {
                if (Connect(cbxComPort.SelectedItem.ToString()))
                {
                    bw.RunWorkerAsync();
                    btnConnect.Content = "Disconnect";
                    lblDeviceInfo.Content = string.Format("Connected to ID: {0}, HW: {1}, FW: {2}@Channel {3}", rrActiveUsb.DecoderID, (float)rrActiveUsb.DecoderHardwareVersion / 10, (float)rrActiveUsb.DecoderFirmwareVersion / 10, rrActiveUsb.ChannelID);
                    cbxComPort.IsEnabled = false;
                    btnReset.IsEnabled = true;
                }
            }
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            rrActiveUsb.ResetPassings();
        }
    }
}
