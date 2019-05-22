using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using PuppeteerSharp.Input;

namespace Pyhh.Browsing
{
    public class VkBrwUser
    {
        public VkBrwUser()
        {
            
        }

        public VkBrwUser(string userPageUrl, VkontakteBrw vkontakte)
        {
            ProfileLink = userPageUrl;
            Vkontakte = vkontakte;
        }

        public string PageName { get; set; }
        public bool HiddenProfile { get; set; }
        public long UserId { get; set; }
        public VkontakteBrw Vkontakte { get; set; }
        public List<VkBrwCommunity> Communitites { get; set; } = new List<VkBrwCommunity>();
        public List<VkBrwUser> Friends { get; set; } = new List<VkBrwUser>();
        public BrowserContext UserPage { get; set; }
        public List<VkBrwUserWallPost> WallPosts { get; set; } = new List<VkBrwUserWallPost>();

        public string ProfileLink { get ; set; }

        private static readonly SemaphoreSlim userPageSemaphore = new SemaphoreSlim(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);

        private Browser _userBrowser;
        private BrowserContext _userBrowserCtx;
        private Page _userPage;


        private async Task<Browser> GetNewBrowserAsync()
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);

            LaunchOptions browserOptions = new LaunchOptions
            {
                Headless = true, Args = new string[] { "--lang=" + Vkontakte.BrowsingLanguage }
            };

            Browser browser = await Puppeteer.LaunchAsync(browserOptions);

