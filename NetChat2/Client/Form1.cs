using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Net;
using System.Windows.Forms;

namespace NetChat2
{
    public partial class Form1 : Form
    {
        // TODO: Fix status messages on statusbar and form title
        private Client client;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "<Pending>")]
        private Server server;

        private readonly string formDefaultText;
        private readonly string connectionStatusLabelDefaultText;

        public Form1()
        {
            InitializeComponent();
            formDefaultText = this.Text;
            connectionStatusLabelDefaultText = connectionStatusLabel.Text;
            ToastNotificationManagerCompat.OnActivated += Toast_OnActivation;
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            if (!(int.TryParse(portTextBox.Text, out int port) && IPAddress.TryParse(ipTextBox.Text, out _)))
                MessageBox.Show("Invalid IP Address or Port specified!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            var userNameForm = new UserNameForm();
            if (userNameForm.ShowDialog() == DialogResult.Cancel)
                return;

            client = new Client(ipTextBox.Text, port, userNameForm.UserName);
            if (client == null)
                throw new ArgumentException("Client is not initialized");
            client.OnServerConnect += Client_Connected;
            client.OnText += Client_Message;
            client.OnServerConnectFail += Client_ConnectionFail;
            client.OnServerDisconnect += Client_Disconnected;
            client.OnCommandTextResponse += Client_CommandTextResponse;
            connectionStatusLabel.Text = "Connecting to " + ipTextBox.Text + "...";
            _ = TryConnectClient();
        }

        private void ListenButton_Click(object sender, EventArgs e)
        {
            if (!(int.TryParse(portTextBox.Text, out int port) && IPAddress.TryParse(ipTextBox.Text, out _)))
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
            client.OnText += Client_Message;
            client.OnServerConnectFail += Client_ConnectionFail;
            client.OnServerDisconnect += Client_Disconnected;
            client.OnCommandTextResponse += Client_CommandTextResponse;
            _ = TryConnectClient();
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
            Text = formDefaultText;
            connectionStatusLabel.Text = connectionStatusLabelDefaultText;
        }

        private void Client_Message(object sender, CommandObject command)
        {
            // Enforce running on the UI thread
            Invoke((MethodInvoker)delegate
            {
                string text = command.Data["TEXT"];
                if (command.Command == BaseCommand.CLIENT_TEXT)
                {
                    text = "<" + command.Data["USERNAME"] + "> " + text;
                    if (WindowState == FormWindowState.Minimized)
                    {
                        new ToastContentBuilder()
                        .AddText("New Message from " + command.Data["USERNAME"] + "!")
                        .AddText(command.Data["TEXT"])
                        .Show();
                        this.FlashWindowEx();
                    }
                }
                historyTextBox.Text += text;
            });
        }

        private void Client_CommandTextResponse(string text)
        {
            Invoke((MethodInvoker)delegate
            {
                historyTextBox.Text += text;

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
                        if (inputTextBox.Text.StartsWith('/'))
                            client.SendCommand(inputTextBox.Text);
                        else
                            client.SendText(inputTextBox.Text + '\n');

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
                    if (inputTextBox.Text.StartsWith('/'))
                        client.SendCommand(inputTextBox.Text);
                    else
                        client.SendText(inputTextBox.Text + '\n');
                    //historyTextBox.Text += message;
                    inputTextBox.Text = "";
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (client?.Available == true)
            {
                client.Disconnect();
                client.Dispose();
            }
        }

        private void SendDisconnectCommandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Client_Disconnected(sender);
        }

        private bool TryConnectClient()
        {
            try
            {
                client.Connect();
            }
            catch (InvalidVersionException ex)
            {
                if (MessageBox.Show("Connection failed because the Server had a different version number.\n" +
                               $"Client Version: {ex.ClientVersion}\n" +
                               $"Server Version: {ex.ServerVersion}\n" + 
                               "\nDo you want to view the exception details?",
                               "Error",
                               MessageBoxButtons.YesNo,
                               MessageBoxIcon.Error
                    ) == DialogResult.Yes)
                    MessageBox.Show(ex.ToString());
                Text = formDefaultText;
                connectionStatusLabel.Text = connectionStatusLabelDefaultText;
            }
            catch (ArgumentException ex)
            {
                if (MessageBox.Show($"The Server reported that an invalid Username was entered: {client.UserName}\n" +
                                "Please enter a different Username and try again.\n" +
                                "\nDo you want to view the exception details?",
                                "Error",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Error
                    ) == DialogResult.Yes)
                    MessageBox.Show(ex.ToString());
                Text = formDefaultText;
                connectionStatusLabel.Text = connectionStatusLabelDefaultText;
            }
            catch (InvalidCommandException ex)
            {
                if (MessageBox.Show($"The Server reported that an invalid Command was sent\n" +
                                "This appears to have occured due to a bug. Please contact your Administrator\n" +
                                "\nDo you want to view the exception details?",
                                "Error",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Error
                    ) == DialogResult.Yes)
                    MessageBox.Show(ex.ToString());
                Text = formDefaultText;
                connectionStatusLabel.Text = connectionStatusLabelDefaultText;
            }
            return false;
        }
    }
}
