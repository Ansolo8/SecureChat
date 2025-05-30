using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;

class SecureChatServer
{
    static List<SslStream> clients = new List<SslStream>();
    static Dictionary<SslStream, string> clientUsernames = new Dictionary<SslStream, string>();

    static async Task Main()
    {
        Console.WriteLine("Starting TLS server...");

        try
        {
#pragma warning disable SYSLIB0057
            X509Certificate2 serverCert = new X509Certificate2("server.pfx", "exportpassword");
#pragma warning restore SYSLIB0057

            TcpListener server = new TcpListener(IPAddress.Any, 5555);
            server.Start();
            Console.WriteLine("Server started on port 5555...");

            while (true)
            {
                TcpClient client = await server.AcceptTcpClientAsync();
                _ = HandleClientAsync(client, serverCert);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server error: {ex.Message}");
        }
    }

    static async Task HandleClientAsync(TcpClient client, X509Certificate2 serverCert)
    {
        SslStream sslStream = new SslStream(client.GetStream(), false);
        try
        {
            await sslStream.AuthenticateAsServerAsync(serverCert, false, System.Security.Authentication.SslProtocols.Tls12, true);
            lock (clients) { clients.Add(sslStream); }
            Console.WriteLine("Client connected.");

           
            byte[] buffer = new byte[1024];
            int bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0)
            {
                throw new Exception("Client disconnected before sending username.");
            }

            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            string username = "Unknown";
            if (message.StartsWith("USERNAME:"))
            {
                username = message.Substring("USERNAME:".Length).Trim();
                lock (clientUsernames) { clientUsernames[sslStream] = username; }
                Console.WriteLine($"{username} connected.");
                await BroadcastMessageAsync($"{username} has joined the chat", null);
            }
            else
            {
                Console.WriteLine("No username received; using 'Unknown'.");
                lock (clientUsernames) { clientUsernames[sslStream] = username; }
            }

            while (true)
            {
                bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received from {username}: {message}");
                await BroadcastMessageAsync(message, sslStream);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client error ({clientUsernames.GetValueOrDefault(sslStream, "Unknown")}): {ex.Message}");
        }
        finally
        {
            string username = clientUsernames.ContainsKey(sslStream) ? clientUsernames[sslStream] : "Unknown";
            lock (clients) { clients.Remove(sslStream); }
            lock (clientUsernames) { clientUsernames.Remove(sslStream); }
            Console.WriteLine($"{username} disconnected.");
            await BroadcastMessageAsync($"{username} has left the chat", null);
            sslStream.Close();
            client.Close();
        }
    }

    static async Task BroadcastMessageAsync(string message, SslStream sender)
    {
        byte[] response = Encoding.UTF8.GetBytes(message);
        List<SslStream> clientsToBroadcast;

        lock (clients)
        {
            clientsToBroadcast = new List<SslStream>(clients);
        }

        Console.WriteLine($"Broadcasting message: '{message}' to {clientsToBroadcast.Count} clients (excluding sender).");

        foreach (var client in clientsToBroadcast)
        {
            if (client != sender) 
            {
                try
                {
                    await client.WriteAsync(response, 0, response.Length);
                    await client.FlushAsync();
                    Console.WriteLine($"Successfully sent to client: {clientUsernames.GetValueOrDefault(client, "Unknown")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error broadcasting to client {clientUsernames.GetValueOrDefault(client, "Unknown")}: {ex.Message}");
                    lock (clients) { clients.Remove(client); }
                    lock (clientUsernames) { clientUsernames.Remove(client); }
                }
            }
        }
    }
}