            return browser;
        }

        private async Task<Page> GetNewPageAsync(BrowserContext context = null)
        {
            BrowserContext ctx;

            if (context == null)
            {
                Browser browser = await GetNewBrowserAsync();
                ctx = await browser.CreateIncognitoBrowserContextAsync();
            }
            else
            {
                ctx = context;
            }

            Page page = await ctx.NewPageAsync();
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 1024,
                Height = 768
            });
            await page.SetUserAgentAsync("Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 74.0.3723.0 Safari / 537.36");

            return page;
        }

        private async Task GetVkUserBrowser()
        {
            await userPageSemaphore.WaitAsync();

            try
            {
                _userBrowser = await GetNewBrowserAsync();
                _userBrowserCtx = await _userBrowser.CreateIncognitoBrowserContextAsync();
                _userPage = await GetNewPageAsync(_userBrowserCtx);
            }
            catch (Exception e)
            {
                Console.WriteLine("Can't get browser page and run for user " + ProfileLink + ": ", e);
            }
        }

        private async Task ReleaseVkUserBrowser()
        {
            try
            {
                if (_userPage != null && !_userPage.IsClosed)
                {
                    await _userPage.CloseAsync();
                }

                if (_userBrowserCtx != null)
                {
                    await _userBrowserCtx.CloseAsync();
                }

                if (_userBrowser != null && !_userBrowser.IsClosed)
                {
                    await _userBrowser.CloseAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Can't release browser resources for user " + ProfileLink + ": ", e);
            }
            finally
            {
                userPageSemaphore.Release();
            }
        }

        public async Task<List<VkBrwUserWallPost>> GetWallPostsAsync()
        {
            List<VkBrwUserWallPost> result = new List<VkBrwUserWallPost>();

            await GetVkUserBrowser();

            NavigationOptions navOptions = new NavigationOptions { Timeout = 120000,
                WaitUntil = new WaitUntilNavigation[] { WaitUntilNavigation.Load, WaitUntilNavigation.Networkidle2 } };
            WaitForSelectorOptions selectorOptions = new WaitForSelectorOptions { Timeout = 30000 };
            //https://vk.com/wall238081?own=1

            Response userPageResponse = null;

            try
            {
                userPageResponse = await _userPage.GoToAsync(Vkontakte.BaseUrl + "/wall" + UserId + "?own=1", navOptions);
            }
            catch (NavigationException e)
            {
                Console.WriteLine("Error opening user page for user " + ProfileLink + ": " + e);
                await ReleaseVkUserBrowser();
                return result;
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine("Error opening user page for user " + ProfileLink + ": " + e);
                await ReleaseVkUserBrowser();
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error opening user page for user " + ProfileLink + ": " + e);
                await ReleaseVkUserBrowser();
                return result;
            }

            if (userPageResponse?.Status != System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine("Cannot open wall records page for user " + ProfileLink + " status code: " + userPageResponse?.Status);
                return result;
            }

            string getUserWallPostsScript = Vkontakte.JsScripts.GetUserWallPosts;

            // Total CPU [unit, %]: 645
            var wallPostsResult = await _userPage.EvaluateFunctionAsync<List<VkBrwUserJsWallPost>>("() => {" + getUserWallPostsScript + "}");

            if (wallPostsResult != null)
            {
                if (wallPostsResult.Count > 0)
                {
                    wallPostsResult.ForEach(jsPost =>
                    {
                        VkBrwUserWallPost post = new VkBrwUserWallPost
                        {
                            Date = Vkontakte.BrowsingLanguage == "ru"
                                ? ConvertVkWallPostRusDate(jsPost.Date)
                                : ConvertVkWallPostEngDate(jsPost.Date),
                            Repost = jsPost.Repost,
                            Text = jsPost.Text,
                            User = this
                        };

                        result.Add(post);
                        WallPosts.Add(post);
                    });
                }
            }

            // Total CPU [unit, %]: 676
            ////ElementHandle wallSection = await _userPage.QuerySelectorAsync("#page_wall_posts.page_wall_posts.mark_top");

            ////if (wallSection != null)
            ////{
            ////    ElementHandle[] wallPostElements = await wallSection.QuerySelectorAllAsync("div._post_content");

            ////    if (wallPostElements != null && wallPostElements.Length > 0)
            ////    {
            ////        foreach (ElementHandle postElement in wallPostElements)
            ////        {
            ////            VkBrwUserWallPost post = new VkBrwUserWallPost();

            ////            ElementHandle dateElement = await postElement.QuerySelectorAsync("span.rel_date");

            ////            if (dateElement != null)
            ////            {
            ////                string postDateRaw = await dateElement.EvaluateFunctionAsync<string>("('span', span => span.innerText)");

            ////                DateTime newDate = ConvertVkWallPostRusDate(postDateRaw);

            ////                if (newDate != DateTime.MinValue)
            ////                {
            ////                    post.Date = newDate;
            ////                }
            ////            }

            ////            ElementHandle wallPostTextElement = await postElement.QuerySelectorAsync("div.wall_text");

            ////            if (wallPostTextElement != null)
            ////            {
            ////                post.Repost = (await wallPostTextElement.QuerySelectorAsync("div.copy_quote") != null);

            ////                ElementHandle postT = await wallPostTextElement.QuerySelectorAsync("div.wall_post_text");

            ////                if (postT != null)
            ////                {
            ////                    post.Text = await postT.EvaluateFunctionAsync<string>("'div', div => div.innerText");
            ////                }
            ////            }

            ////            result.Add(post);

            ////            post.User = this;
            ////            WallPosts.Add(post);
            ////        }
            ////    }
            ////}

            await ReleaseVkUserBrowser();

            return result;
        }

        private DateTime ConvertVkWallPostRusDate(string rawDate)
        {
            DateTime result = DateTime.MinValue;

            string[] dateComponents = rawDate.Split(' ');

            switch (dateComponents.Length)
            {
                case 2:
                    //час назад
                    if (rawDate == "час назад")
                    {
                        return DateTime.Now.AddHours(-1);
                    }
                    else if (rawDate == "минуту назад")
                    {
                        return DateTime.Now.AddMinutes(-1);
                    }
                    else if (rawDate == "только что")
                    {
                        return DateTime.Now;
                    }
                    else
                    {
                        return result;
                    }
                case 3:
                    //сегодня в 8:42
                    if (dateComponents[1].ToLowerInvariant() == "в")
                    {
                        switch (dateComponents[0].ToLowerInvariant())
                        {
                            case "сегодня":
                                DateTime segodnya = DateTime.Now;
                                dateComponents[2] = dateComponents[2].Length == 4
                                    ? "0" + dateComponents[2]
                                    : dateComponents[2];

                                string exampleSegDt = "2015-07-17T" + dateComponents[2] + ":00";
                                bool segSuccess = DateTime.TryParse(exampleSegDt, out DateTime exampleSegResult);

                                if (segSuccess)
                                {
                                    TimeSpan ts = new TimeSpan(exampleSegResult.Hour, exampleSegResult.Minute, 0);
                                    segodnya = segodnya.Date + ts;
                                    return segodnya;
                                }
                                else
                                {
                                    return result;
                                }

                            case "вчера":
                                DateTime vchera = DateTime.Now.AddDays(-1);
                                dateComponents[2] = dateComponents[2].Length == 4
                                    ? "0" + dateComponents[2]
                                    : dateComponents[2];

                                string exampleVchDt = "2015-07-17T" + dateComponents[2] + ":00";
                                bool vchSuccess = DateTime.TryParse(exampleVchDt, out DateTime exampleVchResult);

                                if (vchSuccess)
                                {
                                    TimeSpan ts = new TimeSpan(exampleVchResult.Hour, exampleVchResult.Minute, 0);
                                    vchera = vchera.Date + ts;
                                    return vchera;
                                }
                                else
                                {
                                    return result;
                                }
                        }
                    }

                    //25 янв 2016 
                    if (dateComponents[1].Length == 3 && !dateComponents[1].First().Equals("ч"))
                    {
                        dateComponents[1] = RusMonthToNumber(dateComponents[1]);
                    }
                    else
                    {
                        switch (dateComponents[1].ToLowerInvariant())
                        {
                            case "минут":
                            case "минуту":
                            case "минуты":
                                bool minISuccess = int.TryParse(dateComponents[0], out int minutesI);
                                if (minISuccess)
                                {
                                    result = DateTime.Now.AddMinutes(-minutesI);
                                    return result;
                                }
                                else
                                {
                                    dateComponents[0] = RusNumeralToNumber(dateComponents[0]);

                                    bool minI2Success = int.TryParse(dateComponents[0], out int minI2);
                                    if (minI2Success)
                                    {
                                        result = DateTime.Now.AddMinutes(-minI2);
                                        return result;
                                    }
                                    else
                                    {
                                        return result;
                                    }
                                }
                            case "часа":
                            case "часов":
                                bool houraSuccess = int.TryParse(dateComponents[0], out int houra);
                                if (houraSuccess)
                                {
                                    result = DateTime.Now.AddHours(-houra);
                                    return result;
                                }
                                else
                                {
                                    dateComponents[0] = RusNumeralToNumber(dateComponents[0]);

                                    bool houra2Success = int.TryParse(dateComponents[0], out int houra2);
                                    if (houra2Success)
                                    {
                                        result = DateTime.Now.AddHours(-houra2);
                                        return result;
                                    }
                                    else
                                    {
                                        return result;
                                    }
                                }
                            case "в":
                                break;
                            default:
                                dateComponents[1] = "01";
                                break;
                        }
                    }
                    

                    dateComponents[0] = (dateComponents[0].Length == 1) ? "0" + dateComponents[0] : dateComponents[0];

                    string reconstructedDate = dateComponents[0] + "/" + dateComponents[1] + "/" + dateComponents[2];

                    DateTime successParse = DateTime.MinValue;

                    try
                    {
                        successParse = DateTime.ParseExact(reconstructedDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error parsing post date " + reconstructedDate + " for user " + ProfileLink + ": " + e);
                    }

                    if (successParse != DateTime.MinValue)
                    {
                        result = successParse;
                    }

                    break;
                case 4:
                    //5 мая в 8:45
                    dateComponents[1] = RusMonthToNumber(dateComponents[1]);
                    dateComponents[0] = dateComponents[0].Length == 1 ? "0" + dateComponents[0] : dateComponents[0];
                    string reconstructedDate2 = dateComponents[0] + "/" + dateComponents[1] + "/" + DateTime.Now.Year;

                    DateTime postDate = DateTime.MinValue;

                    try
                    {
                        postDate = DateTime.ParseExact(reconstructedDate2, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error parsing post date " + reconstructedDate2 + " for user " + ProfileLink + ": " + e);
                    }

                    if (postDate != DateTime.MinValue)
                    {
                        dateComponents[3] = dateComponents[3].Length == 4 ? "0" + dateComponents[3] : dateComponents[3];

                        string exampleDt = "2015-07-17T" + dateComponents[3] + ":00";
                        bool exampleParseSuccess = DateTime.TryParse(exampleDt, out DateTime exampleSegResult);

                        if (exampleParseSuccess)
                        {
                            TimeSpan ts = new TimeSpan(exampleSegResult.Hour, exampleSegResult.Minute, 0);
                            postDate = postDate.Date + ts;
                            return postDate;
                        }
                        else
                        {
                            return result;
                        }
                    }
                    else
                    {
                        return result;
                    }
                case 5:
                    break;
                default:
                    break;
            }

            return result;
        }

        private DateTime ConvertVkWallPostEngDate(string rawDate)
        {
            DateTime result = DateTime.MinValue;

            string[] dateComponents = rawDate.Split(' ');

            switch (dateComponents.Length)
            {
                case 2:
                    //час назад
                    switch (rawDate)
                    {
                        case "hour ago":
                            return DateTime.Now.AddHours(-1);
                        case "minute ago":
                            return DateTime.Now.AddMinutes(-1);
                        case "just now":
                            return DateTime.Now;
                        default:
                            return result;
                    }
                case 3:
                    //25 Nov 2016
                    if (dateComponents[1].Length == 3 && !dateComponents[1].First().Equals("h"))
                    {
                        dateComponents[1] = EngMonthToNumber(dateComponents[1]);

                        dateComponents[0] = dateComponents[0].Length == 1 ? "0" + dateComponents[0] : dateComponents[0];

                        string reconstructedDate = dateComponents[0] + "/" + dateComponents[1] + "/" + dateComponents[2];

                        DateTime successParse = DateTime.MinValue;

                        try
                        {
                            successParse = DateTime.ParseExact(reconstructedDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error parsing post date " + reconstructedDate + " for user " + ProfileLink + ": " + e);
                        }

                        if (successParse != DateTime.MinValue)
                        {
                            result = successParse;
                        }
                    }
                    else
                    {
                        switch (dateComponents[1].ToLowerInvariant())
                        {
                            case "minute":
                            case "minutes":
                                bool minISuccess = int.TryParse(dateComponents[0], out int minutesI);

                                if (minISuccess)
                                {
                                    result = DateTime.Now.AddMinutes(-minutesI);
                                    return result;
                                }
                                else
                                {
                                    dateComponents[0] = EngNumeralToNumber(dateComponents[0]);

                                    bool minI2Success = int.TryParse(dateComponents[0], out int minI2);

                                    if (minI2Success)
                                    {
                                        result = DateTime.Now.AddMinutes(-minI2);
                                        return result;
                                    }
                                    else
                                    {
                                        return result;
                                    }
                                }
                            case "hour":
                            case "hours":
                                bool houraSuccess = int.TryParse(dateComponents[0], out int houra);

                                if (houraSuccess)
                                {
                                    result = DateTime.Now.AddHours(-houra);
                                    return result;
                                }
                                else
                                {
                                    dateComponents[0] = EngNumeralToNumber(dateComponents[0]);

                                    bool houra2Success = int.TryParse(dateComponents[0], out int houra2);

                                    if (houra2Success)
                                    {
                                        result = DateTime.Now.AddHours(-houra2);
                                        return result;
                                    }
                                    else
                                    {
                                        return result;
                                    }
                                }
                            case "в":
                                //вконтакте местами показывает дату на русском из-за российского айпишника?
                                break;
                            default:
                                dateComponents[1] = "01";
                                break;
                        }
                    }

                    break;
                case 4:
                    //yesterday at 8:38 pm
                    bool pm = dateComponents[3].ToLowerInvariant() == "pm";                    

                    if (dateComponents[1].ToLowerInvariant() == "at")
                    {
                        switch (dateComponents[0].ToLowerInvariant())
                        {
                            case "today":
                                DateTime today = DateTime.Now;

                                dateComponents[2] = dateComponents[2].Length == 4
                                    ? "0" + dateComponents[2]
                                    : dateComponents[2];

                                string exampleSegDt = "2015-07-17T" + dateComponents[2] + ":00";
                                bool tdySuccess = DateTime.TryParse(exampleSegDt, out DateTime exampleTdyResult);

                                if (tdySuccess)
                                {
                                    DateTime baseDate = pm ? exampleTdyResult.AddHours(12) : exampleTdyResult;

                                    TimeSpan ts = new TimeSpan(baseDate.Hour, baseDate.Minute, 0);

                                    today = today.Date + ts;
                                    return today;
                                }
                                else
                                {
                                    return result;
                                }

                            case "yesterday":
                                DateTime yesterday = DateTime.Now.AddDays(-1);
                                dateComponents[2] = dateComponents[2].Length == 4
                                    ? "0" + dateComponents[2]
                                    : dateComponents[2];

                                string exampleYtdDt = "2015-07-17T" + dateComponents[2] + ":00";
                                bool vchSuccess = DateTime.TryParse(exampleYtdDt, out DateTime exampleYtdResult);

                                if (vchSuccess)
                                {
                                    DateTime baseDate = pm ? exampleYtdResult.AddHours(12) : exampleYtdResult;

                                    TimeSpan ts = new TimeSpan(baseDate.Hour, baseDate.Minute, 0);
                                    yesterday = yesterday.Date + ts;
                                    return yesterday;
                                }
                                else
                                {
                                    return result;
                                }
                            default:
                                break;
                        }
                    }

                    break;
                case 5:
                    //10 May at 7:01 pm
                    bool pm2 = dateComponents[4].ToLowerInvariant() == "pm";

                    dateComponents[1] = EngMonthToNumber(dateComponents[1]);
                    dateComponents[0] = dateComponents[0].Length == 1 ? "0" + dateComponents[0] : dateComponents[0];
                    string reconstructedDate2 = dateComponents[0] + "/" + dateComponents[1] + "/" + DateTime.Now.Year;

                    DateTime postDate = DateTime.MinValue;

                    try
                    {
                        postDate = DateTime.ParseExact(reconstructedDate2, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error parsing post date " + reconstructedDate2 + " for user " + ProfileLink + ": " + e);
                    }

                    if (postDate != DateTime.MinValue)
                    {
                        dateComponents[3] = dateComponents[3].Length == 4 ? "0" + dateComponents[3] : dateComponents[3];

                        string exampleDt = "2015-07-17T" + dateComponents[3] + ":00";
                        bool exampleParseSuccess = DateTime.TryParse(exampleDt, out DateTime exampleSegResult);

                        if (exampleParseSuccess)
                        {
                            DateTime baseDate = pm2 ? exampleSegResult.AddHours(12) : exampleSegResult;

                            TimeSpan ts = new TimeSpan(baseDate.Hour, baseDate.Minute, 0);
                            postDate = postDate.Date + ts;
                            return postDate;
                        }
                        else
                        {
                            return result;
                        }
                    }
                    else
                    {
                        return result;
                    }
                case 6:
                    break;
                default:
                    break;
            }

            return result;
        }

        private static string RusMonthToNumber(string month)
        {
            switch (month.ToLowerInvariant())
            {
                case "янв":
                    return "01";
                case "фев":
                    return "02";
                case "мар":
                    return "03";
                case "апр":
                    return "04";
                case "мая":
                    return "05";
                case "июн":
                    return "06";
                case "июл":
                    return "07";
                case "авг":
                    return "08";
                case "сен":
                    return "09";
                case "окт":
                    return "10";
                case "ноя":
                    return "11";
                case "дек":
                    return "12";
                default:
                    return "";
            }
        }

        private static string EngMonthToNumber(string month)
        {
            switch (month.ToLowerInvariant())
            {
                case "jan":
                    return "01";
                case "feb":
                    return "02";
                case "mar":
                    return "03";
                case "apr":
                    return "04";
                case "may":
                    return "05";
                case "jun":
                    return "06";
                case "jul":
                    return "07";
                case "aug":
                    return "08";
                case "sep":
                    return "09";
                case "oct":
                    return "10";
                case "nov":
                    return "11";
                case "dec":
                    return "12";
                default:
                    return "";
            }
        }

        private static string RusNumeralToNumber(string word)
        {
            switch (word.ToLowerInvariant())
            {
                case "один":
                    return "1";
                case "двe":
                case "две":
                case "два":
                    return "2";
                case "три":
                    return "3";
                case "четыре":
                    return "4";
                case "пять":
                    return "5";
                default:
                    return "";
            }
        }

        private static string EngNumeralToNumber(string word)
        {
            switch (word.ToLowerInvariant())
            {
                case "one":
                    return "1";
                case "two":
                    return "2";
                case "three":
                    return "3";
                case "four":
                    return "4";
                case "five":
                    return "5";
                default:
                    return "";
            }
        }


        ////public List<VkCommunity> GetCommunities()
        ////{
        ////    new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision).GetAwaiter().GetResult();
        ////    var browser = Puppeteer.LaunchAsync(new LaunchOptions
        ////    {
        ////        Headless = false
        ////    }).GetAwaiter().GetResult();
        ////    BrowserContext userContext = browser.CreateIncognitoBrowserContextAsync().GetAwaiter().GetResult();
        ////    var userPage = userContext.NewPageAsync().GetAwaiter().GetResult();
        ////    userPage.SetViewportAsync(new ViewPortOptions
        ////    {
        ////        Height = 768,
        ////        Width = 1024
        ////    }).GetAwaiter().GetResult();
        ////    var ver = browser.GetVersionAsync().Result;
        ////    var userAgent = browser.GetUserAgentAsync().Result;
        ////    userPage.SetUserAgentAsync("Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 74.0.3723.0 Safari / 537.36");

        ////    //page.GoToAsync("http://iloveradio.de/iloveradio/").GetAwaiter().GetResult();
        ////    //var metrics = page.MetricsAsync().Result;
        ////    //var content = page.GetContentAsync().Result;
        ////    //var cookies = page.GetCookiesAsync().Result;
        ////    // ID selector should be marked with hashsign
        ////    // Getting all DIV sub-elements from artisttitle section
        ////    //var elementHandle = page.WaitForSelectorAsync("#artisttitle DIV").Result;

        ////    // innerText is a visible text from an element
        ////    //var artist = page.EvaluateExpressionAsync<string>("$('#artisttitle DIV')[0].innerText").Result;

        ////    List<VkCommunity> nwPages = new List<VkCommunity>();

        ////    userPage.GoToAsync(ProfileLink).GetAwaiter().GetResult();
        ////    var userIsLoaded = userPage.WaitForSelectorAsync("#page_info_wrap.page_info_wrap").Result;

        ////    var hiddenProfileBlock = userPage.QuerySelectorAllAsync("h5.profile_deleted_text").Result;

        ////    if (hiddenProfileBlock.Length == 0)
        ////    {
        ////        var idolsLoaded = userPage.WaitForSelectorAsync("#profile_idols").Result;
        ////        // Интересные страницы
        ////        var noteworthyPages = userPage.QuerySelectorAllAsync("div.line_cell.clear_fix").Result.ToList();

        ////        foreach (var element in noteworthyPages)
        ////        {
        ////            var pageElements = element.QuerySelectorAllAsync("div.fl_l.desc_info").Result.ToList();

        ////            if (pageElements.Count == 1)
        ////            {
        ////                VkCommunity nwPage = new VkCommunity();

        ////                var pageElement = pageElements.First();
        ////                ElementHandle groupName = pageElement.QuerySelectorAsync("div.group_name").Result;
        ////                // equals to $eval('a', a => a.innerText)
        ////                nwPage.Name = groupName.EvaluateFunctionAsync<string>("('a', a => a.innerText)").Result;


        ////                ////await page.EvaluateFunctionAsync(@"() => {
        ////                ////    Array.from(document.querySelectorAll('li'))
        ////                ////    .find(l => l.querySelector('span').innerText === 'fire').querySelector('INPUT').click();
        ////                ////}");


        ////                var groupDesc = pageElement.QuerySelectorAsync("div.group_desc").Result;
        ////                nwPage.Description = groupDesc.EvaluateFunctionAsync<string>("('div', div => div.innerText)").Result;

        ////                ElementHandle groupLink = pageElement.QuerySelectorAsync("a").Result;
        ////                nwPage.CommunityUrl = groupLink.EvaluateFunctionAsync<string>("('a', a => a.href)").Result;

        ////                nwPage.CommunityId = GetGroupId(userContext, nwPage.CommunityUrl);

        ////                nwPages.Add(nwPage);
        ////            }
        ////        }
        ////    }
        ////    else
        ////    {
        ////        HiddenProfile = true;
        ////        //move to constructor
        ////        var pageNameElements = userPage.QuerySelectorAllAsync("h2.page_name").Result;

        ////        if (pageNameElements.Length == 1)
        ////        {
        ////            var pageElement = pageNameElements.First();
        ////            PageName = pageElement.EvaluateFunctionAsync<string>("('h2', h2 => h2.innerText)").Result;
        ////        }
        ////    }



        ////    ////var block = page.EvaluateExpressionAsync<string>("$('#profile_idols DIV')[0].innerText").Result;
        ////    var path = "C:\\Pyhh\\bin\\Debug\\netcoreapp2.2\\screen.file";
        ////    ////page.ScreenshotAsync(path, new ScreenshotOptions { FullPage = true }).GetAwaiter().GetResult();

        ////    browser.CloseAsync().GetAwaiter().GetResult();

        ////    return nwPages;
        ////}

        ////private string GetGroupId(BrowserContext browser, string groupUrl)
        ////{
        ////    Page groupPage = browser.NewPageAsync().GetAwaiter().GetResult();

        ////    ////groupPage.EvaluateFunctionAsync($"() => window.open('{groupUrl}')").GetAwaiter().GetResult();
        ////    ////Target newWindowTarget = browser.WaitForTargetAsync(target => target.Url == "https://www.example.com/").Result;
        ////    ////Page newPage = newWindowTarget.PageAsync().Result;

        ////    groupPage.GoToAsync(groupUrl).GetAwaiter().GetResult();
        ////    var groupLoaded = groupPage.WaitForSelectorAsync("#wall_tabs.ui_tabs.clear_fix.ui_tabs_with_progress").Result;

        ////    ElementHandle groupIdElement = groupPage.QuerySelectorAsync("a.ui_tab.ui_tab_sel").Result;
        ////    string idLink = groupIdElement.EvaluateFunctionAsync<string>("('a', a => a.href)").Result;

        ////    groupPage.CloseAsync().GetAwaiter().GetResult();

        ////    int pFrom = idLink.IndexOf("-", StringComparison.InvariantCultureIgnoreCase) + "-".Length;
        ////    int pTo = idLink.LastIndexOf("?", StringComparison.InvariantCultureIgnoreCase);

        ////    if (pFrom != -1 && pTo != -1)
        ////    {
        ////        return idLink.Substring(pFrom, pTo - pFrom);
        ////    }

        ////    return null;
        ////}
    }
}
