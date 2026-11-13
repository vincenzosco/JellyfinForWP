using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using JellyfinPlayer.Shared;
using JellyfinPlayer.Services;
using Microsoft.Phone.Controls;

namespace JellyfinPlayer
{
    /// <summary>
    /// Server connect / login page. Auto-detects the server when no address is saved.
    /// </summary>
    public partial class MainPage : PhoneApplicationPage
    {
        public MainPage()
        {
            InitializeComponent();
            ServerAddressLabel.Text = Localized.Text("ServerAddressLabel.Text");
            UsernameLabel.Text = Localized.Text("UsernameLabel.Text");
            PasswordLabel.Text = Localized.Text("PasswordLabel.Text");
            ConnectButton.Content = Localized.Text("ConnectButton.Content");
        }

        // Set when the page was opened to sign in again for one saved account.
        private AccountDto _reLogin;

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _reLogin = Services.NavigationService.ConsumePending() as AccountDto;
            if (_reLogin != null)
            {
                // Signing in again for one saved account: the address and the user are
                // known, only the password is missing, and nothing is discovered.
                ServerBox.Text = _reLogin.BaseUrl;
                ServerBox.IsEnabled = false;
                UserBox.Text = AccountItem.DisplayName(_reLogin);
                DiscoveryStatus.Text = Localized.Format("PasswordRequiredStatus", AccountItem.DisplayName(_reLogin));
                return;
            }

            var session = AppSession.Current;
            if (!string.IsNullOrEmpty(session.BaseUrl))
            {
                ServerBox.Text = session.BaseUrl;
                DiscoveryStatus.Text = Localized.Text("StatusUsingSavedServer");
                return;
            }

            ServerBox.Text = "http://";
            await DiscoverAsync();
        }

        // Fills the address field from the first UDP reply. When nothing answers, or
        // several servers do, the user keeps (or still has) manual control.
        private async Task DiscoverAsync()
        {
            DiscoveryStatus.Text = Localized.Text("DiscoverySearching");
            try
            {
                var found = await UdpServerDiscovery.DiscoverAsync(2500);

                AppLog.Info("Discovery found " + (found == null ? 0 : found.Count) + " server(s)");
                if (found == null || found.Count == 0)
                {
                    DiscoveryStatus.Text = Localized.Text("DiscoveryNone");
                    return;
                }

                // 7359 answers with anything that cares to reply, so the address is only
                // filled in once it has answered as a Jellyfin server.
                var session = AppSession.Current;
                var server = await ServerProbe.FindFirstJellyfinAsync(
                    found, baseUrl => new JellyfinApiClient(new HttpTransport(), new JellyfinUrls(baseUrl), session.DeviceId));

                if (server == null)
                {
                    DiscoveryStatus.Text = Localized.Text("DiscoveryRejected");
                    return;
                }

                ServerBox.Text = server.Address;
                if (found.Count == 1)
                {
                    DiscoveryStatus.Text = Localized.Format("DiscoveryFound", server.Name);
                }
                else
                {
                    DiscoveryStatus.Text = Localized.Format("DiscoveryFoundMany", found.Count, server.Name);
                }
            }
            catch (Exception ex)
            {
                DiscoveryStatus.Text = Localized.Format("DiscoveryFailed", ex.Message);
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var baseUrl = ServerBox.Text.Trim();
            if (string.IsNullOrEmpty(baseUrl))
            {
                StatusText.Text = Localized.Text("StatusEnterAddress");
                return;
            }
            if (!baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) baseUrl = "http://" + baseUrl;

            Busy.Visibility = Visibility.Visible;
            ConnectButton.IsEnabled = false;
            StatusText.Text = "";

            try
            {
                var session = AppSession.Current;
                var urls = new JellyfinUrls(baseUrl);
                var client = new JellyfinApiClient(new HttpTransport(), urls, session.DeviceId);

                var info = await client.GetServerInfoAsync();
                if (info == null) throw new JellyfinException(Localized.Text("StatusNotJellyfin"), 0);

                // An older server reads the legacy auth spellings; 12 refuses them.
                bool legacy = ServerCompatibility.IsLegacy(info.Version);
                urls.LegacyAuth = legacy;
                AppLog.Info("Server " + urls.BaseUrl + " version " + info.Version + (legacy ? " (legacy auth)" : ""));

                var result = await client.AuthenticateAsync(UserBox.Text.Trim(), PassBox.Password);
                if (result == null || string.IsNullOrEmpty(result.AccessToken))
                {
                    AppLog.Error("Login refused for " + UserBox.Text.Trim() + " on " + urls.BaseUrl);
                    StatusText.Text = Localized.Text("StatusWrongCredentials");
                    return;
                }

                // The name is what the picker shows, so it is stored with the token.
                session.SignIn(urls.BaseUrl, client.UserId, result.User == null ? null : result.User.Name,
                               client.AccessToken, client.ServerId, info.ServerName, info.Version);
                AppLog.Info("Logged in as " + UserBox.Text.Trim());

                // Entering a different account must not leave the previous one's
                // screens in the back stack.
                Services.NavigationService.ClearBackStack();
                Services.NavigationService.Navigate(typeof(HomePage), null);
            }
            catch (JellyfinException ex)
            {
                AppLog.Error("Login failed", ex);
                StatusText.Text = ex.StatusCode == 401 ? Localized.Text("StatusWrongCredentials") : Localized.DescribeError(ex);
            }
            catch (Exception ex)
            {
                AppLog.Error("Login failed", ex);
                StatusText.Text = Localized.DescribeError(ex);
            }
            finally
            {
                Busy.Visibility = Visibility.Collapsed;
                ConnectButton.IsEnabled = true;
            }
        }
    }
}
