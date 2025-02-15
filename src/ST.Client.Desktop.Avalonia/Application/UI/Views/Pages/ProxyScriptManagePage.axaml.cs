using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Application.Services;
using System.Application.UI.ViewModels;
using System.Collections.Generic;
using System.Properties;

namespace System.Application.UI.Views.Pages
{
    public class ProxyScriptManagePage : UserControl
    {
        public ProxyScriptManagePage()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void AddNewScriptButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var fileDialog = new OpenFileDialog
            {
                Filters = new List<FileDialogFilter> {
                    new FileDialogFilter { Name = "JavaScript Files", Extensions = new List<string> { "js" } },
                    new FileDialogFilter { Name = "Text Files", Extensions = new List<string> { "txt" } },
                    new FileDialogFilter { Name = "All Files", Extensions = new List<string> { "*" } },
                },
                Title = ThisAssembly.AssemblyTrademark,
                AllowMultiple = false,
            };
            fileDialog.ShowAsync(IDesktopAvaloniaAppService.Instance.MainWindow).ContinueWith(s =>
            {
                if (s != null && s.Result.Length > 0)
                {
                    ProxyService.Current.AddNewScript(s.Result[0]);
                }
            });
        }  
    }
}