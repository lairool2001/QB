using System.Runtime.InteropServices;

namespace EverythingWithAI;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
