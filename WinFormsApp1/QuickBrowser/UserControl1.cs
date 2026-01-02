using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace QuickBrowser
{
    public partial class UserControl1 : UserControl
    {
        public enum Type
        {
            file, directory
        }
        public Type type;
        public UserControl1()
        {
            InitializeComponent();
        }
        public Action onMouseDown;
        private void richTextBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (onMouseDown != null)
            {
                onMouseDown();
            }
        }
        public Action onCopyPath;
        private void button1_Click(object sender, EventArgs e)
        {
            if (onCopyPath!=null)
            {
                onCopyPath();
            }
        }
        public void resetColor()
        {
            switch (type)
            {
                case Type.file:
                    BackColor = Color.LightGray;
                    break;
                case Type.directory:
                    BackColor = Color.Gray;
                    break;
                default:
                    break;
            }
        }
        public void selectYellow()
        {
            switch (type)
            {
                case Type.file:
                    BackColor = Color.LightYellow;
                    break;
                case Type.directory:
                    BackColor = Color.Yellow;
                    break;
                default:
                    break;
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (onMouseDown != null)
            {
                onMouseDown();
            }
        }

    }
}
