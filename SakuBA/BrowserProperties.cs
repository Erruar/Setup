namespace SakuBA
{
    using System.Windows;
    using System.Windows.Controls;

    class BrowserProperties
    {
        public static readonly DependencyProperty HtmlDocProperty =
            DependencyProperty.RegisterAttached("HtmlDoc", typeof(string), typeof(BrowserProperties), new PropertyMetadata(OnHtmlDocChanged));

        public static string GetHtmlDoc(DependencyObject obj) => (string)obj.GetValue(HtmlDocProperty);
        public static void SetHtmlDoc(DependencyObject obj, string value) => obj.SetValue(HtmlDocProperty, value);

        private static void OnHtmlDocChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var webBrowser = (WebBrowser)d;
            webBrowser.NavigateToString((string)e.NewValue);
        }
    }
}
