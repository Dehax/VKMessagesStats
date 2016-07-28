using MessagesStats;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VkNet;
using VkNet.Enums.Filters;
using VkNet.Model;
using VkNet.Model.RequestParams;

namespace MessagesStats
{
    public partial class MainWindow : Window
    {
        private const ulong APPLICATION_ID = 3460976;
        private const int BASE_YEAR = 2000;

        private VkApi _vk;

        private BackgroundWorker _worker;
        private long _userId;

        public MainWindow()
        {
            InitializeComponent();

            loginButton.Click += loginButton_Click;
            analyzeButton.Click += analyzeButton_Click;

            _vk = new VkApi();
            _worker = new BackgroundWorker();
            _worker.WorkerReportsProgress = true;
            _worker.WorkerSupportsCancellation = false;
            _worker.DoWork += _worker_DoWork;
            _worker.ProgressChanged += _worker_ProgressChanged;
            _worker.RunWorkerCompleted += _worker_RunWorkerCompleted;

            string accessToken = Properties.Settings.Default.AccessToken;
            long userId = Properties.Settings.Default.UserId;
            if (!string.IsNullOrEmpty(accessToken) && userId != 0)
            {
                _vk.Authorize(accessToken);

                if (_vk.IsAuthorized)
                {
                    loginButton.IsEnabled = false;
                    User user = _vk.Users.Get(userId, ProfileFields.FirstName | ProfileFields.LastName | ProfileFields.Photo100);
                    accountFullNameLabel.Content = $"{user.FirstName} {user.LastName}";
                    accountAvatarImage.Source = new BitmapImage(user.Photo100);
                }
            }
            else
            {
                loginButton.IsEnabled = true;
            }
        }

        private void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = AnalyzeMessages();
        }

        private void _worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar.Value = e.ProgressPercentage;
        }

        private void _worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!e.Cancelled)
            {
                logTextBox.Text = (string)e.Result;
                MessageBox.Show(this, "Анализ успешно завершён!", "Статистика собрана");
            }
        }

        private string AnalyzeMessages()
        {
            int[,] log = new int[100, 12];
            log.Initialize();

            int count = 200;
            int offset = 0;

            MessagesGetHistoryParams getHistoryParams = new MessagesGetHistoryParams()
            {
                UserId = _userId,
                Offset = offset,
                Count = count,
                Reversed = true
            };

            MessagesGetObject answer = _vk.Messages.GetHistory(getHistoryParams);
            Thread.Sleep(1000 / _vk.RequestsPerSecond + 1);

            while (offset + answer.Messages.Count <= answer.TotalCount)
            {
                foreach (Message message in answer.Messages)
                {
                    DateTime dateTime = message.Date.Value;

                    log[dateTime.Year - BASE_YEAR, dateTime.Month - 1]++;
                }

                _worker.ReportProgress((int)((offset + answer.Messages.Count) * 100 / (double)answer.TotalCount));

                offset += answer.Messages.Count;
                getHistoryParams.Offset = offset;
                answer = _vk.Messages.GetHistory(getHistoryParams);

                if (answer.Messages.Count == 0)
                {
                    break;
                }

                Thread.Sleep(1000 / _vk.RequestsPerSecond + 1);
            }

            StringBuilder sb = new StringBuilder();
            DateTime tempDateTime;

            for (int i = 13; i <= 16; i++)
            {
                sb.AppendLine($"{BASE_YEAR + i}");

                for (int j = 1; j <= 12; j++)
                {
                    tempDateTime = new DateTime(1, j, 1);
                    sb.Append(tempDateTime.ToString("MMMM"));
                    sb.Append(": ");
                    sb.Append(log[i, j - 1]);
                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void analyzeButton_Click(object sender, RoutedEventArgs e)
        {
            long userId;
            string userIdText = userIdTextBox.Text;

            if (long.TryParse(userIdText, out userId))
            {
                _userId = userId;
            }
            else
            {
                try
                {
                    Uri uri = new Uri(userIdText);
                    VkObject response = _vk.Utils.ResolveScreenName(uri.LocalPath.Substring(1));

                    if (response.Type != VkNet.Enums.VkObjectType.User || !response.Id.HasValue)
                    {
                        MessageBox.Show(this, "Введите URL, ник или ID пользователя ВК.", "Неправильная ссылка на пользователя");
                    }

                    _userId = response.Id.Value;
                }
                catch (UriFormatException)
                {
                    VkObject response = _vk.Utils.ResolveScreenName(userIdText);

                    if (response.Type != VkNet.Enums.VkObjectType.User || !response.Id.HasValue)
                    {
                        MessageBox.Show(this, "Введите URL, ник или ID пользователя ВК.", "Неправильный ник пользователя");
                    }

                    _userId = response.Id.Value;
                }
            }

            User user = _vk.Users.Get(_userId, ProfileFields.FirstName | ProfileFields.LastName | ProfileFields.Photo100);
            userFullNameLabel.Content = $"{user.FirstName} {user.LastName}";
            userAvatarImage.Source = new BitmapImage(user.Photo100);

            _worker.RunWorkerAsync();
        }

        private void loginButton_Click(object sender, RoutedEventArgs e)
        {
            AuthorizationWindow authWindow = new AuthorizationWindow();
            authWindow.ShowDialog();
            string accessToken = authWindow.AccessToken;
            long userId = authWindow.UserId;
            _vk.Authorize(accessToken);

            if (_vk.IsAuthorized)
            {
                Properties.Settings.Default.AccessToken = accessToken;
                Properties.Settings.Default.UserId = userId;
                Properties.Settings.Default.Save();
                loginButton.IsEnabled = false;
                User user = _vk.Users.Get(userId, ProfileFields.FirstName | ProfileFields.LastName | ProfileFields.Photo100);
                accountFullNameLabel.Content = $"{user.FirstName} {user.LastName}";
                accountAvatarImage.Source = new BitmapImage(user.Photo100);
                MessageBox.Show(this, "Вход успешно выполнен!", "Авторизован");
            }
        }
    }
}
