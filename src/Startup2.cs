﻿#if !__MOBILE__
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
#else
using Xamarin.Essentials;
#endif
#if UI_DEMO
using Moq;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Application.Models;
using System.Application.Services;
using System.Application.Services.CloudService;
using System.Application.Services.Implementation;
using System.Application.UI;
using System.Collections.Generic;
using System.Reflection;
using System.Net;
using System.Application.Services.CloudService.Clients.Abstractions;
using System.Linq;
using CSConst = System.Application.Services.CloudService.Constants;
using System.Properties;
using System.Diagnostics;
using System.IO;
using static System.Application.AppClientAttribute;

namespace System.Application
{
    internal static class Startup
    {
        static bool isInitialized;

        /// <summary>
        /// 初始化启动
        /// </summary>
        public static void Init(DILevel level)
        {
            if (!isInitialized)
            {
                isInitialized = true;
#if !__MOBILE__
                FileSystemDesktop.InitFileSystem();
#endif
                if (level.HasFlag(DILevel.ServerApiClient))
                {
                    ModelValidatorProvider.Init();
                }
                InitDI(level);
                static void InitDI(DILevel level)
                {
#if UI_DEMO
                    DI.Init(new MockServiceProvider(ConfigureDemoServices));
#else
                    DI.Init(s => ConfigureServices(s, level));
                    static void ConfigureServices(IServiceCollection services, DILevel level)
                    {
                        IsMainProcess = level == DILevel.MainProcess;
                        ConfigureRequiredServices(services);
                        ConfigureDemandServices(services, level);
                    }
#endif
                }
            }
        }

        /// <summary>
        /// 配置任何进程都必要的依赖注入服务
        /// </summary>
        /// <param name="services"></param>
        static void ConfigureRequiredServices(IServiceCollection services)
        {
            // 添加日志实现
#if __ANDROID__
            services.AddClientLogging();
#elif __IOS__
            services.AddLogging(cfg => cfg.AddProvider(NullLoggerProvider.Instance));
#else
            services.AddDesktopLogging();
#endif
#if __MOBILE__
            // 添加运行时权限
            services.TryAddPermissions();
            services.AddPlatformPermissions();
#endif
        }

