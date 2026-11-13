Imports Microsoft.Phone.Controls
Imports Microsoft.Phone.Shell
Imports System
Imports System.Windows
Imports System.Windows.Navigation

Partial Public Class App
        Inherits Application

        Public Shared Property RootFrame As PhoneApplicationFrame

        Public Sub New()
            InitializeComponent()
            InitializePhoneApplication()
        End Sub

        Private Sub InitializePhoneApplication()
            If RootFrame IsNot Nothing Then Return

            RootFrame = New PhoneApplicationFrame()
            AddHandler RootFrame.Navigated, AddressOf CompleteInitializePhoneApplication
            AddHandler RootFrame.NavigationFailed, AddressOf RootFrame_NavigationFailed
            RootFrame.Navigate(New Uri("/MainPage.xaml", UriKind.Relative))
        End Sub

        Private Sub CompleteInitializePhoneApplication(sender As Object, e As NavigationEventArgs)
            If RootVisual IsNot RootFrame Then RootVisual = RootFrame
            RemoveHandler RootFrame.Navigated, AddressOf CompleteInitializePhoneApplication
        End Sub

        Private Sub RootFrame_NavigationFailed(sender As Object, e As NavigationFailedEventArgs)
            Throw New InvalidOperationException("Failed to navigate to " & e.Uri.ToString())
        End Sub

        Private Sub Application_Launching(sender As Object, e As LaunchingEventArgs)
        End Sub

        Private Sub Application_Closing(sender As Object, e As ClosingEventArgs)
        End Sub

        Private Sub Application_Activated(sender As Object, e As ActivatedEventArgs)
        End Sub

        Private Sub Application_Deactivated(sender As Object, e As DeactivatedEventArgs)
        End Sub
End Class
