using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using unvell.D2DLib;

namespace QuickBrowser
{
    public static class Ex
    {
        public static D2DSize MeasureText(this D2DGraphics g, string text,Font f)
        {
            return g.MeasureText(text, f.Name, f.Size, new D2DSize(100, 100));
        }
    }
}