        /// <summary>
        /// 配置按需使用的依赖注入服务
        /// </summary>
        /// <param name="services"></param>
        static void ConfigureDemandServices(IServiceCollection services, DILevel level)
        {
            var hasMainProcessRequired = level.HasFlag(DILevel.MainProcessRequired);
#if !__MOBILE__
            HasNotifyIcon = hasMainProcessRequired;
#if !UI_DEMO
            // 桌面平台服务 此项放在其他通用业务实现服务之前
            services.AddDesktopPlatformService();
#endif
#endif
            var hasGUI = level.HasFlag(DILevel.GUI);
            var hasServerApiClient = level.HasFlag(DILevel.ServerApiClient);
            var hasHttpClientFactory = level.HasFlag(DILevel.HttpClientFactory);
            var hasHttpProxy = level.HasFlag(DILevel.HttpProxy);
            var hasHosts = level.HasFlag(DILevel.Hosts);
            var hasSteam = level.HasFlag(DILevel.Steam);

            if (hasGUI)
            {
                // 添加 Toast 提示服务
                services.TryAddToast();
#if __MOBILE__
                // 添加电话服务
                services.AddTelephonyService();
#else
                services.AddSingleton<IDesktopAppService>(s => App.Instance);
                services.AddSingleton<IDesktopAvaloniaAppService>(s => App.Instance);

                // 添加管理主窗口服务
                services.AddWindowService();

                // 添加主线程助手(MainThreadDesktop)
                services.AddMainThreadPlatformService();

                #region MessageBox

                /* System.Windows.MessageBox 在 WPF 库中，仅支持 Win 平台
                 * 改为 System.Windows.MessageBoxCompat 可跨平台兼容
                 * 在其他平台上使用 MessageBox.Avalonia 库实现
                 * API变更说明：
                 * - 如果需要获取返回值，即点击那个按钮，则使用异步版本 ShowAsync
                 * - 如果不需要获取返回值，则可直接使用 同步版本 Show
                 * 注意事项：
                 * - 图标(Icon)与按钮(Button)不要使用标记为 Obsolete 的
                 * - WPF 中 显示窗口(Show)会锁死父窗口等，类似 ShowDialog
                 * - MessageBox.Avalonia 中则不会锁死窗口
                 * 已知问题：
                 * - 在 MessageBox.Avalonia 中
                 *  - 如果内容文本(messageBoxText)过短 UI 上的文字显示不全
                 *  - 点击窗口按 Ctrl+C 无法复制弹窗中的文本内容
                 *  - 按钮文本(ButtonText)缺少本地化翻译(Translate)
                 *  - 某些图标图片与枚举值不太匹配，例如 Information
                 */
                services.AddShowWindowService();

#if WINDOWS
                // 可选项，在 Win 平台使用 WPF 实现的 MessageBox
                //services.AddSingleton<IMessageBoxCompatService, WPFMessageBoxCompatService>();
#endif

                #endregion
#endif
            }

            if (hasHttpClientFactory || hasServerApiClient)
            {
#if __MOBILE__
                // 添加 Http 平台助手移动端实现
                services.AddPlatformHttpPlatformHelper();
#else
                // 添加 Http 平台助手桌面端实现
                services.AddDesktopHttpPlatformHelper();
#endif
            }

            if (hasHttpClientFactory)
            {
#if __MOBILE__
                // 添加 HttpClientFactory 平台原生实现
                services.AddNativeHttpClient();
#endif
                // 通用 Http 服务
                services.AddHttpService();
            }

#if !__MOBILE__
            services.TryAddScriptManager();
#endif
            if (hasHttpProxy)
            {
                // 通用 Http 代理服务
                services.AddHttpProxyService();
            }

            if (hasServerApiClient)
            {
                // 添加 app 配置项
                services.TryAddOptions(AppSettings);
#if !__MOBILE__
                // 添加安全服务
                services.AddSecurityService<EmbeddedAesDataProtectionProvider, LocalDataProtectionProvider>();
#endif
                // 添加模型验证框架
                services.TryAddModelValidator();

                // 添加服务端API调用
                services.TryAddCloudServiceClient<CloudServiceClient>();

                services.AddAutoMapper();

                // 添加仓储服务
                services.AddRepositories();

                // 键值对存储
                services.TryAddStorage();

                // 业务平台用户管理
                services.TryAddUserManager();
            }

            // 添加通知服务
            AddNotificationService();
            void AddNotificationService()
            {
#if !__MOBILE__
                if (!IsMainProcess) return;
#endif
                services.AddNotificationService();
            }

#if !__MOBILE__
            if (hasGUI || hasServerApiClient)
            {
                // 业务用户配置文件服务()
                //services.AddConfigFileService();
            }

            if (hasHosts)
            {
                // hosts 文件助手服务
                services.AddHostsFileService();
            }

            if (hasSteam)
            {
                // Steam 相关助手、工具类服务
                services.AddSteamService();

                // Steamworks LocalApi Service
                services.TryAddSteamworksLocalApiService();

                // SteamDb WebApi Service
                services.AddSteamDbWebApiService();

                // Steamworks WebApi Service
                services.AddSteamworksWebApiService();
            }

            if (hasMainProcessRequired)
            {
                // 应用程序更新服务
                services.AddAppUpdateService();
            }
            if (HasNotifyIcon)
            {
                // 托盘图标
#if WINDOWS
                //services.AddTransient<INotifyIconWindow<ContextMenu>, Win32NotifyIconWindow>();
#endif
#if !UI_DEMO
                services.AddNotifyIcon<NotifyIconImpl>();
#endif
            }
#endif
        }

#if UI_DEMO
        static void ConfigureDemoServices(IServiceCollection services)
        {
            services.AddLogging(cfg => cfg.AddProvider(NullLoggerProvider.Instance));

            services.AddSingleton<ICloudServiceClient, MockCloudServiceClient>();
        }
#endif

