using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pyhh.Browsing;
using Pyhh.VkApi;

namespace Pyhh.ExpertSearcher
{
    class Program
    {
        internal static string VkAppId { private get; set; }
        internal static string VkUserApiKey { private get; set; }
        internal static string VkServiceApiKey { private get; set; }
        internal static bool Mobile { private get; set; } = false;
        internal static string Groups { private get; set; }
        internal static string Users { private get; set; }

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Action action = Arguments.ProcessCommandLine(args);

            switch (action)
            {
                case Action.GetPotentialExperts:
                    await GetExpertsAsync();
                    break;
                case Action.Help:
                default:
                    Usage();
                    break;
            }
        }

        private static async Task GetExpertsAsync()
        {
            Console.WriteLine(DateTime.Now + " Discovering...");

            ulong.TryParse(VkAppId, out ulong id);

            if (id == 0)
            {
                Console.WriteLine(DateTime.Now + " Error getting VK application ID, please supply a numerical value of the application registered in VK.");
                return;
            }

            VkontakteApi vkontakteApi = new VkontakteApi(id, VkUserApiKey, VkServiceApiKey);
            await vkontakteApi.DiscoverAsync();

            VkontakteBrw vkontakteBrw = new VkontakteBrw(Mobile);
            await vkontakteBrw.DiscoverAsync();

            if (vkontakteApi.DiscoveringCompleted.IsSet && vkontakteBrw.DiscoveringCompleted.IsSet)
            {
                Console.WriteLine(DateTime.Now + " Discovering completed.");

                if (!string.IsNullOrEmpty(Groups))
                {
                    Console.WriteLine(DateTime.Now + " Collecting community data...");
                    List<VkApiUser> potentialExperts = await GetPotentialExpertsFromGroups(vkontakteApi, vkontakteBrw, Groups.Split(','));
                    Console.WriteLine(DateTime.Now + " Community data collected.");
                    Console.WriteLine(DateTime.Now + " Potential experts found: " + potentialExperts.Count);
                    Console.WriteLine(DateTime.Now + " Collected profiles: " + vkontakteBrw.CollectedProfiles);
                    Console.WriteLine(DateTime.Now + " Non-public profiles: " + vkontakteBrw.NonPublicProfiles);
                    Console.WriteLine(DateTime.Now + " Non-existing profiles: " + vkontakteBrw.NonExistingProfiles);
                    Console.WriteLine(DateTime.Now + " Empty wall profiles: " + vkontakteBrw.EmptyWallProfiles);

                    if (potentialExperts.Count > 0)
                    {
                        DataTable expertsTable = potentialExperts.ToDataTable(Groups);
                        List<DataTable> reportData = new List<DataTable>();
                        reportData.Add(expertsTable);

                        string exportPath = Path.GetDirectoryName(Environment.CurrentDirectory);

                        ExpertReport expertReport = new ExpertReport("ПотенциальныеЭксперты", exportPath, reportData);
                        expertReport.ExportToExcel();
                    }                    
                }
                else if (!string.IsNullOrEmpty(Users))
                {
                    throw new NotImplementedException();
                }

                Console.WriteLine(DateTime.Now + " Finished.");
            }
            else
            {
                Console.WriteLine(DateTime.Now + " Cannot complete online services discovering, please check your Internet connection.");
            }
        }

