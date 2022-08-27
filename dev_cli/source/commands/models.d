module commands.models;

import jcli;

import commands.context;
import common;

import std.path;
import std.stdio;
import std.process : wait;

@Command("models", "Has to do with 3d model operations.")
@(Subcommands!Package)
struct ModelsContext
{
    @ParentCommand
    Context* context;
    alias context this;

    string modelsPath;

    void onIntermediateExecute()
    {
        modelsPath = buildPath(context.projectDirectory, "3dmodels");
    }
}

@Command("package", "Extracts the output model files into a single folder.")
struct Package
{
    @ParentCommand
    ModelsContext* context;

    @ArgNamed
    bool copyToUnity = true;

    @("Overwrite existing")
    @(ArgConfig.parseAsFlag)
    bool force = false;

    int onExecute()
    {
        import std.file;
        import std.algorithm;

        int errorCount = 0;

        string packageOutputPath = buildPath(context.buildDirectory, "3dmodels_package");
        string unityOutputPath = buildPath(context.unityProjectDirectory, "Assets", "Game", "Content", "3dmodels");

        if (!exists(packageOutputPath))
            mkdirRecurse(packageOutputPath);

        if (copyToUnity && !exists(unityOutputPath))
            mkdirRecurse(unityOutputPath);
        
        foreach (string folder; dirEntries(context.modelsPath, SpanMode.shallow).filter!(a => a.isDir))
        {
            const folderName = baseName(folder);
            const fbxPath = buildPath(folder, "out.fbx");

            if (!fbxPath.exists)
            {
                writeln("Not found file ", fbxPath, ". Export it manually in Blender.");
                errorCount++;
                continue;
            }
            const lastModifiedSource = timeLastModified(fbxPath);

            void exportToPackageFolder(string packageFolder)
            {
                const outFolder = buildPath(packageFolder, folderName);
                if (!exists(outFolder))
                    mkdir(outFolder);

                const outPath = buildPath(outFolder, folderName).setExtension(".fbx");

                void doCopy()
                {
                    copy(fbxPath, outPath);
                }

                if (!outPath.exists || force)
                {
                    doCopy();
                    return;
                }

                const lastModifiedDest = timeLastModified(outPath);

                if (lastModifiedSource > lastModifiedDest)
                    doCopy();
            }

            exportToPackageFolder(packageOutputPath);
            if (copyToUnity)
                exportToPackageFolder(unityOutputPath);
        }

        return errorCount;
    }
}
