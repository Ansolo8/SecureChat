using System;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace SecureChatClient

{
    public class SecureChatClient : Form
    {
        private ListBox lstChat;
        private TextBox txtInput;
        private Button btnSend;
        private SslStream sslStream;
        private TcpClient client;
        private Thread receiveThread;

        public SecureChatClient()
        {
            this.Text = "Secure Chat Client";
            this.Size = new System.Drawing.Size(400, 600);

            lstChat = new ListBox
            {
                Size = new System.Drawing.Size(360, 400)
            };
            this.Controls.Add(lstChat);

            txtInput = new TextBox
            {
                Location = new System.Drawing.Point(10, 420),
                Size = new System.Drawing.Size(260, 30)
            };
            this.Controls.Add(txtInput);

            btnSend = new Button
            {
                Text = "Send",
                Location = new System.Drawing.Point(280, 420),
                Size = new System.Drawing.Size(90, 30)
            };
            btnSend.Click += new EventHandler(this.btnSend_Click);
            this.Controls.Add(btnSend);

            ConnectToServer();
        }

        private void ConnectToServer()
        {
            try
            {
                client = new TcpClient("127.0.0.1", 5555);
                sslStream = new SslStream(client.GetStream(), false, ValidateServerCertificate);
                sslStream.AuthenticateAsClient("localhost");

                lstChat.Items.Add("Connected to server.");

                receiveThread = new Thread(ReceiveMessages);
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to connect: " + ex.Message);
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            string message = txtInput.Text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                lstChat.Items.Add("You: " + message);
                txtInput.Clear();
                lstChat.TopIndex = lstChat.Items.Count - 1;

                SendMessageToServer(message);
            }
        }

        private void SendMessageToServer(string message)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                sslStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                lstChat.Items.Add("Error sending message: " + ex.Message);
            }
        }

        private void ReceiveMessages()
        {
            try
            {
                while (true)
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = sslStream.Read(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Invoke((MethodInvoker)delegate
                        {
                            lstChat.Items.Add("Server: " + message);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Invoke((MethodInvoker)delegate
                {
                    lstChat.Items.Add("Error receiving message: " + ex.Message);
                });
            }
        }

        private static bool ValidateServerCertificate(
            object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return certificate.Subject.Contains("CN=localhost");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            sslStream?.Close();
            client?.Close();
            receiveThread?.Abort();
            base.OnFormClosing(e);
        }

        static void Main()
        {
            Application.Run(new SecureChatClient());
        }
    }
}