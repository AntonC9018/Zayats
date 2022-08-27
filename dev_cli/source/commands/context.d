module commands.context;

import jcli.core;

import std.stdio;

import commands.setup;
import commands.models : ModelsContext;
import commands.kari : KariContext;
import commands.proto;
import common;

@CommandDefault("The context common to all subcommands.")
@(Subcommands!(
    SetupCommand,
    KariContext,
    ModelsContext,
    ConfigContext,
    UnityContext,
    ProtoContext))
struct Context
{
    @(ArgConfig.optional)
    {
        @("Project directory")
        string projectDirectory;

        @("Temp directory")
        string tempDirectory = "temp";

        @("Build output directory")
        string buildDirectory = "build_folder";

        @("Tools directory")
        string toolsDirectory = "build_folder/tools".path;

        @("Unity project directoy")
        string unityProjectDirectoryName = "Zayats";
        
        @("Path to the configuration file, created locally for each machine.")
        string configurationPath;
    }

    string unityProjectDirectory;

    import std.experimental.logger;
    Logger logger;
    
    Nullable!Config _config;

    ref Config config() return
    {
        if (_config.isNull)
        {
            ConfigContext c;
            c.context = &this;
            auto r = c.readJSON();
            if (r.isNull)
            {
                logger.error("The config should have been created at this point, but it's not."
                    ~ "Run 'setup' or 'config init' to create it.");
            }
            _config = mapJSON!Config(r.get());
        }
        return _config.get();
    }

    int onExecute()
    {
        writeln("Call this command with one of the suboptions.");
        return 1;
    }

    int onIntermediateExecute()
    {
        import std.file : exists, getcwd, mkdir;
        import std.path;

        int errorCount = 0;
        
        logger = new FileLogger(stdout);

        if (projectDirectory == "")
            projectDirectory = getcwd();
        else
            projectDirectory = normalizedAbsolutePath(projectDirectory);

        unityProjectDirectory = projectDirectory.buildPath(unityProjectDirectoryName);
        if (!exists(unityProjectDirectory))
        {
            logger.error("Please run the tool in the root directory of the project, or specify it as an argument.");
            errorCount++;
        }

        void normalizeAndCreate(ref string p, string name)
        {
            p = absolutePath(p);
            if (!exists(p))
            {
                mkdir(p);
                logger.log("Created ", name, " directory: ", p);
            }
        }

        normalizeAndCreate(tempDirectory, "temp");
        normalizeAndCreate(buildDirectory, "build");
        normalizeAndCreate(toolsDirectory, "tools");

        {
            if (configurationPath == "")
                configurationPath = buildPath(buildDirectory, "local_config.json");
            else if (!isAbsolute(configurationPath))
            {
                configurationPath = setExtension(configurationPath, ".json");
                configurationPath = buildPath(buildDirectory, configurationPath);
            }
            
            import std.file : exists, isDir;
            if (exists(configurationPath))
            {
                if (isDir(configurationPath))
                {
                    logger.error(configurationPath, " is not a file.");
                    return 1;
                }
            }
        }

        return errorCount;
    }
}