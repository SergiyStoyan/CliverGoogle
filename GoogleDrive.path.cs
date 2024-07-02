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
        void buildPaths(List<Path> paths, Path currentPath, Google.Apis.Drive.v3.Data.File currentObject)
        {
            if (currentObject == null || currentObject.Parents == null || currentObject.Parents.Count < 1)//it is root 'My Drive'
            {
                paths.Add(currentPath);
                return;
            }
            currentPath = new Path(null, currentObject.Name + (currentPath == null ? "" : Path.DirectorySeparatorChar + currentPath));
            foreach (string parentId in currentObject.Parents)
                buildPaths(paths, currentPath, GetObject(parentId, "id, name, parents"));
        }

        public enum GettingMode
        {
            AlwaysCreateNew,
            GetLatestExistingOrCreate,
            GetLatestExistingOnly,
        }
        Google.Apis.Drive.v3.Data.File getFolder(string parentFolderId, string folderName, GettingMode gettingMode, string fields)
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

        public Google.Apis.Drive.v3.Data.File GetFolder(Path folder, GettingMode gettingMode, string fields = "id, webViewLink")
        {
            if (string.IsNullOrEmpty(folder.RelativePath))//root folder
                return GetObject(folder.BaseObjectId, fields);

            if (gettingMode == GettingMode.AlwaysCreateNew
                || !cache.Get(folder, out Google.Apis.Drive.v3.Data.File @object)
                || @object == null && gettingMode == GettingMode.GetLatestExistingOrCreate
                )
            {
                Path folder2;
                if (folder.SplitRelativePath(out string rf, out string folderName))
                {
                    Google.Apis.Drive.v3.Data.File parentFolder = GetFolder(new Path(folder.BaseObjectId, rf), gettingMode == GettingMode.AlwaysCreateNew ? GettingMode.GetLatestExistingOrCreate : gettingMode, fields);
                    if (parentFolder == null)
                        return null;
                    folder2 = new Path(parentFolder.Id, folderName);
                }
                else
                    folder2 = folder;
                @object = getFolder(folder2.BaseObjectId, folder2.RelativePath, gettingMode, fields);
                cache.Set(folder2, @object);
                if (folder2.Key != folder.Key)
                    cache.Set(folder, @object);
            }
            return @object;
        }

        class Cache
        {
            public bool Get(Path path, out Google.Apis.Drive.v3.Data.File @object)
            {
                return paths2object.TryGetValue(path.Key, out @object);
            }

            public void Set(Path path, Google.Apis.Drive.v3.Data.File @object)
            {
                if (@object != null)
                    paths2object[path.Key] = @object;
            }

            Dictionary<string, Google.Apis.Drive.v3.Data.File> paths2object = new Dictionary<string, Google.Apis.Drive.v3.Data.File>();
        }
        readonly Cache cache = new Cache();

        public class Path
        {
            public string BaseObjectIdOrLink { get; private set; }
            public string BaseObjectId { get; private set; }
            public string RelativePath { get; private set; }
            public string Key { get; private set; }

            public const string DirectorySeparatorChar = @"\";

            public override string ToString()
            {
                return Key;
            }

            static public Path Restore(string pathKey)
            {
                try
                {
                    return new Path(pathKey);
                }
                catch
                {
                    return null;
                }
            }

            static public Path Create(string baseObjectIdOrLink, string relativePath)
            {
                try
                {
                    return new Path(baseObjectIdOrLink, relativePath);
                }
                catch
                {
                    return null;
                }
            }

            public Path(string pathKey)
            {
                if (string.IsNullOrEmpty(pathKey))
                {
                    initialize(null, null);
                    return;
                }
                string[] ps = Regex.Split(pathKey, @"\\\\");
                if (ps.Length < 2)
                {
                    throw new Exception2(nameof(pathKey) + " does not comprise of 2 parts: " + "'" + pathKey + "'");
                    //if (!IsObjectLink(ps[0]))
                    //    throw new Exception2(nameof(pathKey) + " is not a google link: " + "'" + pathKey + "'");
                    //initialize(ps[0], null);
                    //return;
                }
                if (ps.Length > 2)
                    throw new Exception2(nameof(pathKey) + " has more than 2 parts: " + "'" + pathKey + "'");
                initialize(ps[0], ps[1]);
            }

            public Path(string baseObjectIdOrLink, string relativePath)
            {
                initialize(baseObjectIdOrLink, relativePath);
            }

            void initialize(string baseObjectIdOrLink, string relativePath)
            {
                //if (relativeFolderPath.Contains(DirectorySeparatorChar))
                //    throw new Exception2(nameof(GoogleDrive.Path) + " cannot contain " + DirectorySeparatorChar);
                if (!string.IsNullOrEmpty(baseObjectIdOrLink) && Regex.IsMatch(baseObjectIdOrLink, @"\s|\\"))
                    throw new Exception2("Parameter " + nameof(baseObjectIdOrLink) + " is not a google link: " + "'" + baseObjectIdOrLink + "'");
                if (string.IsNullOrEmpty(baseObjectIdOrLink))
                {
                    BaseObjectId = RootFolderId;
                    BaseObjectIdOrLink = RootFolderId;
                }
                else
                {
                    BaseObjectId = GetObjectId(baseObjectIdOrLink);
                    BaseObjectIdOrLink = baseObjectIdOrLink;
                }
                if (relativePath != null)
                    RelativePath = Regex.Replace(relativePath, @"\\{2,}", @"\").Trim().Trim('\\');
                Key = BaseObjectId + @"\\" + RelativePath;
            }

            public Path GetDescendant(string relativeDescendantPath)
            {
                return new Path(BaseObjectId, RelativePath + DirectorySeparatorChar + relativeDescendantPath);
            }

            public bool SplitRelativePath(out string relativeFolder, out string FolderOrFileName)
            {
                if (string.IsNullOrEmpty(RelativePath))
                {
                    relativeFolder = null;
                    FolderOrFileName = null;
                    return false;
                }
                Match m = Regex.Match(RelativePath, @"(.*)\\([^\\]+)$");
                if (m.Success)
                {
                    relativeFolder = m.Groups[1].Value;
                    FolderOrFileName = m.Groups[2].Value;
                    return true;
                }
                relativeFolder = null;
                FolderOrFileName = RelativePath;
                return true;
            }

            //public string GetFolderOrFileId(GoogleDrive googleDrive)
            //{
            //    if (!googleDrive.cache.Get(this, out Google.Apis.Drive.v3.Data.File @object))
            //    {
            //        if (!SplitRelativePath(out string parentRelativeFolderPath, out string fileName))
            //            return BaseObjectId;
            //        Google.Apis.Drive.v3.Data.File parentFolder = googleDrive.GetFolder(new Path(BaseObjectId, parentRelativeFolderPath), GettingMode.GetLatestExistingOnly);
            //        if (parentFolder == null)
            //            @object = null;
            //        else
            //        {
            //            SearchFilter sf = new SearchFilter { ParentId = parentFolder.Id, Name = fileName };
            //            IEnumerable<Google.Apis.Drive.v3.Data.File> fs = googleDrive.FindObjects(sf);
            //            @object = fs.FirstOrDefault();
            //        }
            //        googleDrive.cache.Set(this, @object);
            //    }
            //    return @object.Id;
            //}
        }

        //public class FolderPath : Path
        //{
        //    public FolderPath(string pathKey) : base(pathKey)
        //    {
        //    }

        //    public FolderPath(string baseObjectIdOrLink, string relativePath) : base(baseObjectIdOrLink, relativePath)
        //    {
        //    }
        //}

        //public class FilePath : Path
        //{
        //    public readonly string Name;

        //    static public FilePath Restore(string pathKey)
        //    {
        //        try
        //        {
        //            return new FilePath(pathKey);
        //        }
        //        catch
        //        {
        //            return null;
        //        }
        //    }

        //    static public FilePath Create(string baseObjectIdOrLink, string relativePath)
        //    {
        //        try
        //        {
        //            return new FilePath(baseObjectIdOrLink, relativePath);
        //        }
        //        catch
        //        {
        //            return null;
        //        }
        //    }

        //    public FilePath(string pathKey) : base(pathKey)
        //    {
        //        Name = RelativePath
        //    }

        //    public FilePath(string baseObjectIdOrLink, string relativePath) : base(baseObjectIdOrLink, relativePath)
        //    {
        //    }
        //}

        public string GetLink(Path folderOrFile)
        {
            if (IsObjectLink(folderOrFile.BaseObjectIdOrLink))
                return folderOrFile.BaseObjectIdOrLink;

            return getObject(folderOrFile)?.Id;
        }

        Google.Apis.Drive.v3.Data.File getObject(Path folderOrFile, string fields = "id, webViewLink")
        {
            if (!cache.Get(folderOrFile, out Google.Apis.Drive.v3.Data.File @object))
            {
                if (folderOrFile.SplitRelativePath(out string rf, out string folderOrFileName))
                {
                    Google.Apis.Drive.v3.Data.File parentFolder = GetFolder(new Path(folderOrFile.BaseObjectId, rf), GettingMode.GetLatestExistingOnly, fields);
                    if (parentFolder == null)
                        return null;
                    @object = FindObjects(new SearchFilter { Name = folderOrFileName, ParentId = parentFolder.Id }, fields).FirstOrDefault();
                }
                else
                    @object = GetObject(folderOrFile.BaseObjectId, fields);
                cache.Set(folderOrFile, @object);
            }
            return @object;
        }

        public Google.Apis.Drive.v3.Data.File GetFile(Path file, string fields = "id, webViewLink")
        {
            if (!cache.Get(file, out Google.Apis.Drive.v3.Data.File @object))
            {
                if (file.SplitRelativePath(out string parentRelativeFolderPath, out string fileName))
                {
                    Google.Apis.Drive.v3.Data.File parentFolder = GetFolder(new Path(file.BaseObjectId, parentRelativeFolderPath), GettingMode.GetLatestExistingOnly);
                    if (parentFolder == null)
                        return null;
                    SearchFilter sf = new SearchFilter { IsFolder = false, ParentId = parentFolder.Id, Name = fileName };
                    IEnumerable<Google.Apis.Drive.v3.Data.File> fs = FindObjects(sf, fields);
                    @object = fs.FirstOrDefault();
                }
                else
                    @object = GetObject(file.BaseObjectId, fields);
                cache.Set(file, @object);
            }
            return @object;
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

        public Google.Apis.Drive.v3.Data.File UploadFile(string localFile, Path remotefile, string contentType = null, bool updateExisting = true, string fields = "id, webViewLink")
        {
            if (!remotefile.SplitRelativePath(out string remoteRelativeFolderPath, out string fileName)
                && IsObjectLink(remotefile.BaseObjectIdOrLink)
                )
                return UpdateFile(localFile, remotefile.BaseObjectId, PathRoutines.GetFileName(localFile), contentType, fields);

            string folderId = GetFolder(new Path(remotefile.BaseObjectId, remoteRelativeFolderPath), GettingMode.GetLatestExistingOrCreate).Id;

            if (string.IsNullOrWhiteSpace(fileName))
                fileName = PathRoutines.GetFileName(localFile);
            Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File
            {
                Name = fileName,
                //MimeType = getMimeType(localFile), 
                //Description=,
            };
            using (FileStream fileStream = new FileStream(localFile, FileMode.Open, FileAccess.Read))
            {
                if (updateExisting)
                {
                    SearchFilter sf = new SearchFilter { IsFolder = false, ParentId = folderId, Name = file.Name };
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
                        folderId
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

        public Google.Apis.Drive.v3.Data.File DownloadFile(Path remoteFile, string localFile)
        {
            Google.Apis.Drive.v3.Data.File file = GetFile(remoteFile);
            if (file == null)
                return null;
            DownloadFile(file.Id, localFile);
            return file;
        }
    }
}