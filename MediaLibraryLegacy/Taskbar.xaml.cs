﻿using SharedCode.SQLite;
using SharedCode.YT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using YoutubeExplode;

namespace MediaLibraryLegacy
{
    public sealed partial class Taskbar : UserControl
    {
        string mediaPath;
        private IYoutubeClientHelper clientHelper;
        private Queue<MediaJob> jobQueue = new Queue<MediaJob>();

        public event EventHandler OnShowLibrary;
        public event EventHandler OnShowPlaylist;
        public event EventHandler OnShowYoutube;

        public Taskbar()
        {
            this.InitializeComponent();
        }

        public void InitialSetup(string mediapath)
        {
            mediaPath = mediapath;

            // initialize Youtube helpers
            clientHelper = new YoutubeClientHelper(new YoutubeClient(), System.AppDomain.CurrentDomain.BaseDirectory);

            UpdateStatistics();
        }

        public void UpdateStatistics() {
            UpdateLibraryStatistics();
            UpdatePlaylistStatistics();
            UpdateJobStatistics();
        }

        private void DownloadMedia(object sender, RoutedEventArgs e)
        {
            if (!IsValidUrl(tbUrl.Text)) return;

            var mediaType = (string)((ComboBoxItem)cbMediaType.SelectedValue).Content;
            var quality = (mediaType != "mp3") ? (string)((ComboBoxItem)cbFormats.SelectedValue).Content : string.Empty;
            var mediaJob = new MediaJob() { YoutubeUrl = tbUrl.Text, MediaType = mediaType, Quality = quality };

            jobQueue.Enqueue(mediaJob);

            ProcessJobFromQueue();
        }

        private bool isProcessingJob = false;
        private async void ProcessJobFromQueue()
        {
            UpdateStatus();
            UpdateLibraryStatistics();
            UpdateJobStatistics();
            if (isProcessingJob) return;
            UpdateStatusImage(null);
            if (jobQueue.Count == 0) return;
            var job = jobQueue.Dequeue();
            isProcessingJob = true;
            UpdateJobStatistics();
            var videoDetails = await GetVideoDetails(job.YoutubeUrl);
            UpdateStatusImage(videoDetails);
            await ProcessYoutubeVideo(videoDetails, job.MediaType, job.Quality);
            isProcessingJob = false;
        }

        private void UpdateStatus()
        {
            tbStatus.Text = (jobQueue.Count > 0) ? "downloading media" : string.Empty;
            tbStatusTitle.Text = (jobQueue.Count > 0) ? "job" : string.Empty;
            spStatus.Visibility = (jobQueue.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateStatusImage(VideoDetails videoDetails)
        {
            if (videoDetails == null)
            {
                imgStatus.Source = null;
                return;
            }

            var uri = new Uri($"{mediaPath}\\{videoDetails.id}-medium.jpg", UriKind.Absolute);
            imgStatus.Source = new BitmapImage(uri);
        }

        private void UpdateLibraryStatistics() => tbLibraryCount.Text = EntitiesHelper.RetrieveMediaMetadataAsCount().ToString();

        private void UpdatePlaylistStatistics() => tbPlaylistCount.Text = EntitiesHelper.RetrievePlaylistMetadataAsCount().ToString();

        private void UpdateJobStatistics() => tbJobsCount.Text = jobQueue.Count.ToString();

        private async Task ProcessYoutubeVideo(VideoDetails videoDetails, string mediaType, string quality)
        {
            if (videoDetails == null) return;

            await DownloadThumbnails(videoDetails);

            var path = mediaPath + $"\\{videoDetails.id}.{mediaType}";

            try
            {
                if (File.Exists(path)) File.Delete(path);
                await clientHelper.DownloadMedia(videoDetails.id, quality, path, mediaType);
                var fileInfo = new FileInfo(path);
                EntitiesHelper.AddMediaMetadata(videoDetails, mediaType, quality, fileInfo.Length);
            }
            catch (Exception ex)
            {
                // todo: handle error
            }

            UpdateLibraryStatistics();
            UpdateJobStatistics();
            isProcessingJob = false;
            ProcessJobFromQueue();
        }

        private void ShowLibrary(object sender, RoutedEventArgs e) => OnShowLibrary.Invoke(null, null);

        private void ShowPlaylists(object sender, RoutedEventArgs e) => OnShowPlaylist.Invoke(null, null);
        private void ShowYoutube(object sender, RoutedEventArgs e) => OnShowYoutube.Invoke(null, null);

        public async void MediaChanged(Uri media) {
            tbUrl.Text = media.OriginalString;
            cbFormats.Items.Clear();
            spDownloadToolbar.Visibility = Visibility.Collapsed;

            if (IsValidUrl(media.OriginalString))
            {
                var videoDetail = await GetVideoDetails(media.OriginalString);
                if (videoDetail != null)
                {
                    foreach (var mt in videoDetail.qualities)
                    {
                        cbFormats.Items.Add(new ComboBoxItem() { Content = mt });
                    }
                }
                ShouldWeShowToolbar();
                await DownloadMediumThumbnail(videoDetail);
            }
        }

        private async Task DownloadThumbnails(VideoDetails videoDetails)
        {
            if (videoDetails == null) return;
            await StorageHelper.DownloadImageAsync($"{videoDetails.id}-low", new Uri(videoDetails.thumbnails.LowResUrl), mediaPath);
            //await StorageHelper.DownloadImageAsync($"{videoDetails.id}-medium", new Uri(videoDetails.thumbnails.MediumResUrl), mediaPath);
            await StorageHelper.DownloadImageAsync($"{videoDetails.id}-standard", new Uri(videoDetails.thumbnails.StandardResUrl), mediaPath);
            await StorageHelper.DownloadImageAsync($"{videoDetails.id}-high", new Uri(videoDetails.thumbnails.HighResUrl), mediaPath);
            await StorageHelper.DownloadImageAsync($"{videoDetails.id}-max", new Uri(videoDetails.thumbnails.MaxResUrl), mediaPath);
        }

        private async Task DownloadMediumThumbnail(VideoDetails videoDetails)
        {
            if (videoDetails == null) return;
            await StorageHelper.DownloadImageAsync($"{videoDetails.id}-medium", new Uri(videoDetails.thumbnails.MediumResUrl), mediaPath);
        }

        private bool IsValidUrl(string ytUrl)
        {
            if (ytUrl == "https://www.youtube.com/") return false;
            if (string.IsNullOrEmpty(ytUrl)) return false;
            return true;
        }

        private void ShouldWeShowToolbar()
        {
            if (IsValidUrl(tbUrl.Text)) spDownloadToolbar.Visibility = Visibility.Visible;
            else spDownloadToolbar.Visibility = Visibility.Collapsed;
        }

        private async Task<VideoDetails> GetVideoDetails(string ytUrl)
        {
            if (!IsValidUrl(ytUrl)) return null;
            try
            {
                return await clientHelper.GetVideoMetadata(clientHelper.GetVideoID(ytUrl));
            }
            catch (Exception ex)
            {
                // todo: handle error
            }
            return null;
        }

  
    }
}
