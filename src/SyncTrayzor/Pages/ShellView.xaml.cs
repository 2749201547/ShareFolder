using System.Windows;
using System.Windows.Input;

namespace SyncTrayzor.Pages
{
    public partial class ShellView : Window
    {
        public ShellView()
        {
            InitializeComponent();

            CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, OnCloseWindow));
            CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, OnMaximizeWindow));
            CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, OnMinimizeWindow));
            CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, OnRestoreWindow));
        }

        private void OnCloseWindow(object sender, ExecutedRoutedEventArgs e) => SystemCommands.CloseWindow(this);

        private void OnMaximizeWindow(object sender, ExecutedRoutedEventArgs e) => SystemCommands.MaximizeWindow(this);

        private void OnMinimizeWindow(object sender, ExecutedRoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

        private void OnRestoreWindow(object sender, ExecutedRoutedEventArgs e) => SystemCommands.RestoreWindow(this);
    }
}
