using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.TextFormatting;
using System.Windows.Shapes;
using static System.Windows.Forms.LinkLabel;
using Rectangle = System.Drawing.Rectangle;

namespace QuickBrowser
{
    public partial class AlphaRichTextBox : RichTextBox
    {
        /*private Font font;
        private Brush b;
        public Rectangle rec;
        public AlphaRichTextBox()
        {
            InitializeComponent();

            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;

            b = new SolidBrush(Color.Aqua);

            SelectionBullet = false;
            SelectionIndent = 0;

            font = new Font(Font.FontFamily, Font.Size + 5);
        }
        StringFormat stringFormat= StringFormat.GenericTypographic;
        protected override void OnPaint(PaintEventArgs e)
        {
            //base.OnPaint(e);
            if (SelectionLength > 0)
            {
                var start = e.Graphics.MeasureString(Text.Substring(0, SelectionStart), font, PointF.Empty,
                    stringFormat);
                var end = e.Graphics.MeasureString(Text.Substring(0, SelectionStart + SelectionLength), font,
                    PointF.Empty,
                    stringFormat);
                e.Graphics.FillRectangle(b, start.Width, 0, end.Width - start.Width + 0.1f,
                    end.Height);
            }
            var word = e.Graphics.MeasureString(Text, font, PointF.Empty,
                stringFormat);
            rec = new Rectangle(0,0,100,50);

            e.Graphics.DrawString(Text, font, Brushes.Black, rec, stringFormat);
        }
        float clamp(float v, float min, float max)
        {
            if (v > min) v = min;
            if(v < max) v = max;
            return v;
        }*/
    }
}
