using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TunnelTimer.App.ViewModels;
using TunnelTimer.Core;
using Windows.Foundation;

namespace TunnelTimer.App.Views;

public sealed partial class WidgetView : UserControl
{
    public WidgetView(WidgetViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += (_, _) => ApplyPhase(useTransitions: false);
    }

    public WidgetViewModel ViewModel { get; }

    /// <summary>
    /// Bounds of the area holding the buttons, relative to the window content. Everything
    /// above it is a drag surface.
    /// </summary>
    public Rect GetInteractiveBounds() =>
        ActionsPanel
            .TransformToVisual(null)
            .TransformBounds(new Rect(0, 0, ActionsPanel.ActualWidth, ActionsPanel.ActualHeight));

    // Instance method on purpose: x:Bind resolves function bindings on the control instance.
#pragma warning disable CA1822
    public Visibility VisibleWhenFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
#pragma warning restore CA1822

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetViewModel.Phase))
        {
            ApplyPhase(useTransitions: true);
        }
    }

    private void ApplyPhase(bool useTransitions)
    {
        // Hidden has no look of its own: the window is not visible in that phase.
        if (ViewModel.Phase != CountdownPhase.Hidden)
        {
            VisualStateManager.GoToState(this, ViewModel.Phase.ToString(), useTransitions);
        }
    }
}
