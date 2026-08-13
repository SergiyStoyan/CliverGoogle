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
                SearchFilter sf = new SearchFilter { IsFolder = true, ParentLinkOrId = parentFolderId, Name = folderName, OrderBy = "createdTime desc" };
                IEnumerable<Google.Apis.Drive.v3.Data.File> fs = FindObjects(sf, fields, 5);
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
            request.Fields = GetNormalizedRequestFields(fields);
            return request.Execute();
        }

        public Google.Apis.Drive.v3.Data.File GetFolder(Path folder, GettingMode gettingMode, string fields = "id, webViewLink")
        {
            if (string.IsNullOrEmpty(folder.RelativePath))//root folder
                return GetObject(folder.BaseObjectId, fields);

            if (gettingMode == GettingMode.AlwaysCreateNew
                || !Cache.Get(folder, out Google.Apis.Drive.v3.Data.File @object)
                || @object == null && gettingMode == GettingMode.GetLatestExistingOrCreate
                )
            {
                fields = Cache.GetUpdatedFields(fields);
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
                Cache.Set(folder2, @object);
                if (folder2.Key != folder.Key)
                    Cache.Set(folder, @object);
            }
            return @object;
        }

        public class Path2ObjectCache
        {
            public Path2ObjectCache(Modes mode)
            {
                Mode = mode;
            }

            public enum Modes
            {
                NotUsed,
                FoldersOnly,
                AnyObjects
            }
            public Modes Mode = Modes.NotUsed;

            /// <summary>
            /// Some modes need certain fields.
            /// </summary>
            /// <param name="fields"></param>
            public string GetUpdatedFields(string fields)
            {
                if (Mode == Modes.FoldersOnly)
                    fields = GetNormalizedRequestFields(fields, "mimeType");
                return fields;
            }

            public bool Get(Path path, out Google.Apis.Drive.v3.Data.File @object)
            {
                if (Mode == Modes.NotUsed)
                {
                    @object = null;
                    return false;
                }
                return paths2object.TryGetValue(path.Key, out @object);
            }

            /// <summary>
            /// (!) when Mode == Modes.FoldersOnly, the object must have "mimeType" field.
            /// </summary>
            /// <param name="path"></param>
            /// <param name="object"></param>
            public void Set(Path path, Google.Apis.Drive.v3.Data.File @object)
            {
                if (Mode == Modes.NotUsed || Mode == Modes.FoldersOnly && !@object.IsFolder())
                    return;
                paths2object[path.Key] = @object;
            }

            public void Unset(Path path)
            {
                paths2object.Remove(path.Key);
            }

            public void Clear()
            {
                paths2object.Clear();
            }

            Dictionary<string, Google.Apis.Drive.v3.Data.File> paths2object = new Dictionary<string, Google.Apis.Drive.v3.Data.File>();
        }
        /// <summary>
        /// Used to save opearations with getting objects by Path.
        /// (!)Must be used with caution! If objects are moved/trashed, it is unsafe.
        /// </summary>
        public Path2ObjectCache Cache
        {
            get { return cache; }
            set { cache = value ?? throw new ArgumentNullException("value"); }
        }
        Path2ObjectCache cache = new Path2ObjectCache(Path2ObjectCache.Modes.NotUsed);

        /// <summary>
        /// (!) Path implies that only 1 object with the same path can exists which is not true for google drive in general.
        /// So it must be used only when building a windows-like folder structure.
        /// </summary>
        public class Path
        {
            public string BaseObjectIdOrLink { get; private set; }
            [Newtonsoft.Json.JsonIgnore]
            public string BaseObjectId { get; private set; }
            public string RelativePath { get; private set; }
            [Newtonsoft.Json.JsonIgnore]
            public string Key { get; private set; }

            [Newtonsoft.Json.JsonIgnore]
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

            [Newtonsoft.Json.JsonConstructor]
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

            public bool SplitRelativePath(out string relativeFolder, out string folderOrFileName)
            {
                if (string.IsNullOrEmpty(RelativePath))
                {
                    relativeFolder = null;
                    folderOrFileName = null;
                    return false;
                }
                Match m = Regex.Match(RelativePath, @"(.*)\\([^\\]+)$");
                if (m.Success)
                {
                    relativeFolder = m.Groups[1].Value;
                    folderOrFileName = m.Groups[2].Value;
                    return true;
                }
                relativeFolder = null;
                folderOrFileName = RelativePath;
                return true;
            }
        }

        public string GetLink(Path folderOrFile)
        {
            if (IsObjectLink(folderOrFile.BaseObjectIdOrLink))
                return folderOrFile.BaseObjectIdOrLink;

            return getObject(folderOrFile)?.Id;
        }

        Google.Apis.Drive.v3.Data.File getObject(Path folderOrFile, string fields = "id, webViewLink")
        {
            if (!Cache.Get(folderOrFile, out Google.Apis.Drive.v3.Data.File @object))
            {
                fields = Cache.GetUpdatedFields(fields);
                if (folderOrFile.SplitRelativePath(out string rf, out string folderOrFileName))
                {
                    Google.Apis.Drive.v3.Data.File parentFolder = GetFolder(new Path(folderOrFile.BaseObjectId, rf), GettingMode.GetLatestExistingOnly, fields);
                    if (parentFolder == null)
                        return null;
                    @object = FindObjects(new SearchFilter { Name = folderOrFileName, ParentLinkOrId = parentFolder.Id, OrderBy = "createdTime desc" }, fields, 5).FirstOrDefault();
                }
                else
                    @object = GetObject(folderOrFile.BaseObjectId, fields);
                Cache.Set(folderOrFile, @object);
            }
            return @object;
        }

        public Google.Apis.Drive.v3.Data.File GetFile(Path file, string fields = "id, webViewLink")
        {
            if (!Cache.Get(file, out Google.Apis.Drive.v3.Data.File @object))
            {
                fields = Cache.GetUpdatedFields(fields);
                if (file.SplitRelativePath(out string parentRelativeFolderPath, out string fileName))
                {
                    Google.Apis.Drive.v3.Data.File parentFolder = GetFolder(new Path(file.BaseObjectId, parentRelativeFolderPath), GettingMode.GetLatestExistingOnly);
                    if (parentFolder == null)
                        return null;
                    SearchFilter sf = new SearchFilter { IsFolder = false, ParentLinkOrId = parentFolder.Id, Name = fileName, OrderBy = "createdTime desc" };
                    IEnumerable<Google.Apis.Drive.v3.Data.File> fs = FindObjects(sf, fields, 5);
                    @object = fs.FirstOrDefault();
                }
                else
                    @object = GetObject(file.BaseObjectId, fields);
                Cache.Set(file, @object);
            }
            return @object;
        }

        public Google.Apis.Drive.v3.Data.File UploadFile(string localFile, Path remoteFile, bool updateExisting = true, string fields = "id, webViewLink")
        {
            if (string.IsNullOrEmpty(remoteFile.RelativePath))
            {
                if (!updateExisting)
                    throw new Exception(nameof(updateExisting) + " = FALSE. Cannot update the file: " + remoteFile);
                return UpdateFile(localFile, remoteFile.BaseObjectId, PathRoutines.GetFileName(localFile), fields);
            }

            Path remoteFolderPath;
            if (remoteFile.SplitRelativePath(out string remoteRelativeFolderPath, out string remoteFileName))
                remoteFolderPath = new Path(remoteFile.BaseObjectId, remoteRelativeFolderPath);
            else
                remoteFolderPath = new Path(remoteFile.BaseObjectId, null);
            string remoteFolderId = GetFolder(remoteFolderPath, GettingMode.GetLatestExistingOrCreate).Id;
            return UploadFile(localFile, remoteFolderId, remoteFileName, updateExisting, fields);
        }

        public Google.Apis.Drive.v3.Data.File DownloadFile(Path remoteFile, string localFile, bool updateExisting = true)
        {
            Google.Apis.Drive.v3.Data.File file = GetFile(remoteFile);
            if (file == null)
                return null;
            DownloadFile(file.Id, localFile, updateExisting);
            return file;
        }

        public Google.Apis.Drive.v3.Data.File MoveFile(string fileIdOrLink1, Path file2, bool updateExisting = true, string fields = "id, webViewLink")
        {
            if (string.IsNullOrEmpty(file2.RelativePath))
            {
                if (!updateExisting)
                    throw new Exception2(nameof(updateExisting) + " = FALSE. Cannot update the file specified: " + file2);
                return UpdateFile(fileIdOrLink1, file2.BaseObjectId, null, fields);
            }

            Path folderPath2;
            if (file2.SplitRelativePath(out string relativeFolderPath2, out string fileName2))
                folderPath2 = new Path(file2.BaseObjectId, relativeFolderPath2);
            else
                folderPath2 = new Path(file2.BaseObjectId, null);
            string folderId2 = GetFolder(folderPath2, GettingMode.GetLatestExistingOrCreate).Id;
            return MoveObject(fileIdOrLink1, folderId2, fileName2, updateExisting, fields);
        }
    }
}