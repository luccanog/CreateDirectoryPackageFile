using System.Text;
using System.Xml;

namespace CentralizePackageVersions
{
    internal class Program
    {
        static Dictionary<string, PackageInfo> AllPackageReferences = new Dictionary<string, PackageInfo>();
        static string OriginalPath = string.Empty;

        static void Main(string[] args)
        {
            Console.WriteLine(@"Write the repo base path (e.g C:\Users\source\repos\CreateDirectoryPackageFile): ");
            OriginalPath = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(OriginalPath))
            {
                Console.WriteLine("Please provide a directory path.");
                return;
            }

            if (!Directory.Exists(OriginalPath))
            {
                Console.WriteLine("# ERROR: The provided directory does not exist.");
                return;
            }

            List<string> csprojFiles = FindCsprojFiles();

            Console.WriteLine($"Found {csprojFiles.Count} .csproj files:");
            foreach (string file in csprojFiles)
            {
                Console.WriteLine(file);

                string content = File.ReadAllText(file);

                string newCjprojContent = ProcessContent(content);

                // Override the current csproj file.
                File.WriteAllText(file, newCjprojContent);
            }
            SaveDirectoryPackagesProps();
        }

        /// <summary>
        /// Create a new Dierctory.Packages.props file based on the data stored at the <see cref="BuildPackageVersionXmlNode"/> method.
        /// </summary>
        private static void SaveDirectoryPackagesProps()
        {
            try
            {
                var sortedPackages = AllPackageReferences.OrderBy(x => x.Key).ToList();

                string filePath = $"{OriginalPath}\\Directory.Packages.props";

                StringBuilder content = new();
                content.AppendLine("<Project>");
                content.AppendLine("  <ItemGroup>");

                foreach (var package in sortedPackages)
                {
                    content.AppendLine("    " + package.Value.OuterXml);
                }

                content.AppendLine("  </ItemGroup>");
                content.AppendLine("</Project>");

                File.WriteAllText(filePath, content.ToString());
                Console.WriteLine("File created and saved successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        static List<string> FindCsprojFiles()
        {
            List<string> csprojFiles = new List<string>();

            try
            {
                csprojFiles.AddRange(Directory.GetFiles(OriginalPath, "*.csproj", SearchOption.AllDirectories));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"# ERROR: An error occurred while searching for .csproj files: {ex.Message}");
            }

            return csprojFiles;
        }


        static string ProcessContent(string content)
        {
            // Load .csproj XML
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(content);

            XmlNodeList itemGroups = doc.GetElementsByTagName("ItemGroup");
            foreach (XmlNode itemGroup in itemGroups)
            {
                XmlNodeList packageReferences = itemGroup.SelectNodes("PackageReference");

                foreach (XmlNode packageReference in packageReferences)
                {
                    var includeAttribute = packageReference.Attributes["Include"];
                    var versionAttribute = packageReference.Attributes["Version"];

                    if (includeAttribute != null && versionAttribute != null)
                    {
                        Console.WriteLine($"Package: {includeAttribute.Value}, Version: {versionAttribute.Value}");
                    }

                    BuildPackageVersionXmlNode(packageReference, includeAttribute, versionAttribute);

                    // Remove the "Version" attribute since it is no more needed on the .csproj PackageReference nodes.
                    packageReference.Attributes.Remove(versionAttribute);
                }
            }


            // Return the XML content as a string.
            using (StringWriter stringWriter = new StringWriter())
            using (XmlTextWriter xmlTextWriter = new XmlTextWriter(stringWriter))
            {
                doc.WriteTo(xmlTextWriter);
                return stringWriter.ToString();
            }
        }

        /// <summary>
        /// Copy the PackageReference from .CSPROJ file and create a new PackageVersion to be placed at the Directory.Packages.props file.
        /// </summary>
        /// <param name="packageReference">The original PackageReference XML node.</param>
        /// <param name="includeAttribute">The name of the package, e.g. "SPC.Security.Http"</param>
        /// <param name="versionAttribute">The version of the package, e.g. "4.16.0"</param>
        private static void BuildPackageVersionXmlNode(XmlNode packageReference, XmlAttribute? includeAttribute, XmlAttribute? versionAttribute)
        {
            var outerXml = packageReference.OuterXml.Replace("PackageReference", "PackageVersion");

            try
            {
                var version = new Version(versionAttribute.Value);
                var key = includeAttribute.InnerText;
                var success = AllPackageReferences.TryAdd(key, new PackageInfo(outerXml, version));

                // Ensure that the latest version is persisted at the PackageVersion node.
                if (!success && version > AllPackageReferences[key].Version)
                {
                    AllPackageReferences[key] = new PackageInfo(outerXml, version);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"# ERROR: Failure while processing Package: {includeAttribute.Value}, Version: {versionAttribute.Value}. You will need to handle it manually");
                Console.WriteLine(ex.Message);
                Console.WriteLine("# # #");
            }
        }
    }
}

record PackageInfo
{
    public string OuterXml { get; set; }
    public Version Version { get; set; }

    public PackageInfo(string outerXml, Version version)
    {
        OuterXml = outerXml;
        Version = version;
    }
}