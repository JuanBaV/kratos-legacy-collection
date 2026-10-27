using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Input;

namespace KratosLegacyCollection.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => { if (!viewModel.ReducedMotion && SystemParameters.ClientAreaAnimation) BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))); SetInitialFocus(viewModel); };
        viewModel.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.SetupVisible)) Dispatcher.BeginInvoke(() => SetInitialFocus(viewModel)); };
        Closing += OnClosing;
    }
    private void SetInitialFocus(MainViewModel vm)
    {
        var target = vm.SetupVisible ? SetupNext : LibraryNav;
        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(target), target);
        target.Focus();
    }
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } files)
            return;
        var vm = (MainViewModel)DataContext;
        if (vm.ImportTextures.CanExecute(files[0]))
        {
            vm.Page = "ENHANCEMENTS";
            vm.ImportTextures.Execute(files[0]);
        }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel { Busy: true })
        {
            e.Cancel = true;
            MessageBox.Show("Wait for the current operation or choose CANCEL before closing.", "Kratos Legacy Collection", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
