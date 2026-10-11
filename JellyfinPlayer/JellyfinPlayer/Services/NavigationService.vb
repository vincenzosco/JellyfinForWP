Imports System
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls

Namespace Services
    Public Module NavigationService
        ' Set by a page that wants the hardware back button for itself before the
        ' frame handles it - the player uses it to leave fullscreen first.
        ' True means the page consumed the press.
        Public BackInterceptor As Func(Of Boolean)

        Public ReadOnly Property RootFrame As Frame
            Get
                Return TryCast(Window.Current.Content, Frame)
            End Get
        End Property

        Public Sub Navigate(pageType As Type, parameter As Object)
            Dim f = RootFrame
            If f IsNot Nothing Then f.Navigate(pageType, parameter)
        End Sub

        Public Sub GoBack()
            Dim f = RootFrame
            If f IsNot Nothing AndAlso f.CanGoBack Then f.GoBack()
        End Sub

        ' A switch of account must not leave the previous user's screens behind
        ' it: going back would walk into another account's library.
        Public Sub ClearBackStack()
            Dim f = RootFrame
            If f IsNot Nothing Then f.BackStack.Clear()
        End Sub

        Public ReadOnly Property CanGoBack As Boolean
            Get
                Dim f = RootFrame
                Return f IsNot Nothing AndAlso f.CanGoBack
            End Get
        End Property
    End Module
End Namespace
