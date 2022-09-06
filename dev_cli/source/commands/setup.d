module commands.setup;

import jcli;

import commands.context;
import common;

import std.path;
import std.stdio;
import std.process : wait;
import commands.kari;
import commands.nuget;


@Command("setup", "Sets up all the stuff in the project")
struct SetupCommand
{
    @ParentCommand
    Context* context;

    @("In which mode to build kari.")
    string kariConfiguration = "Debug";

    int onExecute()
    {
        {
            import commands.models : Package, ModelsContext;
            ModelsContext modelsContext;
            modelsContext.context = context;
            modelsContext.onIntermediateExecute();

            Package pack;
            pack.context = &modelsContext;
            pack.copyToUnity = true;
            pack.force = false;
            int status = pack.onExecute();
            if (status != 0)
                return status;
        }

        {
            // do the initial configuration
            ConfigContext configContext;
            configContext.context = context;
            {
                int status = configContext.onIntermediateExecute();
                if (status != 0)
                    return status;
            }
            ConfigInitCommand configInit;
            configInit.context = &configContext;
            configInit.allowPrompt = true;
            
            auto r = configInit.fullyInitializeConfig();
            if (r.someConfigValuesAreMissing)
                return 1;

            configContext.saveJSON(r.config);
            Config config = mapJSON!Config(r.config);

            // Save it, there's no reason to load it again for this session.
            context._config = nullable(config);
        }

        {
            // import std.file;
            // DirEntry assetsFolder = buildPath(context.unityProjectDirectory, "Assets");

            int status = context.runUnityMethod(UnityMethod.RestoreSolutionFiles);
            if (status != 0)
            {
                context.logger.error("Could not restore Unity solution solution files."
                    ~ " I'd guess it's because it tried to use a function from the VS plugin, but VS was not installed. FIXME.");
                return status;
            }
        }

        {
            // TODO: replace with libgit2, but that requires a lot of work, because the bindings are broken,
            // and making ones manually requires adding imports manually.
            
            // restore the meta files
            import std.process : spawnProcess;
            int status = spawnProcess(["git", "restore", "Zayats/Assets/Source/**/*.cs.meta"]).wait;
            if (status != 0)
                return status;
        }

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

        // We can now allow ourselves to use read the solution files,
        // instead of scanning the directories manually!
        // TODO: make use of that.
        {
            KariRun run;
            run.context = &kariContext;
            auto status = run.onExecute();
            if (status != 0)
                return status;
        }

        // We don't use NuGetForUnity, instead,
        // we restore separately and then copy the required files.
        {
            NugetContext nuget;
            nuget.context = context;
            int status = nuget.onExecute();
            if (status != 0)
                return status;
        }

        {
            MagicOnionContext magicOnion;
            magicOnion.restoreTools = true;
            int status = magicOnion.onExecute();
            if (status != 0)
                return status;
        }

        return 0;
    }
}

@Command("config", "Configuration commands")
@(Subcommands!(ConfigInitCommand))
struct ConfigContext
{
    @ParentCommand
    Context* context;
    alias context this;
    
    int onIntermediateExecute()
    {
        return 0;
    }
    
    void saveJSON(JSONValue[string] config)
    {
        import std.json;
        import std.file : write;
        bool pretty = true;
        JSONValue root = JSONValue(config);
        const jsonString = toJSON(root, pretty);
        write(configurationPath, jsonString);
    }

    Nullable!(JSONValue[string]) readJSON()
    {
        import std.file : exists, readText;
        import std.json;

        if (exists(context.configurationPath))
        {
            const text = readText(context.configurationPath);
            auto json = parseJSON(text);
            return nullable(json.object);
        }
        return typeof(return).init;
    }
}

struct Config
{
    string fullUnityEditorPath;
}

import std.json : JSONValue;

T mapJSON(T)(JSONValue[string] json)
{
    T result;
    JSONValue* value;
    static foreach (field; T.tupleof)
    {
        value = __traits(identifier, field) in json;
        if (value)
            __traits(child, result, field) = value.get!(typeof(field));
    }
    return result;
}



@Command("init", "Initialize the configuration file, by prompting the user or using the default configuration")
struct ConfigInitCommand
{
    @ParentCommand
    ConfigContext* context;

    @("Whether to prompt the user for input in the case when a configuration value hasn't been found.")
    bool allowPrompt = true;

    // TODO: maybe add a "source from file" option.

    int onExecute()
    {
        auto r = fullyInitializeConfig();
        if (r.someConfigValuesAreMissing)
            return 1;
        context.saveJSON(r.config);
        return 0;
    }

    private static struct Result
    {
        JSONValue[string] config;
        bool someConfigValuesAreMissing;
    }

