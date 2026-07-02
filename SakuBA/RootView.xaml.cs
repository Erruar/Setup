using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;

namespace SakuBA;

public partial class RootView : Window
{
    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
    
    public RootView(RootViewModel viewModel)
    {
        DataContext = viewModel; 
        Loaded += (_, _) => SakuBA_App.Model?.Engine?.CloseSplashScreen();
        Closed += (_, _) => Dispatcher.InvokeShutdown();
        SourceInitialized += OnSourceInitialized;
        
        InitializeComponent();
        
        viewModel.Dispatcher = Dispatcher;
        viewModel.ViewWindowHandle = new WindowInteropHelper(this).EnsureHandle();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int roundMode = 2;
            DwmSetWindowAttribute(hwnd, 33, ref roundMode, sizeof(int));
        }
        catch
        {
            // Ignored on Windows 10 (as should be)
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is RootViewModel { InstallState: InstallationState.Applying } rvm)
        {
            rvm.CancelButton_Click();
            if (rvm.Canceled)
            {
                e.Cancel = true;
                rvm.AutoClose = true;
            }
        }
    }
}