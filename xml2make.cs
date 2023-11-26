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
        static void OutputLinker(string basename, int stack_size, int ram_origin, int ram_size, int flash_origin, int flash_size)
        {
            string ld = @"
/* Entry Point */
ENTRY(Reset_Handler)

/* Highest address of the user mode stack */
_estack = ORIGIN(RAM) + LENGTH(RAM); /* end of ""RAM"" Ram type memory */

_Min_Heap_Size = 0x0; /* required amount of heap */
_Min_Stack_Size = %stack_size%; /* required amount of stack */

/* Memories definition */
MEMORY
{
  RAM    (xrw)    : ORIGIN = %ram_origin%,   LENGTH = %ram_size%K
  FLASH    (rx)    : ORIGIN = %flash_origin%,   LENGTH = %flash_size%K
}

/* Sections */
SECTIONS
{
  /* The startup code into ""FLASH"" Rom type memory */
  .isr_vector :
  {
    . = ALIGN(4);
    KEEP(*(.isr_vector)) /* Startup code */
    . = ALIGN(4);
  } >FLASH

  /* The program code and other data into ""FLASH"" Rom type memory */
  .text :
  {
    . = ALIGN(4);
    *(.text)           /* .text sections (code) */
    *(.text*)          /* .text* sections (code) */
    *(.glue_7)         /* glue arm to thumb code */
    *(.glue_7t)        /* glue thumb to arm code */
    *(.eh_frame)

    KEEP (*(.init))
    KEEP (*(.fini))

    . = ALIGN(4);
    _etext = .;        /* define a global symbols at end of code */
  } >FLASH

  /* Constant data into ""FLASH"" Rom type memory */
  .rodata :
  {
    . = ALIGN(4);
    *(.rodata)         /* .rodata sections (constants, strings, etc.) */
    *(.rodata*)        /* .rodata* sections (constants, strings, etc.) */
    . = ALIGN(4);
  } >FLASH

  .ARM.extab   : {
    . = ALIGN(4);
    *(.ARM.extab* .gnu.linkonce.armextab.*)
    . = ALIGN(4);
  } >FLASH

  .ARM : {
    . = ALIGN(4);
    __exidx_start = .;
    *(.ARM.exidx*)
    __exidx_end = .;
    . = ALIGN(4);
  } >FLASH

  .preinit_array     :
  {
    . = ALIGN(4);
    PROVIDE_HIDDEN (__preinit_array_start = .);
    KEEP (*(.preinit_array*))
    PROVIDE_HIDDEN (__preinit_array_end = .);
    . = ALIGN(4);
  } >FLASH

  .init_array :
  {
    . = ALIGN(4);
    PROVIDE_HIDDEN (__init_array_start = .);
    KEEP (*(SORT(.init_array.*)))
    KEEP (*(.init_array*))
    PROVIDE_HIDDEN (__init_array_end = .);
    . = ALIGN(4);
  } >FLASH

  .fini_array :
  {
    . = ALIGN(4);
    PROVIDE_HIDDEN (__fini_array_start = .);
    KEEP (*(SORT(.fini_array.*)))
    KEEP (*(.fini_array*))
    PROVIDE_HIDDEN (__fini_array_end = .);
    . = ALIGN(4);
  } >FLASH

  /* Used by the startup to initialize data */
  _sidata = LOADADDR(.data);

  /* Initialized data sections into ""RAM"" Ram type memory */
  .data :
  {
    . = ALIGN(4);
    _sdata = .;        /* create a global symbol at data start */
    *(.data)           /* .data sections */
    *(.data*)          /* .data* sections */
    *(.RamFunc)        /* .RamFunc sections */
    *(.RamFunc*)       /* .RamFunc* sections */

    . = ALIGN(4);
    _edata = .;        /* define a global symbol at data end */

  } >RAM AT> FLASH

  /* Uninitialized data section into ""RAM"" Ram type memory */
  . = ALIGN(4);
  .bss :
  {
    /* This is used by the startup in order to initialize the .bss section */
    _sbss = .;         /* define a global symbol at bss start */
    __bss_start__ = _sbss;
    *(.bss)
    *(.bss*)
    *(COMMON)

    . = ALIGN(4);
    _ebss = .;         /* define a global symbol at bss end */
    __bss_end__ = _ebss;
  } >RAM

  /* User_heap_stack section, used to check that there is enough ""RAM"" Ram  type memory left */
  ._user_heap_stack :
  {
    . = ALIGN(8);
    PROVIDE ( end = . );
    PROVIDE ( _end = . );
    . = . + _Min_Heap_Size;
    . = . + _Min_Stack_Size;
    . = ALIGN(8);
  } >RAM

  /* Remove information from the compiler libraries */
  /DISCARD/ :
  {
    libc.a ( * )
    libm.a ( * )
    libgcc.a ( * )
  }

  .ARM.attributes 0 : { *(.ARM.attributes) }
}
";
            ld = ld.Replace("%stack_size%", "0x" + stack_size.ToString("x"));
            ld = ld.Replace("%ram_origin%", "0x" + ram_origin.ToString("x"));
            ld = ld.Replace("%ram_size%", ram_size.ToString());
            ld = ld.Replace("%flash_origin%", "0x" + flash_origin.ToString("x"));
            ld = ld.Replace("%flash_size%", flash_size.ToString());

            StreamWriter sw = new StreamWriter("flash.ld");
            sw.Write(ld);
            sw.Close();
        }
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

        static void parseMemory(XmlNode node, out int mem_base, out int mem_size)
        {
            int type = int.Parse(node.SelectSingleNode("Type").InnerText);
            int start = Convert.ToInt32(node.SelectSingleNode("StartAddress").InnerText, 16);
            int size = Convert.ToInt32(node.SelectSingleNode("Size").InnerText, 16);

            mem_base = start;
            mem_size = size / 1024;
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
xml2make uvisionproject.uvproj
    uvproj = required argument
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
            string startup = "startup.s";
            int stack_size = 0x1000;
            int ram_origin = 0x20000000;
            int ram_size = 128;
            int flash_origin = 0x8000000;
            int flash_size = 512;

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
                    string filetype = f.SelectSingleNode("FileType").InnerText; /* 1 = C 2 = S 5 = H */
                    XmlNode in_build = f.SelectSingleNode("FileOption/CommonProperty/IncludeInBuild");

                    if ((in_build != null) && (in_build.InnerText == "0"))
                        continue;

                    /* for VS filter export */
                    vs_files.Add(filepath);
                    /* for VS vcxproj export */
                    vs_srcs.Add(filepath);

                    /* for build.mk export */
                    int type = int.Parse(filetype);
                    if (type == 1)
                        mk_files.Add(filepath.Replace('\\', '/'));
                    else if (type == 2)
                        startup = filepath.Replace('\\', '/').Replace(".s", "_gcc"); /* we're doing a lot of assumptions here, but please be with me */

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

            /* output linker script */
            XmlNode memory = xmlDoc.SelectSingleNode("Project/Targets/Target/TargetOption/TargetArmAds/ArmAdsMisc/OnChipMemories/IRAM");
            parseMemory(memory, out ram_origin, out ram_size);
            memory = xmlDoc.SelectSingleNode("Project/Targets/Target/TargetOption/TargetArmAds/ArmAdsMisc/OnChipMemories/IROM");
            parseMemory(memory, out flash_origin, out flash_size);
            OutputLinker("flash.ld", stack_size, ram_origin, ram_size, flash_origin, flash_size);

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
