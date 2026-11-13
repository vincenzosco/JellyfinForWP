using System;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;

namespace JellyfinPlayer
{
    public partial class App : Application
    {
        public static PhoneApplicationFrame RootFrame { get; set; }

        public App()
        {
            InitializeComponent();
            InitializePhoneApplication();
        }

        private void InitializePhoneApplication()
        {
            if (RootFrame != null) return;

            RootFrame = new PhoneApplicationFrame();
            RootFrame.Navigated += CompleteInitializePhoneApplication;
            RootFrame.NavigationFailed += RootFrame_NavigationFailed;
            RootFrame.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
        }

        private void CompleteInitializePhoneApplication(object sender, NavigationEventArgs e)
        {
            if (RootVisual != RootFrame) RootVisual = RootFrame;
            RootFrame.Navigated -= CompleteInitializePhoneApplication;
        }

        private void RootFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new InvalidOperationException("Failed to navigate to " + e.Uri.ToString());
        }

        private void Application_Launching(object sender, LaunchingEventArgs e)
        {
        }

        private void Application_Closing(object sender, ClosingEventArgs e)
        {
        }

        private void Application_Activated(object sender, ActivatedEventArgs e)
        {
        }

        private void Application_Deactivated(object sender, DeactivatedEventArgs e)
        {
        }
    }
}
