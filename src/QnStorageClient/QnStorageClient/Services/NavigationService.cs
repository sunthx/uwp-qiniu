﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace QnStorageClient.Services
{
    public static class NavigationService
    {
        private static readonly Dictionary<string,Type> PageTypes = new Dictionary<string, Type>();
        private static Frame _mainFrame;

        static NavigationService()
        {
            SystemNavigationManager.GetForCurrentView().BackRequested += NavigationService_BackRequested            ;
        }

        private static void NavigationService_BackRequested(object sender, BackRequestedEventArgs e)
        {
            GoBack();
        }

        public static Frame MainFrame
        {
            set => _mainFrame = value;
            get => _mainFrame ?? Window.Current.Content as Frame;
        }

        public static bool CanGoBack { get;  } = MainFrame.CanGoBack;

        public static void GoBack()
        {
            if (MainFrame.CanGoBack)
            {
                MainFrame.GoBack();
            }

            UpdateBackButtonState();
        }

        public static void RegisterPageType(string pageKey, Type pageType)
        {
            lock (PageTypes)
            {
                if (PageTypes.ContainsKey(pageKey))
                {
                    return;
                }

                PageTypes.Add(pageKey,pageType);
            }
        }

        public static void NaviageTo(string pageKey,object parameter = null)
        {
            Type pageType = null;
            lock (PageTypes)
            {
                if (PageTypes.ContainsKey(pageKey))
                {
                    pageType = PageTypes[pageKey];
                }
            }

            if (pageType != null)
            {
                MainFrame?.Navigate(pageType, parameter);
            }

            UpdateBackButtonState();
        }

        private static void UpdateBackButtonState()
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = MainFrame.CanGoBack ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }
    }
}
