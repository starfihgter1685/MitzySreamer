using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using RestSharp;

namespace MitzyStreamer
{
    public partial class MainWindow : Window
    {
        private string server;
        private string user;
        private string password;
        private RestClient client = new RestClient();
        private MediaPlayer player = new MediaPlayer();

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            server = ServerBox.Text;
            user = UserBox.Text;
            password = PassBox.Password;

            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Bitte alle Felder ausfüllen.");
                return;
            }

            string url = $"{server}/rest/ping.view?u={user}&p=enc:{ConvertToHex(password)}&v=1.16.1&c=SubClient&f=json";
            var request = new RestRequest(url, Method.Get);

            try
            {
                var response = await client.ExecuteAsync(request);
                if (response.IsSuccessful && response.Content.Contains("\"status\":\"ok\""))
                {
                    MessageBox.Show("✅ Verbindung erfolgreich!");
                    await LoadArtists();
                }
                else
                {
                    MessageBox.Show("❌ Verbindung fehlgeschlagen!\nAntwort: " + response.Content);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("💥 Fehler: " + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task LoadArtists()
        {
            string url = $"{server}/rest/getArtists.view?u={user}&p=enc:{ConvertToHex(password)}&v=1.16.1&c=SubClient&f=json";
            var response = await client.ExecuteAsync(new RestRequest(url, Method.Get));
            dynamic data = JsonConvert.DeserializeObject(response.Content);

            ArtistList.Items.Clear();
            foreach (var index in data["subsonic-response"]?["artists"]?["index"])
            {
                foreach (var artist in index["artist"])
                {
                    ArtistList.Items.Add(new ListBoxItem { Content = artist["name"], Tag = artist["id"].ToString() });
                }
            }
        }

        private async void ArtistList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ArtistList.SelectedItem is ListBoxItem selected)
            {
                string artistId = selected.Tag.ToString();
                string url = $"{server}/rest/getArtist.view?id={artistId}&u={user}&p=enc:{ConvertToHex(password)}&v=1.16.1&c=SubClient&f=json";
                var response = await client.ExecuteAsync(new RestRequest(url, Method.Get));
                dynamic data = JsonConvert.DeserializeObject(response.Content);

                AlbumList.Items.Clear();
                TrackList.Items.Clear();

                foreach (var album in data["subsonic-response"]?["artist"]?["album"])
                {
                    AlbumList.Items.Add(new ListBoxItem { Content = album["name"], Tag = album["id"].ToString() });
                }
            }
        }

        private async void AlbumList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AlbumList.SelectedItem is ListBoxItem selected)
            {
                string albumId = selected.Tag.ToString();
                string url = $"{server}/rest/getMusicDirectory.view?id={albumId}&u={user}&p=enc:{ConvertToHex(password)}&v=1.16.1&c=SubClient&f=json";
                var response = await client.ExecuteAsync(new RestRequest(url, Method.Get));
                dynamic data = JsonConvert.DeserializeObject(response.Content);

                TrackList.Items.Clear();

                foreach (var track in data["subsonic-response"]?["directory"]?["child"])
                {
                    string contentType = track["contentType"];
                    if (contentType == "audio/mpeg" || contentType == "audio/flac")
                    {
                        TrackList.Items.Add(new ListBoxItem { Content = track["title"], Tag = track["id"].ToString() });
                    }
                }
            }
        }

        private void TrackList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TrackList.SelectedItem is ListBoxItem selected)
            {
                string trackId = selected.Tag.ToString();
                string streamUrl = $"{server}/rest/stream.view?id={trackId}&u={user}&p=enc:{ConvertToHex(password)}&v=1.16.1&c=SubClient";

                try
                {
                    player.Stop();
                    player.Open(new Uri(streamUrl));
                    player.Play();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Fehler beim Abspielen: " + ex.Message);
                }
            }
        }

        private string ConvertToHex(string input)
        {
            return string.Concat(System.Text.Encoding.UTF8.GetBytes(input).Select(b => b.ToString("x2")));
        }
    }
}