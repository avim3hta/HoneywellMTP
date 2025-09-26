using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace MTPSimulator.App
{
    public partial class App : Application
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        protected override void OnStartup(StartupEventArgs e)
        {
            // Allocate console for debug output
            AllocConsole();
            Console.WriteLine("MTP Simulator Console - Debug Output");
            Console.WriteLine("====================================");
            
            base.OnStartup(e);
        }
    }
}

