using PuppeteerSharp;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pyhh.Browsing
{
    public enum CommunityTypes
    {
        Unknown = 0,
        Group = 1,
        Public = 2,
        Event = 4
    }

    public class VkBrwCommunity
    {
        public VkBrwCommunity()
        {

        }

        public VkBrwCommunity(string communityUrl)
        {
            CommunityUrl = communityUrl;
        }

        public bool Blocked { get; set; }
        public string Name { get; set; }
        public string CommunityId { get; set; }
        public string CommunityUrl { get; set; }
        public string Status { get; set; }
        public string Size { get; set; }
        public CommunityTypes Type { get; set; }
        public List<VkBrwUser> Users { get; set; } = new List<VkBrwUser>();


        public async Task GetUsers()
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            Browser browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false
            });
            BrowserContext userContext = await browser.CreateIncognitoBrowserContextAsync();
            Page communityPage = await userContext.NewPageAsync();
            await communityPage.SetViewportAsync(new ViewPortOptions
            {
                Height = 768,
                Width = 1024
            });
            string ver = await browser.GetVersionAsync();
            string userAgent = await browser.GetUserAgentAsync();
            await communityPage.SetUserAgentAsync("Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 74.0.3723.0 Safari / 537.36");
            
            await communityPage.GoToAsync(CommunityUrl);
            ElementHandle followersLoaded = await communityPage.WaitForSelectorAsync("#public_followers");
        }
    }
}
