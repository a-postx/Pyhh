using System;
using System.Collections.Generic;
using System.Text;

namespace Pyhh.Browsing
{
    public class VkBrwUserWallPost
    {
        public VkBrwUser User { get; set; }
        public DateTime? Date { get; set; }
        public string Text { get; set; }
        public bool Repost { get; set; }
    }
}
