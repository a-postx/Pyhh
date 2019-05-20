using System;
using System.Collections.Generic;
using System.Text;
using VkNet.Model;

namespace Pyhh.VkApi
{
    public enum HaveRecentPosts
    {
        Unknown = 0,
        No = 1,
        Yes = 2
    }

    public enum Genders
    {
        Unknown = 0,
        Male = 1,
        Female = 2,
        Mixed = 4
    }

    public class VkApiUser
    {
        public VkApiUser()
        {

        }

        public long Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string ScreenName { get; set; }
        public Genders Gender { get; set; }
        public int WallPostsCount { get; set; }
        public int PercentReposts { get; set; }
        public long? FollowersCount { get; set; }
        public bool? IsClosed { get; set; }
        public bool? IsDeactivated { get; set; }
        public string PageLink { get; set; }
        public HaveRecentPosts HaveRecentPosts { get; set; }
        public string WorkingPlaceGroupId { get; set; }
        public string WorkingPlaceGroupDescription { get; set; }
    }
}