        static AppSettings AppSettings
        {
            get
            {
                var options = new AppSettings
                {
                    AppVersion = GetResValueGuid("app-id", isSingle: false, ResValueFormat.StringGuidN),
                    AesSecret = GetResValue("aes-key", isSingle: true, ResValueFormat.String),
                    RSASecret = GetResValue("rsa-public-key", isSingle: false, ResValueFormat.String),
                };
                SetApiBaseUrl(options);
                return options;
                static Guid GetResValueGuid(string name, bool isSingle, ResValueFormat format) => GetResValue(name, isSingle, format).TryParseGuidN() ?? default;
                static string? GetResValue(string name, bool isSingle, ResValueFormat format)
                {
                    const string namespacePrefix = "System.Application.UI.Resources.";
                    var assembly = Assembly.GetExecutingAssembly();
                    Stream? func(string x) => assembly.GetManifestResourceStream(x);
                    var r = AppClientAttribute.GetResValue(func, name, isSingle, namespacePrefix, format);
                    return r;
                }
                static void SetApiBaseUrl(AppSettings s)
                {
#if DEBUG
                    if (BuildConfig.IsAigioPC)
                    {
                        try
                        {
                            var url = CSConst.Prefix_HTTPS + "localhost:5001";
                            var request = WebRequest.CreateHttp(url);
                            request.Timeout = 999;
                            request.GetResponse();
                            s.ApiBaseUrl = url;
                            return;
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.ToString());
                        }
                    }
#endif
                    var value =
                        (ThisAssembly.Debuggable || !s.GetIsOfficialChannelPackage()) ?
                        CSConst.Prefix_HTTPS + "pan.mossimo.net:8862" :
                        CSConst.Prefix_HTTPS + "api.steampp.net";
                    s.ApiBaseUrl = value;
                }
            }
        }

#if !__MOBILE__
        public static bool HasNotifyIcon { get; private set; }

#if !UI_DEMO
        sealed class NotifyIconImpl : NotifyIcon<ContextMenu>, INotifyIcon { }
#endif

#if WINDOWS
        //sealed class Win32NotifyIconWindow : MainWindow, INotifyIconWindow<ContextMenu>
        //{
        //    sealed class Win32WindowImpl : Avalonia.Win32.WindowImpl
        //    {
        //        public Win32NotifyIconWindow? Window { get; set; }

        //        protected override IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        //        {
        //            var _notifyIcon = Window?.NotifyIcon;
        //            var value = _notifyIcon == null ? null : NotifyIcon<ContextMenu>.WndProc(_notifyIcon, msg, wParam, lParam);
        //            return value ?? base.WndProc(hWnd, msg, wParam, lParam);
        //        }
        //    }

        //    public Win32NotifyIconWindow() : base(new Win32WindowImpl())
        //    {
        //        if (PlatformImpl is Win32WindowImpl impl)
        //        {
        //            impl.Window = this;
        //        }
        //        else
        //        {
        //            throw new PlatformNotSupportedException();
        //        }
        //    }

        //    public IntPtr Handle => PlatformImpl.Handle.Handle;

        //    [NotNull, DisallowNull] // C# 8 not null
        //    public NotifyIcon<ContextMenu>? NotifyIcon { get; private set; }

        //    public void Initialize(INotifyIcon<ContextMenu> notifyIcon)
        //    {
        //        if (notifyIcon is NotifyIcon<ContextMenu> _notifyIcon)
        //        {
        //            NotifyIcon = _notifyIcon;
        //        }
        //        else
        //        {
        //            throw new PlatformNotSupportedException();
        //        }
        //        //Content = NotifyIcon;
        //    }
        //}

        //        sealed class WPFMessageBoxCompatService : IMessageBoxCompatService
        //        {
        //            static MessageBoxButton GetButtonEnum(MessageBoxButtonCompat button) => button switch
        //            {
        //                MessageBoxButtonCompat.OK => MessageBoxButton.OK,
        //                MessageBoxButtonCompat.OKCancel => MessageBoxButton.OKCancel,
        //#pragma warning disable CS0618 // 类型或成员已过时
        //                MessageBoxButtonCompat.YesNo => MessageBoxButton.YesNo,
        //                MessageBoxButtonCompat.YesNoCancel => MessageBoxButton.YesNoCancel,
        //#pragma warning restore CS0618 // 类型或成员已过时
        //                _ => throw new ArgumentOutOfRangeException(nameof(button), $"value: {button}"),
        //            };

        //            static MessageBoxImage GetIcon(MessageBoxImageCompat icon) => icon switch
        //            {
        //                MessageBoxImageCompat.Asterisk => MessageBoxImage.Asterisk,
        //                MessageBoxImageCompat.Error => MessageBoxImage.Error,
        //                MessageBoxImageCompat.Exclamation => MessageBoxImage.Exclamation,
        //                MessageBoxImageCompat.None => MessageBoxImage.None,
        //#pragma warning disable CS0618 // 类型或成员已过时
        //                MessageBoxImageCompat.Question => MessageBoxImage.Question,
        //#pragma warning restore CS0618 // 类型或成员已过时
        //                _ => throw new ArgumentOutOfRangeException(nameof(icon), $"value: {icon}"),
        //            };

