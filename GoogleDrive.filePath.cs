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
        Google.Apis.Drive.v3.Data.File getFolder(string parentFolderIdOrLink, string folderName, GettingMode gettingMode, string fields = "id, webViewLink")
        {
            if (parentFolderIdOrLink == null && string.IsNullOrEmpty(folderName))//root folder
                return GetObject(RootFolderId, fields);
            string parentFolderId = GetObjectId(parentFolderIdOrLink);
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

        public Google.Apis.Drive.v3.Data.File GetFolder(string baseFolderIdOrLink, string relativeFolderPath, GettingMode gettingMode, string fields = "id, webViewLink")
        {
            if (string.IsNullOrEmpty(baseFolderIdOrLink))
            {
                if (string.IsNullOrEmpty(relativeFolderPath))//root folder
                    return GetObject(RootFolderId, fields);
                baseFolderIdOrLink = RootFolderId;
            }
            if (string.IsNullOrEmpty(relativeFolderPath))
                return GetObject(baseFolderIdOrLink, fields);

            if (gettingMode == GettingMode.AlwaysCreateNew
                || !cache.Get(baseFolderIdOrLink, relativeFolderPath, out Google.Apis.Drive.v3.Data.File @object)
                || @object == null && gettingMode == GettingMode.GetLatestExistingOrCreate
                )
            {
                Match m = Regex.Match(relativeFolderPath, @"^(.+?)\\(.+)$");
                if (m.Success)
                {
                    Google.Apis.Drive.v3.Data.File parentFolder = getFolder(baseFolderIdOrLink, m.Groups[1].Value, gettingMode == GettingMode.AlwaysCreateNew ? GettingMode.GetLatestExistingOrCreate : gettingMode, fields);
                    if (parentFolder == null)
                        return null;
                    @object = GetFolder(parentFolder.Id, m.Groups[2].Value, gettingMode, fields);
                }
                else
                    @object = getFolder(baseFolderIdOrLink, relativeFolderPath, gettingMode, fields);
                cache.Set(baseFolderIdOrLink, relativeFolderPath, @object);
            }
            return @object;
        }

        public Google.Apis.Drive.v3.Data.File GetFolder(string folderPath, GettingMode gettingMode, string fields = "id, webViewLink")
        {
            return GetFolder(null, folderPath, gettingMode, fields);
        }

        class Cache
        {
            public bool Get(string baseFolderIdOrLink, string relativeFolderPath, out Google.Apis.Drive.v3.Data.File @object)
            {
                return Get(new Path(baseFolderIdOrLink, relativeFolderPath), out @object);
            }

            public bool Get(Path path, out Google.Apis.Drive.v3.Data.File @object)
            {
                return paths2object.TryGetValue(path.Key, out @object);
            }

            public void Set(string baseFolderIdOrLink, string relativeFolderPath, Google.Apis.Drive.v3.Data.File @object)
            {
                Set(new Path(baseFolderIdOrLink, relativeFolderPath), @object);
            }

            public void Set(Path path, Google.Apis.Drive.v3.Data.File @object)
            {
                paths2object[path.Key] = @object;
                //SetPath(@object.Id, path.Key);
            }
            Dictionary<string, Google.Apis.Drive.v3.Data.File> paths2object = new Dictionary<string, Google.Apis.Drive.v3.Data.File>();

            //public bool GetPath(string objectId, out string path)
            //{
            //    return objectIds2path.TryGetValue(objectId, out path);
            //}

            //public void SetPath(string objectId, string path)
            //{
            //    objectIds2path[objectId] = path;
            //}
            //Dictionary<string, string> objectIds2path = new Dictionary<string, string>();
        }
        readonly Cache cache = new Cache();
        class Path
        {
            public string BaseFolderId { get; private set; }
            public string RelativeFolderPath { get; private set; }
            public string Key { get; private set; }

            public Path(string baseFolderIdOrLink, string relativeFolderPath)
            {
                BaseFolderId = GetObjectId(baseFolderIdOrLink);
                RelativeFolderPath = Regex.Replace(relativeFolderPath, @"\\{2,}", @"\").Trim().Trim('\\');
                Key = BaseFolderId + @"\\" + RelativeFolderPath;
            }

            //internal Path(string pathKey)
            //{
            //    Key = pathKey;
            //    int i = pathKey.IndexOf("\\\\");
            //    if (i >= 0)
            //    {
            //        BaseFolderId = pathKey.Substring(0, i);
            //        RelativeFolderPath = pathKey.Substring(i + 2);
            //    }
            //    else
            //        RelativeFolderPath = pathKey;
            //}
        }

        public Google.Apis.Drive.v3.Data.File GetFile(string baseFolderId, string relativeFilePath, string fields = "id, webViewLink")
        {
            if (!cache.Get(baseFolderId, relativeFilePath, out Google.Apis.Drive.v3.Data.File @object))
            {
                Match m = Regex.Match(relativeFilePath, @"^(.+)\\[^\\]*$");
                string parentRelativeFolderPath = m.Success ? m.Groups[1].Value : null;
                Google.Apis.Drive.v3.Data.File parentFolder = GetFolder(baseFolderId, parentRelativeFolderPath, GettingMode.GetLatestExistingOnly);
                if (parentFolder == null)
                    return null;
                string fileName = m.Success ? m.Groups[2].Value : null;
                SearchFilter sf = new SearchFilter { IsFolder = false, ParentId = parentFolder.Id, Name = fileName };
                IEnumerable<Google.Apis.Drive.v3.Data.File> fs = FindObjects(sf, fields);
                @object = fs.FirstOrDefault();
                if (@object != null)
                    cache.Set(baseFolderId, relativeFilePath, @object);
            }
            return @object;
        }

        public Google.Apis.Drive.v3.Data.File GetFile(string filePath, string fields = "id, webViewLink")
        {
            return GetFile(null, filePath, fields);
        }

        //public Google.Apis.Drive.v3.Data.File UploadFile(string localFile, string remoteFolderIdOrLink, string remoteFileName = null, string contentType = null, bool updateExisting = true, string fields = "id, webViewLink")
        //{
        //    //Google.Apis.Drive.v3.Data.File folder = GetObject(remoteFolderIdOrLink);
        //    //Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File
        //    //{
        //    //    Name = remoteFileName != null ? remoteFileName : PathRoutines.GetFileName(localFile),
        //    //    //MimeType = getMimeType(localFile), 
        //    //    //Description=,
        //    //};
        //    //using (FileStream fileStream = new FileStream(localFile, FileMode.Open, FileAccess.Read))
        //    //{
        //    //    if (updateExisting)
        //    //    {
        //    //        SearchFilter sf = new SearchFilter { IsFolder = false, ParentId = folder.Id, Name = file.Name };
        //    //        IEnumerable<Google.Apis.Drive.v3.Data.File> fs = FindObjects(sf, fields);
        //    //        Google.Apis.Drive.v3.Data.File f = fs.FirstOrDefault();
        //    //        if (f != null)
        //    //        {
        //    //            FilesResource.UpdateMediaUpload updateMediaUpload = Service.Files.Update(file, f.Id, fileStream, contentType != null ? contentType : getMimeType(localFile));
        //    //            updateMediaUpload.Fields = getProperFields(fields);
        //    //            Google.Apis.Upload.IUploadProgress uploadProgress = updateMediaUpload.Upload();
        //    //            if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
        //    //                throw new Exception("Uploading file failed.", uploadProgress.Exception);
        //    //            if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
        //    //                throw new Exception("Uploading file has not been completed.");
        //    //            return updateMediaUpload.ResponseBody;
        //    //        }
        //    //    }
        //    //    {
        //    //        file.Parents = new List<string>
        //    //        {
        //    //            folder.Id
        //    //        };
        //    //        FilesResource.CreateMediaUpload createMediaUpload = Service.Files.Create(file, fileStream, contentType != null ? contentType : getMimeType(localFile));
        //    //        createMediaUpload.Fields = getProperFields(fields);
        //    //        Google.Apis.Upload.IUploadProgress uploadProgress = createMediaUpload.Upload();
        //    //        if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
        //    //            throw new Exception("Uploading file failed.", uploadProgress.Exception);
        //    //        if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
        //    //            throw new Exception("Uploading file has not been completed.");
        //    //        return createMediaUpload.ResponseBody;
        //    //    }
        //    //}
        //    return UploadFileByPath(localFile, remoteFolderIdOrLink, remoteFileName, contentType, updateExisting, fields);
        //}

        public Google.Apis.Drive.v3.Data.File UploadFile(string localFile, string remoteBaseFolderId, string remoteRelativeFilePath, string contentType = null, bool updateExisting = true, string fields = "id, webViewLink")
        {
            Match m = Regex.Match(remoteRelativeFilePath, @"^(.+)\\[^\\]*$");
            string remoteRelativeFolderPath = m.Success ? m.Groups[1].Value : null;
            Google.Apis.Drive.v3.Data.File folder = GetFolder(remoteBaseFolderId, remoteRelativeFolderPath, GettingMode.GetLatestExistingOrCreate, fields);
            Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File
            {
                Name = PathRoutines.GetFileName(m.Success ? m.Groups[2].Value : remoteRelativeFilePath),
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
            string ext = System.IO.Path.GetExtension(fileName).ToLower();
            Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
            if (regKey != null && regKey.GetValue("Content Type") != null)
                mimeType = regKey.GetValue("Content Type").ToString();
            return mimeType;
        }

        public Google.Apis.Drive.v3.Data.File UploadFileByPath(string localFile, string remoteFilePath, string contentType = null, bool updateExisting = true, string fields = "id, webViewLink")
        {
            return UploadFile(localFile, null, remoteFilePath, contentType, updateExisting, fields);
        }

        public Google.Apis.Drive.v3.Data.File DownloadFile(string remoteBaseFolderId, string remoteRelativeFilePath, string localFile)
        {
            Google.Apis.Drive.v3.Data.File file = GetFile(remoteBaseFolderId, remoteRelativeFilePath);
            if (file == null)
                return null;
            DownloadFile(file.Id, localFile);
            return file;
        }

        public Google.Apis.Drive.v3.Data.File DownloadFileByPath(string remoteFilePath, string localFile)
        {
            return DownloadFile(null, remoteFilePath, localFile);
        }
    }
}