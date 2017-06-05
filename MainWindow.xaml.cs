/// Yandere Simulator Launcher
/// By NoxCaos (http://noxcaos.github.io)
/// for 'Yandere Simulator' project
/// 
/// The launcher is available on GitHub 
/// by request and permission from YandereDeveloper.
/// 
/// This code is distributed under GPL license
/// 

using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Mega = CG.Web.MegaApiClient;

namespace YandereSimLauncher {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private const string BASE_LINK = "http://yanderesimulator.com/";
        private const string URLS_LINK = "http://yanderesimulator.com/urls.txt";
        //private const string URLS_LINK =    "http://localhost:3000/urls.txt";
        private const string NEWS_URL = "https://public-api.wordpress.com/rest/v1.1/sites/yanderedev.wordpress.com/posts/";
        private const string ZIP_NAME = "content.zip";
        private const int VERSION = 6;

        private enum GameStatus { Updated, Outdated, NotDownloaded, ContentError }

        private enum LinkType {
            version, newlauncher, launcher,
            wordpress, youtube, twitter, twitch,
            volunteer, contact, about, donate
        }

        private Dictionary<LinkType, string> Links = new Dictionary<LinkType, string>() {
            { LinkType.version,     "http://yanderesimulator.com/version.txt" },
            { LinkType.launcher,    "http://yanderesimulator.com/launcherversion.txt" },
            { LinkType.newlauncher, "http://yanderesimulator.com/download/" },

            { LinkType.wordpress,   "http://yanderedev.wordpress.com/" },
            { LinkType.youtube,     "https://www.youtube.com/channel/UC1EBJfK7ltjYUFyzysKxr1g" },
            { LinkType.twitter,     "https://www.twitter.com/YandereDev" },
            { LinkType.twitch,      "https://www.twitch.tv/yanderedev" },

            { LinkType.volunteer,   "http://yanderesimulator.com/volunteer/" },
            { LinkType.contact,     "http://yanderesimulator.com/contact/" },
            { LinkType.about,       "http://yanderesimulator.com/about/" },
            { LinkType.donate,      "http://yanderesimulator.com/donate/" },
        };

        private List<string> contentLinks = new List<string>();

        private WebClient webClient;
        private Mega.MegaApiClient megaClient;
        private string gamePath;
        private long newGameVersion;
        private int curLink;
        private Thread launcherThread;
        private bool isAppClosed;
        private bool isLinkUpdated;
        private FileStream fileStream;
        private ProgressionStream megaStream;
        private Action messageCloseCallback;

        //private List<Post> posts;

        public MainWindow() {
            InitializeComponent();
            webClient = new WebClient();
            gamePath = AppDomain.CurrentDomain.BaseDirectory;

            //Uncomment this for release version
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                Properties.Settings.Default.Crashed = true;

                using (var writer = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "launcherCrashDump.txt", true)) {
                    writer.WriteLine(DateTime.Today.ToShortDateString() + " at " + DateTime.Now.ToString("HH:mm:ss"));
                    writer.WriteLine(e.ExceptionObject.ToString());
                    writer.WriteLine("---------------------------------------\n");
                }

                MessageBox.Show(@"Please report a bug on 'gleb.noxcaos@gmail.com' and attach 'launcherCrashDump.txt' that appeared near launcher.
                    This is THE ONLY ONE address, that accepts launcher errors. 
                    Please, don't send reports directly to Yanderedev.", 
                    "Fatal error");
                var box = MessageBox.Show("Do you want to try in-browser download? Launcher will open a download page for you", "Hey", MessageBoxButton.YesNo);

                if (box == MessageBoxResult.Yes && contentLinks != null && contentLinks.Count > 0)
                    Process.Start(contentLinks[0]);

