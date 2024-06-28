//********************************************************************************************
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Cliver
{
    /// <summary>
    /// Experimental helper class. Must be considered as a framework.
    /// </summary>
    public class Google_
    {
        public static List<System.Net.HttpStatusCode> RetriableHttpCodes = new List<System.Net.HttpStatusCode> {
            System.Net.HttpStatusCode.InternalServerError,
            System.Net.HttpStatusCode.Gone,
            System.Net.HttpStatusCode.BadRequest,
        };

        /// <summary>
        /// Trier adapted for google API requests. Can be used as a framework.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logMessage"></param>
        /// <param name="function"></param>
        /// <param name="pollMinNumber"></param>
        /// <param name="additionalRetriableHttpCodes"></param>
        /// <returns></returns>
        /// <exception cref="Exception2"></exception>
        public static T Try<T>(string logMessage, Func<T> function, int pollMinNumber = 3, IEnumerable<System.Net.HttpStatusCode> additionalRetriableHttpCodes = null) where T : class
        {
            if (additionalRetriableHttpCodes != null)
                RetriableHttpCodes.AddRange(additionalRetriableHttpCodes);
            if (logMessage != null)
                Log.Inform(logMessage);
            T o = SleepRoutines.WaitForObject(
                () =>
                {
                    try
                    {
                        return function();
                    }
                    catch (Google.GoogleApiException ex)
                    {
                        if (RetriableHttpCodes.Contains(ex.HttpStatusCode))
                        {
                            Log.Warning2("Retrying...", ex);
                            return null;
                        }
                        throw;
                    }
                },
                0, 0, false, pollMinNumber
            );
            if (o == null)
            {
                string m = logMessage != null ? Regex.Replace(logMessage, @"\.\.\.", "") : nameof(Google_) + "." + nameof(Try) + "()";
                throw new Exception2("Failed: " + m);
            }
            return o;
        }

        /// <summary>
        /// Trier adapted for google API requests. Can be used as a framework.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="function"></param>
        /// <param name="pollMinNumber"></param>
        /// <param name="additionalRetriableHttpCodes"></param>
        /// <returns></returns>
        public static T Try<T>(Func<T> function, int pollMinNumber = 3, IEnumerable<System.Net.HttpStatusCode> additionalRetriableHttpCodes = null) where T : class
        {
            return Try(null, function, pollMinNumber, additionalRetriableHttpCodes);
        }

        /// <summary>
        /// Trier adapted for google API requests. Can be used as a framework.
        /// </summary>
        /// <param name="logMessage"></param>
        /// <param name="action"></param>
        /// <param name="pollMinNumber"></param>
        /// <param name="additionalRetriableHttpCodes"></param>
        public static void Try(string logMessage, Action action, int pollMinNumber = 3, IEnumerable<System.Net.HttpStatusCode> additionalRetriableHttpCodes = null)
        {
            Try(logMessage, () => { action(); return new Object(); }, pollMinNumber, additionalRetriableHttpCodes);
        }

        /// <summary>
        /// Trier adapted for google API requests. Can be used as a framework.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="pollMinNumber"></param>
        /// <param name="additionalRetriableHttpCodes"></param>
        public static void Try(Action action, int pollMinNumber = 3, IEnumerable<System.Net.HttpStatusCode> additionalRetriableHttpCodes = null)
        {
            Try(null, action, pollMinNumber, additionalRetriableHttpCodes);
        }
    }
}