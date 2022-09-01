module commands.nuget;

import jcli;

import commands.context;
import common;
import common.convenience : exe;

import std.path;
import std.stdio;
import std.process : wait;
import std.file : exists, mkdir, mkdirRecurse;
import std.net.curl;

import acd.versions;
mixin Versions;

@Command("nuget", "Manage nuget packages in unity.")
struct NugetContext
{
    @ParentCommand
    Context* context;
    alias context this;

    @("NuGet path")
    string nugetPath;

    @ArgRaw
    string[] nugetArgs;

    @("Download the tool again even if it already exists")
    @(ArgConfig.parseAsFlag)
    bool forceDownload;

    @("Copy the files again even if the folders exist")
    @(ArgConfig.parseAsFlag)
    bool forceCopy;

    string nugetExecutablePath;

    int onExecute()
    {
        if (nugetPath == null)
            nugetPath = context.toolsDirectory;

        string[1] fileNames = [ "NuGet.exe" ];
        nugetExecutablePath = buildPath(nugetPath, fileNames[0]);

        if (forceDownload || !exists(nugetExecutablePath))
        {
            const toolsVersion = "3.4.3";
            const toolsURL = "https://www.nuget.org/api/v2/package/NuGet.exe/" ~ toolsVersion;
            string archivePath = buildPath(context.tempDirectory, "nuget.zip");
            context.download(toolsURL, archivePath);

            import std.file : read;
            import std.zip;
            auto zipBytes = read(archivePath);
            auto zip = new ZipArchive(zipBytes);

            string folder = "build/native/";
            if (!unpackFiles(context.logger, zip, folder, fileNames, nugetPath))
                return 1;
        }

        const cwd = buildPath(context.unityProjectDirectory, "NugetPackages");

        if (nugetArgs.length > 0)
        {
            string[] args;
            args.length = nugetArgs.length + 1;
            args[0] = nugetExecutablePath;
            args[1 .. $] = nugetArgs[];
            
            return spawnProcess2(args, cwd).wait;
        }

        // if (nugetArgs.length == 0)
        {
            int status = spawnProcess2([nugetExecutablePath, "restore"], cwd).wait;
            if (status != 0)
                return status;

            const unityPackagesFolder = buildNormalizedPath(context.unityProjectDirectory, "Assets", "NugetPackages");
            const nugetPackagesFolder = buildNormalizedPath(context.unityProjectDirectory, "NugetPackages");

            import std.file : isDir, dirEntries, SpanMode, DirEntry, copy;
            import std.path;
            import std.algorithm;
            import std.string : endsWith, indexOf;
            import std.typecons : No;

            foreach (DirEntry libEntry; dirEntries(nugetPackagesFolder, SpanMode.shallow).filter!(d => d.isDir))
            {
                // name.of.package.14.15 -> name.of.package
                string sliceUntilVersion(string src)
                {
                    long index = 0;

                    while (src.length > index)
                    {
                        long nextIndexOfDot = src[index .. $].indexOf('.');
                        if (nextIndexOfDot == -1)
                            return src;

                        nextIndexOfDot += index;

                        if (src[index .. nextIndexOfDot].all!(ch => ch >= '0' && ch <= '9'))
                        {
                            return index == 0
                                ? ""
                                : src[0 .. index - 1];
                        }
                        
                        index = nextIndexOfDot + 1;
                    }

                    return src;
                }

                // Let's not do anything to this yet, might want to migrate with a different command instead.
                string folderName = baseName(libEntry.name);
                string packageName = sliceUntilVersion(folderName);
                context.logger.log("Processing ", packageName);

                const unityPackagePath = buildPath(unityPackagesFolder, folderName);
                if (!exists(unityPackagePath))
                    mkdir(unityPackagePath);

                const string[2] ignoredExtensions = [ ".p7s", ".nupkg" ];

                static struct Entry
                {
                    string relativePath;
                    DirEntry libPath;
                    string unityPath;
                }
                auto iterateEntries(T)(string baseUnityPath, T path)
                {
                    static if (is(T == string))
                        DirEntry entry = DirEntry(path);
                    else
                        DirEntry entry = path;

                    return dirEntries(entry, SpanMode.breadth)
                        .map!((p)
                        {
                            const relative = p.name[entry.name.length + 1 .. $];
                            const unityPath = buildPath(baseUnityPath, relative);
                            return Entry(relative, p, unityPath);
                        })
                        .filter!(p => !exists(p.unityPath)
                            || DirEntry(p.unityPath).timeLastModified < p.libPath.timeLastModified);
                }
                
                foreach (Entry p; iterateEntries(unityPackagePath, libEntry))
                {
                    if (pathSplitter(p.relativePath).front == "lib")
                        continue;

                    if (p.libPath.isDir)
                    {
                        if (!exists(p.unityPath))
                            mkdir(p.unityPath);
                    }
                    else
                    {
                        if (ignoredExtensions[].any!(e => p.relativePath.endsWith(e)))
                            continue;
                        copy(p.libPath, p.unityPath);
                    }
                }

                const libFolderPath = buildPath(libEntry.name, "lib");
                const targetFramework = "netstandard2.0";
                if (exists(libFolderPath))
                {
                    const libUnityPath = buildPath(unityPackagePath, "lib");
                    if (!exists(libUnityPath))
                        mkdir(libUnityPath);

                    const dllsPath = buildPath(libFolderPath, targetFramework);
                    if (!exists(dllsPath))
                    {
                        // It should be possible to take one of multiple frameworks, like either 2.0 or 2.1
                        context.logger.error("Cannot restore package ", libEntry.name, " without a ", targetFramework, " framework support (FIXME).");
                    }
                    else
                    {
                        const frameworkUnityPath = buildPath(libUnityPath, targetFramework);
                        if (!exists(frameworkUnityPath))
                            mkdir(frameworkUnityPath);

                        foreach (Entry p; iterateEntries(frameworkUnityPath, dllsPath))
                        {
                            if (p.libPath.isDir)
                            {
                                if (!exists(p.unityPath))
                                    mkdir(p.unityPath);
                            }
                            else
                            {
                                copy(p.libPath, p.unityPath);
                            }
                        }
                    }
                }
            }
        }

        return 0;
    }
}