                Process.Start(AppDomain.CurrentDomain.BaseDirectory);
                Application.Current.Shutdown();
            };
            
        }

        private void MegaInit() {
            megaClient = new Mega.MegaApiClient();
            megaClient.LoginAnonymous();
        }

        private void ShowMessage(string body, string title, Action clb = null) {
            Dispatcher.Invoke(new Action(() => {
                MessageHeader.Text = title;
                MessageBody.Text = body;
                messageCloseCallback = clb;

                var e = (BlurEffect)MainGrid.Effect;
                e.Radius = 15;
                MessageGrid.Visibility = Visibility.Visible;
            }));            
        }

        #region Events
        private void WindowMouseDown(object sender, MouseButtonEventArgs e) {
            try { DragMove(); } catch { }
        }

        private void OnMessageOKClick(object sender, RoutedEventArgs e) {
            ((BlurEffect)MainGrid.Effect).Radius = 0;
            MessageGrid.Visibility = Visibility.Collapsed;
            if (messageCloseCallback != null) messageCloseCallback();
            messageCloseCallback = null;
        }

        private void OnBugReportClick(object sender, RoutedEventArgs e) {
            var message = "To report a bug: \n\n"
                + " 1. Describe exactly what happens and what isn't working properly. \n"
                + " 2. Describe the steps that should be taken to reproduce the issue. \n"
                + " 3. If possible, provide a screenshot of the problem. \n\n";

            ShowMessage(message, "Bug Reporting");
            MessageBody.Inlines.Add(new Bold(new Run("Submit your report to gleb.noxcaos@gmail.com \n")));
            MessageBody.Inlines.Add(
                "That is the ONLY e-mail address that accepts bug reports about the launcher. Please do not report launcher bugs to YandereDev.");
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e) {
            try {
                launcherThread.Abort();
                isAppClosed = true;
                megaClient.Logout();
                if (megaStream != null) megaStream.Close();
                if (fileStream != null) fileStream.Close();
            } catch { }

            Application.Current.Shutdown();
        }

        private void OnMinimizeClick(object sender, MouseButtonEventArgs e) {
            WindowState = WindowState.Minimized;
        }

        private void OnSocialButtonClick(object sender, MouseButtonEventArgs e) {
            var netw = ((Image)sender).Name.Split('_')[1];
            Process.Start(Links[GetLinkType(netw)]);
        }

        private void OnPlayButtonClick(object sender, RoutedEventArgs e) {
            try {
                Process.Start(gamePath + "YandereSimulator.exe");
                launcherThread.Abort();
                isAppClosed = true;
                if (megaStream != null) megaStream.Close();
                if (fileStream != null) fileStream.Close();
                Application.Current.Shutdown();
            } catch (Win32Exception) { ReportStatus("Can't launch the game"); }
        }

        private void OnRedownloadClick(object sender, RoutedEventArgs e) {
            try {
                RedownloadButton.IsEnabled = false;
                PlayButton.IsEnabled = false;
                CleanUp();
                var t = new Thread(() => StartCheckThread(true));
                t.Start();
            } catch { }
        }

        private void OnHeadTitleClick(object sender, MouseButtonEventArgs e) {
            try {
                var block = (TextBlock)sender;
                var parts = block.Name.Split('_');
                if (block.Name == "headText_Blog") {
                    Process.Start(Links[LinkType.wordpress]);
                } else {
                    Process.Start(Links[GetLinkType(parts[1])]);
                }
            } catch (ArgumentOutOfRangeException) {
                //Error in block name
            } catch (ArgumentException) {
                //The string is not convertable to enum
            }
        }

        private void OnGameBugReportClick(object sender, RoutedEventArgs e) {
            Process.Start(BASE_LINK + "bug-reporting/");
        }
        #endregion

        #region Static
        public static string GetData(string url) {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            WebRequest reqGET = WebRequest.Create(url);
            WebResponse resp = reqGET.GetResponse();
            Stream stream = resp.GetResponseStream();
            StreamReader sr = new StreamReader(stream);
            string html = sr.ReadToEnd();
            return html;
        }

        public static void DeleteDirectory(string target_dir) {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files) {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs) {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }

        public static string BytesToMega(long bytes) {
            return (bytes / 1000000.0).ToString("0.00");
        }

        private static LinkType GetLinkType(string str) {
            return (LinkType)Enum.Parse(typeof(LinkType), str.Trim().ToLower());
        }
        #endregion

        #region Animations
        private void OnButtonHover(object sender, MouseEventArgs e) {
            ScaleTransform rt = new ScaleTransform();
            Image obj = (Image)sender;
            obj.RenderTransform = rt;
            obj.RenderTransformOrigin = new Point(0.5, 0.5);
            DoubleAnimation da = new DoubleAnimation(1, 1.25, new Duration(TimeSpan.FromSeconds(0.2)));
            da.FillBehavior = FillBehavior.HoldEnd;

            rt.BeginAnimation(ScaleTransform.ScaleXProperty, da);
            rt.BeginAnimation(ScaleTransform.ScaleYProperty, da);
        }

        private void OnButtonLeave(object sender, MouseEventArgs e) {
            ScaleTransform rt = new ScaleTransform();
            Image obj = (Image)sender;
            obj.RenderTransform = rt;
            obj.RenderTransformOrigin = new Point(0.5, 0.5);
            DoubleAnimation da = new DoubleAnimation(1.25, 1, new Duration(TimeSpan.FromSeconds(0.2)));
            da.FillBehavior = FillBehavior.Stop;

            rt.BeginAnimation(ScaleTransform.ScaleXProperty, da);
            rt.BeginAnimation(ScaleTransform.ScaleYProperty, da);
        }

        private void OnTextLeave(object sender, MouseEventArgs e) {
            var block = (TextBlock)sender;
            block.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        }

        private void OnTextEnter(object sender, MouseEventArgs e) {
            var text = (TextBlock)sender;
            text.Foreground = new SolidColorBrush(Color.FromArgb(255, 127, 13, 100));
        }

        private void OnPostTitleClick(object sender, MouseButtonEventArgs e) {
            var block = (TextBlock)sender;
            block.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 125, 205));
        }


        #endregion

        #region Logic
        private GameStatus GetGameStatus() {
            try {
                var serverVersion = GetData(Links[LinkType.version]);
                newGameVersion = long.Parse(serverVersion);
            } catch (OverflowException) {
                ReportStatus("System: Wrong version on server");
            } catch (FormatException) {
                ReportStatus("System: Wrong version on server");
            } catch {
                ReportStatus("Error: Check your connection");
            }

            try {
                var executable = Directory.EnumerateFiles(gamePath, "YandereSimulator.exe").ElementAt(0);
                var content = Directory.EnumerateDirectories(gamePath, "YandereSimulator_Data").ElementAt(0);
                var clientVersion = File.ReadAllText(content + "\\version");
                
                if (newGameVersion != long.Parse(clientVersion))
                    return GameStatus.Outdated;
                else return GameStatus.Updated;

            } catch { return GameStatus.NotDownloaded; }
        }

        private void CleanUp() {
            try {
                var executable = Directory.EnumerateFiles(gamePath, "YandereSimulator.exe");
                var content = Directory.EnumerateDirectories(gamePath, "YandereSimulator_Data");
                if (executable.Any()) File.Delete(executable.ElementAt(0));
                if (content.Any()) DeleteDirectory(content.ElementAt(0));
            } catch (IOException) {
                ShowMessage("Looks like you're running Yandere Simulator. Please close the game and try again.", "Ooops!",
                    delegate {
                        if (megaStream != null) megaStream.Close();
                        if (fileStream != null) fileStream.Close();
                        Application.Current.Shutdown();
                    });
            }
        }

        private void DownloadGameContents() {
            if (contentLinks.Count > curLink) {
                ReportStatus("Starting download");

                try { if (File.Exists(gamePath + ZIP_NAME)) File.Delete(gamePath + ZIP_NAME); } catch {
                    ShowMessage("Internal error happened. Launcher needs to be restarted", "Ooops!", 
                        delegate {
                            if (megaStream != null) megaStream.Close();
                            if (fileStream != null) fileStream.Close();
                            Application.Current.Shutdown();
                        });
                }

                try {
                    if (megaClient != null && contentLinks[curLink].Contains("mega")) {
                        string filePath = gamePath + ZIP_NAME;
                        fileStream = new FileStream(filePath, FileMode.Create);

                        try {
                            using (megaStream = new ProgressionStream(megaClient.Download(new Uri(contentLinks[curLink])), PrintProgression)) {
                                megaStream.CopyTo(fileStream);
                            }
                        } catch (Exception) { TryNextLink(); }
                    } else {
                        try {
                            webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(UpdateProgressBar);
                            webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadDataCompleted);
                            webClient.DownloadFileAsync(new Uri(contentLinks[curLink]), gamePath + ZIP_NAME);
                        } catch (SocketException) { TryNextLink(); }
                    }
                } catch (IOException e) {
                    ShowMessage("The following error happened while download: " + e.Message
                        + "Try moving launcher to 'C:\\Program Files'. Also check if firewall doesn't block access to internet", "Error");
                }
            } else {
                if (File.Exists(gamePath + ZIP_NAME)) {
                    Dispatcher.Invoke(new Action(() => {
                        RedownloadButton.IsEnabled = true;
                        PlayButton.IsEnabled = false;
                    }));
                    ShowMessage("Try moving launcher to 'C:\\Program Files'. Also check if firewall doesn't block access to internet", "Invalid permissions");
                } else {
                    SetServerUnavailableStatus("Can't reach download source");
                    ShowMessage("Can't reach download source. Maybe it is unavailable now or your antivirus blocks the connection. "
                        +"Try moving launcher to 'C:\\Program Files' it may fix the access problems", "Ooops!");
                }
            }
        }

        private void TryNextLink() {
            curLink++;
            if (launcherThread != null & launcherThread.IsAlive) launcherThread.Abort();
            launcherThread = new Thread(StartCheckingInternetConnection);
            launcherThread.Start();
        }

        private void PrintProgression(double progression) {
            ReportProgress(progression);
            ReportStatus(string.Format("Downloaded {0}%", progression.ToString("0.00")));

            if (progression >= 100) {
                fileStream.Flush();
                fileStream.Close();
                ExtractFiles();
            }
        }

        private void DownloadDataCompleted(object sender, AsyncCompletedEventArgs e) {
            CleanUp();
            webClient.DownloadProgressChanged -= new DownloadProgressChangedEventHandler(UpdateProgressBar);
            webClient.DownloadFileCompleted -= new AsyncCompletedEventHandler(DownloadDataCompleted);

            ExtractFiles();
        }

        private void ExtractFiles() {
            try { GetData("http://simplehitcounter.com/hit.php?uid=2101053&f=16777215&b=0"); } catch { }

            ReportStatus("Extracting...");
            try {
                using (var arc = ZipFile.Read(gamePath + ZIP_NAME)) {
                    arc.ExtractAll(gamePath, ExtractExistingFileAction.OverwriteSilently);
                }
                File.Delete(gamePath + ZIP_NAME);
            } catch {
                ReportStatus("Error. Retrying...");
                curLink++;
                if (launcherThread != null & launcherThread.IsAlive) launcherThread.Abort();
                launcherThread = new Thread(StartCheckingInternetConnection);
                launcherThread.Start();
                return;
            }

            try {
                File.WriteAllText(gamePath + "YandereSimulator_Data\\" + "version", newGameVersion.ToString());
                SetReadyStatus();
            } catch {
                Dispatcher.Invoke(new Action(() => {
                    RedownloadButton.IsEnabled = true;
                    PlayButton.IsEnabled = false;
                }));
                ShowMessage("Try moving launcher to 'C:\\Program Files'. Also check if firewall doesn't block access to internet", "Invalid permissions");
            }
        }

        private void UpdateProgressBar(object sender, DownloadProgressChangedEventArgs e) {
            ReportProgress(e.ProgressPercentage);
            ReportStatus(string.Format("Downloaded {0}mb of {1}mb", 
                BytesToMega(e.BytesReceived),
                BytesToMega(e.TotalBytesToReceive)
                ));
        }

        private void StartCheckThread(bool force = false) {
            ReportStatus("Checking game version...");

            var status = GetGameStatus();
            if (force && status == GameStatus.Outdated) {
                DownloadGameContents();
                return;
            }

            switch (status) {
                case GameStatus.NotDownloaded:  DownloadGameContents();     break;
                case GameStatus.Outdated:       SetReadyToUpdateStatus();   break;
                case GameStatus.Updated:        SetReadyStatus();           break;
                default:                        DownloadGameContents();     break;
            }
        }

        private void UpdateLinks() {
            try {
                var links = GetData(URLS_LINK).Split('\n');

                foreach (var l in links) {
                    try {
                        var lnk = l.Split('"');

                        if (lnk[0].Trim().StartsWith("contents")) {
                            contentLinks.Add(GetLink(lnk[1]));

                        } else Links[GetLinkType(lnk[0])] = GetLink(lnk[1]);

                    } catch (Exception) { continue; }
                }
                isLinkUpdated = true;

            } catch (WebException) { }
        }

        private string GetLink(string inp) {
            var newLink = inp.Trim();
            if (!newLink.StartsWith("http")) {
                newLink = "http://" + newLink;
            }
            return newLink;
        }

        private bool IsLauncherUpdated() {
            int ver = 0;
            try {
                var launcherVersion = GetData(Links[LinkType.launcher]);
                ver = int.Parse(launcherVersion);
            } catch (FormatException) { return true; }

            return ver == VERSION;
        }

        private void StartCheckingInternetConnection() {
            ReportStatus("Checking server connection...");
            try {
                GetData(BASE_LINK);
                if (!isLinkUpdated) UpdateLinks();
                if (!IsLauncherUpdated()) {
                    ShowMessage("This launcher is now obsolete. A new launcher is now available. Please download the new launcher from the official website!", "Outdated version",
                        delegate {
                            Process.Start(Links[LinkType.newlauncher]);
                            if (megaStream != null) megaStream.Close();
                            if (fileStream != null) fileStream.Close();
                            Application.Current.Shutdown();
                        });
                    return;
                }

                MegaInit();

                var t = new Thread(() => StartCheckThread());
                t.Start();
            } catch (WebException) {
                SetServerUnavailableStatus("Can't connect to update server");
                Thread.Sleep(5000);
                if (!isAppClosed) {
                    launcherThread = new Thread(StartCheckingInternetConnection);
                    launcherThread.Start();
                }
            } catch (SocketException) {
                if (File.Exists(gamePath + ZIP_NAME)) {
                    Dispatcher.Invoke(new Action(() => {
                        RedownloadButton.IsEnabled = true;
                        PlayButton.IsEnabled = false;
                    }));
                    ShowMessage("Try moving launcher to 'C:\\Program Files'. Also check if firewall doesn't block access to internet", "Invalid permissions");
                } else {
                    SetServerUnavailableStatus("Can't connect to update server");
                    ShowMessage("Can't connect. Looks like your antivirus blocks the connection. Please add the app to exclusions or turn the antivirus off", "Ooops!");
                }
            } catch(Exception) {
                SetServerUnavailableStatus("Can't connect to update server");
                ShowMessage("The connection is blocked. Try moving launcher to C:\\ProgramFiles folder and try again", "Ooops!");
            }
        }

        private void SetServerUnavailableStatus(string serverStatus) {
            ReportStatus(serverStatus);
            try {
                var executable = Directory.EnumerateFiles(gamePath, "YandereSimulator.exe").ElementAt(0);
                var content = Directory.EnumerateDirectories(gamePath, "YandereSimulator_Data").ElementAt(0);
                Dispatcher.Invoke(new Action(() => {
                    PlayButton.IsEnabled = true;
                }));
            } catch {
                Dispatcher.Invoke(new Action(() => {
                    RedownloadButton.IsEnabled = false;
                    PlayButton.IsEnabled = false;
                }));
            }
        }

        private void ReportProgress(double progress) {
            Dispatcher.Invoke(new Action(() => {
                ProgressBar.Value = progress;
            }));
        }

        private void ReportStatus(string status) {
            Dispatcher.Invoke(new Action(() => {
                DownloadStatus.Text = status;
            }));
        }

        private void SetReadyToUpdateStatus() {
            Dispatcher.Invoke(new Action(() => {
                PlayButton.IsEnabled = true;
                RedownloadButton.IsEnabled = true;
                RedownloadButton.Content = "Update now";
                RedownloadButton.ToolTip = "Update to new version";
                DownloadStatus.Text = "New version is available!";
                ProgressBar.Value = 0;
            }));
        }

        private void SetReadyStatus() {
            Dispatcher.Invoke(new Action(() => {
                PlayButton.IsEnabled = true;
                RedownloadButton.IsEnabled = true;
                RedownloadButton.Content = "Force re-download";
                RedownloadButton.ToolTip = "Re-download the game in case it doesn't work";
                DownloadStatus.Text = "Game is up-to-date";
                ProgressBar.Value = 100;
            }));
        }
        #endregion

        /// <summary>
        /// MAIN PROCEDURE
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWindowLoaded(object sender, RoutedEventArgs e) {
            RedownloadButton.IsEnabled = false;
            PlayButton.IsEnabled = false;

            //if (Properties.Settings.Default.Crashed) 

            launcherThread = new Thread(StartCheckingInternetConnection);
            launcherThread.Start();
        }
    }
}
