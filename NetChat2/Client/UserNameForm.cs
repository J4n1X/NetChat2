using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace NetChat2
{
    public partial class UserNameForm : Form
    {
        public string UserName = "";
        public UserNameForm()
        {
            InitializeComponent();
            DialogResult = DialogResult.Cancel;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.Length == 0)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
            
            UserName = textBox1.Text;
            if(UserName.Length > 64)
            {
                UserName = UserName.Substring(0, 64);
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Enter)
            {
                if (textBox1.Text.Length == 0)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }

                UserName = textBox1.Text;
                if (UserName.Length > 64)
                {
                    UserName = UserName.Substring(0, 64);
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void UserNameForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(DialogResult != DialogResult.OK)
                this.DialogResult = DialogResult.Cancel;
        }
    }
}
