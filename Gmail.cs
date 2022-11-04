//********************************************************************************************
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Requests;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace Cliver
{
    public class Gmail : GoogleService<GmailService>
    {
        public Gmail(string applicationName, IEnumerable<string> scopes, GoogleUserSettings googleUserSettings, string clientSecretFile = null)
            : base(applicationName, scopes, googleUserSettings, clientSecretFile)
        {
        }

        //public Gmail(string applicationName, IEnumerable<string> scopes, IDataStore dataStore, string clientSecretFile = null)
        //    : base(applicationName, scopes, dataStore, clientSecretFile)
        //{
        //}

        //public Gmail(string applicationName, IEnumerable<string> scopes, string credentialDir = null, string clientSecretFile = null)
        //    : base(applicationName, scopes, credentialDir, clientSecretFile)
        //{
        //}

        public class SearchFilter
        {
            public string CustomRequest = null;
            public bool? Unread = null;
            public bool? Trashed = null;//?by default it is passed over by gmail
            public bool? Spam = null;//?by default it is passed over by gmail
            public bool? Received = null;
            public bool? HasAttachement = null;
            /// <summary>
            /// Space separated.
            /// </summary>
            public List<string> Labels = null;
            public List<string> LabelIds = null;
            public string Pattern = null;
            public string UserId = OwnerMe;
            public DateTime? After;
            public DateTime? Before;

            public const string OwnerMe = "me";

            public string GetRequest()
            {
                List<string> qConditions = new List<string>();

                if (Unread == true)
                    qConditions.Add("is:unread");
                else if (Unread == false)
                    qConditions.Add("-is:unread");

                if (Trashed == true)
                    qConditions.Add("in:trash");
                else if (Trashed == false)
                    //qConditions.Add("not in:anywhere");
                    qConditions.Add("-in:trash");

                if (Spam == true)
                    qConditions.Add("in:spam");
                else if (Spam == false)
                    //qConditions.Add("not in:anywhere");
                    qConditions.Add("-in:spam");

                if (HasAttachement == true)
                    qConditions.Add("has:attachment");
                else if (HasAttachement == false)
                    qConditions.Add("-has:attachment");

                if (Received == true)
                    qConditions.Add("-in:sent");
                else if (Spam == false)
                    //qConditions.Add("not in:anywhere");
                    qConditions.Add("in:sent");

                //All dates used in the search query are interpreted as midnight on that date in the PST timezone. To specify accurate dates for other timezones pass the value in seconds instead.
                if (After != null)
                    //qConditions.Add("after:" + After?.ToString("yyyy/MM/dd"));
                    qConditions.Add("after:" + After.Value/*.ToUniversalTime()*//*.AddHours(-8)*/.GetSecondsSinceUnixEpoch());
                if (Before != null)
                    //qConditions.Add("before:" + Before?.ToString("yyyy/MM/dd"));
                    qConditions.Add("before:" + Before.Value/*.ToUniversalTime()*//*.AddHours(-8)*/.GetSecondsSinceUnixEpoch());

                if (Labels?.Count > 0)
                    qConditions.Add("label:{" + Labels.Select(a => "'" + a + "' ").Aggregate((a, b) => a += b) + "}");//undocumented but seems to work

                if (!string.IsNullOrWhiteSpace(Pattern))
                    qConditions.Add(/*"'" + */Pattern /*+ "'"*/);

                if (!string.IsNullOrWhiteSpace(CustomRequest))
                    if (qConditions.Count > 0)
                        qConditions.Add("(" + CustomRequest + ")");
                    else
                        qConditions.Add(CustomRequest);

                return string.Join(" ", qConditions);
            }
        }

        public class DefaultLabels
        {
            static public Google.Apis.Gmail.v1.Data.Label Unread = new Label { Name = "Unread", Id = "UNREAD" };
        }

        public class Message
        {
            public string UserId;
            public string Id;
            public string To;
            public string From;
            public string Date;
            public string Subject;
            public string Body;
            public string ThreadId;
            public List<Attachment> Attachments = new List<Attachment>();
            public List<string> LabelIds = new List<string>();

            public override string ToString()
            {
                return Subject + "\r\n\r\n" + Body;
            }
        }

        public class Attachment
        {
            public string Id;
            public string FileName;
            public string MimeType;
            public bool Inline;
        }

        public IEnumerable<Message> FindMessages(SearchFilter searchFilter, int pageSize = 100)
        {
            string pageToken = null;
            string requestQ = searchFilter?.GetRequest();
            Google.Apis.Util.Repeatable<string> labelIds = searchFilter.LabelIds != null ? new Google.Apis.Util.Repeatable<string>(searchFilter.LabelIds) : null;
            do
            {
                UsersResource.MessagesResource.ListRequest request = Service.Users.Messages.List(searchFilter.UserId);
                request.LabelIds = labelIds;
                request.Q = requestQ;
                request.PageToken = pageToken;
                request.MaxResults = pageSize;
                ListMessagesResponse rr = request.Execute();
                if (rr.Messages == null)
                    yield break;

                List<Message> ms = new List<Message>();
                void getMessage(Google.Apis.Gmail.v1.Data.Message message, RequestError error, int i, HttpResponseMessage response)
                {
                    if (error != null)
                        throw new Exception("Retrieving email error: [" + error.Code + "] " + error.Message);
                    Message m = new Message { Id = message.Id, UserId = searchFilter.UserId, LabelIds = message.LabelIds.ToList(), ThreadId = message.ThreadId };
                    foreach (var h in message.Payload.Headers)
                    {
                        if (h.Name == "Date")
                            m.Date = h.Value;
                        else if (h.Name == "From")
                            m.From = h.Value;
                        else if (h.Name == "Subject")
                            m.Subject = h.Value;
                    }
                    m.Body = getText(message.Payload, true);
                    m.Attachments = getAttachments(message.Payload);
                    ms.Add(m);
                }
                BatchRequest batchRequest = new BatchRequest(Service);
                foreach (var e in rr.Messages)
                {
                    batchRequest.Queue<Google.Apis.Gmail.v1.Data.Message>(
                      Service.Users.Messages.Get(searchFilter.UserId, e.Id),
                      getMessage
                      );
                }
                batchRequest.ExecuteAsync().Wait();
                foreach (var m in ms)
                    yield return m;
                pageToken = rr.NextPageToken;
            } while (pageToken != null);
        }

        static string getText(MessagePart mp, bool plain)
        {
            if (mp.Body?.Data != null && Regex.IsMatch(mp.MimeType, @"^\s*text\s*/" + (plain ? @"\s*plain" : ""), RegexOptions.IgnoreCase))
                return GetStringFromBase64String(mp.Body?.Data);
            if (mp.Parts != null)
                foreach (MessagePart p in mp.Parts)
                {
                    string t = getText(p, plain);
                    if (t != null)
                        return t;
                }
            return null;
        }

        static List<Attachment> getAttachments(MessagePart mp)
        {
            List<Attachment> @as = new List<Attachment>();
            if (mp.Body?.AttachmentId != null)
            {
                MessagePartHeader mph = mp.Headers?.FirstOrDefault(a => a.Name == "Content-Disposition");
                //!!!It seems to be impossible to recognize if attachment is only inline and should not be considered as such. Sometimes attachement is both pdf and inline (server mistake?)
                //if (mph?.Value == null || !Regex.IsMatch(mph.Value, "inline", RegexOptions.IgnoreCase))
                @as.Add(new Attachment { Id = mp.Body.AttachmentId, FileName = mp.Filename, MimeType = mp.MimeType, Inline = mph != null && Regex.IsMatch(mph.Value, "inline", RegexOptions.IgnoreCase) });
                //else if (Regex.IsMatch(mp.Filename, @"\.pdf$", RegexOptions.IgnoreCase))
                //    throw new Exception("A pdf attachment is not recognized: " + mp.Filename);//!sometimes attachement is both pdf and inline!
            }
            if (mp.Parts != null)
                foreach (MessagePart p in mp.Parts)
                    @as.AddRange(getAttachments(p));
            return @as;
        }

        public static string GetStringFromBase64String(string s)
        {
            if (s == null)
                return null;
            return System.Text.Encoding.UTF8.GetString(GetBytesFromBase64String(s));
        }

        public static byte[] GetBytesFromBase64String(string s)
        {
            if (s == null)
                return null;
            return Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/'));
        }

        public void DownloadAttachment(Message message, string attachmentId, string file)
        {
            MessagePartBody mpb = Service.Users.Messages.Attachments.Get(message.UserId, message.Id, attachmentId).Execute();
            byte[] data = GetBytesFromBase64String(mpb.Data);
            File.WriteAllBytes(file, data);
        }

        public void AddLabels(Message message, List<string> addLabelIds, List<string> removeLabelIds)
        {
            var mmr = new ModifyMessageRequest { AddLabelIds = addLabelIds, RemoveLabelIds = removeLabelIds };
            Service.Users.Messages.Modify(mmr, message.UserId, message.Id).Execute();
        }

        public void SetRead(Message message)
        {
            AddLabels(message, null, new List<string> { DefaultLabels.Unread.Id });
        }

        public IEnumerable<Google.Apis.Gmail.v1.Data.Label> GetLabels(string userId = Gmail.SearchFilter.OwnerMe)
        {
            UsersResource.LabelsResource.ListRequest request = Service.Users.Labels.List(userId);
            ListLabelsResponse rr = request.Execute();
            if (rr.Labels == null)
                yield break;

            foreach (var l in rr.Labels)
                yield return l;
        }

        public Profile GetUserProfile(string userId = Gmail.SearchFilter.OwnerMe)
        {
            UsersResource.GetProfileRequest r = Service.Users.GetProfile(userId);
            return r.Execute();
        }

        public string GetUserMainEmail(string userId = Gmail.SearchFilter.OwnerMe)
        {
            return GetUserProfile(userId)?.EmailAddress;
        }

        public Google.Apis.Gmail.v1.Data.Message Send(Message message)
        {
            string rm = $"To: {message.To}\r\nSubject: {message.Subject}\r\nContent-Type: text/html;charset=utf-8\r\n\r\n{message.Body}";
            return send(Base64UrlEncode(rm), message.UserId);
        }

        static public string Base64UrlEncode(string input)
        {
            var data = System.Text.Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(data).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        Google.Apis.Gmail.v1.Data.Message send(string raw, string userId = Gmail.SearchFilter.OwnerMe)
        {
            Google.Apis.Gmail.v1.Data.Message m = new Google.Apis.Gmail.v1.Data.Message { Raw = raw };
            UsersResource.MessagesResource.SendRequest request = Service.Users.Messages.Send(m, userId);
            return request.Execute();
        }

        public Google.Apis.Gmail.v1.Data.Message Send(System.Net.Mail.MailMessage message, string userId = Gmail.SearchFilter.OwnerMe)
        {
            System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            System.Reflection.Assembly assembly = typeof(System.Net.Mail.SmtpClient).Assembly;
            Type mailWriterType = assembly.GetType("System.Net.Mail.MailWriter");
            string raw;
            using (MemoryStream stream = new MemoryStream())
            {
                System.Reflection.ConstructorInfo mailWriterConstructor = mailWriterType.GetConstructor(flags, null, new[] { typeof(Stream) }, null);
                object mailWriter = mailWriterConstructor.Invoke(new object[] { stream });
                System.Reflection.MethodInfo sendMethod = typeof(System.Net.Mail.MailMessage).GetMethod("Send", flags);
                sendMethod.Invoke(message, flags, null, new[] { mailWriter, true, true }, null);
                stream.Seek(0, SeekOrigin.Begin);
                using (StreamReader sr = new StreamReader(stream))
                {
                    raw = sr.ReadToEnd();
                }
                System.Reflection.MethodInfo closeMethod = mailWriter.GetType().GetMethod("Close", flags);
                closeMethod.Invoke(mailWriter, flags, null, new object[] { }, null);
            }
            return send(raw, userId);
        }
    }
}