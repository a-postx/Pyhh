using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VkNet.Enums;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Exception;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Model.RequestParams;
using VkNet.Utils;
using VkNet.Utils.JsonConverter;

namespace Pyhh.VkApi
{
    public class VkontakteApi
    {
        public VkontakteApi(ulong appId, string userApiKey, string serviceApiKey)
        {
            AppId = appId;
            ServiceApiKey = serviceApiKey;
            UserAccessFlowApiKey = userApiKey;

            Init();
        }

        private ulong AppId { get; }
        private string ServiceApiKey { get; }
        private string UserAccessFlowApiKey { get; }

        public VkNet.VkApi UserApi { get; set; }
        public VkNet.VkApi ServiceApi { get; set; }
        public string ApiVersion { get; set; }

        public List<VkApiCommunity> Communities { get; set; } = new List<VkApiCommunity>();
        public List<VkApiUser> Users { get; set; } = new List<VkApiUser>();

        public ManualResetEventSlim DiscoveringCompleted { get; } = new ManualResetEventSlim(false);

        private void Init()
        {
            
        }

        public Task DiscoverAsync()
        {
            return DiscoverSystemAsync();
        }

        private async Task DiscoverSystemAsync()
        {
            bool success = await GetVkontakteApiAsync();

            if (success)
            {
                DiscoveringCompleted.Set();
            }
        }

