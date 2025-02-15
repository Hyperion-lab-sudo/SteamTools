﻿using Avalonia.Controls;
using ReactiveUI;
using System.Application.UI.ViewModels;
using System.Threading.Tasks;
using System.Windows;

namespace System.Application.Services.Implementation
{
    internal sealed class ShowWindowServiceImpl : IShowWindowService
    {
        static Type GetWindowType(CustomWindow customWindow)
        {
            var windowType = Type.GetType($"System.Application.UI.Views.Windows.{customWindow}Window");
            if (windowType != null && typeof(Window).IsAssignableFrom(windowType)) return windowType;
            throw new ArgumentOutOfRangeException(nameof(customWindow), customWindow, null);
        }

        //static Type GetWindowType(CustomWindow customWindow) => customWindow switch
        //{
        //    CustomWindow.MessageBox => typeof(MessageBoxWindow),
        //    CustomWindow.LoginOrRegister => typeof(LoginOrRegisterWindow),
        //    CustomWindow.AddAuth => typeof(AddAuthWindow),
        //    CustomWindow.ShowAuth => typeof(ShowAuthWindow),
        //    CustomWindow.AuthTrade => typeof(AuthTradeWindow),
        //    _ => throw new ArgumentOutOfRangeException(nameof(customWindow), customWindow, null),
        //};

        Task Show(Type typeWindowViewModel,
            bool isDialog,
            CustomWindow customWindow,
            string title,
            WindowViewModel? viewModel,
            ResizeModeCompat resizeMode,
            Action<DialogWindowViewModel>? actionDialogWindowViewModel = null)
            => MainThreadDesktop.InvokeOnMainThreadAsync(async () =>
            {
                var windowType = GetWindowType(customWindow);
                var window = (Window)Activator.CreateInstance(windowType);
                if (viewModel == null) viewModel = (WindowViewModel)Activator.CreateInstance(typeWindowViewModel);
                if (!string.IsNullOrEmpty(title))
                {
                    viewModel.Title = title;
                }
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.SetResizeMode(resizeMode);
                if (typeof(DialogWindowViewModel).IsAssignableFrom(typeWindowViewModel))
                {
                    static void BindingDialogWindowViewModel(Window window, DialogWindowViewModel dialogWindowViewModel, Action<DialogWindowViewModel>? actionDialogWindowViewModel)
                    {
                        actionDialogWindowViewModel?.Invoke(dialogWindowViewModel);
                        if (dialogWindowViewModel.OK == null)
                        {
                            dialogWindowViewModel.OK = ReactiveCommand.Create(() =>
                            {
                                dialogWindowViewModel.DialogResult = true;
                                window.Close();
                            });
                        }
                        if (dialogWindowViewModel.Cancel == null)
                        {
                            dialogWindowViewModel.Cancel = ReactiveCommand.Create(() =>
                            {
                                dialogWindowViewModel.DialogResult = false;
                                window.Close();
                            });
                        }
                    }
                    if (viewModel == null)
                    {
                        void Window_DataContextChanged(object _, EventArgs __)
                        {
                            if (window.DataContext is DialogWindowViewModel dialogWindowViewModel)
                            {
                                BindingDialogWindowViewModel(window, dialogWindowViewModel, actionDialogWindowViewModel);
                            }
                        }
                        void Window_Closed(object _, EventArgs __)
                        {
                            window.DataContextChanged -= Window_DataContextChanged;
                            window.Closed -= Window_Closed;
                        }
                        window.DataContextChanged += Window_DataContextChanged;
                        window.Closed += Window_Closed;
                    }
                    else if (viewModel is DialogWindowViewModel dialogWindowViewModel)
                    {
                        BindingDialogWindowViewModel(window, dialogWindowViewModel, actionDialogWindowViewModel);
                    }
                }
                window.DataContext = viewModel;
                if (isDialog)
                {
                    await IDesktopAvaloniaAppService.Instance.ShowDialogWindow(window);
                }
                else
                {
                    IDesktopAvaloniaAppService.Instance.ShowWindow(window);
                }
            });

        public Task Show<TWindowViewModel>(
            CustomWindow customWindow,
            TWindowViewModel? viewModel = null,
            string title = "",
            ResizeModeCompat resizeMode = ResizeModeCompat.NoResize,
            bool isDialog = false)
            where TWindowViewModel : WindowViewModel, new() => Show(typeof(TWindowViewModel),
                customWindow, viewModel, title, resizeMode, isDialog);

        public Task Show(Type typeWindowViewModel,
            CustomWindow customWindow,
            WindowViewModel? viewModel = null,
            string title = "",
            ResizeModeCompat resizeMode = ResizeModeCompat.NoResize,
            bool isDialog = false) => Show(typeWindowViewModel, isDialog, customWindow,
                title, viewModel, resizeMode);

        public async Task<bool> ShowDialog<TWindowViewModel>(
            CustomWindow customWindow,
            TWindowViewModel? viewModel = null,
            string title = "",
            ResizeModeCompat resizeMode = ResizeModeCompat.NoResize,
            bool isDialog = true)
            where TWindowViewModel : WindowViewModel, new()
        {
            DialogWindowViewModel? dialogWindowViewModel = null;
            await Show(typeof(TWindowViewModel), isDialog, customWindow,
                title, viewModel, resizeMode, dwvm =>
            {
                dialogWindowViewModel = dwvm;
            });
            return dialogWindowViewModel?.DialogResult ?? false;
        }
    }
}