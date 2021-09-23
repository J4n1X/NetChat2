using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Net;
using System.Windows.Forms;

namespace NetChat2
{
    public partial class Form1 : Form
    {
        private Client client = new Client();
        public Form1()
        {
            InitializeComponent();
            client.OnServerConnect += client_Connected;
            client.OnTextFromServer += client_TextFromClient;
            client.OnServerConnectFail += client_ConnectionFail;
            client.OnServerDisconnect += client_Disconnected;
            ToastNotificationManagerCompat.OnActivated += toast_OnActivation;
        }


        private void connectButton_Click(object sender, EventArgs e)
        {
            int port = 0;
            if (!(Int32.TryParse(portTextBox.Text, out port) && IPAddress.TryParse(ipTextBox.Text, out _)))
                MessageBox.Show("Invalid IP Address or Port specified!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            var userNameForm = new UserNameForm();
            if (userNameForm.ShowDialog() == DialogResult.Cancel)
                return;
            client.Connect(ipTextBox.Text, port, userNameForm.UserName);
            connectionStatusLabel.Text = "Connecting to " + ipTextBox.Text + "...";
        }

        private void listenButton_Click(object sender, EventArgs e)
        {
            int port = 0;
            if (!(int.TryParse(portTextBox.Text, out port) && IPAddress.TryParse(ipTextBox.Text, out _)))
                MessageBox.Show("Invalid IP Address or Port specified!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            var userNameForm = new UserNameForm();
            if (userNameForm.ShowDialog() == DialogResult.Cancel)
                return;
            client.StartServer(ipTextBox.Text, port);
            while (!client.LocalServer.Available)
                System.Threading.Thread.Sleep(10);
            client.Connect(ipTextBox.Text, port, userNameForm.UserName);
            connectionStatusLabel.Text = "Hosting on " + ipTextBox.Text + " Port " + port;
        }

        private void client_Connected(object sender, string message)
        {
            connectionStatusLabel.Text = "Connected to Server!";
            historyTextBox.Text += message;
            this.Text = "Connected as " + client.UserName + " | NetChat 2";
        }

        private void client_ConnectionFail(object sender, string reason)
        {
            MessageBox.Show(reason, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void client_Disconnected(object sender, string reason = "")
        {
            if (!client.Available)
                return;
            client.Disconnect();
            Text = "Not Connected | NetChat 2";
            connectionStatusLabel.Text = "Not Connected!";
        }

        private void client_TextFromClient(object sender, string text)
        {
            Invoke((MethodInvoker)delegate
            {
                // Running on the UI thread
                historyTextBox.Text += text;
                if (WindowState == FormWindowState.Minimized)
                {
                    new ToastContentBuilder()
                    .AddText("New Message!")
                    .AddText(text)
                    .Show();
                    this.FlashWindowEx();
                }
            });
        }

        private void toast_OnActivation(ToastNotificationActivatedEventArgsCompat toastArgs)
        {
            /* Use these commands to get arguments and further input.
             * https://docs.microsoft.com/en-us/windows/apps/design/shell/tiles-and-notifications/send-local-toast?tabs=desktop
             * ToastArguments args = ToastArguments.Parse(toastArgs.Argument);
             * var userInput = toastArgs.UserInput;
             */
            Invoke((MethodInvoker)delegate
            {
                this.Restore();
            });
        }


        private void inputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (client.Available)
                {
                    if (inputTextBox.Text.Length > 0)
                    {
                        string message = inputTextBox.Text + '\n';
                        client.SendText(message, TextFlags.Unicode);
                        //historyTextBox.Text += message;
                        inputTextBox.Text = "";
                    }
                }
            }
        }

        private void sendButton_Click(object sender, EventArgs e)
        {
            if (client.Available)
            {
                if (inputTextBox.Text.Length > 0)
                {
                    string message = inputTextBox.Text + '\n';
                    client.SendText(message, TextFlags.Unicode);
                    //historyTextBox.Text += message;
                    inputTextBox.Text = "";
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (client.Available)
                client.Disconnect();
            client.Dispose();
        }

        private void sendDisconnectCommandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            client_Disconnected(sender, "Client closed connection.");
        }
    }
}
