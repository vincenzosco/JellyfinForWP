Imports System.Collections.Generic
Imports JellyfinPlayer.Shared

<TestClass>
Public Class NextEpisodeTests
    Private Function Episode(id As String, season As Integer?, number As Integer?) As BaseItemDto
        Return New BaseItemDto With {.Id = id, .Type = "Episode", .ParentIndexNumber = season, .IndexNumber = number}
    End Function

    <TestMethod>
    Public Sub Picks_the_next_episode_in_the_same_season()
        Dim current = Episode("e1", 1, 1)
        Dim episodes As New List(Of BaseItemDto) From {
            Episode("e3", 1, 3), current, Episode("e2", 1, 2)
        }
        Assert.AreEqual("e2", NextEpisode.Find(current, episodes).Id)
    End Sub

    <TestMethod>
    Public Sub Jumps_to_the_first_episode_of_the_next_season()
        Dim current = Episode("e4", 1, 4)
        Dim episodes As New List(Of BaseItemDto) From {
            current, Episode("s2e2", 2, 2), Episode("s2e1", 2, 1)
        }
        Assert.AreEqual("s2e1", NextEpisode.Find(current, episodes).Id)
    End Sub

    <TestMethod>
    Public Sub Returns_nothing_after_the_last_episode()
        Dim current = Episode("e4", 1, 4)
        Assert.IsNull(NextEpisode.Find(current, New List(Of BaseItemDto) From {current}))
    End Sub

    <TestMethod>
    Public Sub Returns_nothing_without_episode_numbers()
        Dim current = Episode("e1", Nothing, Nothing)
        Dim episodes As New List(Of BaseItemDto) From {Episode("e2", 1, 2)}
        Assert.IsNull(NextEpisode.Find(current, episodes))
    End Sub

    <TestMethod>
    Public Sub Ignores_specials_and_items_before_the_current_one()
        Dim current = Episode("e2", 1, 2)
        Dim episodes As New List(Of BaseItemDto) From {
            Episode("special", 0, 1), Episode("e1", 1, 1), Episode("e4", 1, 4)
        }
        Assert.AreEqual("e4", NextEpisode.Find(current, episodes).Id)
    End Sub

    <TestMethod>
    Public Sub Nothing_in_nothing_out()
        Assert.IsNull(NextEpisode.Find(Nothing, New List(Of BaseItemDto)()))
        Assert.IsNull(NextEpisode.Find(Episode("e1", 1, 1), Nothing))
    End Sub
End Class
