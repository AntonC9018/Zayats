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

        // We don't use NuGetForUnity, instead,
        // we restore separately and then copy the required files.
        {
            NugetContext nuget;
            nuget.context = context;
            int status = nuget.onExecute();
            if (status != 0)
            {
                context.logger.error("Nuget restore failed.");
                return status;
            }
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

            // Restore the meta files.
            // When opening unity in batch mode, without the generated files being there, it deletes the associated meta files too.
            // There might be an undocumented flag, but I asked on Discord and googled to no avail.
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

        {
            MagicOnionContext magicOnion;
            magicOnion.context = context;
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
    string msbuildFolderPath;
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

        char[] inputBufferCache;

        enum PromptResultKind
        {
            EnteredPath,
            Skip,
        }

        struct PromptResult
        {
            PromptResultKind kind;
            string path;
        }

        PromptResult readPromptDirectory(string prompt)
        {
            while (true)
            {
                import std.string;
                
                writeln(prompt, " (type \"skip\" to skip)");

                size_t numCharsRead = readln(inputBufferCache);
                if (numCharsRead == 0)
                    continue;
                char[] input = inputBufferCache[0 .. numCharsRead].strip;
                if (input == "skip" || input == "s")
                    return PromptResult(PromptResultKind.Skip, null);

                string inputString = input.idup;
                const p = absolutePath(inputString, context.projectDirectory);
                if (!exists(p))
                {
                    writeln("The file or directory ", p, " does not exist.");
                    continue;
                }
                return PromptResult(PromptResultKind.EnteredPath, inputString);
            }
        }

        PromptResult readPromptFile(string prompt, string fileName)
        {
            while (true)
            {
                auto input = readPromptDirectory(prompt);
                if (input.kind == PromptResultKind.Skip)
                    return input;

                if (isDir(input.path))
                {
                    const filePath = buildPath(input.path, fileName);
                    if (!exists(filePath))
                    {
                        writeln("The file named ", filePath, " does not exist in given directory.");
                        continue;
                    }
                    input.path = filePath;
                }

                return input;
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

                auto input = readPromptFile("Please enter the path to the Unity Editor executable ", unityEditorName);
                return (input.kind == PromptResultKind.Skip) ? null_ : nullable(input.path);
            }

            maybeSetValue("fullUnityEditorPath", getUnityPath());
        }

        if ("msbuildFolderPath" !in config)
        {
            Nullable!string getMSBuildFodlerPath()
            {
                enum null_ = typeof(return).init;
                string GetMSBuildPath_ProjectPath = buildPath(context.projectDirectory, "stuff", "GetMSBuildPath");
                {
                    int status = spawnProcess2(["dotnet", "publish", "--configuration", "Release"], GetMSBuildPath_ProjectPath).wait;
                    if (status != 0)
                    {
                        context.logger.error("Could not compile GetMSBuildPath");
                        return null_;
                    }
                }
                {
                    auto r = execute2([
                            "dotnet", "run",
                            buildPath(GetMSBuildPath_ProjectPath, "bin", "release", "publish", "GetMSBuildPath.dll")],
                        GetMSBuildPath_ProjectPath);
                    if (r.status != 0)
                    {
                        context.logger.error("GetMSBuildPath execution error.");
                        return null_;
                    }

                    import std.algorithm;
                    import std.range;
                    import std.string;
                    auto lines = r.output.splitter("\n");
                    return lines.empty ? null_ : nullable(lines.front.strip);
                }
            }

            maybeSetValue("msbuildFolderPath", getMSBuildFodlerPath());
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
        Path csprojPath = buildPath(context.unityProjectDirectory, "Zayats.Net.Shared.csproj");
        if (!exists(csprojPath))
        {
            context.logger.error("The project file at ", csprojPath, " has not been found."
                ~ " You must first regenerate the project files.");
            return 1;
        }
        Path magicOnionProjectPath = buildPath(context.unityProjectDirectory, "Assets", "Source", "MagicOnion", "Runtime");


        // TODO: make a kari plugin for both.
        import common.tools;
        auto mo = magicOnion();
        mo.unuseUnityAttr = true;
        mo.input = csprojPath;
        mo.output = buildPath(magicOnionProjectPath, "MessagePackGenerated");
        mo.messagePackGeneratedNamespace = "Zayats.Unity.Net.Generated";
        mo.namespace = mo.messagePackGeneratedNamespace;
        mo.conditionalSymbols = ["UNITY_EDITOR"];

        status = spawnProcess(mo, context.unityProjectDirectory).wait;
        if (status != 0)
            return status;

        auto mp = messagePack();
        copyPropertiesWithSameName(mo, mp);
        mp.resolverName = "MyResolver";

        status = spawnProcess(mp, context.unityProjectDirectory).wait;
        if (status != 0)
            return status;

        return 0;
    }
}

@Command("create-sln")
struct CreateSolution
{
    @ParentCommand
    Context* context;
    alias context this;

    @(ArgConfig.optional | ArgConfig.parseAsFlag)
    {
        @ArgNamed("kari", "Whether to include kari into the generated solution.")
        bool includeKari;

        @ArgNamed("kari-plugins", "Whether to include kari plugins into the generated solution.")
        bool includeKariPlugins;

        @ArgNamed("server", "Whether to include all the server code into the generated solution.")
        bool includeServer;

        @ArgNamed("unity", "Whether to include unity into the generated solution.")
        bool includeUnity;
    }

    int onExecute()
    {
        import std.file;

        if (context.config.msbuildFolderPath == null)
        {
            context.logger.error("Please, first set the msbuild path with `dev config init`.");
            return 1;
        }

        static struct SolutionInfo
        {
            string folder;
            string fileName;
            string fullPath;

            this(string folder, string fileName)
            {
                this.folder = folder;
                this.fileName = fileName;
                this.fullPath = buildPath(folder, fileName);
            }
        }

        int makeSolutionFileFromAllCsProjInFolder(in SolutionInfo solution)
        {
            if (exists(solution.fullPath))
                std.file.remove(solution.fullPath);

            {
                int status = spawnProcess2(["dotnet", "new", "sln", solution.fileName], solution.folder).wait;
                if (status != 0)
                    return status;
            }

            foreach (p; dirEntries(solution.folder, "*.csproj", SpanMode.depth))
            {
                string relativeProjectPath = relativePath(p, solution.folder);
                int status = spawnProcess2(["dotnet", "sln", solution.fileName, "add", relativeProjectPath], solution.folder).wait;
                if (status != 0)
                    return status;
            }

            return 0;
        }

        const server = SolutionInfo(buildPath(context.projectDirectory, "Server"), "Server.sln");
        if (includeServer)
        {
            // int status = makeSolutionFileFromAllCsProjInFolder(server);
            // if (status != 0)
            //     return status;
        }

        const kariPlugins = SolutionInfo(buildPath(context.projectDirectory, "kari_stuff", "plugins"), "Plugins.sln");
        if (includeKariPlugins)
        {
            // int status = makeSolutionFileFromAllCsProjInFolder(kariPlugins);
            // if (status != 0)
            //     return status;
        }

        const unity = SolutionInfo(context.unityProjectDirectory, "Zayats.sln");
        const kari = SolutionInfo(buildPath(context.projectDirectory, "kari_stuff", "Kari"), "Kari.sln");

        const solutionName = "ZayatsSolution.sln";
        const solutionFullPath = buildPath(context.projectDirectory, solutionName);
        if (exists(solutionFullPath))
            remove(solutionFullPath);

        {
            import std.array;
            import std.process;
            import std.algorithm;

            auto args = appender(["dotnet", "slngen"]);

            if (includeKari)
            {
                string baseDir = buildPath(kari.folder, "source");

                foreach (dir; dirEntries(baseDir, SpanMode.shallow).filter!(d => d.isDir))
                {
                    // these don't compile anyway.
                    if (["dotnet_new_templates", "Kari.Plugin.Sdk"].canFind(dir[baseDir.length + 1 .. $]))
                        continue;
                    
                    foreach (string csproj; dirEntries(dir, "*.csproj", SpanMode.depth))
                        args ~= csproj;
                }
            }
            if (includeKariPlugins)
            {
                foreach (string csproj; dirEntries(kariPlugins.folder, "*.csproj", SpanMode.depth))
                    args ~= csproj;
            }
            if (includeServer)
            {
                foreach (string csproj; dirEntries(server.folder, "*.csproj", SpanMode.depth))
                    args ~= csproj;
            }
            if (includeUnity)
                args ~= buildPath(unity.folder, "Assembly-CSharp-Editor.csproj");
            
            args ~= [
                "--launch", "false",
                "--solutiondir", context.projectDirectory,
                "--solutionfile", solutionName,
            ];

            string[string] envs;
            string p = environment.get("PATH");
            if (p !is null)
                p ~= ";";
            envs["PATH"] = p ~ context.config.msbuildFolderPath;

            int status = spawnProcessEnv(args[], envs, context.projectDirectory).wait;
            if (status != 0)
                return status;
        }

        return 0;
    }
}
