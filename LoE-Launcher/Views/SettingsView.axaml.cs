using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LoE_Launcher.ViewModels;

namespace LoE_Launcher.Views;

public partial class SettingsView : UserControl
{
    private ScrollViewer _pageScroll = null!;
    private TextBox _testerUsername = null!;
    private SettingsViewModel? _viewModel;

    public SettingsView()
    {
        AvaloniaXamlLoader.Load(this);
        _pageScroll = this.FindControl<ScrollViewer>("pageScroll")!;
        _testerUsername = this.FindControl<TextBox>("testerUsername")!;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Closed -= OnPanelClosed;
        }

        _viewModel = DataContext as SettingsViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Closed += OnPanelClosed;
        }

        base.OnDataContextChanged(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.SelectedPage))
        {
            ScrollToTop();
        }
        else if (e.PropertyName == nameof(SettingsViewModel.IsTesterLoginOpen))
        {
            ScrollToTop();
            if (_viewModel is { IsTesterLoginOpen: true })
            {
                Dispatcher.UIThread.Post(() => _testerUsername.Focus(), DispatcherPriority.Loaded);
            }
        }
    }

    private void OnPanelClosed(object? sender, EventArgs e) => ScrollToTop();

    private void ScrollToTop() => _pageScroll.Offset = new Vector(0, 0);
}
