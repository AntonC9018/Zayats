module commands.setup;

import jcli;

import commands.context;
import common;

import std.path;
import std.stdio;
import std.process : wait;


@Command("setup", "Sets up all the stuff in the project")
struct SetupCommand
{
    @ParentCommand
    Context* context;

    @("In which mode to build kari.")
    string kariConfiguration = "Debug";

    int onExecute()
    {
        KariContext kariContext;
        kariContext.context = context;
        kariContext.configuration = kariConfiguration;
        kariContext.onIntermediateExecute();

        {
            KariBuild build;
            build.context = &kariContext;
            auto status = build.buildKari();
            if (status != 0)
                return status;

            status = build.buildPlugins(Plugins.custom, Plugins.kariInternal);
            if (status != 0)
                return status;
        }
        {
            KariRun run;
            run.context = &kariContext;
            auto status = run.onExecute();
            if (status != 0)
                return status;
        }
        
        return 0;
    }
}

// TODO: Maybe should build too?
@Command("kari", "Deals with the code generator.")
@(Subcommands!(KariRun, KariBuild))
struct KariContext
{
    @ParentCommand
    Context* context;
    alias context this;

    @("The configuration in which Kari was built.")
    string configuration = "Debug";

    string kariStuffPath;
    string kariPath;

    void onIntermediateExecute()
    {
        kariStuffPath = context.projectDirectory.buildPath("kari_stuff");
        kariPath = kariStuffPath.buildPath("Kari");
    }
}


struct Plugins
{
    @disable this();
    static immutable(string[]) kariInternal = ["Flags"];
    static immutable(string[]) custom = ["AdvancedEnum"];
}


@Command("run", "Runs Kari.")
struct KariRun
{
    @ParentCommand
    KariContext* context;
    
    @("Extra arguments passed to Kari")
    @(ArgRaw)
    string[] rawArgs;

    int onExecute()
    {
        // TODO: this path should be provided by the build system or something
        // msbuild cannot do that afaik, so study the alternatives asap.
        string kariExecutablePath = buildPath(
            context.buildDirectory, "bin", "Kari.Generator", context.configuration, "net6.0", "Kari.Generator");
        version (Windows)
            kariExecutablePath ~= ".exe";

        string getPluginDllPath(string pluginName, string pluginDllName)
        {
            return buildPath(
                context.buildDirectory, 
                "bin", pluginName, context.configuration, "net6.0",
                pluginDllName);
        }

        {
            import std.algorithm;
            import std.range;

            // TODO: Improve Kari's argument parsing capabilities, or call it directly
            auto pid = spawnProcess2([
                    kariExecutablePath,
                    "-configurationFile", buildPath(context.projectDirectory, context.unityProjectDirectoryName, "kari.json"),
                    "-pluginPaths", 
                        chain(
                            Plugins.kariInternal.map!(p => getPluginDllPath(p, "Kari.Plugins." ~ p ~ ".dll")),
                            Plugins.custom.map!(p => getPluginDllPath(p, p ~ ".dll")))
                        .join(","),
                    "-gitignoreTemplate", `# Code generation is optional for now, so we don't ignore the generated files`
                ] ~ rawArgs, context.projectDirectory);
            const status = wait(pid);
            if (status != 0)
            {
                writeln("Kari execution failed.");
                return status;
            }
        }

        return 0;
    }
}


@Command("build", "Builds Kari.")
struct KariBuild
{
    @ParentCommand
    KariContext* context;
    
    @("Whether to build all plugins at once")
    @(ArgConfig.parseAsFlag)
    bool allPlugins;

    @("Plugin names to be built. Can include either internal or external plugin names.")
    @(ArgConfig.aggregate)
    string[] plugins;

    int buildPlugins(const string[] customPluginNames, const string[] internalPluginNames)
    {
        import std.range;
        import std.algorithm;

        string customPluginDirectoryPath = buildPath(context.kariStuffPath, "plugins");

        foreach (path; customPluginNames
            .map!(p => customPluginDirectoryPath.buildPath(p, p ~ ".csproj"))
            .chain(internalPluginNames
                .map!(p => context.kariPath.buildPath("source", "Kari.Plugins", p, p ~ ".csproj"))))
        {
            import std.process;
            auto pid = spawnProcess([
                "dotnet", "build",
                path,
                "--configuration", context.configuration,
                "/p:KariBuildPath=" ~ context.buildDirectory ~ `\`]);
            const status = wait(pid);
            if (status != 0)
            {
                writeln(path, " Kari plugin build failed.");
                return status;
            }
        }
        return 0;
    }

    int onExecute()
    {
        import std.algorithm;

        if (allPlugins)
            return buildPlugins(Plugins.custom, Plugins.kariInternal);

        if (plugins.length == 0)
            return buildKari();

        auto isPluginBuilt = new bool[plugins.length];

        string[] customPluginsToBuild;
        foreach (index, p; plugins)
        {
            if (Plugins.custom.canFind(p))
            {
                customPluginsToBuild ~= p;
                isPluginBuilt[index] = true;
            }
        }

        string[] internalPluginsToBuild;
        foreach (index, p; plugins)
        {
            if (Plugins.kariInternal.canFind(p))
            {
                internalPluginsToBuild ~= p;
                isPluginBuilt[index] = true;
            }
        }

        if (!isPluginBuilt.all)
        {
            foreach (index, value; isPluginBuilt)
            {
                if (value == false)
                    writeln("Invalid plugin name: ", plugins[index]);
            }
            return 1;
        }

        return buildPlugins(customPluginsToBuild, internalPluginsToBuild);
    }

    int buildKari()
    {
        writeln("Building Kari.");
        auto pid = spawnProcess2([
            "dotnet", "build", 
            "--configuration", context.configuration,
            "/p:KariBuildPath=" ~ context.buildDirectory ~ `\`],
            context.kariPath);
        const status = wait(pid);
        if (status != 0)
        {
            writeln("Kari build failed.");
            return status;
        }
        return status;
    }
}