    Result fullyInitializeConfig()
    {
        import std.json;
        import std.file;
        
        JSONValue[string] config;
        {
            auto r = context.readJSON();
            if (!r.isNull)
                config = r.get();
        }

        bool changedAnything = false;
        bool skippedAnything = false;

        void maybeSetValue(T)(string name, Nullable!T value)
        {
            if (value.isNull)
            {
                skippedAnything = true;
            }
            else
            {
                config[name] = JSONValue(value.get());
                changedAnything = true;
            }
        }

        if ("fullUnityEditorPath" !in config)
        {
            string projectVersionPath = buildPath(context.unityProjectDirectory, "ProjectSettings", "ProjectVersion.txt");
            const key = "m_EditorVersion:";
            import std.algorithm;
            import std.range;
            import std.string;
            auto projectVersion = File(projectVersionPath)
                .byLine
                .map!(a => a.stripLeft)
                .filter!(a => a.startsWith(key))
                .front
                .drop(key.length)
                .strip
                .idup;
            context.logger.log("The project unity version is ", projectVersion);
            
            // https://support.unity.com/hc/en-us/articles/4402520309908-How-do-I-add-a-version-of-Unity-that-does-not-appear-in-the-Hub-installs-window-
            static string getDefaultInstallLocation()
            {
                version(Windows)
                    return `C:\Program Files\Unity\Hub\Editor`;
                else version(linux)
                    return `~/Unity/Hub/Editor`;
                else version(OSX)
                    return `/Applications/Unity/Hub/Editor`;
                else
                    return null;
            }

            string unityEditorName = "Unity".exe;

            Nullable!string getUnityPath()
            {
                enum null_ = typeof(return).init;

                string defaultInstallLocation = getDefaultInstallLocation();
                if (defaultInstallLocation !is null)
                {
                    const path = buildPath(defaultInstallLocation, projectVersion, "Editor", unityEditorName);
                    if (exists(path))
                        return nullable(path);
                    writeln("The default path does not include the required Unity Editor version.");
                }

                // maybe try getting the editor path from the registry on windows?

                if (!allowPrompt)
                    return null_;

                char[] buffer;
                while (true)
                {
                    writeln("Please enter the path to the Unity Editor executable (type \"skip\" to skip): ");

                    size_t numCharsRead = readln(buffer);
                    if (numCharsRead == 0)
                        return null_;
                    char[] input = buffer[0 .. numCharsRead].strip;
                    if (input == "skip" || input == "s")
                        return null_;
                        
                    const p = absolutePath(input.idup, context.projectDirectory);
                    if (!exists(p))
                    {
                        writeln("The file or directory ", p, " does not exist.");
                        continue;
                    }
                    
                    if (isDir(p))
                    {
                        const unityPath = buildPath(input, unityEditorName);
                        if (!exists(unityPath))
                        {
                            writeln("The unity executable ", unityPath, " does not exist in directory.");
                            continue;
                        }
                        return nullable(unityPath);
                    }

                    return nullable(p);
                }
            }

            maybeSetValue("fullUnityEditorPath", getUnityPath());
        }

        return Result(config, skippedAnything);
    }
}

@Command("unity", "Stuff related to Unity Editor")
struct UnityContext
{
    @ParentCommand
    Context* context;
    alias context this;

    enum Action
    {
        none,
        open,
        nugetRestore,
        restoreSolutionFiles,
    }
    @("Which action to execute?")
    @(ArgConfig.positional)
    Action action;

    int onExecute()
    {
        switch (action)
        {
            default:
                return 0;
            case Action.open:
            {
                openUnity(context.unityProjectDirectory, context.config.fullUnityEditorPath);
                return 0;
            }
            case Action.nugetRestore:
                return context.runUnityMethod(UnityMethod.NugetRestore);
            case Action.restoreSolutionFiles:
                return context.runUnityMethod(UnityMethod.RestoreSolutionFiles);
        }
    }
}

auto openUnity(Path unityProjectDirectory, Path unityEditorPath)
{
    import std.array;
    auto args = staticArray([
        unityEditorPath,
        "-projectPath", unityProjectDirectory,
    ]);
    return spawnProcess2(args[], unityProjectDirectory);
}

enum UnityMethod : string
{
    NugetRestore = "NugetForUnity.NugetHelper.Restore",
    RestoreSolutionFiles = "UnityEditor.SyncVS.SyncSolution",
}

int executeUnityMethod(Path unityEditorPath, Path unityProjectDirectory, string method)
{
    import std.array;
    auto args = staticArray([
        unityEditorPath,
        "-quit",
        "-batchmode",
        "-projectPath", unityProjectDirectory,
        "-executeMethod", method,
        "-nographics",
        "-ignoreCompilerErrors",
    ]);
    return spawnProcess2(args[], unityProjectDirectory).wait;
}

@Command("magic-onion", "Runs magic onion on the unity project")
struct MagicOnionContext
{
    @ParentCommand
    Context* context;
    alias context this;

    @("Whether to call `dotnet tool restore` prior to running the tool.")
    @(ArgConfig.parseAsFlag)
    bool restoreTools;

    int onExecute()
    {
        int status;

        if (restoreTools)
        {
            status = spawnProcess2(["dotnet", "tool", "restore"], context.projectDirectory).wait;
            if (status != 0)
                return status;
        }

        import std.file : exists;
        Path csprojPath = buildPath(context.unityProjectDirectory, "Zayats.Unity.MagicOnion.csproj");
        if (!exists(csprojPath))
        {
            context.logger.error("The project file at ", csprojPath, " has not been found."
                ~ " You must first regenerate the project files.");
            return 1;
        }
        
        // TODO: make a kari plugin for both.
        import common.tools;
        auto mo = magicOnion();
        mo.unuseUnityAttr = true;
        mo.input = csprojPath;
        mo.output = buildPath(mo.input, "MessagePackGenerated");
        mo.messagePackGeneratedNamespace = "Zayats.Unity.MagicOnion.Generated";
        mo.namespace = mo.messagePackGeneratedNamespace;
        mo.conditionalSymbols = ["UNITY_EDITOR"];

        status = spawnProcess(mo, context.unityProjectDirectory).wait;
        if (status != 0)
            return status;

        auto mp = messagePack();
        copyPropertiesWithSameName(mo, mp);
        mp.resolverName = "Resolver";

        status = spawnProcess(mp, context.unityProjectDirectory).wait;
        if (status != 0)
            return status;

        return 0;
    }
}