        public static async Task<List<VkApiUser>> GetPotentialExpertsFromGroups(VkontakteApi vkApi, VkontakteBrw vkBrw, string[] groups)
        {
            List<VkApiUser> result = new List<VkApiUser>();

            int recentMonths = 3;

            ////bool tr = false;
            ////while(!tr)
            ////{
            ////    foreach (string group in groups)
            ////    {
            ////        VkApiCommunity community = await vkApi.GetCommunity(group);

            ////        if (community == null)
            ////        {
            ////            Console.WriteLine(DateTime.Now + " Cannot get community details for " + group + ".");
            ////        }
            ////        else
            ////        {
            ////            Console.WriteLine(DateTime.Now + " Members for group " + group + " collected.");
            ////        }
            ////    }
            ////}

            foreach (string group in groups)
            {
                VkApiCommunity community = await vkApi.GetCommunityAsync(group);

                if (community == null)
                {
                    Console.WriteLine(DateTime.Now + " Cannot get community details for " + group + ".");
                    return result;
                }

                //huge are not tested
                if (community.Users.Count > 500000)
                {
                    Console.WriteLine(DateTime.Now + " Cannot get results for community " + group + ": too many members.");
                    return result;
                }

                Console.WriteLine(DateTime.Now + " Community: " + community.ScreenName + ". Members count: " + community.UserIds.Count);

                //create BrwCommunity

                List<VkBrwUser> communityUsers = new List<VkBrwUser>();

                community.Users.Where(u => u.IsDeactivated == false && u.IsClosed == false).ToList()
                    .ForEach(user =>
                {
                    VkBrwUser vkBrwUser = new VkBrwUser
                    {
                        ProfileLink = user.PageLink,
                        UserId = user.Id,
                        PageName = user.ScreenName,
                        Vkontakte = vkBrw
                    };

                    vkBrw.Users.Add(vkBrwUser);

                    communityUsers.Add(vkBrwUser);
                });

                Console.WriteLine(DateTime.Now + " Community: " + community.ScreenName + ". Open members count: " + communityUsers.Count);
                Console.WriteLine(DateTime.Now + " Getting wall posts from open members...");

                List<VkBrwUserWallPost> wallPosts = await vkBrw.GetWallRecords(communityUsers);
                
                int expectedRepostsPercent = Mobile ? 40 : 50;

                foreach (VkBrwUser user in vkBrw.Users)
                {
                    List<VkBrwUserWallPost> recentWallPosts = user.WallPosts.Where(p => p.Date > DateTime.Now.AddMonths(-recentMonths) && p.Text?.Length > 30).ToList();

                    if (recentWallPosts.Count > 0)
                    {
                        int repostsCount = recentWallPosts.Count(p => p.Repost == true);
                        int ownPostsCount = recentWallPosts.Count(p => p.Repost == false);
                        int repostsPercent = (repostsCount * 100) / (repostsCount + ownPostsCount);

                        if (ownPostsCount >= recentMonths && repostsPercent <= expectedRepostsPercent)
                        {
                            VkApiUser correspondingUser = community.Users.FirstOrDefault(u => u.Id == user.UserId);

                            if (correspondingUser != null)
                            {
                                correspondingUser.WallPostsCount = recentWallPosts.Count;
                                correspondingUser.PercentReposts = repostsPercent;
                                correspondingUser.HaveRecentPosts = HaveRecentPosts.Yes;

                                if (!string.IsNullOrEmpty(correspondingUser.WorkingPlaceGroupId))
                                {
                                    string commId = correspondingUser.WorkingPlaceGroupId.Split("/").Last();
                                    VkApiCommunity commDetails = await vkApi.DiscoverCommunityAsync(commId);

                                    if (commDetails != null)
                                    {
                                        correspondingUser.WorkingPlaceGroupDescription = commDetails.Name;
                                    }
                                }

                                if (result.FirstOrDefault(u => u.Id == correspondingUser.Id) == null)
                                {
                                    result.Add(correspondingUser);
                                }
                            }
                        }
                    }
                }
            }            

            return result;
        }

        internal static void Usage()
        {
            Console.WriteLine("Параметры командной строки:");
            Console.WriteLine(string.Empty);
            Console.WriteLine("  -H[elp] | -?             Показать помощь.");
            Console.WriteLine("  -VKAPPID <id>            ИД приложения Вконтакте (веб-сервис).");
            Console.WriteLine("  -VKUSERAPIKEY <key>      Пользовательский АПИ-ключ Вконтакте.");
            Console.WriteLine("  -VKSERVICEAPIKEY <key>   Сервисный АПИ-ключ Вконтакте.");
            Console.WriteLine("  -GROUPS <groupIds>       Список групп, разделённых запятыми (айди или названия), из которых необходимо получить информацию.");
            Console.WriteLine("  -GETPOTENTIALEXPERTS     Получить список потенциальных экспертов из указанных групп.");
            Console.WriteLine("  -MOBILE                  Использовать мобильную версию ВК (результат быстрее, но он нестабилен).");
            Console.WriteLine(string.Empty);
            Console.WriteLine(Environment.NewLine);
        }
    }
}
