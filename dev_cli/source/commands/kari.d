module commands.kari;

import jcli;

import commands.context;
import common.tools;
import common;

import std.path;
import std.stdio;
import std.process : wait;

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

    DotnetBuild dotnetBuild()
    {
        DotnetBuild d = .dotnetBuild();
        d.msbuildProperties["KariBuildPath"] = context.buildDirectory ~ `\`;
        d.configuration = configuration;
        return d;
    }
}


struct Plugins
{
    @disable this();
    static immutable(string[]) kariInternal = ["Flags", "DataObject"];
    static immutable(string[]) custom = ["AdvancedEnum", "Forward", "Zayats.Exporter"];
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
            context.buildDirectory, "bin", "Kari.Generator", context.configuration, "net6.0", "Kari.Generator").exe;

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

            Kari kari;
            kari.toolPath = [kariExecutablePath];
            kari.configurationFile = buildPath(context.projectDirectory, context.unityProjectDirectoryName, "kari.json");
            kari.pluginPaths = chain(
                    Plugins.kariInternal.map!(p => getPluginDllPath(p, "Kari.Plugins." ~ p ~ ".dll")),
                    Plugins.custom.map!(p => getPluginDllPath(p, p ~ ".dll")))
                .array;
            kari.gitignoreTemplate = `# We don't ignore the generated files`;
            kari.moreArguments = rawArgs;

            auto pid = startProcess(kari);
            const status = wait(pid);
            if (status != 0)
            {
                context.logger.error("Kari execution failed.");
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
        auto d = context.dotnetBuild();

        foreach (path; customPluginNames
            .map!(p => customPluginDirectoryPath.buildPath(p, p ~ ".csproj"))
            .chain(internalPluginNames
                .map!(p => context.kariPath.buildPath("source", "Kari.Plugins", p, p ~ ".csproj"))))
        {
            d.path = path;
            auto pid = startProcess(d);
            const status = wait(pid);
            if (status != 0)
            {
                context.logger.error(path, " Kari plugin build failed.");
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
                    context.logger.error("Invalid plugin name: ", plugins[index]);
            }
            return 1;
        }

        return buildPlugins(customPluginsToBuild, internalPluginsToBuild);
    }

    int buildKari()
    {
        context.logger.log("Building Kari.");
        
        auto d = context.dotnetBuild();
        d.path = context.kariPath;
        auto pid = startProcess(d);
        const status = wait(pid);
        if (status != 0)
        {
            context.logger.error("Kari build failed.");
            return status;
        }
        return status;
    }
}
