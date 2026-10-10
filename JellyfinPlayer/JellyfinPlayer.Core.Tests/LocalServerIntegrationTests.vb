Imports System
Imports System.Collections.Generic
Imports JellyfinPlayer.Shared

' Read-only tests against a real Jellyfin server. Configure via
' test.settings.local or the JELLYFIN_TEST_* environment variables; they report
' Inconclusive when no server is configured and never mutate server state.
<TestClass>
Public Class LocalServerIntegrationTests
    Private _settings As TestServerSettings

    <TestInitialize>
    Public Sub Setup()
        _settings = TestServerSettings.Load()
    End Sub

    Private Function RequireConfigured() As Boolean
        If _settings.IsConfigured Then Return True
        Assert.Inconclusive("Jellyfin test server not configured (test.settings.local or JELLYFIN_TEST_* env vars).")
        Return False
    End Function

    Private Function MakeClient() As JellyfinApiClient
        Return New JellyfinApiClient(New HttpTransport(), New JellyfinUrls(_settings.Url), "integration-test")
    End Function

    Private Function AuthenticatedClient() As JellyfinApiClient
        Dim client = MakeClient()
        client.AuthenticateAsync(_settings.User, _settings.Password).GetAwaiter().GetResult()
        Return client
    End Function

    <TestMethod>
    <TestCategory("Integration")>
    Public Sub Server_is_reachable_and_identifies_itself()
        If Not RequireConfigured() Then Return
        Dim info = MakeClient().GetServerInfoAsync().GetAwaiter().GetResult()
        Assert.IsNotNull(info)
        Assert.IsFalse(String.IsNullOrEmpty(info.Id), "server Id should not be empty")
    End Sub

    <TestMethod>
    <TestCategory("Integration")>
    Public Sub Login_with_configured_credentials_returns_a_token()
        If Not RequireConfigured() Then Return
        Dim client = MakeClient()
        Try
            client.AuthenticateAsync(_settings.User, _settings.Password).GetAwaiter().GetResult()
        Catch ex As JellyfinException
            Assert.Fail("Authentication failed (HTTP " & ex.StatusCode & "): " & ex.Message)
        End Try
        Assert.IsFalse(String.IsNullOrEmpty(client.AccessToken), "AccessToken")
        Assert.IsFalse(String.IsNullOrEmpty(client.UserId), "UserId")
    End Sub

    <TestMethod>
    <TestCategory("Integration")>
    Public Sub Views_are_readable()
        If Not RequireConfigured() Then Return
        Dim client = AuthenticatedClient()
        Dim views = client.GetViewsAsync(client.UserId).GetAwaiter().GetResult()
        Assert.IsNotNull(views)
    End Sub

    <TestMethod>
    <TestCategory("Integration")>
    Public Sub Items_load_for_the_first_library()
        If Not RequireConfigured() Then Return
        Dim client = AuthenticatedClient()
        Dim views = client.GetViewsAsync(client.UserId).GetAwaiter().GetResult()
        If views Is Nothing OrElse views.Count = 0 Then
            Assert.Inconclusive("The server has no libraries to page through.")
            Return
        End If
        Dim q = client.GetItemsAsync(client.UserId, views(0).Id, "Movie,Series,Episode,Video,BoxSet", 0, 5).GetAwaiter().GetResult()
        Assert.IsNotNull(q)
        Assert.IsNotNull(q.Items)
        Assert.IsTrue(q.TotalRecordCount >= 0)
    End Sub

    <TestMethod>
    <TestCategory("Integration")>
    Public Sub Search_returns_results_for_an_existing_item_name()
        If Not RequireConfigured() Then Return
        Dim client = AuthenticatedClient()
        Dim views = client.GetViewsAsync(client.UserId).GetAwaiter().GetResult()

        Dim first As BaseItemDto = Nothing
        If views IsNot Nothing Then
            For Each library In views
                Dim q = client.GetItemsAsync(client.UserId, library.Id, "Movie,Series,Episode,Video,BoxSet", 0, 5).GetAwaiter().GetResult()
                If q IsNot Nothing AndAlso q.Items IsNot Nothing AndAlso q.Items.Count > 0 Then
                    first = q.Items(0)
                    Exit For
                End If
            Next
        End If

        If first Is Nothing OrElse String.IsNullOrEmpty(first.Name) Then
            Assert.Inconclusive("No item available to search for.")
            Return
        End If

        Dim results = client.SearchAsync(client.UserId, first.Name).GetAwaiter().GetResult()
        Assert.IsNotNull(results)
        Assert.IsNotNull(results.Items)
        Assert.IsTrue(results.Items.Count >= 1, "search for '" & first.Name & "' should return at least the item itself")
    End Sub

    <TestMethod>
    <TestCategory("Integration")>
    Public Sub Playback_info_returns_a_playable_source_for_the_first_video()
        If Not RequireConfigured() Then Return
        Dim client = AuthenticatedClient()
        Dim views = client.GetViewsAsync(client.UserId).GetAwaiter().GetResult()

        Dim video As BaseItemDto = Nothing
        If views IsNot Nothing Then
            For Each library In views
                Dim q = client.GetItemsAsync(client.UserId, library.Id, "Movie,Episode,Video", 0, 10).GetAwaiter().GetResult()
                If q IsNot Nothing AndAlso q.Items IsNot Nothing AndAlso q.Items.Count > 0 Then
                    video = q.Items(0)
                    Exit For
                End If
            Next
        End If

        If video Is Nothing Then
            Assert.Inconclusive("No video item available to test playback info.")
            Return
        End If

        Dim info = client.GetPlaybackInfoAsync(client.UserId, video.Id, 0L).GetAwaiter().GetResult()
        Assert.IsNotNull(info)
        Assert.IsNotNull(info.MediaSources)
        Assert.IsTrue(info.MediaSources.Count >= 1, "expected at least one media source")
        ' The server often returns only flags; PlaybackUrlResolver must still produce a URL.
        Dim source = info.MediaSources(0)
        Dim resolved = PlaybackUrlResolver.Resolve(_settings.Url, client.AccessToken, video.Id, source, 8000000)
        Assert.IsNotNull(resolved, "a playable URL should be resolvable for '" & video.Name & "'")
        Assert.IsFalse(String.IsNullOrEmpty(resolved.Url), "resolved URL should not be empty")
    End Sub

    <TestMethod>
    <TestCategory("Integration")>
    Public Sub Resume_list_is_readable()
        If Not RequireConfigured() Then Return
        Dim client = AuthenticatedClient()
        Dim list = client.GetResumeAsync(client.UserId, 5).GetAwaiter().GetResult()
        Assert.IsNotNull(list)
    End Sub
End Class
