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
            webLink = Regex.Replace(webLink, @"/(edit|view)[^/]*\s*$", "", RegexOptions.IgnoreCase);
            Match m = Regex.Match(webLink, @"/([^/]*)$", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        public static bool IsObjectLink(string v)
        {
            return Regex.IsMatch(v, @"^\s*https?\://(docs|drive)\.google\.com/", RegexOptions.IgnoreCase);
        }
    }
}