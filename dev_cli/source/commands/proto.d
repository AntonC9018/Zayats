module commands.proto;

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

enum DownloadToolOption
{
    libcurl_dll,
    curl,
}

@Command("protoc", "Protobuf compiler.")
struct ProtoContext
{
    @ParentCommand
    Context* context;
    alias context this;

    @("Protoc path")
    string protocPath;

    @("Protoc executable path")
    string protocExecutablePath;

    @ArgRaw
    string[] protocArgs;

    @("What to do http requests with")
    auto dowloadTool = DownloadToolOption.curl;

    int onExecute()
    {
        if (protocPath == null)
        {
            protocPath = buildPath(context.toolsDirectory, "protoc");
            if (!exists(protocPath))
                mkdirRecurse(protocPath);
        }
        
        if (protocExecutablePath == null)
            protocExecutablePath = buildPath(protocPath, "bin", exe!"protoc");

        if (!exists(protocExecutablePath))
        {
            string protocVersion = "21.5";
            string platform;
            static if (Version.Win64)
                platform = "win64";
            else static if (Version.Win32)
                platform = "win32";
            else static if (Version.linux)
            {
                static if (Version.X86_64)
                    platform = "linux-x86_64";
                else
                    platform = "linux-x86_32";
            }
            else
            {
                static assert(false, "modify the script to accomodate your platform.");    
            }

            enum releases = "https://github.com/protocolbuffers/protobuf/releases/download";
            string url = releases ~ "/v" ~ protocVersion ~ "/protoc-" ~ protocVersion ~ "-" ~ platform ~ ".zip";
            string archivePath = buildPath(context.tempDirectory, "protoc.zip");

            if (dowloadTool == DownloadToolOption.curl)
            {
                import common.tools;
                Curl c = curlTool();
                c.url = url;
                c.followRedirects = true;
                c.outputFile = archivePath;
                execute(c);
            }
            else
            {
                download(url, archivePath);
            }
            unzip(archivePath, protocPath);
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
            p.toolPath = protocExecutablePath;
            p.protoFiles = dirEntries(inputPath, SpanMode.breadth)
                .filter!(d => isFile(d) && d.extension == ".proto")
                .map!(d => cast(string) d)
                .array;
            p.includePaths = [inputPath];
            p.csharpOut = outputPath;

            auto r = execute(p);
            writeln(r.output);
        }

        return 0;
    }
}
