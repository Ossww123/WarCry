using System;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Text;

public class IPAddressSender : MonoBehaviour
{
    [SerializeField] private string serverAddress = "127.0.0.1"; // Default to localhost
    [SerializeField] private int serverPort = 8080;

    /// <summary>
    /// Retrieves the local IP address of the machine as a string. The method attempts to determine the IP address
    /// by creating a socket and connecting to a public IP address. If this fails, a fallback mechanism scans
    /// through the host's IP addresses to find an IPv4 address.
    /// </summary>
    /// <returns>
    /// A string representing the local IPv4 address. If no valid IPv4 address is found, "0.0.0.0" is returned.
    /// </returns>
    public string GetLocalIPAddress()
    {
        string localIP = "0.0.0.0";
        try
        {
            // This approach doesn't require admin rights
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                // Connect to a public address (doesn't actually send data)
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address.ToString();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{DebugUtils.GetCallerInfo()}] Error getting IP address: {e.Message}");
            
            // Fallback method
            string hostName = Dns.GetHostName();
            IPHostEntry host = Dns.GetHostEntry(hostName);
            
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                    break;
                }
            }
        }
        
        Debug.Log($"[{DebugUtils.GetCallerInfo()}] Your ip address is {localIP}");
        return localIP;
    }

    /// <summary>
    /// Sends the local IP address of the machine to the specified server using a UDP client.
    /// The method retrieves the local IP address and starts a coroutine to handle the asynchronous
    /// transmission of the IP to the server.
    /// </summary>
    public void SendIPToServer()
    {
        StartCoroutine(SendIPCoroutine());
    }

    /// <summary>
    /// Handles the asynchronous transmission of the local IP address to a specified server using a UDP client.
    /// The method retrieves the IP address, encodes it into bytes, and sends it to the provided server address and port.
    /// Provides error handling for transmission failures.
    /// </summary>
    /// <returns>
    /// An IEnumerator to support asynchronous operation within a Unity coroutine.
    /// Yields null after completing the IP address transmission.
    /// </returns>
    private IEnumerator SendIPCoroutine()
    {
        string ipAddress = GetLocalIPAddress();
        
        // Create a client
        using (UdpClient client = new UdpClient())
        {
            try
            {
                // Convert IP to bytes
                byte[] data = Encoding.UTF8.GetBytes(ipAddress);
                
                // Send data
                client.Send(data, data.Length, serverAddress, serverPort);
                Debug.Log($"[{DebugUtils.GetCallerInfo()}] IP address sent to {serverAddress}:{serverPort}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[{DebugUtils.GetCallerInfo()}] Error sending IP: {e.Message}");
            }
        }
        
        yield return null;
    }

    /// <summary>
    /// Logs the local IP address of the machine to the Unity console. The method retrieves the IP address
    /// using GetLocalIPAddress and formats the output with caller information for debugging purposes.
    /// </summary>
    public void GetAndDisplayIP()
    {
        Debug.LogError($"[{DebugUtils.GetCallerInfo()}] Your IP address is: {GetLocalIPAddress()}");
    }

    /// <summary>
    /// Unity lifecycle method that is automatically called when the script instance is first enabled.
    /// It executes initialization logic, which includes retrieving and displaying the machine's local
    /// IP address to the Unity console for debugging purposes.
    /// </summary>
    private void Start()
    {
        GetAndDisplayIP();
    }
}