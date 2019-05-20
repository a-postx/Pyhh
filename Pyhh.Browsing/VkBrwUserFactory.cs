using PuppeteerSharp;
using PuppeteerSharp.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pyhh.Browsing
{
    public class VkBrwUserFactory
    {
        public VkBrwUserFactory(VkontakteBrw vk)
        {
            Vkontakte = vk;
        }

        private VkontakteBrw Vkontakte { get; set; }

        protected VkBrwUser GetEmpty()
        {
            return new VkBrwUser();
        }

        protected async Task<VkBrwUser> GetNewAsync(string userPageUrl)
        {
            VkBrwUser user = new VkBrwUser(userPageUrl, Vkontakte);
            
            await InitializeAsync(user);
            
            return user;
        }

        private async Task<VkBrwUser> InitializeAsync(VkBrwUser user)
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);

            LaunchOptions browserOptions = new LaunchOptions {Headless = false, Args = new string[] {"--lang=ru"}};

            Browser brw = await Puppeteer.LaunchAsync(browserOptions);

            BrowserContext userContext = await brw.CreateIncognitoBrowserContextAsync();
            Page userPage = await userContext.NewPageAsync();
            await userPage.SetViewportAsync(new ViewPortOptions
            {
                Width = 1024,
                Height = 768
            });

            await userPage.SetUserAgentAsync("Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 74.0.3723.0 Safari / 537.36");

            user.Communitites = await GetCommunities(userContext, userPage, user);
            bool successParse = long.TryParse(user.ProfileLink.Split('/').LastOrDefault()?.Substring(2), out long parseResult);
            user.UserId = successParse ? parseResult : default;

            await userPage.CloseAsync();
            await userContext.CloseAsync();

            return user;
        }

        public async Task<List<VkBrwCommunity>> GetCommunities(BrowserContext userContext, Page userPage, VkBrwUser user)
        {
            List<VkBrwCommunity> nwPages = new List<VkBrwCommunity>();

            await userPage.GoToAsync(user.ProfileLink);
            ElementHandle userIsLoaded = await userPage.WaitForSelectorAsync("#page_info_wrap.page_info_wrap");

            ElementHandle[] hiddenProfileBlock = await userPage.QuerySelectorAllAsync("h5.profile_deleted_text");

            if (hiddenProfileBlock.Length == 0)
            {
                ElementHandle idolsLoaded = await userPage.WaitForSelectorAsync("#profile_idols");

                // Интересные страницы
                ElementHandle[] noteworthyPagesBlock = await userPage.QuerySelectorAllAsync("#profile_idols.module.clear.page_list_module._module");

                if (noteworthyPagesBlock.Length == 1)
                {
                    ElementHandle noteworthyPages = noteworthyPagesBlock.First();
                    ElementHandle[] noteworthyPagesHeaderBlock = await noteworthyPages.QuerySelectorAllAsync("a.module_header");

                    if (noteworthyPagesHeaderBlock.Length == 1)
                    {
                        ClickOptions clickOptions = new ClickOptions { Delay = new Random().Next(30, 100) };

                        ElementHandle noteworthyPagesLinkElement = noteworthyPagesHeaderBlock.First();
                        await noteworthyPagesLinkElement.ClickAsync(clickOptions);

                        ElementHandle noteworthyPagesIsOpened = await userPage.WaitForSelectorAsync("#fans_rowsidols.fans_rows.fans_idols");

                        ElementHandle[] closeBlock = await userPage.QuerySelectorAllAsync("div.box_x_button");

                        if (closeBlock.Length == 1)
                        {
                            ElementHandle[] pagesCountBlock = await userPage.QuerySelectorAllAsync("span.ui_box_header_cnt");

                            if (pagesCountBlock.Length == 1)
                            {
                                ElementHandle pagesTotalCountElement = pagesCountBlock.First();
                                string pagesTotalCountValue = await pagesTotalCountElement.EvaluateFunctionAsync<string>("('span', span => span.innerText)");

                                if (!string.IsNullOrEmpty(pagesTotalCountValue))
                                {
                                    bool pagesTotalCountReceived = int.TryParse(pagesTotalCountValue, out int pagesTotalCount);

                                    if (pagesTotalCountReceived && pagesTotalCount > 0)
                                    {
                                        ElementHandle[] pagesVisibleElements = await userPage.QuerySelectorAllAsync("div.fans_idol_row.inl_bl");

                                        if (pagesVisibleElements.Length < pagesTotalCount)
                                        {
                                            PressOptions pressOptions = new PressOptions { Delay = new Random().Next(20, 40)};

                                            await userPage.FocusAsync("input");
                                            await userPage.Keyboard.PressAsync("Tab", pressOptions);

                                            int visiblePagesCounter = pagesVisibleElements.Length;

                                            while (visiblePagesCounter < pagesTotalCount)
                                            {
                                                await userPage.Keyboard.PressAsync("PageDown", pressOptions);
                                                await Task.Delay(new Random().Next(250, 350));
                                                await userPage.Keyboard.PressAsync("PageDown", pressOptions);
                                                await Task.Delay(new Random().Next(250, 350));
                                                await userPage.Keyboard.PressAsync("PageDown", pressOptions);
                                                await Task.Delay(new Random().Next(250, 350));
                                                await userPage.Keyboard.PressAsync("PageDown", pressOptions);
                                                await Task.Delay(new Random().Next(250, 350));

                                                ElementHandle[] newPagesVisibleElements = await userPage.QuerySelectorAllAsync("div.fans_idol_row.inl_bl");

                                                if (newPagesVisibleElements.Length == visiblePagesCounter)
                                                {
                                                    break;
                                                }

                                                visiblePagesCounter = newPagesVisibleElements.Length;
                                            }
                                        }

                                        ElementHandle[] nwPagesElements = await userPage.QuerySelectorAllAsync("div.fans_idol_info");

                                        foreach (var element in nwPagesElements)
                                        {
                                            VkBrwCommunity community = await GetCommunityAsync(element, userContext);

                                            if (community != null)
                                            {
                                                nwPages.Add(community);
                                                community.Users.Add(user);
                                            }
                                        }
                                    }
                                }
                            }

                            ElementHandle closeButtonElement = closeBlock.First();
                            await closeButtonElement.ClickAsync(clickOptions);
                        }
                    }
                }
            }
            else
            {
                user.HiddenProfile = true;

                ElementHandle[] pageNameElements = await userPage.QuerySelectorAllAsync("h2.page_name");

                if (pageNameElements.Length == 1)
                {
                    var pageElement = pageNameElements.First();
                    user.PageName = await pageElement.EvaluateFunctionAsync<string>("('h2', h2 => h2.innerText)");
                }
            }

            return nwPages;
        }

        private async Task<VkBrwCommunity> GetCommunityAsync(ElementHandle communityElement, BrowserContext userContext)
        {
            ElementHandle communityNameElement = await communityElement.QuerySelectorAsync("div.fans_idol_name");

            if (communityNameElement != null)
            {
                VkBrwCommunity nwPage = new VkBrwCommunity();

                ////await page.EvaluateFunctionAsync(@"() => {
                ////    Array.from(document.querySelectorAll('li'))
                ////    .find(l => l.querySelector('span').innerText === 'fire').querySelector('INPUT').click();
                ////}");

                // equals to $eval('a', a => a.innerText)
                nwPage.Name = await communityNameElement.EvaluateFunctionAsync<string>("('a', a => a.innerText)");

                ElementHandle communityLinkElement = await communityElement.QuerySelectorAsync("a.fans_idol_lnk");
                nwPage.CommunityUrl = await communityLinkElement.EvaluateFunctionAsync<string>("('a', a => a.href)");

                ElementHandle communityStatusElement = await communityElement.QuerySelectorAsync("div.fans_idol_status");
                nwPage.Status = await communityStatusElement.EvaluateFunctionAsync<string>("('div', div => div.innerText)");

                ElementHandle communitySizeElement = await communityElement.QuerySelectorAsync("div.fans_idol_size");
                nwPage.Size = await communitySizeElement.EvaluateFunctionAsync<string>("('div', div => div.innerText)");

                if (!nwPage.CommunityUrl.Contains("event"))
                {
                    await GetCommunityDetailsAsync(userContext, nwPage);
                }

                return nwPage;
            }
            else
            {
                return null;
            }
        }

        private async Task GetCommunityDetailsAsync(BrowserContext browserContext, VkBrwCommunity community)
        {
            Page communityPage = await browserContext.NewPageAsync();

            ////groupPage.EvaluateFunctionAsync($"() => window.open('{groupUrl}')").GetAwaiter().GetResult();
            ////Target newWindowTarget = browser.WaitForTargetAsync(target => target.Url == "https://www.example.com/").Result;
            ////Page newPage = newWindowTarget.PageAsync().Result;

            try
            {
                await communityPage.GoToAsync(community.CommunityUrl);
            }
            catch (NavigationException)
            {
                return;
            }

            WaitForSelectorOptions waitSelectorOptions = new WaitForSelectorOptions { Timeout = 10000 };

            ElementHandle communityLoadedElement = await communityPage.WaitForSelectorAsync("div#page_wrap.scroll_fix_wrap._page_wrap", waitSelectorOptions);

            if (communityLoadedElement != null)
            {
                ElementHandle communityBlockedElement = await communityPage.QuerySelectorAsync("div.groups_blocked");

                if (communityBlockedElement != null)
                {
                    community.Blocked = true;
                }
                else
                {
                    community.Type = await GetCommunityTypeAsync(communityPage);
                    community.CommunityId = await GetCommunityIdAsync(communityPage, community.Type);
                }
            }

            await communityPage.CloseAsync();
        }

        private async Task<CommunityTypes> GetCommunityTypeAsync(Page communityPage)
        {
            ElementHandle publicElement = await communityPage.QuerySelectorAsync("#public_followers.module.clear.people_module._module");

            if (publicElement!= null)
            {
                return CommunityTypes.Public;
            }

            ElementHandle groupElement = await communityPage.QuerySelectorAsync("#group_followers.module.clear.people_module._module");

            if (groupElement != null)
            {
                return CommunityTypes.Group;
            }

            if (communityPage.Url.Contains("event"))
            {
                return CommunityTypes.Event;
            }

            return CommunityTypes.Unknown;
        }

        private async Task<string> GetCommunityIdAsync(Page communityPage, CommunityTypes type)
        {
            string result = null;

            string linkString = (type == CommunityTypes.Public) ?
                "#public_followers.module.clear.people_module._module" : "#group_followers.module.clear.people_module._module";

            ElementHandle communityFollowersElement = await communityPage.QuerySelectorAsync(linkString);
            
            if (communityFollowersElement != null)
            {
                ElementHandle communityFollowersSearchElement = await communityFollowersElement.QuerySelectorAsync("a.module_header");
                string communityFollowersSearchLink = await communityFollowersSearchElement.EvaluateFunctionAsync<string>("('a', a => a.href)");

                string linkLastPart = communityFollowersSearchLink.Split("=").Last();

                if (!string.IsNullOrEmpty(linkLastPart))
                {
                    result = linkLastPart;
                }
            }
            else
            {
                bool isPersonalPage = await IsPersonalPage(communityPage);

                if (isPersonalPage)
                {
                    //создать пользователя
                }
            }

            return result;
        }

        private async Task<bool> IsPersonalPage(Page page)
        {
            ElementHandle pageFollowersSearchElement = await page.QuerySelectorAsync("#profile.profile_content");

            return pageFollowersSearchElement != null;
        }
    }
}

