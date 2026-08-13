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
        /// Ensures: id.
        /// Removes duplicates. (The server tolerates duplicates though.)
        /// </summary>
        /// <param name="fieldss"></param>
        /// <returns></returns>
        public static string GetNormalizedRequestFields(params string[] fieldss)
        {
            //if (fields.StartsWith(",,"))//normalized
            //    return fields;

            IEnumerable<string> getFs(string fS)
            {
                return fS.Split(',').Select(a => a.Trim()).Where(a => !string.IsNullOrWhiteSpace(a)); //.ForEach(a => { a[0] = char.ToLower(a[0]); });
            }
            var fs = new HashSet<string> { "id" };
            fieldss.ForEach(a => getFs(a).ForEach(b => fs.Add(b)));

            //return ",," + string.Join(",", fs);
            return string.Join(",", fs);
        }

        //static string GetProperRequestFields(string fields, params string[] fields2)
        //{
        //    void add(string f)
        //    {
        //        if (!Regex.IsMatch(fields, @"(^|,)\s*id\s*($|,)"))
        //            fields += ", " + f;
        //    }
        //    //return fields + (Regex.IsMatch(fields, @"(^|\s|,)id($|\s|,)", RegexOptions.IgnoreCase) ? "" : ", id");
        //    add("id");
        //    fields2.ForEach(add);
        //    //add("webViewLink");
        //    return fields;
        //}
    }
}