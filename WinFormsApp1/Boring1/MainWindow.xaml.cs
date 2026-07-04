using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.WebRequestMethods;

namespace Boring1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent(); 
            
            string html = "<html><body><p>哥哥你好呀～</p></body></html>";
            this.WebView1.NavigateToString(html);
        }

        private void RichTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key== Key.Enter)
            {
                this.WebView1.ExecuteScriptDirectAsync(
                    "document.body.innerHTML += '<p>哥哥你好呀～這是妹妹加的一行字！</p>';"
                );
            }
        }

        private void WebView1_WebViewCreated(object sender, EventArgs e)
        {
            this.WebView1.NavigateToString("<html><body></body></html>");
        }

        private void WebView1_WebMessageReceived(object sender, Diga.WebView2.Wrapper.EventArguments.WebMessageReceivedEventArgs e)
        {

        }
    }
}