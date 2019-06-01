using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Model;
using VkNet.Model.RequestParams;
using VkNet.Utils;

namespace Pyhh.VkApi
{
    public class VkApiCommunity
    {
        public VkApiCommunity(VkontakteApi vk, Group group)
        {
            Vk = vk;
            Group = group;
            Id = group.Id;
            Name = group.Name;
            ScreenName = group.ScreenName;

        }

        private Group Group { get; }
        private VkontakteApi Vk { get; }

        public List<long> UserIds { get; set; } = new List<long>();
        public List<VkApiUser> Users { get; set; } = new List<VkApiUser>();
        public long Id { get; }
        public string Name { get; }
        public string ScreenName { get; }

        public async Task GetDetails()
        {
            await GetMemberIdsAsync();
            await GetMemberDetailsAsync();
        }

        private async Task<List<VkApiUser>> GetMemberDetailsAsync()
        {
            List<VkApiUser> result = new List<VkApiUser>();
            
            if (UserIds.Count > 0)
            {
                List<User> usersReceived = await Vk.GetUsersCareerAsync(UserIds);

                if (usersReceived != null)
                {
                    if (usersReceived.Count > 0)
                    {
                        foreach (User user in usersReceived)
                        {
                            VkApiUser vkUser = new VkApiUser
                            {
                                Id = user.Id,
                                PageLink = "https://vk.com/id" + user.Id,
                                IsClosed = user.IsClosed,
                                IsDeactivated = user.IsDeactivated,
                                FirstName = user.FirstName,
                                LastName = user.LastName,
                                ScreenName = user.ScreenName,
                                Gender = (user.Sex == VkNet.Enums.Sex.Male) ? Genders.Male
                                    : (user.Sex == VkNet.Enums.Sex.Female) ? Genders.Female : Genders.Unknown,
                                FollowersCount = user.FollowersCount
                            };

                            if (user.Career.Count > 0)
                            {
                                List<Career> careersWithGroup = user.Career.Where(dp => dp.GroupId != null).ToList();

                                if (careersWithGroup.Count > 0)
                                {
                                    List<Career> sortedCareers = careersWithGroup.OrderByDescending(s => s.GroupId).ToList();
                                    vkUser.WorkingPlaceGroupId = "https://vk.com/club" + sortedCareers[0].GroupId;
                                }
                            }

                            Users.Add(vkUser);
                            Vk.Users.Add(vkUser);

                            result.Add(vkUser);
                        }
                    }
                }
            }

            return result;
        }

        public async Task<List<long>> GetMemberIdsAsync()
        {
            List<long> result = new List<long>();

            var groupId = Group.Id;

            long offset = 0;
            bool membersIdsFinished = false;
            long maxCommunityMembersBatch = Options.VkMaxExecuteApiCallResults;
            int retryCounter = 0;
            int maxRetries = 2;

            while (!membersIdsFinished)
            {
                List<long> membersBatch = null;

                try
                {
                    VkParameters parameters = new VkParameters {{"community", groupId}, {"offset", offset}};

                    membersBatch = await Vk.UserApi.Execute.StoredProcedureAsync<List<long>>("GetCommunityUserIds", parameters);
                }
                catch (JsonSerializationException e)
                {
                    Console.WriteLine("Error deserializing user ids for community " + groupId + ": " + e);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error getting user ids for community " + groupId + ": " + e);
                }

                if (membersBatch != null)
                {
                    if (membersBatch.Count > 0)
                    {
                        result.AddRange(membersBatch);
                        offset = result.Count;
                    }

                    if (membersBatch.Count < maxCommunityMembersBatch)
                    {
                        membersIdsFinished = true;
                    }
                }
                else
                {
                    if (retryCounter == maxRetries)
                    {
                        break;
                    }
                    
                    await Task.Delay(new TimeSpan(0,1,0));

                    retryCounter = retryCounter + 1;
                }
            }

            UserIds = result;

            return result;
        }

        public async Task<List<User>> GetMembersAsync()
        {
            List<User> result = new List<User>();

            var groupId = Group.Id;

            GroupsGetMembersParams groupMembersCountParams = new GroupsGetMembersParams
            {
                GroupId = groupId.ToString(),
                Fields = UsersFields.Status | UsersFields.Counters
                                            | UsersFields.Sex | UsersFields.City | UsersFields.Country
                                            | UsersFields.BirthDate | UsersFields.PhotoMaxOrig | UsersFields.Site
            };

            long communityOffset = 0;
            bool communityMembersFinished = false;
            long maxCommunityMembersBatch = 1000;

            while (!communityMembersFinished)
            {
                GroupsGetMembersParams communityMembersParams = new GroupsGetMembersParams
                {
                    GroupId = groupId.ToString(),
                    Fields = UsersFields.Counters,
                    Offset = communityOffset,
                    Sort = GroupsSort.IdAsc,
                    Count = maxCommunityMembersBatch
                };

                List<User> membersBatch;

                try
                {
                    VkCollection<User> vkResult = await Vk.ServiceApi.Groups.GetMembersAsync(communityMembersParams);
                    membersBatch = vkResult.ToList();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error getting members for community " + groupId + ": " + e);
                    throw;
                }

                if (membersBatch.Count > 0)
                {
                    result.AddRange(membersBatch);
                    communityOffset = result.Count;
                }

                if (membersBatch.Count < maxCommunityMembersBatch)
                {
                    communityMembersFinished = true;
                }
            }

            return result;
        }
    }
}