        //            static MessageBoxResultCompat GetResult(MessageBoxResult result) => result switch
        //            {
        //                MessageBoxResult.OK => MessageBoxResultCompat.OK,
        //#pragma warning disable CS0618 // 类型或成员已过时
        //                MessageBoxResult.Yes => MessageBoxResultCompat.Yes,
        //                MessageBoxResult.No => MessageBoxResultCompat.No,
        //#pragma warning restore CS0618 // 类型或成员已过时
        //                MessageBoxResult.Cancel => MessageBoxResultCompat.Cancel,
        //                MessageBoxResult.None => MessageBoxResultCompat.None,
        //                _ => throw new ArgumentOutOfRangeException(nameof(result), $"value: {result}"),
        //            };

        //            public Task<MessageBoxResultCompat> ShowAsync(string messageBoxText, string caption, MessageBoxButtonCompat button, MessageBoxImageCompat? icon)
        //            {
        //                var button_ = GetButtonEnum(button);
        //                if (icon.HasValue)
        //                {
        //                    var icon_ = GetIcon(icon.Value);
        //                    var result = MessageBox.Show(messageBoxText, caption, button_, icon_);
        //                    return Task.FromResult(GetResult(result));
        //                }
        //                else
        //                {
        //                    var result = MessageBox.Show(messageBoxText, caption, button_);
        //                    return Task.FromResult(GetResult(result));
        //                }
        //            }
        //        }
#endif
#endif

#if UI_DEMO
        sealed class MockServiceProvider : IServiceProvider
        {
            static readonly Type typeMock = typeof(Mock<>);
            readonly Dictionary<Type, object?> pairs = new();
            readonly IServiceProvider serviceProvider;

            public MockServiceProvider(Action<IServiceCollection> configureServices)
            {
                var services = new ServiceCollection();
                configureServices(services);
                serviceProvider = services.BuildServiceProvider();
            }

            public object? GetService(Type serviceType)
            {
                var service = serviceProvider.GetService(serviceType);
                if (service != null) return service;
                if (pairs.ContainsKey(serviceType)) return pairs[serviceType];
                var mockServiceType = typeMock.MakeGenericType(serviceType);
                var mockService = (Mock?)Activator.CreateInstance(mockServiceType);
                service = mockService?.Object;
                pairs.Add(serviceType, service);
                return service;
            }
        }
#endif

        /// <summary>
        /// 当前是否是主进程
        /// </summary>
        internal static bool IsMainProcess { get; set; }

        /// <inheritdoc cref="IActiveUserClient.Post(ActiveUserRecordDTO, Guid?)"/>
        internal static async void ActiveUserPost(ActiveUserType type)
        {
            if (!IsMainProcess) return;
            try
            {
#if !__MOBILE__
                var screens = App.Instance.MainWindow.Screens;
#else
                var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
                var mainDisplayInfoH = mainDisplayInfo.Height.ToInt32(NumberToInt32Format.Ceiling);
                var mainDisplayInfoW = mainDisplayInfo.Width.ToInt32(NumberToInt32Format.Ceiling);
#endif
                var req = new ActiveUserRecordDTO
                {
                    Type = type,
#if __MOBILE__
                    ScreenCount = 1,
                    PrimaryScreenPixelDensity = mainDisplayInfo.Density,
                    PrimaryScreenWidth = mainDisplayInfoW,
                    PrimaryScreenHeight = mainDisplayInfoH,
                    SumScreenWidth = mainDisplayInfoW,
                    SumScreenHeight = mainDisplayInfoH,
#else
                    ScreenCount = screens.ScreenCount,
                    PrimaryScreenPixelDensity = screens.Primary.PixelDensity,
                    PrimaryScreenWidth = screens.Primary.Bounds.Width,
                    PrimaryScreenHeight = screens.Primary.Bounds.Height,
                    SumScreenWidth = screens.All.Sum(x => x.Bounds.Width),
                    SumScreenHeight = screens.All.Sum(x => x.Bounds.Height),
#endif
                };
                Guid? lastNotificationRecordId = default;
                if (type == ActiveUserType.OnStartup)
                {
                    lastNotificationRecordId = await INotificationService.GetLastNotificationRecordId();
                }
                var rsp = await ICloudServiceClient.Instance.ActiveUser.Post(req, lastNotificationRecordId);
                INotificationService.Notify(rsp, type);
            }
            catch (Exception e)
            {
                Log.Error(nameof(App), e, "ActiveUserPost");
            }
        }
    }
}