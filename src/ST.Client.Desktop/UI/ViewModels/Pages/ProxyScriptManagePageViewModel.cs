﻿using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using ReactiveUI;
using System.Application.Models;
using System.Application.Repositories;
using System.Application.Services;
using System.Application.UI.Resx;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Properties;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static Newtonsoft.Json.JsonConvert;

namespace System.Application.UI.ViewModels
{
	public class ProxyScriptManagePageViewModel : TabItemViewModel
	{
		private readonly Subject<Unit> updateSource = new();
		public bool IsReloading { get; set; }

		public override string Name
		{
			get => AppResources.ScriptConfig;
			protected set { throw new NotImplementedException(); }
		}

		private IObservable<Unit> UpdateAsync()
		{
			bool Predicate(ScriptDTO s)
			{
				if (string.IsNullOrEmpty(SerachText))
					return true;
				if (!string.IsNullOrEmpty(SerachText))
				{
					if (s.Name.Contains(SerachText, StringComparison.OrdinalIgnoreCase) ||
						s.Author.Contains(SerachText, StringComparison.OrdinalIgnoreCase) ||
						s.Description.Contains(SerachText, StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
				return false;
			}

			return Observable.Start(() =>
			{
				var list = ProxyService.Current.ProxyScripts?.Where(x => Predicate(x)).OrderBy(x => x.Name).ToList();
				if (list.Any_Nullable())
					ProxyScripts = new ObservableCollection<ScriptDTO>(list);
				else
					ProxyScripts = null;
			});
		}

		public void Update()
		{
			updateSource.OnNext(Unit.Default);
		}

		public ProxyScriptManagePageViewModel()
		{
			IconKey = nameof(ProxyScriptManagePageViewModel).Replace("ViewModel", "Svg");

			MenuItems = new ObservableCollection<MenuItemViewModel>()
			{
				   new MenuItemViewModel (nameof(AppResources.CommunityFix_EnableScriptService)),
				   new MenuItemViewModel (nameof(AppResources.CommunityFix_ScriptManage)),
			};
			this.updateSource
			.Do(_ => this.IsReloading = true)
			.SelectMany(x => this.UpdateAsync())
			.Do(_ => this.IsReloading = false)
			.Subscribe()
			.AddTo(this);

			ProxyService.Current
				.WhenAnyValue(x => x.ProxyScripts)
				.Subscribe(_ => Update());

			this.WhenAnyValue(x => x.SerachText)
				  .Subscribe(_ =>
				  {
					  Update();
				  });
			//ProxyService.Current.Subscribe(nameof(ProxyService.Current.ProxyScripts), this.Update).AddTo(this);
		}

		private string? _SerachText;
		public string? SerachText
		{
			get => _SerachText;
			set => this.RaiseAndSetIfChanged(ref _SerachText, value);
		}

		private IList<ScriptDTO>? _ProxyScripts;
		public IList<ScriptDTO>? ProxyScripts
		{
			get => _ProxyScripts;
			set
			{
				if (_ProxyScripts != value)
				{
					_ProxyScripts = value;
					this.RaisePropertyChanged();
					this.RaisePropertyChanged(nameof(IsProxyScriptsEmpty));
				}
			}
		}

		public bool IsProxyScriptsEmpty => !ProxyScripts.Any_Nullable();

		public void RefreshScriptButton()
		{
			ProxyService.Current.RefreshScript();
			Toast.Show(string.Format(@AppResources.Success_, @AppResources.Refresh));
		}
		public void DeleteScriptItemButton(ScriptDTO script)
		{
			var result = MessageBoxCompat.ShowAsync(@AppResources.Script_DeleteItem, ThisAssembly.AssemblyTrademark, MessageBoxButtonCompat.OKCancel).ContinueWith(async (s) =>
			{
				if (s.Result == MessageBoxResultCompat.OK)
				{
					var item = await DI.Get<IScriptManagerService>().DeleteScriptAsync(script);
					if (item.state)
					{
						if (ProxyService.Current.ProxyScripts != null)
							ProxyService.Current.ProxyScripts.Remove(script);

					}
					Toast.Show(item.msg);
				}
			});
		}
		public void EditScriptItemButton(ScriptDTO script)
		{

			var url = Path.Combine(IOPath.AppDataDirectory, script.FilePath);
			DI.Get<IDesktopPlatformService>().OpenFileByTextReader(url);
			var result = MessageBoxCompat.ShowAsync(@AppResources.Script_EditTxt, ThisAssembly.AssemblyTrademark, MessageBoxButtonCompat.OKCancel).ContinueWith(async (s) =>
			{
				if (s.Result == MessageBoxResultCompat.OK)
				{
					var item = await DI.Get<IScriptManagerService>().AddScriptAsync(url, script);
					if (item.state)
					{
						if (ProxyService.Current.ProxyScripts != null && item.model != null)
						{
							var index = ProxyService.Current.ProxyScripts.IndexOf(script);
							if (index > -1)
							{
								ProxyService.Current.ProxyScripts[index] = item.model;
							}
						}
					}
				}
			});
		}
		public void OpenHomeScriptItemButton(ScriptDTO script)
		{
			Services.CloudService.Constants.BrowserOpen(script.SourceLink);
		}
		public async void RefreshScriptItemButton(ScriptDTO script)
		{
			if (script != null)
				if (script.FilePath != null)
				{
					var item = await DI.Get<IScriptManagerService>().AddScriptAsync(script.FilePath);
					if (item.state)
						if (item.model != null)
							script = item.model;
						else
						{
							script.Enable = false;
							Toast.Show(item.msg);
						}
				}
		}
	}
}