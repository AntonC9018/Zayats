module commands.proto;

import jcli;

import commands.context;
import common;
import common.convenience : exe;

import std.path;
import std.stdio;
import std.process : wait;
import std.file : exists, mkdir, mkdirRecurse;

import acd.versions;
mixin Versions;


@Command("protoc", "Protobuf compiler.")
struct ProtoContext
{
    @ParentCommand
    Context* context;
    alias context this;

    @("Protoc path")
    string protocPath;

    @ArgRaw
    string[] protocArgs;

    @("Download the proto tool again even if it already exists")
    bool force;

    string protocExecutablePath;
    string protocGRPCPluginPath;

    int onExecute()
    {
        if (protocPath == null)
        {
            protocPath = buildPath(context.toolsDirectory, "protoc");
            if (!exists(protocPath))
                mkdirRecurse(protocPath);
        }

        string[2] protocFileNames = [ "protoc".exe, "grpc_csharp_plugin".exe ];
        protocExecutablePath = buildPath(protocPath, protocFileNames[0]);
        protocGRPCPluginPath = buildPath(protocPath, protocFileNames[1]);

        if (force
            || !exists(protocExecutablePath)
            || !exists(protocGRPCPluginPath))
        {
            const toolsVersion = "2.48.0";
            const toolsURL = "https://www.nuget.org/api/v2/package/Grpc.Tools/" ~ toolsVersion;
            string archivePath = buildPath(context.tempDirectory, "protoc.zip");
            context.download(toolsURL, archivePath);

            import std.file : read;
            import std.zip;
            auto zipBytes = read(archivePath);
            auto zip = new ZipArchive(zipBytes);

            string getFolderName()
            {
                static if (Version.Windows && Version.X86)
                    return "windows_x86";
                else static if (Version.Windows && Version.X86_64)
                    return "windows_x64";
                else static if (Version.linux)
                {
                    static if (Version.ARM)
                        return "linux_arm64";
                    else static if (Version.X86_64)
                        return "linux_x64";
                    else static if (Version.X86)
                        return "linux_x86";
                    else
                        static assert(0, "Unsupported platform (probably)");
                }
                else static if (Version.OSX && Version.X86)
                    return "macosx_x86";
                else static if (Version.OSX && Version.X86_64)
                    return "macosx_x64";
                else
                    static assert(0, "Unsupported platform (probably)");
            }

            string folder = "tools/" ~ getFolderName() ~ "/";
            if (!unpackFiles(context.logger, zip, folder, protocFileNames, protocPath))
                return 1;
        }

        auto inputPath = buildPath(context.projectDirectory, "Server", "ProtobufAPI");
        auto outputPath = buildPath(inputPath, "Generated");
        if (!exists(outputPath))
            mkdirRecurse(outputPath);

        {
            import common.tools;
            import std.file : dirEntries, SpanMode, isFile;
            import std.algorithm;
            import std.array;

            Protoc p = protocTool();
            p.toolPath = [protocExecutablePath];
            p.protoFiles = dirEntries(inputPath, SpanMode.breadth)
                .filter!(d => isFile(d) && d.extension == ".proto")
                .map!(d => cast(string) d)
                .array;
            p.includePaths = [inputPath];
            p.csharpOut = outputPath;
            p.plugins ~= Protoc.Plugin("protoc-gen-grpc", protocGRPCPluginPath);
            p.grpcOut = outputPath;

            return startProcess(p).wait;
        }
    }
}
