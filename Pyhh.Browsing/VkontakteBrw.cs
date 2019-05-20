using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Input;

namespace Pyhh.Browsing
{
    public class VkontakteBrw
    {
        public VkontakteBrw()
        {
            BaseUrl = "https://vk.com";
        }

        public List<VkBrwUser> Users { get; set; } = new List<VkBrwUser>();
        public List<VkBrwCommunity> Communities { get; set; } = new List<VkBrwCommunity>();
        public string BaseUrl { get; set; }
        internal Countries CurrentCountry { get; set; }
        internal string BrowsingLanguage { get; set; }

        public ManualResetEventSlim DiscoveringCompleted { get; } = new ManualResetEventSlim(false);

        internal JavaScriptItems JsScripts { get; set; } = new JavaScriptItems();

        public Task DiscoverAsync()
        {
            return DiscoverSystemAsync();
        }

        private async Task DiscoverSystemAsync()
        {
            GeoDetector geoDetector = new GeoDetector();
            Countries country = await geoDetector.GetCountryAsync();
            CurrentCountry = (country != Countries.UN) ? country : Countries.UN;

            switch (CurrentCountry.ToString().ToLowerInvariant())
            {
                case "ru":
                    BrowsingLanguage = "ru";
                    break;
                case "cn":
                case "us":
                case "de":
                case "fr":
                case "ie":
                case "gb":
                case "sg":
                    BrowsingLanguage = "en";
                    break;
                default:
                    BrowsingLanguage = "ru";
                    break;
            }

            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);

            LaunchOptions browserOptions = new LaunchOptions
            {
                Headless = true, Args = new string[] {"--lang=" + BrowsingLanguage}
            };

            Browser browser = await Puppeteer.LaunchAsync(browserOptions);

            BrowserContext discoveryContext = await browser.CreateIncognitoBrowserContextAsync();
            Page page = await discoveryContext.NewPageAsync();
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 1024,
                Height = 768
            });
            string ver = await browser.GetVersionAsync();
            string userAgent = await browser.GetUserAgentAsync();
            await page.SetUserAgentAsync("Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 74.0.3723.0 Safari / 537.36");

            NavigationOptions navOptions = new NavigationOptions { Timeout = 30000 };
            WaitForSelectorOptions selectorOptions = new WaitForSelectorOptions { Timeout = 10000 };
            ClickOptions clickOptions = new ClickOptions { Delay = new Random().Next(100, 200) };

            await page.GoToAsync(BaseUrl, navOptions);
            ElementHandle allProductsButton = await page.WaitForSelectorAsync("a.login_all_products_button", selectorOptions);

            if (allProductsButton != null)
            {
                await allProductsButton.ClickAsync(clickOptions);
                ElementHandle topMenuElements = await page.WaitForSelectorAsync("ul.ui_tabs.clear_fix.blog_about_tabs.page_header_wrap", selectorOptions);

                if (topMenuElements != null)
                {
                    DiscoveringCompleted.Set();
                }
            }

            if (!page.IsClosed)
            {
                await page.CloseAsync();
            }
            
            await discoveryContext.CloseAsync();

            if (!browser.IsClosed)
            {
                await browser.CloseAsync();
            }
        }

        public void AddUser(VkBrwUser user)
        {
            Users.Add(user);

            if (user.Communitites.Count > 0)
            {
                foreach (VkBrwCommunity community in user.Communitites)
                {
                    VkBrwCommunity existingCommunity = Communities.FirstOrDefault(c => c.CommunityId == community.CommunityId);

                    if (existingCommunity == null)
                    {
                        Communities.Add(community);
                    }
                    else
                    {
                        VkBrwUser existingUser = existingCommunity.Users.FirstOrDefault(u => u.ProfileLink == user.ProfileLink);

                        if (existingUser == null)
                        {
                            existingCommunity.Users.Add(user);
                        }
                    }
                }
            }
        }

        public async Task<List<VkBrwUserWallPost>> GetWallRecords(List<VkBrwUser> users)
        {
            List<VkBrwUserWallPost> result = new List<VkBrwUserWallPost>();

            if (users.Count > 0)
            {
                int usersCount = users.Count;
                int maxParallelism = Environment.ProcessorCount * 2;
                int usersInBatch = usersCount / maxParallelism;

                IEnumerable<List<VkBrwUser>> userBatches = users.SplitList(usersInBatch);

                List<Task<List<VkBrwUserWallPost>>> postTasks = userBatches.Select(GetUserWallRecords).ToList();
                await Task.WhenAll(postTasks);

                foreach (Task<List<VkBrwUserWallPost>> task in postTasks.Where(t => t.IsCompleted && t.Result != null))
                {
                    result.AddRange(task.Result);
                }
            }            

            return result;
        }

        public async Task<List<VkBrwUserWallPost>> GetUserWallRecords(List<VkBrwUser> users)
        {
            List<VkBrwUserWallPost> result = new List<VkBrwUserWallPost>();

            foreach (VkBrwUser user in users)
            {
                List<VkBrwUserWallPost> posts = await user.GetWallPostsAsync();

                if (posts.Count > 0)
                {
                    result.AddRange(posts);
                }
            }

            return result;
        }
    }
}
