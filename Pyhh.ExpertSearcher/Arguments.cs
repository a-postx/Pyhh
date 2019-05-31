using System;
using System.Collections.Generic;
using System.Text;

namespace Pyhh.ExpertSearcher
{
    internal enum Action
    {
        Invalid,
        Help,
        GetPotentialExperts
    }

    public class Arguments
    {
        internal static Action ProcessCommandLine(string[] args)
        {
            Action result = Action.Invalid;

            if (args.Length == 0)
                result = Action.Help;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i][0] == '/' || args[i][0] == '-')
                {
                    string command = args[i].Substring(1);

                    switch (command.ToUpper())
                    {
                        case "VKAPPID":
                            Program.VkAppId = args[i + 1];
                            i++;
                            break;

                        case "VKUSERAPIKEY":
                            Program.VkUserApiKey = args[i + 1];
                            i++;
                            break;

                        case "VKSERVICEAPIKEY":
                            Program.VkServiceApiKey = args[i + 1];
                            i++;
                            break;

                        case "GROUPS":
                            Program.Groups = args[i + 1];
                            i++;
                            break;

                        case "USERS":
                            Program.Users = args[i + 1];
                            i++;
                            break;

                        case "GETPOTENTIALEXPERTS":
                            result = Action.GetPotentialExperts;
                            break;

                        case "MOBILE":
                            Program.Mobile = true;
                            break;

                        case "HELP":
                        case "?":
                        default:
                            result = Action.Help;
                            break;
                    }
                }
                else
                {
                    result = Action.Help;
                    break;
                }
            }

            return result;
        }
    }
}
