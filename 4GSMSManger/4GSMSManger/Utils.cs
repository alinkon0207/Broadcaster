using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace _4GSMSManger
{
    public class Utils
    {
        public static ArrayList GetIPAddressList()
        {
            ArrayList result = new ArrayList();
            // Get all network interfaces (NICs) on the local machine
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface nic in nics)
            {
                // Select only Ethernet or Wireless interfaces
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                {
                    // Get the IP properties for this NIC
                    IPInterfaceProperties ipProps = nic.GetIPProperties();


                    // Get the IP addresses assigned to this NIC
                    UnicastIPAddressInformationCollection addrList = ipProps.UnicastAddresses;

                    foreach (UnicastIPAddressInformation addr in addrList)
                    {
                        // Print out the IP address and subnet mask for this address

                        if (addr.Address.IsIPv6LinkLocal)
                            continue;
                        Console.WriteLine("------- {0}", addr.Address.ToString());
                        string str = addr.Address.ToString();
                        result.Add(str);
                    }
                }
            }

            return result;
        }

        public static int ToInt32(object obj)
        {
            try
            {
                return Convert.ToInt32(obj);
            }
            catch (Exception ex)
            {

            }

            return 0;
        }

        public static string ToString(object obj)
        {
            try
            {
                if (obj == null)
                    return string.Empty;


                return obj.ToString();
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }
    }
}
