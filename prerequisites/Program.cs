using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Xml.XPath;

class Program
{
    // Access the embedded resource stream
    static string logDirectory = @"C:\TI_Audio\Audio\Logs";
    static string logFilePath = Path.Combine(logDirectory, $"Prechecker_{DateTime.Now.ToString("yyyyMMdd HHmmss")}.txt");

    static int Main()
    {   // Check if the operating system is Windows
        log("Verifying Enviornment");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            log("Enviornment Supported");
            log("Initiating WMI query");

            // Function call to retrieve system information
            var System_Manufacturer = getinformation("SELECT * FROM Win32_ComputerSystem", new string[] { "Manufacturer" });
            log("Hardware:" + System_Manufacturer["Manufacturer"]);

            var Sytsem_Processor = getinformation("SELECT * FROM Win32_Processor", new string[] { "Manufacturer", "Name" });
            log("Processor:" + Sytsem_Processor["Manufacturer"] + "," + Sytsem_Processor["Name"]);

            var System_OS = getinformation("SELECT * FROM Win32_OperatingSystem", new string[] { "Caption", "BuildNumber", "SystemDrive", "OSArchitecture" });
            log("Operating System:" + System_OS["Caption"]);
            log("Build:" + System_OS["BuildNumber"]);
            log("Architecture:" + System_OS["OSArchitecture"]);
            log("Drive:" + System_OS["SystemDrive"]);

            var System_Space = getinformation($"SELECT * FROM Win32_LogicalDisk WHERE DeviceID = '{System_OS["SystemDrive"]}'", new string[] { "FreeSpace" });
            log("FreeSpace:" + double.Parse(System_Space["FreeSpace"]) / 1048576 + " " + "MB");

            var Sound_Hardware = getinformation("SELECT * FROM Win32_SoundDevice", new string[] { "PNPDeviceID" });
            log("HardwareID:" + Sound_Hardware["PNPDeviceID"]);

            

            //Declaring Variables for Evaluation
            String OS_Name = System_OS["Caption"].Replace(" ", "").ToUpper().Contains("WINDOWS11") ? "Windows 11" : System_OS["Caption"];
            int Build = int.Parse(System_OS["BuildNumber"]) >= 22621? 22621 : int.Parse(System_OS["BuildNumber"]);

            String Architecture = System_OS["OSArchitecture"] == "64-bit" ? "x64" : System_OS["OSArchitecture"];
            String Space = double.Parse(System_Space["FreeSpace"]) / 1048576 >= 2048 ? "2048" : System_Space["FreeSpace"];
            string Family = findProcessorFamily(Sytsem_Processor["Name"]);
            //string HardwareId = findHardwareId(Sound_Hardware["PNPDeviceID"]) == "Found" ? Sound_Hardware["PNPDeviceID"] : "Not Found";

            //Evaluating the System Infomation with Pre-requisite XML
            int evaluationcode = 11708;
            if (Sytsem_Processor["Manufacturer"].ToUpper().Contains("INTEL"))
            {
                evaluationcode=CheckingCompatibility_Intel(Family, OS_Name, Build.ToString(), Architecture, Space);
                log("Evaluation Completed, Exit:" +evaluationcode);
                return evaluationcode;
            }
            else if (Sytsem_Processor["Manufacturer"].ToUpper().Contains("AMD"))
            {
                evaluationcode= CheckingCompatibility_AMD(OS_Name, Build.ToString(), Architecture, Space);
                log("Evaluation Completed, Exit:" +evaluationcode);
                return evaluationcode;
            }
            else
            {
                log("Un-Supported Processor Manufacturer Exit: 11701");
                return evaluationcode;
            }

        }
        log("Enviorment Not Supported");
        return 11702;
    }
    // Retrieve information from WMI using provided query and property names
    static Dictionary<string, String> getinformation(String query, string[] propertyNames)
    {
        
        ManagementObjectCollection queryCollection1 = new ManagementObjectSearcher(query).Get();       
        Dictionary<string, String> inputDictionary = new Dictionary<string, string>();
      

        if (query== "SELECT * FROM Win32_SoundDevice")
        {
            if (!(queryCollection1.Count > 0))
            {
                inputDictionary["PNPDeviceID"] = "Not Found";  
               return inputDictionary;

            }


        }
         foreach (ManagementObject information in queryCollection1)
        {
            foreach (string propertyName in propertyNames)
            {
                inputDictionary[propertyName] = information[propertyName].ToString();
                if (information[propertyName]==null)
                {
                    inputDictionary[propertyName] = "Not Found";
                }
            }
        }
       
        return inputDictionary;
    }

    //Find Processor Family
    static string findProcessorFamily(String ProcessorName)
    {
        //Load the XML file
        XDocument xdoc = LoadXmlFile("prerequisites.Compatible_System.xml");
        String[] FamilyNames = new String[] { "ADL", "RPL", "MTL", "TestRVP" };
       
        foreach (String FamilyName in FamilyNames)
            {
                if (xdoc.Descendants(FamilyName).Elements("name").Any(e => ProcessorName.Replace(" ", "").IndexOf(e.Value.Trim().Replace(" ", ""), StringComparison.OrdinalIgnoreCase) >= 0))
                    return FamilyName;
            }
        
        // Return unknown if no match is found
        return "unknown";
    }
    //find the sound hardware id
    static String findHardwareId(String PNPDeviceID)
    {
        //Load the XML file
        XDocument xdoc = LoadXmlFile("prerequisites.Compatible_System.xml");
        if (PNPDeviceID != null)
        {
           
            if (xdoc.Descendants("HardwareID").Elements("Id").Any(e => PNPDeviceID.IndexOf(e.Value, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "Found";
            }
            
        }        
           
            return "NotFound";
       
    }


   static int CheckingCompatibility_Intel( string Family, String OS, String Build, String Architecture, String Space)
        {
        //Load the XML file
        XDocument xdoc = LoadXmlFile("prerequisites.Compatible_System.xml");
        if (Family != "unknown")
        {
            log("Processor Family:" + Family);
            var elements = xdoc.XPathSelectElements($"/prechecker/Intel_Compatibility/{Family}[Name='{OS}' and Build='{Build}' and Architecture='{Architecture}' and Space='{Space}']");
            if (elements != null && elements.Any())
                //if ((xdoc.XPathSelectElements($"/prechecker/Intel_Compatibility/{Family}[Name='{OS}' and Build='{Build}' and Architecture='{Architecture}' and Space='{Space}']") != null))
            {
               
                string returnCode = xdoc.XPathSelectElement($"/prechecker/Intel_Compatibility/{Family}[Name='{OS}' and Build='{Build}' and Architecture='{Architecture}' and Space='{Space}']/ReturnCode")?.Value;
                //string returnCode = matchingNodes.First().Element("ReturnCode").Value;
                log("The Sytem is Compatible. Exit:" + returnCode);
                 return int.Parse(returnCode);
            }
            else
            {
               
                log($"{(OS != "Windows 11" ? "OS Not Supported\n" : "")}" +
                  $"{(Build != "22621" ? "Build Not Supported\n" : "")}" +
                  $"{(Architecture != "x64" ? "Architecture Not Supported\n" : "")}" +
                  $"{(Space != "2048" ? "Space Not Supported\n" : (OS != "Windows 11" || Build != "22621" || Architecture != "x64" || Family == "unknown" ? "The System is Not Compatible. Exit: 11708" : ""))}");

                return 11708;
            }
        }
        else
        {
            log("The Processor Family is Not supported Exit 11703");
            log($"{(OS != "Windows 11" ? "OS Not Supported\n" : "")}" +
              $"{(Build != "22621" ? "Build Not Supported\n" : "")}" +
              $"{(Architecture != "x64" ? "Architecture Not Supported\n" : "")}" +
              $"{(Space != "2048" ? "Space Not Supported\n" : (OS != "Windows 11" || Build != "22621" || Architecture != "x64"|| Family == "unknown" ? "The System is Not Compatible. Exit: 11708" : ""))}");
             
            return 11708;
        }

        }
    static int CheckingCompatibility_AMD(String OS, String Build, String Architecture, String Space)
            {
        //Load the XML file
        XDocument xdoc = LoadXmlFile("prerequisites.Compatible_System.xml");
        var elements = xdoc.XPathSelectElements($"/prechecker/AMD_Compatibility/Compatible_System[contains(Name, '{OS}')' and Build='{Build}' and Architecture='{Architecture}' and Space='{Space}']");
        if (elements != null && elements.Any())
            //if ((xdoc.XPathSelectElements($"/prechecker/AMD_Compatibility/Compatible_System[contains(Name, '{OS}')' and Build='{Build}' and Architecture='{Architecture}' and Space='{Space}']") != null))
        {
            string returnCode = xdoc.XPathSelectElement($"/prechecker/AMD_Compatibility/Compatible_System[contains(Name, '{OS}')' and Build='{Build}' and Architecture='{Architecture}' and Space='{Space}']/ReturnCode")?.Value;
            //string returnCode = matchingNodes.First().Element("ReturnCode").Value;

            log("The Sytem is Not Compatible. Exit:" + returnCode);
            return int.Parse(returnCode);
        }
        else
        {
            log($"{(OS != "Windows 11" ? "OS Not Supported\n" : "")}" +
      $"{(Build != "22621" ? "Build Not Supported\n" : "")}" +
      $"{(Architecture != "x64" ? "Architecture Not Supported\n" : "")}" +
      $"{(Space != "2048" ? "Space Not Supported\n" : (OS != "Windows 11" || Build != "22621" || Architecture != "x64" ? "The System is Not Compatible. Exit: 11708" : ""))}");



            return 11708;           
        }
    }

    static XDocument LoadXmlFile(string resourceName)
    {
        using (Stream stream = typeof(Program).Assembly.GetManifestResourceStream(resourceName))
        {
            return XDocument.Load(stream);
        }
    }

    // Log messages with timestamp to a file
    static void log(string message)
    {
        // Create the log message with timestamp
        string logMessage = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} - {message}";

        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
        // Append the log message to the log file
        File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
    }
}











