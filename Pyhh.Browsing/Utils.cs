using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Pyhh.Browsing
{
    public static class Utils
    {
        public static IEnumerable<List<T>> SplitList<T>(this List<T> list, int batchSize)
        {
            for (int i = 0; i < list.Count; i += batchSize)
            {
                yield return list.GetRange(i, Math.Min(batchSize, list.Count - i));
            }
        }

        public static DateTime UnixTimeStampToDateTime(this double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        public static double ToUnixTimestamp(this DateTime dateTime)
        {
            return (TimeZoneInfo.ConvertTimeToUtc(dateTime) -
                    new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
        }

        public static string ListToString(this IList list)
        {
            StringBuilder result = new StringBuilder(string.Empty);

            if (list.Count > 0)
            {
                result.Append(list[0]);
                for (int i = 1; i < list.Count; i++)
                    result.AppendFormat(",{0}", list[i]);
            }
            return result.ToString();
        }

        public static HttpClient GetHttpClient(string userAgent = "Pyhh/1.0", int requestTimeout = 60)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
            client.Timeout = TimeSpan.FromSeconds(requestTimeout);

            return client;
        }

        public static async Task<T> ReadAsJsonAsync<T>(this HttpContent content)
        {
            string json = await content.ReadAsStringAsync();
            T value = JsonConvert.DeserializeObject<T>(json);
            return value;
        }
    }
}
