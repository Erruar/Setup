using System.Windows.Interop;
using System.Windows;

namespace SakuBA;

public partial class RootView : Window
{
    public RootView(RootViewModel viewModel)
    {
        DataContext = viewModel; 
        Loaded += (_, _) => SakuBA_App.Model?.Engine?.CloseSplashScreen();
        Closed += (_, _) => Dispatcher.InvokeShutdown();
        
        InitializeComponent();
        
        viewModel.Dispatcher = Dispatcher;
        viewModel.ViewWindowHandle = new WindowInteropHelper(this).EnsureHandle();
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