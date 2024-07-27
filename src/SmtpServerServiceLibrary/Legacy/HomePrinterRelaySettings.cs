using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SmtpServerServiceLibrary.Legacy
{
    public class HomePrinterRelaySettings
    {
        public string FilePath { get; set; }
        public int SmtpPort { get; set; } = 25;

        public static HomePrinterRelaySettings LoadSettings(string[] args)
        {
            // Load settings from file
            return new HomePrinterRelaySettings()
            {
                FilePath = GetSetting(args, "OutputPath"),
                SmtpPort = GetSettingInt(args, "SmtpPort", 25)
            };
        }

        private static int GetSettingInt(string[] args, string settingName, int defaultValue = 0)
        {
            string str = GetSetting(args, settingName, defaultValue.ToString());
            if(!int.TryParse(str, out int val))
            {
                return defaultValue;
            }
            return val;
        }

        private static string GetSetting(string[] args, string settingName, string defaultValue = null)
        {
            // Step 1: Get setting from args
            var argListParamName = "--" + settingName;
            for(int i=0; i<args.Length-1; i++)
            {
                if (string.Compare(args[i], argListParamName, true) == 0)
                {
                    return args[i + 1];
                }
            }

            // step 2:  Look for setting in environment variables
            var envVarName = "SMTP_" + settingName.ToUpper();
            var envVarValue = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrEmpty(envVarValue))
            {
                return envVarValue;
            }

            // Step 3: Return default
            return defaultValue;
        }
    }
}
