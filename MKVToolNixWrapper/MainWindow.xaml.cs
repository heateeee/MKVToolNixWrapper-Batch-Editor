using MKVToolNixWrapper.Dtos;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace MKVToolNixWrapper
{
    public partial class cMainWindow : Window
    {
        #region member
        private List<cFileMeta> FileMetaList { get; set; } = [];
        private List<cTrackListMeta> TrackList { get; set; } = [];
        private static string MkvMergePath { get; set; } = "C:\\Program Files\\MKVToolNix\\mkvmerge.exe";
        private List<int> ProcessIdTracker { get; set; } = [];
        private ManualResetEventSlim PauseEvent { get; } = new(true);
        private bool IsEventListening { get; set; } = false;
        private bool WasBatchStopped { get; set; } = false;
        #endregion

        #region new
        public cMainWindow()
        {
            InitializeComponent();

            SizeChanged += MainWindow_SizeChanged;
            LocationChanged += MainWindow_LocationChanged;
            Loaded += MainWindow_Loaded;
            Title = $"MKVToolNixWrapper v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(2)}";
            this.Activated += OnAppActivated;
        }
        #endregion

        #region load or window events
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WriteOutputLine($"Welcome to MKVToolNixWrapper v{Assembly.GetExecutingAssembly().GetName().Version}");
            DataContext = this;
            TrackGrid.IsEnabled = false;
            AnalyzeButton.IsEnabled = false;
            BatchButton.IsEnabled = false;
            PlayIntroSound();
            MkvMergeExistsCheck();
            StartPulsing(BrowseFolderButton, 2000);
            StartPulsing(SelectedFolderPathLabel, 2000);
            TaskbarItemInfo.ProgressValue = 100;

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        }
        private void OnAppActivated(object? sender, EventArgs e)
        {
            // only run this logic if the listener is active
            if (IsEventListening)
            {
                Dispatcher.Invoke(() =>
                {
                    if (TaskbarItemInfo != null)
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;

                    IsEventListening = false;
                });
            }
        }
        #endregion

        #region paint
          private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
          {
              // check if the window is in the normal state (not minimized or maximized)
              if (WindowState == WindowState.Normal)
              {
                  // optimize the resize
                  VerticalSplitter.Visibility = Visibility.Hidden;
                  cDrawingControl.SuspendDrawing(cDrawingControl.GetWindowHandle(this));
                  cDrawingControl.ResumeDrawing(cDrawingControl.GetWindowHandle(this));
                  VerticalSplitter.Visibility = Visibility.Visible;
              }
          }
          private void MainWindow_LocationChanged(object? sender, EventArgs e)
          {
              // check if the window is in the normal state (not minimized or maximized)
              if (WindowState == WindowState.Normal)
              {
                  // optimize when moving
                  Focus();
        
                  VerticalSplitter.Visibility = Visibility.Hidden;
                  cDrawingControl.SuspendDrawing(cDrawingControl.GetWindowHandle(this));
                  cDrawingControl.ResumeDrawing(cDrawingControl.GetWindowHandle(this));
                  VerticalSplitter.Visibility = Visibility.Visible;
              }
          }
        #endregion

        #region mkv merge path
        private void MkvMergeExistsCheck()
        {
            // load "MkvMergePath" from the user-level configuration file if it exists
            Configuration roaming = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            ExeConfigurationFileMap fileMap = new()
            {
                ExeConfigFilename = roaming.FilePath
            };
            Configuration config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
            string? appSettingsMkvMergePath = config?.AppSettings?.Settings["MkvMergePath"]?.Value;
            MkvMergePath = string.IsNullOrEmpty(appSettingsMkvMergePath) ? MkvMergePath : appSettingsMkvMergePath;

            if (File.Exists(MkvMergePath))
            {
                WriteOutputLine($"Succesfully located MKVMerge at: \"{MkvMergePath}\"");
            }
            else
            {
                MessageBox.Show($"Unable to locate mkvmerge.exe\r\nPlease click OK and locate your mkvmerge.exe", "Failed to locate MKVMerge", MessageBoxButton.OK, MessageBoxImage.Error);
                WriteOutputLine($"Failed to locate MKVMerge at: \"{MkvMergePath}\" prompting user for location");

                if (!SetMkvMergePath())
                {
                    ToggleUI(false);
                }
            }
        }

        private bool SetMkvMergePath(bool restart = true)
        {
            var openFileDlg = new System.Windows.Forms.OpenFileDialog
            {
                Title = "Locate your mkvmerge.exe",
                Filter = "Executable Files|*.exe",
                FileName = "mkvmerge.exe",
                CheckFileExists = true,
                CheckPathExists = true
            };

            var result = openFileDlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                if (Path.GetFileName(openFileDlg.FileName).Equals("mkvmerge.exe"))
                {
                    // save to config
                    Configuration roaming = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                    ExeConfigurationFileMap fileMap = new()
                    {
                        ExeConfigFilename = roaming.FilePath
                    };
                    Configuration config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
                    if (config.AppSettings.Settings["MkvMergePath"] == null)
                    {
                        config.AppSettings.Settings.Add("MkvMergePath", openFileDlg.FileName);
                    }
                    else
                    {
                        config.AppSettings.Settings["MkvMergePath"].Value = openFileDlg.FileName;
                    }
                    config.Save(ConfigurationSaveMode.Modified);

                    // update path
                    MkvMergePath = openFileDlg.FileName;
                    WriteOutputLine($"Succesfully located MKVMerge at: \"{MkvMergePath}\"");
                    return true;
                }
                else
                {
                    MessageBox.Show($"You have selected an invalid executable\r\nEnsure the file is correctly named 'mkvmerge.exe'\r\nPlease restart the application and try again\r\n\r\nClick on 'Help' if you need more information on MkvMerge", "Failed to locate MKVMerge", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (restart)
            {
                Close();
                // commented out as it's not possible to close it without task manager
                // SetMkvMergePath(); 
            }

            return false;
        }
        #endregion

        #region buttons 
        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            TrackGrid.IsEnabled = false;
            AnalyzeButton.IsEnabled = false;
            BatchButton.IsEnabled = false;
            TrackGrid.ItemsSource = null;
            FileListBox.ItemsSource = null;

            System.Windows.Forms.FolderBrowserDialog openFileDlg = new();
            System.Windows.Forms.DialogResult result = openFileDlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                BrowseFolderHandler(openFileDlg.SelectedPath);
            }
            else
            {
                SelectedFolderPathLabel.Content = "";
            }
        }
        //private void EditMKVMergePath_Click(object sender, RoutedEventArgs e) // button is currently not avaliable, as i dont know where to effectively place it, to keep a small minwidth size
        //{
        //    SetMkvMergePath(false);
        //}
        private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            // ResetFileStatus();
            AnalyzeMkvFiles();
        }
        private async void BatchButton_Click(object sender, RoutedEventArgs e)
        {
            StopButton.Visibility = Visibility.Visible;
            StopButton.Margin = new Thickness(10, 0, 0, 0);
            StopButton.Width = 80;
            if (BatchButton.Content.ToString() == "Pause Batch")
            {
                // pause the batch process
                Dispatcher.Invoke(() => TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused);
                Dispatcher.Invoke(() => Mouse.OverrideCursor = null);
                BatchButton.Content = "Continue";
                Dispatcher.Invoke(() => StartPulsing(BatchButton));
                PauseEvent.Reset();
                return;
            }
            else if (BatchButton.Content.ToString() == "Continue")
            {
                // Resume the batch process
                Dispatcher.Invoke(() => TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate);
                Dispatcher.Invoke(() => Mouse.OverrideCursor = Cursors.Wait);
                BatchButton.Content = "Pause Batch";
                Dispatcher.Invoke(() => StopPulsing(BatchButton));
                PauseEvent.Set();
                return;
            }
            await StartBatchProcess();
        }
        private void BatchButtonStop_Click(object sender, RoutedEventArgs e)
        {
            WasBatchStopped = true;
            PauseEvent.Set();
            Dispatcher.Invoke(() => StopPulsing(BatchButton));
            StopButton.Visibility = Visibility.Hidden;
            StopButton.Margin = new Thickness(0, 0, 0, 0);
            StopButton.Width = 0;
            ClearFilesButton.Visibility = Visibility.Visible;
            ClearFilesButton.Width = 80;
        }
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            PlayNotificationSound();

            using (FileStream fileStream = File.Create($"{Path.GetTempPath()}\\Info.txt"))
            {
                GetType().Assembly.GetManifestResourceStream("MKVToolNixWrapper.Assets.Info.txt")!.CopyTo(fileStream);
            }

            Process process = Process.Start("notepad", $"{Path.GetTempPath()}\\Info.txt");
            process.Dispose();
        }

        #region table file list button clicks
        private void SelectAllTrackButton_Click(object sender, RoutedEventArgs e)
        {
            TrackList.ForEach(item => item.Include = true);
            TrackGrid.ItemsSource = null;
            TrackGrid.ItemsSource = TrackList;
        }

        private void SelectNoneTrackButton_Click(object sender, RoutedEventArgs e)
        {
            TrackList.ForEach(item => item.Include = false);
            TrackGrid.ItemsSource = null;
            TrackGrid.ItemsSource = TrackList;
        }

        private void SelectAllFileButton_Click(object sender, RoutedEventArgs e)
        {
            FileMetaList.ForEach(item => item.Include = true);
            FileListBox.ItemsSource = null;
            FileListBox.ItemsSource = FileMetaList;
        }

        private void SelectNoneFileButton_Click(object sender, RoutedEventArgs e)
        {
            FileMetaList.ForEach(item => item.Include = false);
            FileListBox.ItemsSource = null;
            FileListBox.ItemsSource = FileMetaList;
        }

        private void InvertFileButton_Click(object sender, RoutedEventArgs e)
        {
            FileMetaList.ForEach(item => item.Include = !item.Include);
            FileListBox.ItemsSource = null;
            FileListBox.ItemsSource = FileMetaList;
        }
        #region filelistbox
        private void FileListBoxCheckBox_Click(object sender, RoutedEventArgs e)
        {
            TrackGrid.ItemsSource = null;
            TrackGrid.IsEnabled = false;
            BatchButton.IsEnabled = false;
        }
        #endregion

        private void DeselectFailsButton_Click(object sender, RoutedEventArgs e)
        {
            FileMetaList.ForEach(x =>
            {
                if (x.Status == FileStatusEnum.FailedAnalysis)
                {
                    x.Include = false;
                }
            });
            ForceSetControlItemsSourceBinding(FileListBox, FileMetaList);
        }

        private void SelectUnprocessedButton_Click(object sender, RoutedEventArgs e)
        {
            FileMetaList.ForEach(x =>
            {
                if (x.Status == FileStatusEnum.Unprocessed)
                {
                    x.Include = true;
                }
            });
            ForceSetControlItemsSourceBinding(FileListBox, FileMetaList);
        }
        #endregion

        #endregion

        #region click functionality

        #region browse

        private async void BrowseFolderHandler(string path)
        {
            try
            {
                WriteOutputLine($"Source folder selected: \"{path}\"");
                UI_EditMKVMergePath(false);
                BrowseFolderButton.IsEnabled = false;
                ClearFilesButton.IsEnabled = true;

                // show mouse spinner
                Dispatcher.Invoke(() => Mouse.OverrideCursor = Cursors.Wait);

                List<string> mkvFiles = await Task.Run(() => GetMkvFilesInFolder(path));
                if (mkvFiles.Count == 0)
                {
                    MessageBox.Show("Unable to locate .mkv files in the selected folder, please select a valid folder.\r\n\r\nNote: 'MasteredFiles' folder is reserved and not included in the search!", "Directory error", MessageBoxButton.OK, MessageBoxImage.Error);
                    WriteOutputLine($"Error: Unable to locate .mkv files in the selected folder: \"{path}\"");
                    SelectedFolderPathLabel.Content = "Please select a directory or files to process";
                }
                else
                {
                    SelectedFolderPathLabel.Content = $"Selected Path: \"{path}\"";
                    WriteOutputLine($"Source folder successfully validated - MKV count: {mkvFiles.Count}");
                    WriteOutputLine();
                    AnalyzeButton.IsEnabled = true;
                    StartPulsing(AnalyzeButton, 2000);
                    // populate file list
                    FileMetaList = mkvFiles.Select(x => new cFileMeta() { FilePath = x, Include = true, Status = FileStatusEnum.Unprocessed }).ToList();
                    FileListBox.ItemsSource = FileMetaList;
                }
            }
            finally
            {
                // hide mouse spinner and enable browse button
                Dispatcher.Invoke(() => Mouse.OverrideCursor = null);
                BrowseFolderButton.IsEnabled = true;
            }
        }
        private void GetFilesFromSelection(List<string> selectedPaths)
        {
            // get all the mkv files from the selected paths
            List<string> mkvFiles = new();
            foreach (var path in selectedPaths)
            {
                if (Directory.Exists(path))
                {
                    mkvFiles.AddRange(GetMkvFilesInFolder(path));
                }
                else if (File.Exists(path) && Path.GetExtension(path).Equals(".mkv", StringComparison.OrdinalIgnoreCase))
                {
                    mkvFiles.Add(path);
                }
            }

            if (mkvFiles.Count > 0)
            {
                UI_EditMKVMergePath(false);
                WriteOutputLine($"Source files successfully validated - MKV count: {mkvFiles.Count}");
                SelectedFolderPathLabel.Content = $"Selected Files: {mkvFiles.Count}";
                AddFilesToList(mkvFiles);
            }
            else
            {
                MessageBox.Show("Unable to locate .mkv files in the selected items, please select valid files or folders.", "Directory error", MessageBoxButton.OK, MessageBoxImage.Error);
                WriteOutputLine("Error: Unable to locate .mkv files in the selected items.");
                SelectedFolderPathLabel.Content = "Please select a directory or files to process";
            }
        }
        private static List<string> GetMkvFilesInFolder(string path)
        {
            return Directory.GetFiles(path, "*.mkv", SearchOption.AllDirectories).Where(filePath => !filePath.Contains("\\MasteredFiles\\")).ToList();
        }
        #endregion

        #region analyze
        private async void AnalyzeMkvFiles()
        {
            await Task.Run(() =>
            {
                WriteOutputLine("**** ANALYSIS START ****");
                Dispatcher.Invoke(() => TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate);

                // Clear any previously processes track items
                TrackList = [];
                Dispatcher.Invoke(() => ToggleUI(false));

                // Clear an failed analysis files
                foreach (var file in FileMetaList)
                {
                    if (file.Status == FileStatusEnum.FailedAnalysis)
                    {
                        file.Status = FileStatusEnum.Unprocessed;
                    }
                }
                ForceSetControlItemsSourceBinding(FileListBox, FileMetaList);

                // Force mouse spinner
                Dispatcher.Invoke(() => Mouse.OverrideCursor = Cursors.Wait);

                // Clear Track grid on the UI
                Dispatcher.Invoke(() => TrackGrid.ItemsSource = null);

                // Get set of files which are checked in the UI
                List<cFileMeta> includedFiles = FileMetaList.Where(x => x.Include).ToList();

                // Analyze each file against comparison file
                bool allPassed = true;
                cFileMeta? CompareFileItem = null;
                List<cTrack> CompareMkvSubTracks = [];
                List<cTrack> CompareMkvAudioTracks = [];
                List<cTrack> CompareMkvVideoTracks = [];
                foreach (var fileItem in FileMetaList.Where(x => x.Include).ToList())
                {
                    cRootObject? MKVInfo = QueryMkvFile(fileItem.FilePath);
                    if (MKVInfo is null)
                    {
                        WriteOutputLine($"FAIL - \"{Path.GetFileName(fileItem.FilePath)}\" is not a valid mkv file");
                        fileItem.Status = FileStatusEnum.FailedAnalysis;
                        ForceSetControlItemsSourceBinding(FileListBox, FileMetaList);
                        continue;
                    }

                    // Attachment info dump
                    int? attachmentCount = MKVInfo.Attachments?.Count;
                    Regex coverRegex = new(@"cover.*\.(png|jpg)$", RegexOptions.IgnoreCase);
                    IEnumerable<cAttachment>? coverArtAttachments = MKVInfo.Attachments?.Where(x => x.File_Name != null && coverRegex.IsMatch(x.File_Name));
                    if (attachmentCount > 0)
                    {
                        WriteOutputLine($"Attachment(s) Found: {attachmentCount} {(coverArtAttachments?.Count() > 0 ? $"- Cover Art Found: {coverArtAttachments?.Count()}" : "")}");
                    }

                    if (CompareFileItem == null)
                    {
                        // Populate comparison point
                        WriteOutputLine($"PASS - \"{Path.GetFileName(fileItem.FilePath)}\" marked as comparison point");
                        CompareFileItem = fileItem;
                        CompareMkvSubTracks = [.. MKVInfo.Tracks.Where(x => x.Type == "subtitles").OrderBy(y => y.Id).ThenBy(z => z.Properties?.Track_Name)];
                        CompareMkvAudioTracks = [.. MKVInfo.Tracks.Where(x => x.Type == "audio").OrderBy(y => y.Id).ThenBy(z => z.Properties?.Track_Name)];
                        CompareMkvVideoTracks = [.. MKVInfo.Tracks.Where(x => x.Type == "video").OrderBy(y => y.Id).ThenBy(z => z.Properties?.Track_Name)];

                        // Using track info from first file, will later be applied to the track grid given it passes
                        TrackList = MKVInfo.Tracks.OrderBy(y => y.Id).ThenBy(z => z.Properties?.Track_Name)
                            .Select(x => new cTrackListMeta
                            {
                                Id = x.Id,
                                Name = x.Properties?.Track_Name,
                                Language = x.Properties?.Language,
                                Type = x.Type,
                                Codec = x.Properties?.Codec_id,
                                Include = true,
                                Default = x.Properties?.Default_Track ?? false,
                                Forced = x.Properties?.Forced_Track ?? false
                            }).ToList();
                        continue;
                    }

                    var curMkvVideoTracks = MKVInfo.Tracks.Where(x => x.Type == "video").OrderBy(y => y.Id).ThenBy(z => z.Properties?.Track_Name).ToList();
                    if (!CheckTracks("video", fileItem.FilePath, curMkvVideoTracks, CompareFileItem.FilePath, CompareMkvVideoTracks))
                    {
                        allPassed = false;
                        fileItem.Status = FileStatusEnum.FailedAnalysis;
                        ForceSetControlItemsSourceBinding(FileListBox, FileMetaList);
                        continue;
                    }

                    var curMkvAudioTracks = MKVInfo.Tracks.Where(x => x.Type == "audio").OrderBy(y => y.Id).ThenBy(z => z.Properties?.Track_Name).ToList();
                    if (!CheckTracks("audio", fileItem.FilePath, curMkvAudioTracks, CompareFileItem.FilePath, CompareMkvAudioTracks))
                    {
                        allPassed = false;
                        fileItem.Status = FileStatusEnum.FailedAnalysis;
                        ForceSetControlItemsSourceBinding(FileListBox, FileMetaList);
                        continue;
                    }

                    var curMkvSubTracks = MKVInfo.Tracks.Where(x => x.Type == "subtitles").OrderBy(y => y.Id).ThenBy(z => z.Properties?.Track_Name).ToList();
                    if (!CheckTracks("subtitles", fileItem.FilePath, curMkvSubTracks, CompareFileItem.FilePath, CompareMkvSubTracks))
                    {
                        allPassed = false;
                        fileItem.Status = FileStatusEnum.FailedAnalysis;
                        ForceSetControlItemsSourceBinding(FileListBox, FileMetaList);
                        continue;
                    }

                    WriteOutputLine($"PASS - \"{Path.GetFileName(fileItem.FilePath)}\"");
                    fileItem.Status = FileStatusEnum.PassedAnalysis;
                    ForceSetControlItemsSourceBinding(FileListBox, FileMetaList);
                }

                // unset mouse spinner
                Dispatcher.Invoke(() => Mouse.OverrideCursor = null);
                // Unlock ui
                Dispatcher.Invoke(() => ToggleUI(true));

                if (allPassed)
                {
                    ForceSetControlItemsSourceBinding(TrackGrid, TrackList);
                    Dispatcher.Invoke(() => TrackGrid.IsEnabled = true);
                    Dispatcher.Invoke(() => BatchButton.IsEnabled = true);
                    Dispatcher.Invoke(() => StartPulsing(BatchButton, 2000));
                    WriteOutputLine("Analysis Completed - Outcome: PASS");
                    Dispatcher.Invoke(() => TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal);
                }
                else
                {
                    Dispatcher.Invoke(() => BatchButton.IsEnabled = false);
                    Dispatcher.Invoke(() => TrackGrid.IsEnabled = false);
                    WriteOutputLine("Analysis Completed - Outcome: FAIL");
                    WriteOutputLine("Explanation: Unable to unlock batching as the selected files have differing sub/audio/video track setup, proceeding would result in missmatched tracks");
                    WriteOutputLine("Resolution: Deselect the MKV's that have FAILED and process them on their own. Only once all selected files PASS is the batch button unlocked");
                    SystemSounds.Hand.Play();
                    Dispatcher.Invoke(() => TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error);
                }

                IsEventListening = true;

                WriteOutputLine($"**** ANALYSIS END ****");
                WriteOutputLine();
            });
        }

        private bool CheckTracks(string trackType, string? currentFilePath, List<cTrack> currentTracks, string? compareFilePath, List<cTrack> compareTracks)
        {
            if (compareFilePath is null) return false;

            int currentCount = currentTracks.Count;
            int compareCount = compareTracks.Count;

            if (currentCount != compareCount)
            {
                WriteOutputLine($"FAIL - \"{Path.GetFileName(currentFilePath)}\" due to differing {trackType} track count compared to \"{Path.GetFileName(compareFilePath)}\": {currentCount} vs {compareCount}");
                return false;
            }

            for (int i = 0; i < currentCount; i++)
            {
                if (currentTracks[i].Properties?.Language != compareTracks[i].Properties?.Language)
                {
                    WriteOutputLine($"FAIL - \"{Path.GetFileName(currentFilePath)}\" due to {trackType} track {i} having a different lang flag compared to \"{Path.GetFileName(compareFilePath)}\": {currentTracks[i].Properties?.Language} vs {compareTracks[i].Properties?.Language}");
                    return false;
                }

                if (currentTracks[i].Properties?.Track_Name != compareTracks[i].Properties?.Track_Name)
                {
                    WriteOutputLine($"FAIL - \"{Path.GetFileName(currentFilePath)}\" due to {trackType} track {i} having a different track name compared to \"{Path.GetFileName(compareFilePath)}\": {currentTracks[i].Properties?.Track_Name} vs {compareTracks[i].Properties?.Track_Name}");
                    return false;
                }
            }

            return true;
        }
        private cRootObject? QueryMkvFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;
            try
            {
                string jsonOut = QueryMkvFileToJson(filePath);
                return JsonSerializer.Deserialize<cRootObject>(jsonOut);
            }
            catch (Exception ex)
            {
                WriteOutputLine($"An exception occured deserializing the JSON output from MKVMerge: {ex}");
                return null;
            }
        }
        private string QueryMkvFileToJson(string filePath)
        {
            try
            {
                // Request JSON from MKVMerge
                using Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = MkvMergePath,
                        Arguments = $"--identification-format json --identify \"{filePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };

                process.Start();
                ProcessIdTracker.Add(process.Id);

                string standardOuput = process.StandardOutput.ReadToEnd();
                string standardErrorOutput = process.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(standardErrorOutput))
                {
                    WriteOutputLine($"An error occured with mkvmerge.exe identify: {standardErrorOutput}");
                }
                process.WaitForExit();
                ProcessIdTracker.Remove(process.Id);
                return standardOuput;
            }
            catch (Exception ex)
            {
                WriteOutputLine($"An exception occured requesting JSON from MKVMerge: {ex}");
                return "";
            }
        }
        #endregion

        #region batch
        private async Task StartBatchProcess()
        {
            // start the batch process
            PauseEvent.Set();
            BatchButton.Content = "Pause Batch";
            ClearFilesButton.Visibility = Visibility.Hidden;
            ClearFilesButton.Width = 0;

            ToggleUI(false);
            BatchButton.IsEnabled = true;

            await Task.Run(() =>
            {
                WriteOutputLine("**** BATCH START ****");
                // taskbar - In Progress
                Dispatcher.Invoke(() => TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate);

                // force mouse spinner
                Dispatcher.Invoke(() => Mouse.OverrideCursor = Cursors.Wait);

                string OutputPath = "\\MasteredFiles\\";
                string mergeCommandString = "";

                IEnumerable<cTrackListMeta> videoTracks = TrackList.Where(x => x.Type == "video" && x.Include);
                if (videoTracks.Any())
                {
                    mergeCommandString += $"--video-tracks {string.Join(",", videoTracks.Select(x => x.Id))} ";
                }
                else
                {
                    mergeCommandString += "--no-video ";
                }

                IEnumerable<cTrackListMeta> audioTracks = TrackList.Where(x => x.Type == "audio" && x.Include);
                if (audioTracks.Any())
                {
                    mergeCommandString += $"--audio-tracks {string.Join(",", audioTracks.Select(x => x.Id))} ";
                }
                else
                {
                    mergeCommandString += "--no-audio ";
                }

                IEnumerable<cTrackListMeta> subtitleTracks = TrackList.Where(x => x.Type == "subtitles" && x.Include);
                if (subtitleTracks.Any())
                {
                    mergeCommandString += $"--subtitle-tracks {string.Join(",", subtitleTracks.Select(x => x.Id))} ";
                }
                else
                {
                    mergeCommandString += "--no-subtitles ";
                }

                List<cTrackListMeta> includedTracks = TrackList.Where(x => x.Include).ToList();

                // add track names, default, forced and
                mergeCommandString += string.Join(" ", includedTracks.Select(x => $"--track-name {x.Id}:\"{x.Name}\"")) + " ";
                mergeCommandString += string.Join(" ", includedTracks.Select(x => $"--default-track {x.Id}:{(x.Default ? "yes" : "no")}")) + " ";
                mergeCommandString += string.Join(" ", includedTracks.Select(x => $"--forced-track {x.Id}:{(x.Forced ? "yes" : "no")}")) + " ";
                mergeCommandString += string.Join(" ", includedTracks.Select(x => $"--language {x.Id}:{x.Language}")) + " ";

                if (Dispatcher.Invoke(() => (AttachmentsCheckbox.IsChecked == true)))
                {
                    mergeCommandString += "--no-attachments ";
                }

                // add track order
                mergeCommandString += string.Join(" ", $"--track-order {string.Join(",", includedTracks.Select(x => $"0:{x.Id}"))}") + " ";

                foreach (cFileMeta filePath in FileMetaList.Where(x => x.Include))
                {
                    if (WasBatchStopped)
                    {
                        break;
                    }

                    try
                    {
                        string outputFilePath = $"{Path.GetDirectoryName(filePath.FilePath)}\\{OutputPath}\\{Path.GetFileName(filePath.FilePath)}";
                        string mkvMergeArgument = $"-o \"{outputFilePath}\" {mergeCommandString} \"{filePath.FilePath}\"";

                        // inform user
                        WriteOutputLine($"Writing  \"{outputFilePath}\"");
                        filePath.Status = FileStatusEnum.WritingFile;
                        ForceSetControlItemsSourceBinding(FileListBox, FileMetaList);

                        using Process process = new()
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = MkvMergePath,
                                Arguments = mkvMergeArgument,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                            }
                        };

                        process.OutputDataReceived += (sender, e) =>
                        {
                            if (e.Data != null)
                            {
                                // if it's a progress line (but not 0%) we set replace last line to true
                                WriteOutputLine(e.Data, Regex.IsMatch(e.Data, @"^Progress: [1-9]\d*%$"));
                            }
                        };

                        process.Start();
                        ProcessIdTracker.Add(process.Id);
                        process.BeginOutputReadLine();

                        // wait for the process to exit or for the stop signal
                        while (!process.HasExited)
                        {
                            if (WasBatchStopped)
                            {
                                process.Kill();
                                break;
                            }
                            if (!PauseEvent.IsSet)
                            {
                                break;
                            }
                            Thread.Sleep(100); // add a small delay to avoid busy-waiting
                        }

                        ProcessIdTracker.Remove(process.Id);

                        if (WasBatchStopped)
                        {
                            break;
                        }

                        string standardError = process.StandardError.ReadToEnd();
                        if (string.IsNullOrEmpty(standardError) && File.Exists(outputFilePath) && process.ExitCode == 0)
                        {
                            filePath.Status = FileStatusEnum.WrittenFile;
                            WriteOutputLine($"Writing Complete  \"{outputFilePath}\"");
                        }
                        else
                        {
                            filePath.Status = FileStatusEnum.Error;
                            WriteOutputLine($"Writing Error! - Please review the output for details {standardError}");
                            SystemSounds.Hand.Play();
                            // taskbar - Error
                            Dispatcher.Invoke(() => TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error);
                        }
                        WriteOutputLine();
                        ForceSetControlItemsSourceBinding(FileListBox, FileMetaList);

                        PauseEvent.Wait();
                    }
                    catch (Exception ex)
                    {
                        WriteOutputLine($"An exception occured attempting to invoke mkvmerge for {filePath}: {ex}");
                    }
                }

                // restore mouse cursor
                Dispatcher.Invoke(() => Mouse.OverrideCursor = null);

                // taskbar - Success
                Dispatcher.Invoke(() => TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal);

                PlayNotificationSound();

                WriteOutputLine("**** BATCH END ****");

                IsEventListening = true;
                Dispatcher.Invoke(() => BatchButton.Content = "Start Batch");
            });
            if (WasBatchStopped)
            {
                // reset the status of all files to Unprocessed
                ResetFileStatus();
                Dispatcher.Invoke(() => TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal);
            }
            WasBatchStopped = false;

            // hide the stop button
            StopButton.Visibility = Visibility.Hidden;
            StopButton.Margin = new Thickness(0, 0, 0, 0);
            StopButton.Width = 0;
            ClearFilesButton.Visibility = Visibility.Visible;
            ClearFilesButton.Width = 80;
            ToggleUI(true);
        }
        #endregion

        #region table display        
        private void ResetFileStatus()
        {
            foreach (var file in FileMetaList)
            {
                file.Status = FileStatusEnum.Unprocessed;
                ForceSetControlItemsSourceBinding(FileListBox, FileMetaList);
            }
        }
        #endregion

        #endregion

        #region output
        private void WriteOutputLine(string text = "", bool replaceLastLine = false)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (replaceLastLine)
                    {
                        int secondToLastIndex = OutputTextBox.Text.LastIndexOf("\r\n", OutputTextBox.Text.Length - 3);
                        if (secondToLastIndex >= 0)
                        {
                            OutputTextBox.Text = OutputTextBox.Text.Substring(0, secondToLastIndex + 2);
                        }
                    }

                    if (string.IsNullOrEmpty(text))
                    {
                        OutputTextBox.Text += "\r\n";
                    }
                    else
                    {
                        OutputTextBox.Text += $"{DateTime.Now} - {text}\r\n";
                    }
                    OutputTextBox.CaretIndex = OutputTextBox.Text.Length;
                    OutputTextBox.ScrollToEnd();
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to write to output window: {ex.Message}");
            }
        }
        #endregion

        #region ui
        // ToDo: Instead of a dumb toggle have a enum that dictates stage, dependent on the stage activate x,y,z ui element
        private void ToggleUI(bool enable)
        {
            cDrawingControl.SuspendDrawing(cDrawingControl.GetWindowHandle(this));

            TrackGrid.IsEnabled = enable;
            BrowseFolderButton.IsEnabled = enable;
            AnalyzeButton.IsEnabled = enable;
            BatchButton.IsEnabled = enable;
            InvertFileButton.IsEnabled = enable;
            SelectAllFileButton.IsEnabled = enable;
            SelectAllTrackButton.IsEnabled = enable;
            SelectNoneFileButton.IsEnabled = enable;
            SelectNoneTrackButton.IsEnabled = enable;
            DeselectFailsButton.IsEnabled = enable;
            SelectUnprocessedButton.IsEnabled = enable;
            ClearFilesButton.IsEnabled = enable;
            //ChangeMKVMergeButton.IsEnabled = enable;

            cDrawingControl.ResumeDrawing(cDrawingControl.GetWindowHandle(this));
        }

        #region pulsing
        private static readonly Dictionary<UIElement, Storyboard> PulsingStoryboards = new();
        private static async void StartPulsing(UIElement element, int durationMs = 0)
        {
            // setup storyboard + animation
            Storyboard storyboard = new();
            DoubleAnimation opacityAnimation1 = new()
            {
                From = 1.0,
                To = 0.2,
                Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            // assign to passed in element
            storyboard.Children.Add(opacityAnimation1);
            Storyboard.SetTarget(opacityAnimation1, element);
            Storyboard.SetTargetProperty(opacityAnimation1, new PropertyPath(UIElement.OpacityProperty));

            // add storyboard to dictionary
            lock (PulsingStoryboards)
            {
                if (PulsingStoryboards.TryGetValue(element, out var value))
                {
                    value.Stop();
                    PulsingStoryboards.Remove(element);
                }
                PulsingStoryboards[element] = storyboard;
            }

            // start animation and stop it after the passed in duration
            storyboard.Begin();
            if (durationMs > 0)
            {
                // if duration is greater than 0, stop pulsing after the specified duration
                await Task.Delay(durationMs);
                StopPulsing(element);
            }
        }

        private static void StopPulsing(UIElement element)
        {
            lock (PulsingStoryboards)
            {
                if (PulsingStoryboards.TryGetValue(element, out var storyboard))
                {
                    storyboard.Stop();
                    PulsingStoryboards.Remove(element);
                    element.Opacity = 1;
                }
            }
        }
        #endregion

        // Hack to avoid using INotifyPropertyChanged as it's messy to implement and restricts us to only using ObservableCollection :/
        // Nulling the item source and re-assigning it our collection effectively re-syncs with the UI. Aware this is not great but the 'offical' solution is some what to be desired.
        private void ForceSetControlItemsSourceBinding<T>(Control control, List<T> items)
        {
            // apply list to the UI
            Dispatcher.Invoke(() =>
            {
                switch (control)
                {
                    case ListBox listBox:
                        listBox.ItemsSource = null;
                        listBox.ItemsSource = items;
                        break;
                    case DataGrid dataGrid:
                        dataGrid.ItemsSource = null;
                        dataGrid.ItemsSource = items;
                        break;
                }
            });
        }
        private void UI_EditMKVMergePath(bool visible)
        {
            // commented out, as the button is not avaliable
            if (visible)
                return;
            //    {
            //        ChangeMKVMergeButton.Visibility = Visibility.Visible;
            //        ChangeMKVMergeButton.Width = 130;
            //        ClearFilesButton.Margin = new Thickness(0, 0, 10, 0);
            //    }
            //    else
            //    {
            //        ChangeMKVMergeButton.Visibility = Visibility.Hidden;
            //        ChangeMKVMergeButton.Width = 0;
            //        ClearFilesButton.Margin = new Thickness(0, 0, 0, 0);
            //    }
        }

        #region reset
        private void ClearFilesButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Are you sure you want to clear the files? Click 'Yes' to confirm.", "Confirm Clear Files", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                ResetWindow();
            }
        }
        private void ResetWindow()
        {
            // reset the UI to startup state
            FileMetaList.Clear();
            SelectedFolderPathLabel.Content = "Please select a directory or files to process";
            TrackGrid.IsEnabled = false;
            AnalyzeButton.IsEnabled = false;
            BatchButton.IsEnabled = false;
            ClearFilesButton.IsEnabled = false;
            TrackList = [];
            TrackGrid.ItemsSource = null;
            UI_EditMKVMergePath(true);
            ForceSetControlItemsSourceBinding(FileListBox, FileMetaList);
        }
        #endregion

        #endregion

        #region events

        #region datagrid file properties
        private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Check if the edited column is the language code column
            if (e.Column == LanguageCodeColumn)
            {
                // Get the edited text box
                // Validate the language code
                if (e.EditingElement is TextBox textBox && !IsValidLanguageCode(textBox.Text))
                {
                    // Cancel the edit and show an error message
                    e.Cancel = true;
                    MessageBox.Show($"The language code \"{textBox.Text}\" is invalid.\r\nPlease enter a valid ISO 639-2 language code.", "Invalid language code", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not cTrackListMeta track) return;

            int oldIndex = TrackList.IndexOf(track);
            if (oldIndex > 0)
            {
                // Move the track in TrackList
                cTrackListMeta oldValue = TrackList[oldIndex];
                cTrackListMeta previousValue = TrackList[oldIndex - 1];

                // Swap the Ids
                (previousValue.Id, oldValue.Id) = (oldValue.Id, previousValue.Id);
                TrackList.RemoveAt(oldIndex);
                TrackList.Insert(oldIndex - 1, oldValue);

                // Find the corresponding file in FileMetaList and move it
                cFileMeta? fileMeta = FileMetaList.FirstOrDefault(f => f.FilePath == track.Name);
                if (fileMeta != null)
                {
                    int fileMetaIndex = FileMetaList.IndexOf(fileMeta);
                    if (fileMetaIndex > 0)
                    {
                        cFileMeta fileMetaValue = FileMetaList[fileMetaIndex];
                        FileMetaList.RemoveAt(fileMetaIndex);
                        FileMetaList.Insert(fileMetaIndex - 1, fileMetaValue);
                    }
                }

                // Refresh the DataGrid
                TrackGrid.ItemsSource = null;
                TrackGrid.ItemsSource = TrackList;
            }
        }

        #endregion

        #region drag and drop
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            // show the user that the drop is allowed
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
            {
                if (e.Data.GetData(DataFormats.FileDrop, true) is string[] paths && paths.Length > 0)
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            // add dropped files to the list
            if (FileMetaList.Count > 0)
            {
                MessageBox.Show("You have analyzed or processed data in the track list.\r\nPlease clear the current data before dropping new files.", "Unfinished Progress", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ResetWindow();
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true) &&
                e.Data.GetData(DataFormats.FileDrop, true) is string[] paths &&
                paths.Length > 0)
            {
                GetFilesFromSelection([.. paths]);
            }
            e.Handled = true;
        }
        private void AddFilesToList(List<string> mkvFiles)
        {
            // populate file list
            FileMetaList = mkvFiles.Select(x => new cFileMeta() { FilePath = x, Include = true, Status = FileStatusEnum.Unprocessed }).ToList();
            ForceSetControlItemsSourceBinding(FileListBox, FileMetaList);
            AnalyzeButton.IsEnabled = true;
            ClearFilesButton.IsEnabled = true;
            StartPulsing(AnalyzeButton, 2000);
        }
        #endregion

        #region close
        protected override void OnClosing(CancelEventArgs e)
        {
            // Check for any running mkvmerge processes we started and kill them
            foreach (int processId in ProcessIdTracker)
            {
                try
                {
                    Process process = Process.GetProcessById(processId);
                    if (process != null && !process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit();
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to kill process with ID {processId}: {ex.Message}");
                }
            }

            base.OnClosing(e);
        }
        #endregion

        #region additional functions
        private static bool IsValidLanguageCode(string code)
        {
            if (!Regex.IsMatch(code, @"^[a-z]{3}$"))
            {
                return false;
            }

            try
            {
                CultureInfo.GetCultureInfoByIetfLanguageTag(code);
                return true;
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }

        private void PlayNotificationSound()
        {
            //var notificationSound = new SoundPlayer(GetType().Assembly.GetManifestResourceStream("MKVToolNixWrapper.Assets.SH3Menu.wav"));
            //notificationSound.Play();
        }

        private void PlayIntroSound()
        {
            //var notificationSound = new SoundPlayer(GetType().Assembly.GetManifestResourceStream("MKVToolNixWrapper.Assets.SH2Menu.wav"));
            //notificationSound.Play();
        }
        #endregion

        #endregion
    }
}
