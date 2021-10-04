using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Net;
using System.Windows.Forms;

namespace NetChat2
{
    public partial class Form1 : Form
    {
        private Client client;
        private Server server;
        public Form1()
        {
            InitializeComponent();
            ToastNotificationManagerCompat.OnActivated += Toast_OnActivation;
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            int port = 0;
            if (!(Int32.TryParse(portTextBox.Text, out port) && IPAddress.TryParse(ipTextBox.Text, out _)))
                MessageBox.Show("Invalid IP Address or Port specified!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            var userNameForm = new UserNameForm();
            if (userNameForm.ShowDialog() == DialogResult.Cancel)
                return;

            client = new Client(ipTextBox.Text, port, userNameForm.UserName);
            if (client == null)
                throw new ArgumentException("Client is not initialized");
            client.OnServerConnect += Client_Connected;
            client.OnText += Client_Text;
            client.OnServerConnectFail += Client_ConnectionFail;
            client.OnServerDisconnect += Client_Disconnected;
            connectionStatusLabel.Text = "Connecting to " + ipTextBox.Text + "...";
            client.Connect();
        }

        private void ListenButton_Click(object sender, EventArgs e)
        {
            int port = 0;
            if (!(int.TryParse(portTextBox.Text, out port) && IPAddress.TryParse(ipTextBox.Text, out _)))
                MessageBox.Show("Invalid IP Address or Port specified!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            var userNameForm = new UserNameForm();
            if (userNameForm.ShowDialog() == DialogResult.Cancel)
                return;
            server = new Server(ipTextBox.Text, port);

            client = new Client(ipTextBox.Text, port, userNameForm.UserName);
            connectionStatusLabel.Text = "Hosting on " + ipTextBox.Text + " Port " + port;
            if (client == null)
                throw new ArgumentException("Client is not initialized");
            client.OnServerConnect += Client_Connected;
            client.OnText += Client_Text;
            client.OnServerConnectFail += Client_ConnectionFail;
            client.OnServerDisconnect += Client_Disconnected;
            client.Connect();
        }

        private void Client_Connected(object sender, string message)
        {
            connectionStatusLabel.Text = "Connected to Server!";
            historyTextBox.Text += message;
            this.Text = "Connected as " + client.UserName + " | NetChat 2";
        }

        private void Client_ConnectionFail(object sender, string reason)
        {
            MessageBox.Show(reason, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void Client_Disconnected(object sender)
        {
            if (!client.Available)
                return;
            client.Disconnect();
            Text = "Not Connected | NetChat 2";
            connectionStatusLabel.Text = "Not Connected!";
        }

        private void Client_Text(object sender, BaseCommand command)
        {
            Invoke((MethodInvoker)delegate
            {
                string text = command.Data["TEXT"];
                if (command.Command == Commands.ClientText)
                    text = "<" + command.Data["USERNAME"] + "> " + text;
                // Running on the UI thread
                historyTextBox.Text += text;
                if (WindowState == FormWindowState.Minimized)
                {
                    new ToastContentBuilder()
                    .AddText("New Message from " + command.Data["USERNAME"] + "!")
                    .AddText(command.Data["TEXT"])
                    .Show();
                    this.FlashWindowEx();
                }
            });
        }

        private void Toast_OnActivation(ToastNotificationActivatedEventArgsCompat toastArgs)
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


        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (client.Available)
                {
                    if (inputTextBox.Text.Length > 0)
                    {
                        string message = inputTextBox.Text + '\n';
                        client.SendText(message);
                        //historyTextBox.Text += message;
                        inputTextBox.Text = "";
                    }
                }
            }
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            if (client.Available)
            {
                if (inputTextBox.Text.Length > 0)
                {
                    string message = inputTextBox.Text + '\n';
                    client.SendText(message);
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

        private void SendDisconnectCommandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Client_Disconnected(sender);
        }
    }
}
