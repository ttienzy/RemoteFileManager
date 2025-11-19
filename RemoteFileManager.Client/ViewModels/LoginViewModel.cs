using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RemoteFileManager.Client.Services.GrpcServices;
using RemoteFileManager.Client.Views;
using System.Windows;

namespace RemoteFileManager.Client.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthGrpcClient _authClient;
    private readonly FileManagerGrpcClient _fileManagerClient;
    private readonly StreamingGrpcClient _streamingClient;

    [ObservableProperty]
    private string _serverAddress = "http://localhost:5000";

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;
    public bool IsNotLoading => !IsLoading;

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotLoading));
    }
    public LoginViewModel(
        AuthGrpcClient authClient,
        FileManagerGrpcClient fileManagerClient,
        StreamingGrpcClient streamingClient)
    {
        _authClient = authClient;
        _fileManagerClient = fileManagerClient;
        _streamingClient = streamingClient;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter username and password";
            return;
        }

        IsLoading = true;

        try
        {
            var response = await _authClient.LoginAsync(Username, Password);

            if (response.Success)
            {
                // Set token for other clients
                _fileManagerClient.SetAuthToken(response.Token);
                _streamingClient.SetAuthToken(response.Token);

                // Open main window
                var mainWindow = new MainWindow
                {
                    DataContext = new MainViewModel(
                        _fileManagerClient,
                        _streamingClient,
                        response.UserInfo.Username)
                };

                mainWindow.Show();

                // Close login window
                Application.Current.Windows.OfType<LoginWindow>().FirstOrDefault()?.Close();
            }
            else
            {
                ErrorMessage = response.Message;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}