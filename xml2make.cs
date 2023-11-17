using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace xml2make
{
    internal class xml2make
    {
        static void OutputMakefile(string basename, string startup)
        {
            string mf = @"include build.mk

CC = arm-none-eabi-gcc
OBJCOPY = arm-none-eabi-objcopy

OPTFLAGS = -O0
CFLAGS = -Wall $(OPTFLAGS) -mcpu=cortex-m4 -mfloat-abi=hard -mfpu=fpv4-sp-d16 -gdwarf-2 -ffunction-sections -fdata-sections $(INCLUDES) $(DEFINES)
LDFLAGS = -Tflash.ld -Wl,--gc-sections

STARTUP = %startup%.o
TARGET = %basename%.axf
TARGET_HEX = %basename%.hex
TARGET_BIN = %basename%.bin

$(TARGET): $(STARTUP) $(OBJECTS)
	$(CC) $(CFLAGS) $(LDFLAGS) --specs=nosys.specs $^ -o $@
	$(OBJCOPY) -O ihex $(TARGET) $(TARGET_HEX)
	$(OBJCOPY) -O binary $(TARGET) $(TARGET_BIN)

%.o: %.c
	$(CC) $(CFLAGS) -c $< -o $@

%.o: %.s
	$(CC) $(CFLAGS) -c $< -o $@

clean:
	rm -f $(STARTUP) $(OBJECTS) $(TARGET) $(TARGET_HEX) $(TARGET_BIN)
";
            mf = mf.Replace("%basename%", basename);
            mf = mf.Replace("%startup%", startup);

            StreamWriter sw = new StreamWriter("Makefile");
            sw.Write(mf);
            sw.Close();
        }
        static void Main(string[] args)
        {
            XmlDocument xmlDoc = new XmlDocument();
            List<string> mk_srcs = new List<string>();
            List<string> mk_objs = new List<string>();
            List<string> mk_objs_short = new List<string>();
            List<string> vs_srcs = new List<string>();

            if (args.Count() == 0)
            {
                Console.Write(@"Usage:
xml2make uvisionproject.uvproj [startup file]
    uvproj = required
    startup file = path to startup file WITHOUT extension (i.e. 'lib/CMSIS/f4xx_startup')
");
                return;
            }

            try {
                xmlDoc.Load(args[0]);
            } catch (Exception) {
                Console.WriteLine("Cannot open file {0}, stopping", args.Length > 0 ? args[0] : "(null)");
                return;
            }

            string basename = Path.GetFileNameWithoutExtension(args[0]);
            string startup = args.Count() > 1 ? args[1] : "lib/CMSIS/startup_stm32f407vetx";

            /* Target  */
            XmlNode defines = xmlDoc.SelectSingleNode("Project/Targets/Target/TargetOption/TargetArmAds/Cads/VariousControls/Define");
            /* Define list is split by comma */
            List<string> defs = defines.InnerText.Split(',').ToList();

            string mk_defs = "DEFINES = -D";
            mk_defs += String.Join(" -D", defs);

            XmlNode includes = xmlDoc.SelectSingleNode("Project/Targets/Target/TargetOption/TargetArmAds/Cads/VariousControls/IncludePath");
            /* Include list is split by semicolon */
            List<string> incs = includes.InnerText.Split(';').ToList();

            string mk_incs = "INCLUDES = -I";
            mk_incs += String.Join(" -I", incs).Replace('\\', '/');

            /* output filename / basename */
            XmlNode outdir = xmlDoc.SelectSingleNode("Project/Targets/Target/TargetOption/TargetCommonOption/OutputDirectory");
            XmlNode outname = xmlDoc.SelectSingleNode("Project/Targets/Target/TargetOption/TargetCommonOption/OutputName");

            /* get the object path+filename to use as makefile output */
            string outputname = (outdir.InnerText + outname.InnerText).Replace('\\', '/');

            /* parse groups */
            XmlNode first_group = xmlDoc.SelectSingleNode("Project/Targets/Target/Groups");
            XmlNodeList groups = first_group.SelectNodes("Group");

            List<string> vs_groups = new List<string>();
            Dictionary<string, List<string>> keys = new Dictionary<string, List<string>>();

            foreach (XmlNode g in groups)
            {
                string group_name = g.SelectSingleNode("GroupName").InnerText;
                string mk_group_name = group_name.ToUpper();
                /* Add to vs group name list */
                vs_groups.Add(group_name);

                XmlNodeList files = g.SelectNodes("Files/File");
                List<string> vs_files = new List<string>();
                List<string> mk_files = new List<string>();

                foreach (XmlNode f in files) {
                    string filename = f.SelectSingleNode("FileName").InnerText;
                    string filepath = f.SelectSingleNode("FilePath").InnerText.Substring(2);
                    string filetype = f.SelectSingleNode("FileType").InnerText; /* 1 = C 5 = H */
                    XmlNode in_build = f.SelectSingleNode("FileOption/CommonProperty/IncludeInBuild");

                    if ((in_build != null) && (in_build.InnerText == "0"))
                        continue;

                    /* for VS filter export */
                    vs_files.Add(filepath);
                    /* for VS vcxproj export */
                    vs_srcs.Add(filepath);

                    /* for build.mk export */
                    if (int.Parse(filetype) == 1)
                        mk_files.Add(filepath.Replace('\\', '/'));

                    keys[group_name] = vs_files;
                }

                /* generate SOURCES_XXX output */
                mk_srcs.Add("SOURCES_" + mk_group_name + " = " + String.Join(" ", mk_files));
                mk_objs.Add("OBJECTS_" + mk_group_name + " = $(patsubst %.c,%.o,$(SOURCES_" + mk_group_name + "))");
                mk_objs_short.Add("$(OBJECTS_" + mk_group_name + ")");

            }

            /* write out build.mk */
            /* Generate final string */
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Auto-generated by xml2make, edit carefully");
            sb.AppendLine(mk_incs);
            sb.AppendLine(mk_defs);
            foreach (string src in mk_srcs)
                sb.AppendLine(src);
            foreach (string def in mk_objs)
                sb.AppendLine(def);
            string objs = "OBJECTS = " + String.Join(" ", mk_objs_short);
            sb.AppendLine(objs);

            /* output build.mk */
            StreamWriter sw = new StreamWriter("build.mk");
            sw.Write(sb.ToString());
            sw.Close();

            /* output makefile, too */
            OutputMakefile(outputname, startup);

            /* Common settings for XML output */
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = ("  ");
            settings.CloseOutput = true;

            /* Write out vcxproj.filters */
            XmlWriter w = XmlWriter.Create(basename + ".vcxproj.filters", settings);
            w.WriteStartDocument(false);
            w.WriteStartElement("Project", "http://schemas.microsoft.com/developer/msbuild/2003");
            w.WriteAttributeString("ToolsVersion", "4.0");
            w.WriteAttributeString("xmlns", "http://schemas.microsoft.com/developer/msbuild/2003");

            w.WriteStartElement("ItemGroup"); /* Filter ItemGroup */

            /* loop filters */
            foreach (string g in vs_groups)
            {
                w.WriteStartElement("Filter");
                w.WriteAttributeString("Include", g);
                w.WriteElementString("UniqueIdentifier", Guid.NewGuid().ToString());
                w.WriteEndElement(); /* Filter */
            }
            w.WriteEndElement(); /* ItemGroup (Filter) */

            w.WriteStartElement("ItemGroup"); /* ClCompile ItemGroup */

            foreach (string g in keys.Keys)
            {
                foreach (string k in keys[g])
                {
                    w.WriteStartElement("ClCompile");
                    w.WriteAttributeString("Include", k);
                    w.WriteElementString("Filter", g);
                    w.WriteEndElement(); /* ClCompile */
                }

            }
            w.WriteEndElement(); /* ItemGroup (ClCompile) */

            w.WriteEndElement(); /* Project */
            w.Flush();
            w.Close();

            /* Write out vcxproj */
            w = XmlWriter.Create(basename + ".vcxproj", settings);
            w.WriteStartElement("Project", "http://schemas.microsoft.com/developer/msbuild/2003");
            w.WriteAttributeString("DefaultTargets", "Build");
            w.WriteAttributeString("xmlns", "http://schemas.microsoft.com/developer/msbuild/2003");

            /* ProjectConfigurations */
            w.WriteStartElement("ItemGroup");
            w.WriteAttributeString("Label", "ProjectConfigurations");
            w.WriteStartElement("ProjectConfiguration");
            w.WriteAttributeString("Include", "Build|x64");
            w.WriteElementString("Configuration", "Build");
            w.WriteElementString("Platform", "x64");
            w.WriteEndElement(); /* ProjectConfiguration */
            w.WriteEndElement(); /* ItemGroup */

            /* Globals */
            w.WriteStartElement("PropertyGroup");
            w.WriteAttributeString("Label", "Globals");
            w.WriteElementString("VCProjectVersion", "17.0");
            w.WriteElementString("ProjectGuid", "{13478CB4-88A0-494E-9340-AB0135BE6601}");
            w.WriteElementString("Keyword", "Win32Proj");
            w.WriteEndElement(); /* PropertyGroup */

            /* Import */
            w.WriteStartElement("Import");
            w.WriteAttributeString("Project", "$(VCTargetsPath)\\Microsoft.Cpp.Default.props");
            w.WriteEndElement(); /* Import */

            /* PropertyGroup */
            w.WriteStartElement("PropertyGroup");
            w.WriteAttributeString("Condition", "'$(Configuration)|$(Platform)'=='Build|x64'");
            w.WriteAttributeString("Label", "Configuration");
            w.WriteElementString("ConfigurationType", "Makefile");
            w.WriteElementString("UseDebugLibraries", "true");
            w.WriteElementString("PlatformToolset", "v143");
            w.WriteEndElement(); /* PropertyGroup */

            /* Import */
            w.WriteStartElement("Import");
            w.WriteAttributeString("Project", "$(VCTargetsPath)\\Microsoft.Cpp.props");
            w.WriteEndElement(); /* Import */

            /* ImportGroup */
            w.WriteStartElement("ImportGroup");
            w.WriteAttributeString("Label", "ExtensionSettings");
            w.WriteEndElement(); /* ImportGroup */

            /* ImportGroup */
            w.WriteStartElement("ImportGroup");
            w.WriteAttributeString("Label", "Shared");
            w.WriteEndElement(); /* ImportGroup */

            /* ImportGroup */
            w.WriteStartElement("ImportGroup");
            w.WriteAttributeString("Label", "PropertySheets");
            w.WriteAttributeString("Condition", "'$(Configuration)|$(Platform)'=='Build|x64'");
            w.WriteStartElement("Import");
            w.WriteAttributeString("Project", "$(UserRootDir)\\Microsoft.Cpp.$(Platform).user.props");
            w.WriteAttributeString("Condition", "exists('$(UserRootDir)\\Microsoft.Cpp.$(Platform).user.props')");
            w.WriteAttributeString("Label", "LocalAppDataPlatform");
            w.WriteEndElement(); /* Import */
            w.WriteEndElement(); /* ImportGroup */

            /* PropertyGroup */
            w.WriteStartElement("PropertyGroup");
            w.WriteAttributeString("Label", "UserMacros");
            w.WriteEndElement(); /* PropertyGroup */

            /* PropertyGroup */
            w.WriteStartElement("PropertyGroup");
            w.WriteAttributeString("Condition", "'$(Configuration)|$(Platform)'=='Build|x64'");
            w.WriteElementString("NMakeBuildCommandLine", "make");
            w.WriteElementString("NMakeOutput", basename + ".elf");
            w.WriteElementString("NMakeCleanCommandLine", "make clean");
            defs.Add("__GNUC__");
            w.WriteElementString("NMakePreprocessorDefinitions", string.Join(";", defs));
            w.WriteElementString("NMakeIncludeSearchPath", string.Join(";", incs));
            w.WriteEndElement(); /* PropertyGroup */

            /* ItemDefinitionGroup */
            w.WriteElementString("ItemDefinitionGroup", "");

            /* ItemGroup */
            w.WriteStartElement("ItemGroup");
            foreach (string f in vs_srcs) {
                w.WriteStartElement("ClCompile");
                w.WriteAttributeString("Include", f);
                w.WriteEndElement(); /* ClCompile */
            }
            w.WriteEndElement(); /* ItemGroup */

            /* Import */
            w.WriteStartElement("Import");
            w.WriteAttributeString("Project", "$(VCTargetsPath)\\Microsoft.Cpp.targets");
            w.WriteEndElement(); /* Import */

            /* ImportGroup */
            w.WriteStartElement("ImportGroup");
            w.WriteAttributeString("Label", "ExtensionTargets");
            w.WriteEndElement(); /* ImportGroup */

            w.WriteEndElement(); /* Project */
            w.Flush();
            w.Close();
        }
    }
}
