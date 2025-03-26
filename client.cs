using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
//using MySql.Data;
//using MySql.Data.MySqlClient;

class SecureChatServer
{

    static List<SslStream> clients = new List<SslStream>();

    static void Main()
    {
        Console.WriteLine("Starting TLS server...");

        X509Certificate2 serverCert = new X509Certificate2("server.pfx", "exportpassword");
        TcpListener server = new TcpListener(IPAddress.Any, 5555);
        server.Start();
        Console.WriteLine("Server started on port 5555...");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            Task.Run(() => HandleClient(client, serverCert));
        }
    }

    static void HandleClient(TcpClient client, X509Certificate2 serverCert)
    {
        SslStream sslStream = new SslStream(client.GetStream(), false);
        sslStream.AuthenticateAsServer(serverCert, false, System.Security.Authentication.SslProtocols.Tls12, true);

        lock (clients) { clients.Add(sslStream); }

        Console.WriteLine("Client connected.");

        try
        {
            byte[] buffer = new byte[1024];
            while (true)
            {
                int bytesRead = sslStream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received: {message}");

                BroadcastMessage(message, sslStream);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client disconnected: {ex.Message}");
        }
        finally
        {
            lock (clients) { clients.Remove(sslStream); }
            sslStream.Close();
            client.Close();
        }
    }

    static void BroadcastMessage(string message, SslStream sender)
    {
        byte[] response = Encoding.UTF8.GetBytes(message);
        lock (clients)
        {
            foreach (var client in clients)
            {
                if (client != sender)  
                {
                    try { client.Write(response, 0, response.Length); }
                    catch { }
                }
            }
        }
    }
}