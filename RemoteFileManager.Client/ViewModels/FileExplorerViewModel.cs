using MaterialDesignThemes.Wpf;
using RemoteFileManager.Client.Core;
using RemoteFileManager.Client.Services;
using RemoteFileManager.Client.Views;
using RemoteFileManager.Shared.Enums;
using RemoteFileManager.Shared.Models.FileSystem;
using RemoteFileManager.Shared.Models.Network;
using RemoteFileManager.Shared.Models.Transfer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace RemoteFileManager.Client.ViewModels
{
    public class FileExplorerViewModel : ViewModelBase
    {
        private INetworkService? _networkService;
        private ServerSession _currentSession;
        private string _currentPath = string.Empty;
        private bool _isLoading;

        private bool _isDownloading;
        private double _downloadProgress;
        private FileStream? _downloadStream; // Stream để ghi file đang tải
        private string _downloadFilePath;    // Đường dẫn file đang lưu
        private long _currentDownloadSize;

        private string? _pendingUploadPath;
        private bool _pendingUploadDeleteTemp;

        private readonly HashSet<string> _editableExtensions = new()
        {
            ".txt", ".log", ".json", ".xml", ".config", ".cs", ".html", ".css", ".js", ".md", ".ini", ".bat", ".sh"
        };
        public SnackbarMessageQueue MessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
        // Thêm Property
        public bool IsDownloading
        {
            get => _isDownloading;
            set => SetProperty(ref _isDownloading, value);
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set => SetProperty(ref _downloadProgress, value);
        }
        // Danh sách chứa cả Ổ đĩa (DriveDto) và File (FileDto)
        // Chúng ta dùng object để chứa được cả 2 loại
        public ObservableCollection<object> Items { get; } = new();
        public FileExplorerViewModel()
        {
            // Khởi tạo tất cả Command tại đây
            OpenItemCommand = new RelayCommand(ExecuteOpenItem);
            GoBackCommand = new RelayCommand(ExecuteGoBack, _ => !string.IsNullOrEmpty(CurrentPath));
            RefreshCommand = new RelayCommand(_ => LoadData());
            DeleteItemCommand = new RelayCommand(ExecuteDeleteItem);
            CreateFolderCommand = new RelayCommand(ExecuteCreateFolder);
            DownloadItemCommand = new RelayCommand(ExecuteDownloadItem);
            UploadFileCommand = new RelayCommand(ExecuteUploadFile);
            EditFileCommand = new RelayCommand(ExecuteEditFile);
        }

        public FileExplorerViewModel(INetworkService networkService)
        {

            _networkService = networkService;

            // Lắng nghe dữ liệu từ Server
            _networkService.PacketReceived += OnPacketReceived;

            // Các lệnh thao tác
            OpenItemCommand = new RelayCommand(ExecuteOpenItem);
            GoBackCommand = new RelayCommand(ExecuteGoBack, _ => !string.IsNullOrEmpty(CurrentPath));
            RefreshCommand = new RelayCommand(_ => LoadData());
            DeleteItemCommand = new RelayCommand(ExecuteDeleteItem);
            CreateFolderCommand = new RelayCommand(ExecuteCreateFolder);
            DownloadItemCommand = new RelayCommand(ExecuteDownloadItem);
            UploadFileCommand = new RelayCommand(ExecuteUploadFile);
            SendClipboardCommand = new RelayCommand(ExecuteSendClipboard);
            GetClipboardCommand = new RelayCommand(ExecuteGetClipboard);
            EditFileCommand = new RelayCommand(ExecuteEditFile);

            // Tự động tải danh sách ổ đĩa khi vừa vào màn hình này
            LoadData();
        }
        public void SetTargetServer(ServerSession session)
        {
            // A. Hủy đăng ký sự kiện ở máy cũ (nếu có)
            if (_networkService != null)
            {
                _networkService.PacketReceived -= OnPacketReceived;
            }

            // B. Gán NetworkService của máy mới
            _networkService = session.NetworkService;

            // C. Đăng ký sự kiện lại
            _networkService.PacketReceived += OnPacketReceived;

            // D. Reset giao diện
            CurrentPath = string.Empty;
            Items.Clear();

            // E. Load dữ liệu ổ đĩa máy mới
            LoadData();
        }

        private void LoadData()
        {
            // Kiểm tra null để tránh lỗi khi chưa chọn máy nào
            if (_networkService == null || !_networkService.IsConnected) return;

            IsLoading = true;
            Items.Clear();

            if (string.IsNullOrEmpty(CurrentPath))
            {
                SendRequest(CommandCode.GetDrives);
            }
            else
            {
                SendRequest(CommandCode.GetDirectoryContent, CurrentPath);
            }
        }

        private async void SendRequest(CommandCode command, object? payload = null)
        {
            if (_networkService == null) return;

            var packet = new Packet { Command = command };
            if (payload != null) packet.SetPayload(payload);

            await _networkService.SendPacketAsync(packet);
        }
        public string CurrentPath
        {
            get => _currentPath;
            set
            {
                if (SetProperty(ref _currentPath, value))
                {
                    // Khi đường dẫn thay đổi, cập nhật lại trạng thái nút Back
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand OpenItemCommand { get; }
        public ICommand GoBackCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand CreateFolderCommand { get; }
        public ICommand DownloadItemCommand { get; }
        public ICommand UploadFileCommand { get; }
        public ICommand SendClipboardCommand { get; }
        public ICommand GetClipboardCommand { get; }
        public ICommand EditFileCommand { get; }
        private void ExecuteEditFile(object? item)
        {
            if (item is FileDto file)
            {
                string ext = Path.GetExtension(file.Name).ToLower();

                // Kiểm tra xem có trong danh sách hỗ trợ không
                if (!_editableExtensions.Contains(ext))
                {
                    MessageBox.Show($"Chức năng sửa chưa hỗ trợ định dạng '{ext}'.\nChỉ hỗ trợ file văn bản.",
                                    "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                IsLoading = true;
                // Gửi yêu cầu lấy nội dung
                SendRequest(CommandCode.GetFileContent, file.FullPath);
            }
        }
        private async void ShowEditorDialog(FileContentDto dto)
        {
            var editorView = new TextEditorDialog();
            editorView.FileNameText.Text = $"Đang sửa: {Path.GetFileName(dto.FullPath)}";
            editorView.EditorTextBox.Text = dto.Content;

            // Cuộn con trỏ lên đầu
            editorView.EditorTextBox.CaretIndex = 0;

            // Hiện Dialog
            var result = await DialogHost.Show(editorView, "RootDialog");

            // Nếu user bấm Lưu (Result trả về string content)
            if (result is string newContent)
            {
                // Gửi lệnh lưu về Server
                var saveDto = new FileContentDto
                {
                    FullPath = dto.FullPath,
                    Content = newContent
                };
                SendRequest(CommandCode.SaveFileContent, saveDto);

                IsLoading = true; // Hiện loading chờ server lưu
            }
        }
        private void ExecuteSendClipboard(object? obj)
        {
            // Lấy text từ Clipboard máy Client
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                SendRequest(CommandCode.SendClipboard, text);
                MessageBox.Show("Đã gửi nội dung Clipboard lên Server!");
            }
            else
            {
                MessageBox.Show("Clipboard trống hoặc không phải text.");
            }
        }
        private void ExecuteGetClipboard(object? obj)
        {
            SendRequest(CommandCode.GetClipboard);
        }
        private void ExecuteDeleteItem(object? item)
        {
            if (item is FileDto fileDto)
            {
                var result = MessageBox.Show($"Bạn có chắc muốn xóa '{fileDto.Name}'?",
                                             "Xác nhận xóa",
                                             MessageBoxButton.YesNo,
                                             MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Gửi lệnh xóa kèm đường dẫn đầy đủ
                    SendRequest(CommandCode.DeleteItem, fileDto.FullPath);
                }
            }
        }
        private async void ExecuteCreateFolder(object? obj)
        {
            if (string.IsNullOrEmpty(CurrentPath))
            {
                MessageBox.Show("Vui lòng chọn một ổ đĩa hoặc thư mục trước.");
                return;
            }

            // 1. Khởi tạo giao diện Dialog
            var dialogView = new CreateFolderDialog();

            // 2. Hiện Dialog lên và chờ người dùng bấm nút
            // "RootDialog" phải trùng tên với Identifier bên XAML
            var result = await DialogHost.Show(dialogView, "RootDialog");

            // 3. Kiểm tra kết quả trả về
            // Nếu bấm Hủy -> result là null. Nếu bấm Tạo -> result là chuỗi tên folder.
            if (result is string folderName && !string.IsNullOrWhiteSpace(folderName))
            {
                // 4. Xử lý logic tạo (Clean tên folder để tránh ký tự đặc biệt)
                char[] invalidChars = Path.GetInvalidFileNameChars();
                if (folderName.IndexOfAny(invalidChars) >= 0)
                {
                    MessageBox.Show("Tên thư mục chứa ký tự không hợp lệ!");
                    return;
                }

                string fullPath = Path.Combine(CurrentPath, folderName);
                SendRequest(CommandCode.CreateFolder, fullPath);
            }
        }
        // Logic bấm nút Download
        private void ExecuteDownloadItem(object? item)
        {
            if (item is FileDto file)
            {
                // Mở hộp thoại chọn nơi lưu
                var dialog = new Microsoft.Win32.SaveFileDialog();
                dialog.FileName = file.Name;
                if (dialog.ShowDialog() == true)
                {
                    _downloadFilePath = dialog.FileName;

                    // Xóa file cũ nếu có để ghi mới
                    if (File.Exists(_downloadFilePath)) File.Delete(_downloadFilePath);

                    // Gửi yêu cầu tải
                    SendRequest(CommandCode.RequestDownload, file.FullPath);

                    IsDownloading = true;
                    DownloadProgress = 0;
                    _currentDownloadSize = 0;
                }
            }
        }
        //private void LoadData()
        //{
        //    IsLoading = true;
        //    Items.Clear();

        //    if (string.IsNullOrEmpty(CurrentPath))
        //    {
        //        // Nếu đường dẫn rỗng -> Lấy danh sách ổ đĩa
        //        SendRequest(CommandCode.GetDrives);
        //    }
        //    else
        //    {
        //        // Nếu có đường dẫn -> Lấy danh sách file trong đó
        //        SendRequest(CommandCode.GetDirectoryContent, CurrentPath);
        //    }
        //}

        //private async void SendRequest(CommandCode command, object? payload = null)
        //{
        //    var packet = new Packet { Command = command };
        //    if (payload != null) packet.SetPayload(payload);

        //    await _networkService.SendPacketAsync(packet);
        //}

        // Xử lý khi User Double Click vào 1 item
        private void ExecuteOpenItem(object? item)
        {
            if (item is DriveDto drive)
            {
                CurrentPath = drive.Name; // Ví dụ: "C:\"
                LoadData();
            }
            else if (item is FileDto file && file.IsFolder)
            {
                CurrentPath = file.FullPath;
                LoadData();
            }
            // Nếu là File thường thì chưa làm gì (sẽ làm tính năng Download sau)
        }

        private void ExecuteGoBack(object? obj)
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;

            // Logic tìm thư mục cha
            var parent = Directory.GetParent(CurrentPath);
            if (parent == null)
            {
                // Đã về gốc -> Về danh sách ổ đĩa
                CurrentPath = string.Empty;
            }
            else
            {
                CurrentPath = parent.FullName;
            }
            LoadData();
        }

        // --- XỬ LÝ PHẢN HỒI TỪ SERVER ---
        private async void OnPacketReceived(Packet packet)
        {
            // LƯU Ý: Hàm này được gọi từ luồng Background (NetworkService).
            // KHÔNG BAO Dispatcher Ở ĐÂY.

            switch (packet.Command)
            {
                // --- NHÓM LỆNH CẬP NHẬT UI (Cần Dispatcher) ---
                case CommandCode.GetDrives:
                case CommandCode.GetDirectoryContent:
                case CommandCode.DeleteItem:
                case CommandCode.CreateFolder:
                case CommandCode.SystemAlert:
                case CommandCode.SaveFileContent:
                case CommandCode.DownloadResponse:
                case CommandCode.FileChunk: // Download
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Copy logic xử lý UI cũ của bạn vào đây
                        HandleUiCommands(packet);
                    });
                    break;
                case CommandCode.GetClipboard:
                    string text = packet.GetPayload<string>();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (!string.IsNullOrEmpty(text))
                        {
                            Clipboard.SetText(text);
                            MessageBox.Show($"Đã lấy Clipboard từ Server:\n{text}");
                        }
                        else
                        {
                            MessageBox.Show("Clipboard Server trống.");
                        }
                    });
                    break;
                
                case CommandCode.GetFileContent:
                    // BẮT BUỘC PHẢI DÙNG DISPATCHER
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsLoading = false;
                        var contentDto = packet.GetPayload<FileContentDto>();
                        if (contentDto != null)
                        {
                            // Hàm này tạo new TextEditorDialog() -> Phải chạy trên UI Thread
                            ShowEditorDialog(contentDto);
                        }
                    });
                    break;

                // --- NHÓM LỆNH LOGIC NẶNG (Không dùng Dispatcher ngay) ---
                case CommandCode.UploadReady:
                    // Server đã sẵn sàng, ta bắt đầu gửi file.
                    // Việc này tốn thời gian, phải chạy ở Background, KHÔNG ĐƯỢC chạy trên UI Thread.

                    if (!string.IsNullOrEmpty(_pendingUploadPath))
                    {
                        try
                        {
                            // Gọi hàm gửi file (bất đồng bộ)
                            await UploadFileChunks(_pendingUploadPath);

                            // Gửi xong -> Xóa file temp (IO operation)
                            if (_pendingUploadDeleteTemp && File.Exists(_pendingUploadPath))
                            {
                                File.Delete(_pendingUploadPath);
                            }

                            _pendingUploadPath = null;

                            // Sau khi xong hết mới gọi UI để báo thành công
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                IsDownloading = false;
                                MessageBox.Show("Upload thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                                LoadData(); // Refresh lại danh sách
                            });
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                IsDownloading = false;
                                MessageBox.Show($"Lỗi Upload: {ex.Message}");
                            });
                        }
                    }
                    break;
            }
        }
        private void HandleUiCommands(Packet packet)
        {
            switch (packet.Command)
            {
                case CommandCode.GetDrives:
                    IsLoading = false;
                    var drives = packet.GetPayload<List<DriveDto>>();
                    Items.Clear();
                    if (drives != null)
                    {
                        foreach (var d in drives) Items.Add(d);
                    }
                    break;
                case CommandCode.GetDirectoryContent:
                    IsLoading = false;
                    var files = packet.GetPayload<List<FileDto>>();
                    Items.Clear();
                    if (files != null)
                    {
                        // Sắp xếp: Folder lên đầu, File xuống dưới
                        foreach (var f in files.OrderByDescending(x => x.IsFolder).ThenBy(x => x.Name))
                            Items.Add(f);
                    }
                    break;

                case CommandCode.SystemAlert:
                    // 1. Tắt loading dù có lỗi xảy ra (để người dùng còn thao tác tiếp)
                    IsLoading = false;

                    // 2. Hiện thông báo lỗi chi tiết từ Server (Icon X đỏ)
                    // packet.Message sẽ chứa: "Lỗi khi lưu file: ...", "File quá lớn...", "Access Denied..."
                    MessageQueue.Enqueue($"❌ {packet.Message}");

                    // 3. (Logic phụ) Nếu lỗi là do mất kết nối hoặc sai đường dẫn nghiêm trọng
                    // ta có thể kiểm tra chuỗi message để quyết định có back lại hay không.
                    // Nhưng với lỗi lưu file thông thường, chỉ cần hiện thông báo là đủ.
                    break;

                case CommandCode.SaveFileContent:
                    // 1. Quan trọng nhất: Tắt biểu tượng Loading quay quay
                    IsLoading = false;

                    // 2. Hiện thông báo thành công (Icon tích xanh)
                    // MessageQueue đã được cấu hình tự tắt sau 3s
                    MessageQueue.Enqueue("✅ Đã lưu thay đổi thành công!");

                    // (Tùy chọn) Nếu muốn chắc chắn, có thể reload lại list file để cập nhật ngày sửa đổi/kích thước
                    // LoadData(); 
                    break;
                case CommandCode.DeleteItem:
                case CommandCode.CreateFolder:
                    // Thao tác thành công, load lại danh sách để thấy thay đổi
                    LoadData();
                    break;

                case CommandCode.DownloadResponse:
                    // Server báo chuẩn bị gửi. Ta mở Stream sẵn sàng hứng.
                    var info = packet.GetPayload<FileChunkDto>();
                    if (info != null)
                    {
                        _downloadStream = new FileStream(_downloadFilePath, FileMode.Append, FileAccess.Write);
                    }
                    break;
                case CommandCode.FileChunk:
                    if (_downloadStream != null)
                    {
                        var chunk = packet.GetPayload<FileChunkDto>();
                        if (chunk != null)
                        {
                            // 1. Ghi byte vào ổ cứng
                            _downloadStream.Write(chunk.Data, 0, chunk.Data.Length);

                            // 2. Tính toán Progress
                            _currentDownloadSize += chunk.Data.Length;
                            if (chunk.TotalSize > 0)
                            {
                                DownloadProgress = (double)_currentDownloadSize / chunk.TotalSize * 100;
                            }

                            // 3. Nếu là gói cuối -> Đóng stream
                            if (chunk.IsLastChunk)
                            {
                                _downloadStream.Close();
                                _downloadStream.Dispose();
                                _downloadStream = null;
                                IsDownloading = false;
                                MessageBox.Show("Tải file thành công!", "Download", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                    break;
            }
        }
        private async Task UploadFileChunks(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                long totalSize = fs.Length;
                long currentOffset = 0;
                byte[] buffer = new byte[64 * 1024]; // 64KB
                int bytesRead;

                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    byte[] dataToSend = new byte[bytesRead];
                    Array.Copy(buffer, dataToSend, bytesRead);

                    var chunk = new FileChunkDto
                    {
                        CurrentOffset = currentOffset,
                        TotalSize = totalSize,
                        Data = dataToSend,
                        IsLastChunk = (currentOffset + bytesRead >= totalSize)
                    };

                    var packet = new Packet { Command = CommandCode.FileChunk };
                    packet.SetPayload(chunk);

                    // Gửi gói tin đi (Không liên quan UI -> OK)
                    await _networkService.SendPacketAsync(packet);

                    currentOffset += bytesRead;

                    // CẬP NHẬT UI: Phải dùng Dispatcher.Invoke ở đây
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Chỉ cập nhật biến số, UI sẽ tự bind
                        DownloadProgress = (double)currentOffset / totalSize * 100;
                    });

                    // Delay cực nhỏ để UI kịp vẽ lại thanh ProgressBar
                    await Task.Delay(5);
                }
            }
        }
        private async void ExecuteUploadFile(object? obj)
        {
            // 1. Mở dialog chọn file trên máy Client
            var openDialog = new Microsoft.Win32.OpenFileDialog();
            if (openDialog.ShowDialog() != true) return;

            var originalFilePath = openDialog.FileName;
            var originalFileName = Path.GetFileName(originalFilePath);

            // 2. Hiện Dialog cấu hình (UploadDialog)
            var dialogView = new UploadDialog();
            // Set tên mặc định vào TextBox
            dialogView.FileNameTextBox.Text = originalFileName;

            var result = await DialogHost.Show(dialogView, "RootDialog");

            // 3. Xử lý kết quả từ Dialog
            if (result is Tuple<string, bool> paramsData)
            {
                string targetFileName = paramsData.Item1;
                bool isZip = paramsData.Item2;
                string fileToSendPath = originalFilePath;
                bool needDeleteTemp = false; // Cờ để xóa file temp sau khi gửi

                try
                {
                    IsDownloading = true; // Tận dụng biến này để hiện ProgressBar
                    DownloadProgress = 0; // Reset thanh process

                    // 4. Xử lý Nén ZIP nếu được chọn
                    if (isZip)
                    {
                        // Nếu người dùng chưa thêm đuôi .zip, ta tự thêm
                        if (!targetFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            targetFileName += ".zip";

                        // Tạo file zip tạm
                        string tempZipPath = Path.Combine(Path.GetTempPath(), targetFileName);
                        if (File.Exists(tempZipPath)) File.Delete(tempZipPath);

                        // Nén file gốc vào zip
                        using (var zip = ZipFile.Open(tempZipPath, ZipArchiveMode.Create))
                        {
                            zip.CreateEntryFromFile(originalFilePath, originalFileName);
                        }

                        fileToSendPath = tempZipPath;
                        needDeleteTemp = true; // Đánh dấu để xóa sau khi xong
                    }

                    // 5. Gửi Yêu cầu Upload (RequestUpload)
                    // Ghép đường dẫn đích trên Server
                    string serverDestPath = Path.Combine(CurrentPath, targetFileName);

                    var requestPayload = new FileChunkDto
                    {
                        FileName = serverDestPath, // Gửi full path để Server biết lưu vào đâu
                        TotalSize = new FileInfo(fileToSendPath).Length
                    };

                    // Gửi gói tin RequestUpload
                    // LƯU Ý: Ta cần đợi Server trả lời "UploadReady" mới bắn dữ liệu
                    // Để đơn giản hóa logic async/await trong ViewModel, ta sẽ bắn Request
                    // Và xử lý việc bắn Data trong sự kiện OnPacketReceived

                    // Tuy nhiên, để code clean hơn, ta sẽ gửi Request, và Client sẽ tự động gửi Data luôn 
                    // (giả định Server luôn chấp nhận - Optimistic). 
                    // Hoặc chuẩn nhất: Gửi Request -> Server Rep -> Client Send Chunks.

                    // Ở đây mình làm cách chuẩn: Gửi Request và lưu trạng thái chờ.
                    _pendingUploadPath = fileToSendPath; // Biến tạm lưu đường dẫn file đang chờ gửi
                    _pendingUploadDeleteTemp = needDeleteTemp; // Biến tạm lưu cờ xóa

                    SendRequest(CommandCode.RequestUpload, requestPayload);
                }
                catch (Exception ex)
                {
                    IsDownloading = false;
                    MessageBox.Show("Lỗi chuẩn bị file: " + ex.Message);
                }
            }
        }
    }
}
