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
        public static string GetObjectId(string objectIdOrLink)
        {
            return IsObjectLink(objectIdOrLink) ? ExtractObjectIdFromWebLink(objectIdOrLink) : objectIdOrLink;
        }

        public static string ExtractObjectIdFromWebLink(string webLink)
        {
            Match m = Regex.Match(webLink.Trim(), @"\.google\.com.*?/([^/]+?)((\?|/(edit|view))[^/]*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            if (!m.Success)
                throw new Exception("Could not parse the link: " + webLink);
            return m.Groups[1].Value;
        }

        public static bool IsObjectLink(string v)
        {
            return Regex.IsMatch(v.Trim(), @"^\s*https?\://(docs|drive)\.google\.com/", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        /// <summary>
        /// Ensures: id, webViewLink.
        /// Removes duplicates. (The server tolerates duplicates though.)
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="mustFields"></param>
        /// <returns></returns>
        public static string GetNormalizedRequestFields(string fields, string mustFields = "id, webViewLink")
        {
            if (fields == null)
                fields = "";
            //if (fields.StartsWith(",,"))//normalized
            //    return fields;
            var fs = new HashSet<string>(fields.Split(',').Select(a => a.Trim()));
            mustFields.Split(',').ForEach(f => fs.Add(f.Trim()));
            //fs.Add("id");
            //fs.Add("webViewLink");
            //return ",," + string.Join(",", fs);
            return string.Join(",", fs);
        }
    }
}