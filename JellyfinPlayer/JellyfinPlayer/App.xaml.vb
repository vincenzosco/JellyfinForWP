' The Blank Application template is documented at http://go.microsoft.com/fwlink/?LinkID=391641

Imports System
Imports JellyfinPlayer.Services
Imports Windows.Phone.UI.Input
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls
Imports Windows.UI.Xaml.Media.Animation
Imports Windows.UI.Xaml.Navigation

''' <summary>
''' Provides application-specific behavior to supplement the default Application class.
''' </summary>
NotInheritable Class App
    Inherits Application

    Private _transitions As TransitionCollection

    Public Sub New()
        InitializeComponent()
        AppSession.Current.Load()
        AddHandler HardwareButtons.BackPressed, AddressOf OnBackPressed
    End Sub

    Private Sub OnBackPressed(sender As Object, e As BackPressedEventArgs)
        If NavigationService.BackInterceptor IsNot Nothing AndAlso NavigationService.BackInterceptor() Then
            e.Handled = True
            Return
        End If
        If NavigationService.CanGoBack Then
            NavigationService.GoBack()
            e.Handled = True
        End If
    End Sub

    Protected Overrides Sub OnLaunched(e As LaunchActivatedEventArgs)
        Dim rootFrame As Frame = TryCast(Window.Current.Content, Frame)

        If rootFrame Is Nothing Then
            rootFrame = New Frame()
            rootFrame.CacheSize = 2

            Window.Current.Content = rootFrame
        End If

        If rootFrame.Content Is Nothing Then
            If rootFrame.ContentTransitions IsNot Nothing Then
                _transitions = New TransitionCollection()
                For Each transition As Transition In rootFrame.ContentTransitions
                    _transitions.Add(transition)
                Next
            End If

            rootFrame.ContentTransitions = Nothing
            AddHandler rootFrame.Navigated, AddressOf RootFrame_FirstNavigated

            Dim target As Type = If(AppSession.Current.IsLoggedIn, GetType(HomePage), GetType(MainPage))
            If Not rootFrame.Navigate(target, e.Arguments) Then
                Throw New Exception("Failed to create initial page")
            End If
        End If

        Window.Current.Activate()
    End Sub

    Private Sub RootFrame_FirstNavigated(sender As Object, e As NavigationEventArgs)
        Dim newTransitions As TransitionCollection
        If _transitions Is Nothing Then
            newTransitions = New TransitionCollection()
            newTransitions.Add(New NavigationThemeTransition())
        Else
            newTransitions = _transitions
        End If

        Dim rootFrame As Frame = DirectCast(sender, Frame)
        rootFrame.ContentTransitions = newTransitions
        RemoveHandler rootFrame.Navigated, AddressOf RootFrame_FirstNavigated
    End Sub

    Private Sub OnSuspending(sender As Object, e As SuspendingEventArgs) Handles Me.Suspending
        Dim deferral As SuspendingDeferral = e.SuspendingOperation.GetDeferral()
        deferral.Complete()
    End Sub
End Class
