using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Management;
using System.Windows.Forms;

namespace QuickBrowser
{
    public partial class ChooseBox : Form
    {
        public string encode
        {
            get
            {
                return comboBox1.SelectedItem.ToString().Split(':')[1];
            }
        }
        public ChooseBox()
        {
            InitializeComponent();
        }
        DialogResult result;
        private void button1_Click(object sender, EventArgs e)
        {
            result = DialogResult.OK;
            Close();
        }

        private void ChooseBox_Load(object sender, EventArgs e)
        {
            comboBox1.SelectedIndex = 0;
        }

        private void ChooseBox_Shown(object sender, EventArgs e)
        {
            result = DialogResult.Cancel;
        }

        private void ChooseBox_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult = result;
        }
    }
}
