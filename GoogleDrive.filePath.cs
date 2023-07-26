//********************************************************************************************
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Requests;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace Cliver
{
    public partial class GoogleDrive 
    {
        void buildPaths(List<string> paths, string currentPath, Google.Apis.Drive.v3.Data.File currentObject)
        {
            if (currentObject == null || currentObject.Parents == null || currentObject.Parents.Count < 1)//it is root 'My Drive'
            {
                paths.Add(currentPath);
                return;
            }
            currentPath = currentObject.Name + (string.IsNullOrWhiteSpace(currentPath) ? "" : "\\" + currentPath);
            foreach (string parentId in currentObject.Parents)
                buildPaths(paths, currentPath, GetObject(parentId, "id, name, parents"));
        }

        public enum GettingMode
        {
            AlwaysCreateNew,
            GetLatestExistingOrCreate,
            GetLatestExistingOnly,
        }
        public Google.Apis.Drive.v3.Data.File GetFolder(string parentFolderId, string folderName, GettingMode gettingMode, string fields = "id, webViewLink")
        {
            if (parentFolderId == null && string.IsNullOrEmpty(folderName))//root folder
                return GetObject(RootFolderId, fields);
            if (gettingMode != GettingMode.AlwaysCreateNew)
            {
                SearchFilter sf = new SearchFilter { IsFolder = true, ParentId = parentFolderId, Name = folderName };
                IEnumerable<Google.Apis.Drive.v3.Data.File> fs = FindObjects(sf, fields);
                Google.Apis.Drive.v3.Data.File ff = fs.FirstOrDefault();
                if (ff != null)
                    return ff;
                if (gettingMode == GettingMode.GetLatestExistingOnly)
                    return null;
            }
            Google.Apis.Drive.v3.Data.File f = new Google.Apis.Drive.v3.Data.File
            {
                Name = folderName,
                MimeType = FolderMimeType,
                Parents = parentFolderId != null ? new List<string> { parentFolderId } : null
            };
            var request = Service.Files.Create(f);
            request.Fields = getProperFields(fields);
            return request.Execute();
        }
        //replace GetFolder(string parentFolderId, string folderName, GettingMode gettingMode, string fields = "id, webViewLink")
        //public Google.Apis.Drive.v3.Data.File GetFolder(string baseFolderId, string relativeFolderPath, GettingMode gettingMode, string fields = "id, webViewLink"){ }

        public Google.Apis.Drive.v3.Data.File GetFolder(string folderPath, GettingMode gettingMode, string fields = "id, webViewLink")
        {
            if (gettingMode == GettingMode.AlwaysCreateNew
                || !paths2object.TryGetValue(folderPath, out Google.Apis.Drive.v3.Data.File _object)
                || _object == null && gettingMode == GettingMode.GetLatestExistingOrCreate
                )
            {
                Match m = Regex.Match(folderPath, @"^(.*)\\+([^\\]+)$");
                string folderName;
                string parentFolderId;
                if (m.Success)
                {
                    folderName = m.Groups[2].Value;
                    Google.Apis.Drive.v3.Data.File parentFolder = GetFolder(m.Groups[1].Value, gettingMode == GettingMode.AlwaysCreateNew ? GettingMode.GetLatestExistingOrCreate : gettingMode, fields);
                    if (parentFolder == null)
                        return null;
                    parentFolderId = parentFolder.Id;
                }
                else
                {
                    folderName = folderPath;
                    parentFolderId = RootFolderId;
                }
                _object = GetFolder(parentFolderId, folderName, gettingMode, fields);
                paths2object[folderPath] = _object;
            }
            return _object;
        }

        Dictionary<string, Google.Apis.Drive.v3.Data.File> paths2object = new Dictionary<string, Google.Apis.Drive.v3.Data.File>();

        public Google.Apis.Drive.v3.Data.File GetFile(string filePath, string fields = "id, webViewLink")
        {
            if (!paths2object.TryGetValue(filePath, out Google.Apis.Drive.v3.Data.File @object))
            {
                string parentFolderPath = Regex.Replace(filePath, @"\\[^\\]*$", "");
                Google.Apis.Drive.v3.Data.File parentFolder = GetFolder(parentFolderPath, GettingMode.GetLatestExistingOnly);
                if (parentFolder == null)
                    return null;

                string fileName = Regex.Replace(filePath, @".*\\", "");
                SearchFilter sf = new SearchFilter { IsFolder = false, ParentId = parentFolder.Id, Name = fileName };
                IEnumerable<Google.Apis.Drive.v3.Data.File> fs = FindObjects(sf, fields);
                @object = fs.FirstOrDefault();
                if (@object != null)
                    paths2object[filePath] = @object;
            }
            return @object;
        }
        //replace GetFile(string filePath, string fields = "id, webViewLink")
        //public Google.Apis.Drive.v3.Data.File GetFile(string baseFolderId, string relativeFilePath, string fields = "id, webViewLink"){ }

        public Google.Apis.Drive.v3.Data.File UploadFile(string localFile, string remoteFolderIdOrLink, string remoteFileName = null, string contentType = null, bool updateExisting = true, string fields = "id, webViewLink")
        {
            Google.Apis.Drive.v3.Data.File folder = GetObject(remoteFolderIdOrLink);
            Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File
            {
                Name = remoteFileName != null ? remoteFileName : PathRoutines.GetFileName(localFile),
                //MimeType = getMimeType(localFile), 
                //Description=,
            };
            using (FileStream fileStream = new FileStream(localFile, FileMode.Open, FileAccess.Read))
            {
                if (updateExisting)
                {
                    SearchFilter sf = new SearchFilter { IsFolder = false, ParentId = folder.Id, Name = file.Name };
                    IEnumerable<Google.Apis.Drive.v3.Data.File> fs = FindObjects(sf, fields);
                    Google.Apis.Drive.v3.Data.File f = fs.FirstOrDefault();
                    if (f != null)
                    {
                        FilesResource.UpdateMediaUpload updateMediaUpload = Service.Files.Update(file, f.Id, fileStream, contentType != null ? contentType : getMimeType(localFile));
                        updateMediaUpload.Fields = getProperFields(fields);
                        Google.Apis.Upload.IUploadProgress uploadProgress = updateMediaUpload.Upload();
                        if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
                            throw new Exception("Uploading file failed.", uploadProgress.Exception);
                        if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
                            throw new Exception("Uploading file has not been completed.");
                        return updateMediaUpload.ResponseBody;
                    }
                }
                {
                    file.Parents = new List<string>
                    {
                        folder.Id
                    };
                    FilesResource.CreateMediaUpload createMediaUpload = Service.Files.Create(file, fileStream, contentType != null ? contentType : getMimeType(localFile));
                    createMediaUpload.Fields = getProperFields(fields);
                    Google.Apis.Upload.IUploadProgress uploadProgress = createMediaUpload.Upload();
                    if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
                        throw new Exception("Uploading file failed.", uploadProgress.Exception);
                    if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
                        throw new Exception("Uploading file has not been completed.");
                    return createMediaUpload.ResponseBody;
                }
            }
        }

        public Google.Apis.Drive.v3.Data.File UploadFileByPath(string localFile, string remoteFilePath, string contentType = null, bool updateExisting = true, string fields = "id, webViewLink")
        {
            Google.Apis.Drive.v3.Data.File folder = GetFolder(PathRoutines.GetFileDir(remoteFilePath), GettingMode.GetLatestExistingOrCreate, fields);
            Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File
            {
                Name = PathRoutines.GetFileName(remoteFilePath),
                //MimeType = getMimeType(localFile), 
                //Description=,
            };
            using (FileStream fileStream = new FileStream(localFile, FileMode.Open, FileAccess.Read))
            {
                if (updateExisting)
                {
                    SearchFilter sf = new SearchFilter { IsFolder = false, ParentId = folder.Id, Name = file.Name };
                    IEnumerable<Google.Apis.Drive.v3.Data.File> fs = FindObjects(sf, fields);
                    Google.Apis.Drive.v3.Data.File f = fs.FirstOrDefault();
                    if (f != null)
                    {
                        FilesResource.UpdateMediaUpload updateMediaUpload = Service.Files.Update(file, f.Id, fileStream, contentType != null ? contentType : getMimeType(localFile));
                        updateMediaUpload.Fields = getProperFields(fields);
                        Google.Apis.Upload.IUploadProgress uploadProgress = updateMediaUpload.Upload();
                        if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
                            throw new Exception("Uploading file failed.", uploadProgress.Exception);
                        if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
                            throw new Exception("Uploading file has not been completed.");
                        return updateMediaUpload.ResponseBody;
                    }
                }
                {
                    file.Parents = new List<string>
                    {
                        folder.Id
                    };
                    FilesResource.CreateMediaUpload createMediaUpload = Service.Files.Create(file, fileStream, contentType != null ? contentType : getMimeType(localFile));
                    createMediaUpload.Fields = getProperFields(fields);
                    Google.Apis.Upload.IUploadProgress uploadProgress = createMediaUpload.Upload();
                    if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
                        throw new Exception("Uploading file failed.", uploadProgress.Exception);
                    if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
                        throw new Exception("Uploading file has not been completed.");
                    return createMediaUpload.ResponseBody;
                }
            }
        }
        static string getMimeType(string fileName)
        {
            string mimeType = "application/unknown";
            string ext = Path.GetExtension(fileName).ToLower();
            Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
            if (regKey != null && regKey.GetValue("Content Type") != null)
                mimeType = regKey.GetValue("Content Type").ToString();
            return mimeType;
        }
        //replace UploadFile(string localFile, string remoteFilePath, string contentType = null, bool updateExisting = true, string fields = "id, webViewLink")
        //public Google.Apis.Drive.v3.Data.File UploadFile(string localFile, string remoteBaseFolderId, string remoteRelativeFilePath, string contentType = null, bool updateExisting = true, string fields = "id, webViewLink"){ }

        public Google.Apis.Drive.v3.Data.File DownloadFileByPath(string remoteFilePath, string localFile)
        {
            Google.Apis.Drive.v3.Data.File file = GetFile(remoteFilePath);
            if (file == null)
                return null;
            DownloadFile(file.Id, localFile);
            return file;
        }
        //replace public Google.Apis.Drive.v3.Data.File DownloadFileByPath(string remoteFilePath, string localFile)
        //public Google.Apis.Drive.v3.Data.File DownloadFileByPath(string remoteBaseFolderId, string remoteRelativeFilePath, string localFile){}
    }
}