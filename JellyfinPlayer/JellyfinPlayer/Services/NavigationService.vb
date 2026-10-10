Imports System
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls

Namespace Services
    Public Module NavigationService
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

        Public ReadOnly Property CanGoBack As Boolean
            Get
                Dim f = RootFrame
                Return f IsNot Nothing AndAlso f.CanGoBack
            End Get
        End Property
    End Module
End Namespace
