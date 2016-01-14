using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace InOutDemo
{
    static class Tools
    {
        public static string TRACE_CATEGORY_INFO = "INFO";
        public static string TRACE_CATEGORY_ERROR = "ERROR";

        public static List<String> ListComPorts(uint vid, uint pid)
        {
            List<String> devices = new List<String>();
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", string.Format("SELECT Name FROM Win32_PnPEntity WHERE Name like '%Port%' AND DeviceID like '%{0}%{1}%'", vid, pid));
            Regex pattern = new Regex(@"(COM\d+)");

            foreach (ManagementObject device in searcher.Get())
            {
                var name = device.GetPropertyValue("Name").ToString();                    
                var comPort = pattern.Match(name).Groups[0].ToString();
                
                Trace.WriteLine(string.Format("Found USB Serial Port @{0}", comPort), TRACE_CATEGORY_INFO);                    
                devices.Add(comPort);
            }

            return devices;
        }
    }
}
