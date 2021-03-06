﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Toolkit.Uwp;
using Qiniu.Share.Storage;
using QnStorageClient.Models;
using QnStorageClient.Services;
using QnStorageClient.Utils;

namespace QnStorageClient.ViewModels
{
    public class FileListPageViewModel : ViewModelBase
    {                                      
        public FileListPageViewModel()
        {
            UploadFileCommand = new RelayCommand(async()=> await UploadFileCommandExecute());
            RefreshFileListCommand = new RelayCommand(RefreshFileListCommandExecute);
            DeleteFileCommand = new RelayCommand<FileItemViewModel>(async (item) => await DeleteFileCommandExecute(item));
            DownloadFileCommand = new RelayCommand<FileItemViewModel>(DownloadFileCommandExecute);
            CopyFileLinkCommand = new RelayCommand<FileItemViewModel>(CopyFileLinkCommandExecute);
        }

        private BucketObject _currentBucketInfo;
        public BucketObject CurrentBucketInfo
        {
            get => _currentBucketInfo;
            set => Set(ref _currentBucketInfo, value);
        }

        private IncrementalLoadingCollection<FileListSource, FileItemViewModel> _fileItems;
        public IncrementalLoadingCollection<FileListSource, FileItemViewModel> FileItems
        {
            set => Set(ref _fileItems, value);
            get => _fileItems;
        }

        public RelayCommand UploadFileCommand { get; set; }

        public RelayCommand RefreshFileListCommand { get; set; }

        public RelayCommand<FileItemViewModel> CopyFileLinkCommand { get; set; }
      
        public RelayCommand<FileItemViewModel> DeleteFileCommand { get; set; }

        public RelayCommand UploadCommand { get; set; }

        public RelayCommand<FileItemViewModel> DownloadFileCommand { get; set; }

        public void LoadFiles(BucketObject bucketInfo)
        {
            FileItems = new IncrementalLoadingCollection<FileListSource, FileItemViewModel>(new FileListSource(bucketInfo.Name));

            FileItems.OnStartLoading += async () =>
            {
                NotificationService.ShowMessage(ResourceUtils.GetText("LoadFileList"));

                if (CurrentBucketInfo == null || CurrentBucketInfo.Name != bucketInfo.Name)
                {
                    CurrentBucketInfo = bucketInfo;
                    CurrentBucketInfo.RegionName = await GetZoneName(bucketInfo.Name);
                    CurrentBucketInfo.Domains = await QiniuService.Domains(bucketInfo.Name);
                    CurrentBucketInfo.CurrentUsingDomain = CurrentBucketInfo.Domains[0];
                }
            };

            FileItems.OnEndLoading += NotificationService.Dismiss;
        }

        private async Task<string> GetZoneName(string bucketName)
        {
            string zone;
            var zoneInfo = await QiniuService.GetBucketZoneInfo(bucketName);
            if (zoneInfo.ApiHost == Zone.ZONE_AS_Singapore.ApiHost)
            {
                zone = "新加坡";
            }
            else if (zoneInfo.ApiHost == Zone.ZONE_CN_East.ApiHost)
            {
                zone = "华东";
            }
            else if (zoneInfo.ApiHost == Zone.ZONE_CN_North.ApiHost)
            {
                zone = "华北";
            }
            else if (zoneInfo.ApiHost == Zone.ZONE_CN_South.ApiHost)
            {
                zone = "华南";
            }
            else if (zoneInfo.ApiHost == Zone.ZONE_US_North.ApiHost)
            {
                zone = "北美";
            }
            else
            {
                zone = "未知";
            }

            return zone;
        }

        private void DownloadFileCommandExecute(FileItemViewModel item)
        {
            if (item == null)
            {
                foreach (var fileItemViewModel in GetCheckedItems())
                {
                    DownloadFile(fileItemViewModel);
                }
            }
            else
            {
                DownloadFile(item);
            }
        }

        private async Task UploadFileCommandExecute()
        {
            var fileOpenPicker = new FileOpenPicker();
            fileOpenPicker.FileTypeFilter.Add("*");

            var selectResult = await fileOpenPicker.PickMultipleFilesAsync();
            if (!selectResult.Any())
            {
                return;
            }

            foreach (var storageFile in selectResult)
            {
                var fileObject = new FileObject
                {
                    FileName = storageFile.Name,
                    LocalPath = storageFile.Path
                };

                var fileTransferTask = new FileTransferTask(fileObject)
                {
                    BucketObject = CurrentBucketInfo,
                    TransferType = TransferType.Upload,
                    TransferState = TransferState.Idle
                };

                StorageApplicationPermissions.FutureAccessList.AddOrReplace(fileObject.FileName, storageFile);
                FileTransferService.AddTransferTask(fileTransferTask);
            }
        }

        private void RefreshFileListCommandExecute()
        {
            LoadFiles(CurrentBucketInfo);
        }

        private async Task DeleteFileCommandExecute(FileItemViewModel item)
        {
            bool result = false;
            NotificationService.ShowMessage(ResourceUtils.GetText("FileDeleting"));

            if (item == null)
            {
                foreach (var fileItemViewModel in GetCheckedItems())
                {
                    result = await DeleteFile(fileItemViewModel);
                    if (!result)
                        break;
                }
            }
            else
            {
                result = await DeleteFile(item);
            }

            NotificationService.ShowMessage(
                result ? ResourceUtils.GetText("FileDeleted") : ResourceUtils.GetText("FileDeleteFailed"), 2000);
        }

        private void CopyFileLinkCommandExecute(FileItemViewModel item)
        {
            string resouceUrl = QiniuService.CreateResourcePublicUrl(CurrentBucketInfo.CurrentUsingDomain, item.FileObject.FileName);
            Clipboard.Clear();

            DataPackage dataPackage = new DataPackage();
            dataPackage.SetText(resouceUrl);
            Clipboard.SetContent(dataPackage);

            NotificationService.ShowMessage(ResourceUtils.GetText("FileLinkCopied"),2000);
        }

        private List<FileItemViewModel> GetCheckedItems()
        {
            return FileItems.Where(file => file.IsChecked).ToList();
        }

        private void DownloadFile(FileItemViewModel item)
        {
            var resouceUrl = QiniuService.CreateResourcePublicUrl(CurrentBucketInfo.CurrentUsingDomain, item.FileObject.FileName);
            item.FileObject.PublicUrl = $"http://{resouceUrl}";

            var fileTransferTask = new FileTransferTask(item.FileObject)
            {
                TransferType = TransferType.Download
            };

            FileTransferService.AddTransferTask(fileTransferTask);
        }

        private async Task<bool> DeleteFile(FileItemViewModel item)
        {
            bool result = await QiniuService.DeleteFile(CurrentBucketInfo.Name, item.FileObject.FileName);
            if (result)
            {
                FileItems.Remove(item);
            }
            return result;
        }
    }
}