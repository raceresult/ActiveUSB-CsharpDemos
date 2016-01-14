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
    /// Simple enum that holds whether a transponder is in our out of the danger zone
    /// </summary>
    public enum DangerZone { In, Out };

    /// <summary>
    /// Class that holds the actual RRACtivePassing data along with some additional data like a caption. Plublishs some properties for XAML usage. 
    /// Also implements IComparable for simple adding to and removing from collecitons.
    /// </summary>
    public class Detection : IComparable
    {
        private RRActivePassing passing;
        private DateTime time;
        private String caption;

        public string TransponderCode { get { return passing.TransponderCode; }  }
        public DateTime Time { get { return time; } }
        public string Caption { get { return caption;  } }

        public DangerZone DangerZone { get; set; }

        /// <summary>
        /// Constructor. Create a Detection object given a passing and a caption.
        /// </summary>
        /// <param name="passing">Actual passing data received from RRActiveConnector.GetNextPassing()</param>
        /// <param name="caption">A caption which will be associated with the detection</param>
        public Detection (RRActivePassing passing, string caption)
	    {
            this.passing = passing;
            this.time = DateTime.Parse(passing.TimeStamp);
            this.caption = caption;
	    }

        /// <summary>
        /// Override ToString() function to show transponder code and detection time
        /// </summary>
        /// <returns>transponder code + detection time</returns>
        public override string ToString()
        {
            return passing.TransponderCode + " " + time.ToShortTimeString();
        }

        /// <summary>
        /// Override Equals() function to compare transponder code only
        /// </summary>
        /// <param name="obj">Other object</param>
        /// <returns>Whether obj equals this. Detections equals iff they have the same transponder code.</returns>
        public override bool Equals(object obj)
        {
            if (obj.GetType() != GetType()) return false;
            return passing.TransponderCode.Equals(((Detection)obj).passing.TransponderCode);
        }

        /// <summary>
        /// Override GetHasCode() function to return hash code of the transponder code
        /// </summary>
        /// <returns>HashCode of Detection</returns>
        public override int GetHashCode()
        {
            return passing.TransponderCode.GetHashCode();
        }        

        /// <summary>
        /// Override CompareTo() function to compare transponder code only
        /// </summary>
        /// <param name="obj">Other object</param>
        /// <returns>TransponderCode.CompareTo(obj.TransponderCode)</returns>
        public int CompareTo(object obj)
        {
            return passing.TransponderCode.CompareTo(((Detection)obj).passing.TransponderCode);
        }
    }
    
    /// <summary>
    /// MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Interface to race result USB Timing Box
        RRActiveConnector rrActiveUsb;
        // We will poll for new passings using a background worker
        BackgroundWorker bw;

        // In this Demo each transponder code is associated with a name. These are the names that are available in this demo...
        public static string[] Names = new string[] { "Carl", "Ned", "Homer", "Bart", "Milhouse", "Apu", "Krsuty", "Mr. Burns", "Smithers", "Dr. Hibbert" };

        // Three observable collections which XAML binds to: Transponders currently in danger zone, transponders out of danger zone, transponder history
        public ObservableCollection<Detection> TranspondersIn { get; private set; }
        public ObservableCollection<Detection> TranspondersOut { get; private set; }
        public ObservableCollection<Detection> TransponderHistory { get; private set; }

        // List of possible serial ports to which a USB Timing Box is connected
        public List<String> ComPorts { get; private set; }
        // True, iff additional loop boxes are used to detect whether 
        public Boolean MultiLoop { get; set; }

        // Maps transponder codes to display names
        private Dictionary<string, string> TransponderNamesMap = new Dictionary<string, string>();

        /// <summary>
        /// Get name for given transponder code
        /// </summary>
        /// <param name="transponderCode">Transponder code for which to get the name.</param>
        /// <returns>Display name</returns>
        private string GetName(string transponderCode)
        {
            if (!TransponderNamesMap.ContainsKey(transponderCode))
            {
                foreach (var n in Names)
                {
                    if (!TransponderNamesMap.ContainsValue(n))
                    {
                        TransponderNamesMap[transponderCode] = n;
                        break;
                    }
                }
            }

            if (!TransponderNamesMap.ContainsKey(transponderCode)) TransponderNamesMap[transponderCode] = "Smith";

            return TransponderNamesMap[transponderCode];
        }
              
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

            TranspondersIn = new ObservableCollection<Detection>();
            TranspondersOut = new ObservableCollection<Detection>();
            TransponderHistory = new ObservableCollection<Detection>();

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
                    // Get passing, create Detection object
                    var passing = rrActiveUsb.GetNextPassing();  
                    Trace.WriteLine(string.Format("New Passing: Transponder {0}@{1} - time: {2}", passing.TransponderCode, passing.LoopID, passing.TimeStamp), Tools.TRACE_CATEGORY_INFO);
                    var detection = new Detection(passing, GetName(passing.TransponderCode));

                    // always add to transponder History
                    this.Dispatcher.InvokeAsync(new Action(() => { TransponderHistory.Add(detection); }));

                    // add to either danger or save zone depending on the MultiLoop configuration
                    if (!MultiLoop)
                    {
                        if (!TranspondersIn.Contains(detection))
                        {
                            detection.DangerZone = DangerZone.In;
                            this.Dispatcher.InvokeAsync(new Action(() => { TranspondersOut.Remove(detection); TranspondersIn.Add(detection); }));
                        }
                        else
                        {
                            detection.DangerZone = DangerZone.Out;
                            this.Dispatcher.InvokeAsync(new Action(() => { TranspondersIn.Remove(detection); TranspondersOut.Add(detection); }));
                        }
                    }
                    else 
                    {
                        if (passing.LoopID == 1)
                        {
                            detection.DangerZone = DangerZone.In;
                            if (!TranspondersIn.Contains(detection)) this.Dispatcher.InvokeAsync(new Action(() => { TranspondersOut.Remove(detection); TranspondersIn.Add(detection); }));
                        }
                        else
                        {
                            detection.DangerZone = DangerZone.Out;
                            if (!TranspondersOut.Contains(detection)) this.Dispatcher.InvokeAsync(new Action(() => { TranspondersIn.Remove(detection); TranspondersOut.Add(detection); }));
                        }
                    }
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
                rrActiveUsb.ResetPassings();
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
                }
            }
        }
    }
}