        private async Task<bool> GetVkontakteApiAsync()
        {
            bool result = false;

            if (!string.IsNullOrEmpty(UserAccessFlowApiKey) && !string.IsNullOrEmpty(ServiceApiKey))
            {
                var userApi = new VkNet.VkApi();

                try
                {
                    await userApi.AuthorizeAsync(new ApiAuthParams
                    {
                        AccessToken = UserAccessFlowApiKey,
                        Settings = Settings.All | Settings.Offline
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error authorizing vkontakte api via user access flow key: " + e);
                }

                if (userApi.IsAuthorized)
                {
                    UserApi = userApi;
                    UserApi.SetLanguage(Language.Ru);
                }

                var serviceApi = new VkNet.VkApi();

                try
                {
                    await serviceApi.AuthorizeAsync(new ApiAuthParams
                    {
                        AccessToken = ServiceApiKey,
                        ApplicationId = AppId,
                        Settings = Settings.All | Settings.Offline
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error authorizing vkontakte api via service key: " + e);
                }

                if (serviceApi.IsAuthorized)
                {
                    ServiceApi = serviceApi;
                    ServiceApi.SetLanguage(Language.Ru);
                }

                if (UserApi != null && ServiceApi != null)
                {
                    ApiVersion = userApi.VkApiVersion.Version;
                    result = true;
                }
            }
            else
            {
                Console.WriteLine("Error connecting to Vkontakte: no access keys available.");
            }

            return result;
        }

        public async Task<VkApiCommunity> DiscoverCommunity(string groupId)
        {
            VkApiCommunity result = null;

            try
            {
                //"kate_kul" error 100
                var groupInfo = await UserApi.Groups.GetByIdAsync(null, groupId, GroupsFields.All);

                if (groupInfo.Count == 1)
                {
                    VkApiCommunity community = new VkApiCommunity(this, groupInfo[0]);

                    if (Communities.FirstOrDefault(c => c.Id == community.Id) == null)
                    {
                        Communities.Add(community);
                    }

                    result = community;
                }
            }
            catch (ParameterMissingOrInvalidException)
            {
                Console.WriteLine("Error discovering community " + groupId + ": community not found.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error discovering community " + groupId + ": " + e);
            }

            return result;
        }

        public async Task<List<VkApiCommunity>> DiscoverCommunities(List<string> communityIds)
        {
            List<VkApiCommunity> result = new List<VkApiCommunity>();

            foreach (string id in communityIds)
            {
                VkApiCommunity community = await DiscoverCommunity(id);

                if (community != null)
                {
                    result.Add(community);
                }
            }

            return result;
        }

        public async Task<List<User>> GetUsersCareerAsync(List<long> userIds)
        {
            List<User> result = new List<User>();

            int maxUsersBatch = Options.VkMaxApiResultsCount;

            IEnumerable<List<long>> idBatches = userIds.SplitList(maxUsersBatch);

            ProfileFields profileFields = ProfileFields.FirstName | ProfileFields.LastName | ProfileFields.Sex
                                          | ProfileFields.ScreenName | ProfileFields.PhotoMaxOrig | ProfileFields.Career
                                          | ProfileFields.FollowersCount;

            foreach (List<long> batch in idBatches)
            {
                List<User> liveUsers = null;

                try
                {
                    ReadOnlyCollection<User> requestResult = await ServiceApi.Users.GetAsync(batch, profileFields);
                    liveUsers = requestResult?.ToList();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error getting users career: " + e);
                }

                if (liveUsers != null)
                {
                    if (liveUsers.Count > 0)
                    {
                        result.AddRange(liveUsers);
                    }
                }
            }

            return result;
        }

        public async Task<List<Post>> GetUserWallPostsAsync(User user)
        {
            List<Post> result = new List<Post>();

            ulong maxPostsBatch = 100;
            ulong postsOffset = 0;
            bool postsFinished = false;
            int borderTimestamp = (int)DateTime.UtcNow.AddMonths(-3).ToUnixTimestamp();
            long targetUserId = user.Id;

            while (!postsFinished)
            {
                VkResponse wallPostsResponse = null;

                try
                {
                    var parametr = new VkParameters
                    {
                        {"user", targetUserId}, {"offset", postsOffset}, {"deadline", borderTimestamp}
                    };

                    var yyy = await UserApi.Execute.StoredProcedureAsync<JObject>("GetUserWall", parametr);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error getting wall posts for user " + targetUserId + ": " + e);
                }

                if (!string.IsNullOrEmpty(wallPostsResponse?.RawJson))
                {
                    VkCollection<Post> postCollection = wallPostsResponse.ToVkCollectionOf<Post>(p => p);

                    if (postCollection.Count > 0)
                    {
                        result.AddRange(postCollection);

                        if (postCollection.Count < (int)maxPostsBatch)
                        {
                            postsFinished = true;
                        }
                        else
                        {
                            postsOffset = (ulong)result.Count;
                        }
                    }
                    else
                    {
                        postsFinished = true;
                    }
                }
                else
                {
                    postsFinished = true;
                }
            }

            return result;
        }

        public async Task<List<Post>> GetUserWallPostsViaWallGetAsync(User user)
        {
            List<Post> result = new List<Post>();

            ulong maxPostsBatch = 100;
            ulong postsOffset = 0;
            bool postsFinished = false;
            // community with dash
            //long targetGroup = -83415396;
            // user
            long targetObject = user.Id;

            while (!postsFinished)
            {
                WallGetParams wallGetParams = new WallGetParams
                {
                    OwnerId = targetObject,
                    //wallGetParams.Fields = GroupsFields.All;
                    Filter = WallFilter.Owner,
                    Offset = postsOffset,
                    Extended = false,
                    Count = maxPostsBatch
                };

                WallGetObject requestResult = new WallGetObject();

                try
                {
                    // ограничение 5000 запросов в день
                    requestResult = await ServiceApi.Wall.GetAsync(wallGetParams);
                }
                catch (CannotBlacklistYourselfException e)
                {
                    //10361 - неизвестная проблема, пользователь закрыл все посты
                    Console.WriteLine("Error getting wall posts via VK API: " + e);
                }
                catch (NullReferenceException e)
                {
                    //336185 - неизвестная проблема, Audioplaylist
                    Console.WriteLine("Error getting wall posts via VK API: " + e);
                }
                catch (RateLimitReachedException e)
                {
                    Console.WriteLine("Error getting wall posts via VK API: " + e);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error getting wall posts via VK API: " + e);
                }

                if (requestResult.WallPosts != null)
                {
                    result.AddRange(requestResult.WallPosts);

                    if (requestResult.WallPosts.Count < (int)maxPostsBatch)
                    {
                        postsFinished = true;
                    }
                    else
                    {
                        postsOffset = (ulong)result.Count;
                    }
                }
                else
                {
                    postsFinished = true;
                }
            }

            return result;
        }

        public async Task<VkApiCommunity> GetCommunity(string commId)
        {
            VkApiCommunity result = null;

            VkApiCommunity community = await DiscoverCommunity(commId);

            if (community != null)
            {
                await community.GetDetails();

                if (community.Users.Count > 0)
                {
                    result = community;
                }
            }

            return result;
        }

        ////public async Task SearchForPotentialExpert()
        ////{
        ////    ////var ggg = new int[1];
        ////    ////ggg.SetValue(1,0);
        ////    ////ggg.Initialize();
        ////    ////IReadOnlyCollection<MutualFriend> mmm = api.Friends.GetMutualAsync(new FriendsGetMutualParams { SourceUid = 67007296, TargetUid = 44517649 }).GetAwaiter().GetResult();
        ////    ////var mmm = api.Search.GetHints(new SearchGetHintsParams{ Query = "в шоколаде", Limit = 200, Filters = SearchFilter.Groups});
        ////    ////var searchResult = api.Wall.Search(new WallSearchParams{OwnerId = -105086185, Count = 100, Query = "какой мотороллер залезет на дерево"});

        ////    //нет прав
        ////    ////var kkk = api.Stats.Get(new StatsGetParams { GroupId = 105086185 });

        ////    List<string> comms = new List<string>();
        ////    comms.Add("hudeem_v_shokolade");

        ////    VkApiCommunity community = await DiscoverCommunity("hudeem_v_shokolade");

        ////    List<VkApiUser> vkUsers = new List<VkApiUser>();

        ////    if (community != null)
        ////    {
        ////        List<User> communityMembers = await community.GetMembersAsync();

        ////        if (communityMembers.Count > 0)
        ////        {
        ////            List<User> openFemaleProfiles = communityMembers.Where(u => u.Sex == Sex.Female && u.IsClosed == false).ToList();

        ////            List<User> usersWithCareer = await GetUsersCareerAsync(openFemaleProfiles);

        ////            foreach (User person in openFemaleProfiles)
        ////            {
        ////                VkApiUser expert = new VkApiUser();
        ////                expert.Id = person.Id;
        ////                expert.FirstName = person.FirstName;
        ////                expert.LastName = person.LastName;
        ////                expert.ScreenName = person.ScreenName;
        ////                expert.PhotoUrl = person.PhotoMaxOrig;
        ////                expert.PageLink = "https://vk.com/id" + expert.Id;

        ////                User userWithCareer = usersWithCareer.FirstOrDefault(u => u.Id == expert.Id);

        ////                //4036463,4745556 - работа не получается

        ////                if (userWithCareer != null)
        ////                {
        ////                    if (userWithCareer.Career.Count > 0)
        ////                    {
        ////                        List<Career> careersWithGroup = userWithCareer.Career.Where(dp => dp.GroupId != null).ToList();

        ////                        if (careersWithGroup.Count > 0)
        ////                        {
        ////                            List<Career> sortedCareers = careersWithGroup.OrderByDescending(s => s.GroupId).ToList();
        ////                            expert.WorkingPlaceGroupId = "https://vk.com/club" + sortedCareers[0].GroupId;
        ////                        }
        ////                    }
        ////                }

        ////                List<Post> allWallPosts = await GetUserWallPostsAsync(person);

        ////                if (allWallPosts.Count > 0)
        ////                {
        ////                    DateTime borderDate = DateTime.UtcNow.AddMonths(-3);

        ////                    List<Post> recentNonEmptyPosts = allWallPosts.Where(p => p.Date > borderDate && p.FromId == person.Id && p.Text.Length > 10).ToList();

        ////                    if (recentNonEmptyPosts.Count > 0)
        ////                    {
        ////                        expert.HaveRecentPosts = HaveRecentPosts.Yes;

        ////                        List<Post> recentWallPosts = new List<Post>();

        ////                        recentWallPosts.AddRange(recentNonEmptyPosts);

        ////                        int repostsCount = recentWallPosts.Where(p => p.CopyHistory.Count > 0).ToList().Count;
        ////                        expert.PercentReposts = (repostsCount * 100) / recentWallPosts.Count;
        ////                    }
        ////                }

        ////                vkUsers.Add(expert);
        ////            }
        ////        }
        ////    }

        ////    var activeUsers = vkUsers.Where(u => u.HaveRecentPosts == HaveRecentPosts.Yes && u.PercentReposts < 50).OrderBy(u => u.PercentReposts).ToList();
        ////    var potentialExperts = vkUsers.Where(u => u.HaveRecentPosts == HaveRecentPosts.Yes && u.PercentReposts < 50 && u.WorkingPlaceGroupId != null).OrderBy(u => u.PercentReposts).ToList();

        ////    foreach (var expert in potentialExperts)
        ////    {
        ////        Console.WriteLine(expert.PageLink);
        ////    }
        ////}
    }
}
