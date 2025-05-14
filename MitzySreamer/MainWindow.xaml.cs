using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
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
        private int currentTrackIndex = -1;
        private bool isPlaying = false;
        private DispatcherTimer progressTimer;
        private TimeSpan currentSongDuration = TimeSpan.Zero;
        private DateTime songStartTime;
        private string currentSongName = "";
        private bool isShuffleEnabled = false;
        private bool isManualSelection = false;


        private readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MitzyStreamer",
            "settings.json"
        );

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            progressTimer = new DispatcherTimer();
            progressTimer.Interval = TimeSpan.FromSeconds(1);
            progressTimer.Tick += ProgressTimer_Tick;
        
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
                    SaveSettings();
                    await LoadArtists();
                    await LoadPlaylists();
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
        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            isShuffleEnabled = !isShuffleEnabled;
            ShuffleButton.Content = isShuffleEnabled ? "Shuffle: Ein" : "Shuffle: Aus";
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

        private async System.Threading.Tasks.Task LoadPlaylists()
        {
            string url = $"{server}/rest/getPlaylists.view?u={user}&p=enc:{ConvertToHex(password)}&v=1.16.1&c=SubClient&f=json";
            var response = await client.ExecuteAsync(new RestRequest(url, Method.Get));
            dynamic data = JsonConvert.DeserializeObject(response.Content);

            PlaylistList.Items.Clear();

            foreach (var playlist in data["subsonic-response"]?["playlists"]?["playlist"])
            {
                PlaylistList.Items.Add(new ListBoxItem
                {
                    Content = playlist["name"].ToString(),
                    Tag = playlist["id"].ToString()
                });
            }
        }

        private async void PlaylistList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlaylistList.SelectedItem is ListBoxItem selected)
            {
                string playlistId = selected.Tag.ToString();
                string url = $"{server}/rest/getPlaylist.view?id={playlistId}&u={user}&p=enc:{ConvertToHex(password)}&v=1.16.1&c=SubClient&f=json";
                var response = await client.ExecuteAsync(new RestRequest(url, Method.Get));
                dynamic data = JsonConvert.DeserializeObject(response.Content);

                TrackList.Items.Clear();

                foreach (var entry in data["subsonic-response"]?["playlist"]?["entry"])
                {
                    if ((string)entry["contentType"] == "audio/mpeg" || (string)entry["contentType"] == "audio/flac")
                    {
                        var track = new TrackData
                        {
                            Id = entry["id"].ToString(),
                            Title = entry["title"].ToString(),
                            Duration = entry["duration"] != null
                                ? TimeSpan.FromSeconds((int)entry["duration"])
                                : TimeSpan.FromMinutes(3)
                        };

                        TrackList.Items.Add(new ListBoxItem
                        {
                            Content = track.Title,
                            Tag = track
                        });
                    }
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
            if (TrackList.SelectedItem is ListBoxItem selectedItem)
            {
                // Eventhandler temporär entfernen
                TrackList.SelectionChanged -= TrackList_SelectionChanged;

                int index = TrackList.Items.IndexOf(selectedItem);
                isManualSelection = true;
                PlayTrackByIndex(index);
                isManualSelection = false;

                if (isShuffleEnabled)
                {
                    ShuffleTrackListPreserveCurrent();
                }

                // Eventhandler wieder hinzufügen
                TrackList.SelectionChanged += TrackList_SelectionChanged;
            }
        }
        private void ShuffleTrackListPreserveCurrent()
        {
            if (TrackList.SelectedIndex < 0) return;

            var currentItem = TrackList.SelectedItem;
            var items = TrackList.Items.Cast<ListBoxItem>().ToList();
            var rng = new Random();

            // Entferne das aktuell gespielte Lied
            items.Remove((ListBoxItem)currentItem);

            // Shuffle den Rest
            items = items.OrderBy(x => rng.Next()).ToList();

            // Aktuelles Lied wieder vorne einfügen
            items.Insert(0, (ListBoxItem)currentItem);

            // Liste updaten
            TrackList.Items.Clear();
            foreach (var item in items)
            {
                TrackList.Items.Add(item);
            }

            // Wieder auf das aktuelle setzen
            TrackList.SelectedIndex = 0;
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (isPlaying)
            {
                player.Pause();
                isPlaying = false;
            }
            else
            {
                if (player.Source != null)
                {
                    player.Play();
                    isPlaying = true;
                }
                else if (TrackList.Items.Count > 0)
                {
                    currentTrackIndex = 0;
                    PlayTrackByIndex(currentTrackIndex);
                }
            }
        }

        private void PrevTrack_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrackIndex > 0)
            {
                currentTrackIndex--;
                PlayTrackByIndex(currentTrackIndex);
            }
        }

        private void NextTrack_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrackIndex < TrackList.Items.Count - 1)
            {
                currentTrackIndex++;
                PlayTrackByIndex(currentTrackIndex);
            }
        }

        private void PlayTrackByIndex(int index)
        {
            if (index >= 0 && index < TrackList.Items.Count)
            {
                var selected = (ListBoxItem)TrackList.Items[index];
                TrackList.SelectedIndex = index;

                var trackData = (TrackData)selected.Tag;

                string streamUrl = $"{server}/rest/stream.view?id={trackData.Id}&u={user}&p=enc:{ConvertToHex(password)}&v=1.16.1&c=SubClient";

                try
                {
                    player.Stop();
                    player.Open(new Uri(streamUrl));
                    player.Play();
                    isPlaying = true;

                    currentSongName = trackData.Title;
                    songStartTime = DateTime.Now;
                    currentSongDuration = trackData.Duration;

                    NowPlayingText.Text = $"Spiele: {currentSongName}";
                    ElapsedText.Text = "00:00";
                    DurationText.Text = currentSongDuration.ToString(@"mm\:ss");

                    SongProgress.Minimum = 0;
                    SongProgress.Maximum = currentSongDuration.TotalSeconds;
                    SongProgress.Value = 0;

                    progressTimer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Fehler beim Abspielen: " + ex.Message);
                }
            }
        }


        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            var elapsed = DateTime.Now - songStartTime;

            if (elapsed >= currentSongDuration)
            {
                progressTimer.Stop();
                SongProgress.Value = currentSongDuration.TotalSeconds;
                ElapsedText.Text = currentSongDuration.ToString(@"mm\:ss");
                // Du kannst hier z. B. automatisch den nächsten Song starten lassen
                return;
            }

            SongProgress.Value = elapsed.TotalSeconds;
            ElapsedText.Text = elapsed.ToString(@"mm\:ss");
        }

        private void SaveSettings()
        {
            var settings = new UserSettings
            {
                Server = server,
                User = user,
                Password = password
            };

            var dir = Path.GetDirectoryName(SettingsFilePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(SettingsFilePath, JsonConvert.SerializeObject(settings));
        }

        private async void LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonConvert.DeserializeObject<UserSettings>(json);

                server = settings.Server;
                user = settings.User;
                password = settings.Password;

                ServerBox.Text = server;
                UserBox.Text = user;
                PassBox.Password = password;

                await System.Threading.Tasks.Task.Delay(100); // Kurzer Delay für UI-Init
                Login_Click(null, null); // Auto-Login ausführen
            }
        }

        private string ConvertToHex(string input)
        {
            return string.Concat(System.Text.Encoding.UTF8.GetBytes(input).Select(b => b.ToString("x2")));
        }
    }
}
