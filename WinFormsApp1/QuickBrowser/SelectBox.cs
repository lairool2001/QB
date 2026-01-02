using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QuickBrowser
{
    public partial class SelectBox : Form
    {
        DialogResult result;
        public SelectBox()
        {
            InitializeComponent();
        }
        public string command => richTextBox1.Text;

        private void button1_Click(object sender, EventArgs e)
        {
            result = DialogResult.OK;
            Close();
        }

        private void SelectBox_Shown(object sender, EventArgs e)
        {
            result = DialogResult.Cancel;
        }

        private void SelectBox_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult = result;
        }
    }
}
