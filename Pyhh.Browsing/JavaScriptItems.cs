using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Pyhh.Browsing
{
    internal class JavaScriptItems
    {
        internal JavaScriptItems()
        {
            _resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();

            GetJsScripts();
        }

        internal string GetUserWallPosts { get; set; }

        private readonly string[] _resources;

        private void GetJsScripts()
        {
            using (Stream stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(_resources.Single(str => str.EndsWith("GetUserWallPosts.js"))))
            using (StreamReader reader = new StreamReader(stream))
            {
                string getUserWallPosts = reader.ReadToEnd();

                if (!string.IsNullOrEmpty(getUserWallPosts))
                {
                    GetUserWallPosts = getUserWallPosts;
                }
            }
        }
    }
}
