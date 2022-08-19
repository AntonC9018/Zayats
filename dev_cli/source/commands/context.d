module commands.context;

import jcli.core;

import std.stdio;

import commands.setup : SetupCommand, KariContext;
import commands.models : ModelsContext;
import commands.proto : ProtoContext;
import common;

@CommandDefault("The context common to all subcommands.")
@(Subcommands!(SetupCommand, KariContext, ModelsContext, ProtoContext))
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
        string toolsDirectory = "build_folder/tools";

        @("Unity project directoy")
        string unityProjectDirectoryName = "Zayats";
    }

    string unityProjectDirectory;


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


        if (projectDirectory == "")
        {
            projectDirectory = getcwd();
        }
        else
        {
            projectDirectory = absolutePath(projectDirectory);
        }


        unityProjectDirectory = projectDirectory.buildPath(unityProjectDirectoryName);
        if (!exists(unityProjectDirectory))
        {
            writeln("Please run the tool in the root directory of the project, or specify it as an argument.");
            errorCount++;
        }
        

        tempDirectory = absolutePath(tempDirectory);
        if (!exists(tempDirectory))
        {
            mkdir(tempDirectory);
            writeln("Created temp directory: ", tempDirectory);
        }


        buildDirectory = absolutePath(buildDirectory);
        if (!exists(buildDirectory))
        {
            mkdir(buildDirectory);
            writeln("Created the build directory: ", buildDirectory);
        }

        toolsDirectory = absolutePath(toolsDirectory);
        if (!exists(toolsDirectory))
        {
            mkdir(toolsDirectory);
            writeln("Created the tools directory: ", toolsDirectory);
        }

        return errorCount;
    }
}