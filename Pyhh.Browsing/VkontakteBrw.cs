using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Input;

namespace Pyhh.Browsing
{
    public enum BrowsingLanguage
    {
        UN = 0,
        RU = 1,
        EN = 2
    }

    public class VkontakteBrw
    {
        public VkontakteBrw(bool mobile)
        {
            Mobile = mobile;
            BaseUrl = mobile ? "https://m.vk.com" : "https://vk.com";
            UserAgent = Mobile ?
                "Mozilla / 5.0(Linux; Android 4.2.1; en - us; Nexus 5 Build / JOP40D) AppleWebKit / 535.19(KHTML, like Gecko; googleweblight) Chrome / 38.0.1025.166 Mobile Safari/ 535.19" :
                "Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 74.0.3723.0 Safari / 537.36";

            DefaultNavigationOptions = new NavigationOptions { Timeout = 30000 };
            DefaultWaitForSelectorOptions = new WaitForSelectorOptions { Timeout = 10000 };
            DefaultClickOptions = new ClickOptions { Delay = new Random().Next(100, 200) };
        }

        public List<VkBrwUser> Users { get; set; } = new List<VkBrwUser>();
        public List<VkBrwCommunity> Communities { get; set; } = new List<VkBrwCommunity>();
        public bool Mobile { get; set; }
        public string UserAgent { get; set; }
        public string BaseUrl { get; set; }
        public NavigationOptions DefaultNavigationOptions { get; set; }
        public WaitForSelectorOptions DefaultWaitForSelectorOptions { get; set; }
        public ClickOptions DefaultClickOptions { get; set; }
        internal Countries CurrentCountry { get; set; }
        internal BrowsingLanguage Language { get; set; }
        public int CollectedProfiles { get; set; }
        public int NonPublicProfiles { get; set; }
        public int NonExistingProfiles { get; set; }
        public int EmptyWallProfiles { get; set; }

        public ManualResetEventSlim DiscoveringCompleted { get; } = new ManualResetEventSlim(false);

        internal JavaScriptItems JsScripts { get; set; } = new JavaScriptItems();

        public Task DiscoverAsync()
        {
            return DiscoverSystemAsync();
        }

        private async Task DiscoverSystemAsync()
        {
            GeoDetector geoDetector = new GeoDetector();
            CurrentCountry = await geoDetector.GetCountryAsync();

            switch (CurrentCountry)
            {
                case Countries.RU:
                    Language = BrowsingLanguage.RU;
                    break;
                case Countries.CN:
                case Countries.US:
                case Countries.DE:
                case Countries.FR:
                case Countries.IE:
                case Countries.GB:
                case Countries.SG:
                    Language = BrowsingLanguage.EN;
                    break;
                default:
                    Language = BrowsingLanguage.RU;
                    break;
            }

            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);

            LaunchOptions browserOptions = new LaunchOptions
            {
                Headless = true, Args = new string[] {"--lang=" + Language.ToString().ToLowerInvariant()}
            };

            Browser browser = await Puppeteer.LaunchAsync(browserOptions);

            BrowserContext discoveryContext = await browser.CreateIncognitoBrowserContextAsync();
            Page page = await discoveryContext.NewPageAsync();
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 1024,
                Height = 768
            });            
            await page.SetUserAgentAsync(UserAgent);

            if (Mobile)
            {
                await DiscoverServiceMobile(page);
            }
            else
            {
                await DiscoverServiceDesktop(page);
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

        private async Task DiscoverServiceMobile(Page page)
        {
            await page.GoToAsync(BaseUrl, DefaultNavigationOptions);
            ElementHandle forgetPasswordButton = await page.WaitForSelectorAsync("div.near_btn.wide_button.login_restore", DefaultWaitForSelectorOptions);

            if (forgetPasswordButton != null)
            {
                await forgetPasswordButton.ClickAsync(DefaultClickOptions);
                ElementHandle restoreElements = await page.WaitForSelectorAsync("div.PanelHeader__container", DefaultWaitForSelectorOptions);

                if (restoreElements != null)
                {
                    DiscoveringCompleted.Set();
                }
            }

        }

        private async Task DiscoverServiceDesktop(Page page)
        {
            await page.GoToAsync(BaseUrl, DefaultNavigationOptions);
            ElementHandle allProductsButton = await page.WaitForSelectorAsync("a.login_all_products_button", DefaultWaitForSelectorOptions);

            if (allProductsButton != null)
            {
                await allProductsButton.ClickAsync(DefaultClickOptions);
                ElementHandle topMenuElements = await page.WaitForSelectorAsync("ul.ui_tabs.clear_fix", DefaultWaitForSelectorOptions);

                if (topMenuElements != null)
                {
                    DiscoveringCompleted.Set();
                }
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
                var cts = new CancellationTokenSource();
                List<Task<List<VkBrwUserWallPost>>> postTasks = userBatches.Select(GetUserWallRecords).ToList();
                await Task.WhenAll(postTasks).ConfigureAwait(false);

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
