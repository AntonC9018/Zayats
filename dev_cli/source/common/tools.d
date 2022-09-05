module common.tools;
import std.conv : to;
import common.convenience;

struct Tool
{
    const(string)[] toolPath;
    string[] moreArguments;
}

struct Curl
{
    Tool tool;
    alias tool this;
    
    string url;

    //  -d, --data <data>          HTTP POST data
    string httpPostData;
    
    // -f, --fail                 Fail fast with no output on HTTP errors
    bool failFast;
    
    // -h, --help <category>      Get help for commands
    bool help;

    // -i, --include              Include protocol response headers in the output
    string[] includeHeaders;
    
    // -o, --output <file>        Write to file instead of stdout
    string outputFile;
    
    // -O, --remote-name          Write output to a file named as the remote file
    string remoteName;
    
    // -s, --silent               Silent mode
    bool silent;
    
    // -T, --upload-file <file>   Transfer local FILE to destination
    string uploadFile;

    // -u, --user <user:password> Server user and password
    string user;
    string password;

    // -A, --user-agent <name>    Send User-Agent <name> to server
    string userAgent;
    
    // -v, --verbose              Make the operation more talkative
    bool verbose;

    bool followRedirects;
}

Curl curlTool()
{
    Curl c;
    c.toolPath = ["curl"];
    return c;
}

void maybeAdd(ref string[] args, string option, string value)
{
    if (value != null)
    {
        args ~= option;
        args ~= value;
    }
}

void maybeAdd_Equal(ref string[] args, string option, string value)
{
    if (value != null)
        args ~= option ~ "=" ~ value;
}

void maybeAdd_EqualQuote(ref string[] args, string option, string value)
{
    if (value != null)
        args ~= option ~ "=" ~ quote(value);
}

void maybeAddFlag(ref string[] args, string option, bool value)
{
    if (value)
        args ~= option;
}

void maybeAddMany(ref string[] args, string option, const string[] values)
{
    foreach (v; values)
    {
        args ~= option;
        args ~= v;
    }
}

void maybeAddMany_OV(ref string[] args, string option, const string[] values)
{
    foreach (v; values)
        args ~= option ~ v;
}

void maybeAddMany_Equal(ref string[] args, string option, const string[] values)
{
    foreach (v; values)
        args ~= option ~ "=" ~ v;
}

void toolStartBuildingArguments(in Tool tool, ref string[] args)
{
    assert(tool.toolPath);
    args ~= tool.toolPath;
}

void toolEndBuildingArguments(in Tool tool, ref string[] args)
{
    args ~= tool.moreArguments;
}

string[] buildArgs(in Curl curl)
{
    string[] args;
    toolStartBuildingArguments(curl, args);

    args ~= curl.url;

    maybeAdd(args, "--data", curl.httpPostData);
    maybeAddFlag(args, "--fail", curl.failFast);
    maybeAddFlag(args, "--help", curl.help);
    maybeAddMany(args, "--include", curl.includeHeaders);
    maybeAdd(args, "--output", curl.outputFile);
    maybeAdd(args, "--remote-name", curl.remoteName);
    maybeAddFlag(args, "--silent", curl.silent);
    maybeAdd(args, "--upload-file", curl.uploadFile);
    if (curl.user)
    {
        args ~= "--user";
        args ~= curl.user ~ ":" ~ curl.password;
    }
    maybeAdd(args, "--user-agent", curl.userAgent);
    maybeAddFlag(args, "--verbose", curl.verbose);
    maybeAddFlag(args, "--location", curl.followRedirects);

    toolEndBuildingArguments(curl, args);
    return args;
}

import std.file : getcwd;

auto execute(ToolT)(in ToolT tool, string workingDirectory = getcwd()) 
{
    return execute2(buildArgs(tool), workingDirectory);
}

auto startProcess(ToolT)(in ToolT tool, string workingDirectory = getcwd()) 
{
    return spawnProcess2(buildArgs(tool), workingDirectory);
}

auto spawnProcess(ToolT)(in ToolT tool, string workingDirectory = getcwd())
{
    return startProcess(tool, workingDirectory);
}


// https://helpmanual.io/help/protoc/
struct Protoc
{
    Tool tool;
    alias tool this;

    string[] protoFiles;
    string[] includePaths;
    bool version_;
    bool help;
    string encode;
    bool deterministicInput;
    string decode;
    bool decodeRaw;
    string fileDescriptorSetIn;
    string fileDescriptorSetOut;
    bool includeImports;
    bool includeSourceInfo;
    string dependencyOut;

    enum ErrorFormat
    {
        none, gcc, msvc
    }
    ErrorFormat errorFormat;
    bool printFreeFieldNumbers;

    struct Plugin
    {
        string name;
        string path = "";
    }
    Plugin[] plugins;
    string cppOut;
    string javaOut;
    string pythonOut;
    string csharpOut;
    string grpcOut;
}

Protoc protocTool()
{
    Protoc p;
    p.toolPath = ["protoc"];
    return p;
}

string[] buildArgs(in Protoc protoc)
{
    string[] args;
    toolStartBuildingArguments(protoc, args);
    
    //     Usage: protoc [OPTION] PROTO_FILES
    // Parse PROTO_FILES and generate output based on the options given:
    //   -IPATH, --proto_path=PATH   Specify the directory in which to search for
    //                               imports.  May be specified multiple times;
    //                               directories will be searched in order.  If not
    //                               given, the current working directory is used.
    //                               If not found in any of the these directories,
    //                               the --descriptor_set_in descriptors will be
    //                               checked for required proto file.
    maybeAddMany_OV(args, "-I", protoc.includePaths);
    //   --version                   Show version info and exit.
    maybeAddFlag(args, "--version", protoc.version_);
    //   -h, --help                  Show this text and exit.
    maybeAddFlag(args, "--help", protoc.help);
    //   --encode=MESSAGE_TYPE       Read a text-format message of the given type
    //                               from standard input and write it in binary
    //                               to standard output.  The message type must
    //                               be defined in PROTO_FILES or their imports.
    maybeAdd(args, "--encode", protoc.encode);
    //   --deterministic_output      When using --encode, ensure map fields are
    //                               deterministically ordered. Note that this order
    //                               is not canonical, and changes across builds or
    //                               releases of protoc.
    maybeAddFlag(args, "--deterministic_output", protoc.deterministicInput);
    //   --decode=MESSAGE_TYPE       Read a binary message of the given type from
    //                               standard input and write it in text format
    //                               to standard output.  The message type must
    //                               be defined in PROTO_FILES or their imports.
    maybeAdd(args, "--decode", protoc.decode);
    //   --decode_raw                Read an arbitrary protocol message from
    //                               standard input and write the raw tag/value
    //                               pairs in text format to standard output.  No
    //                               PROTO_FILES should be given when using this
    //                               flag.
    maybeAddFlag(args, "--decodeRaw", protoc.decodeRaw);
    //   --descriptor_set_in=FILES   Specifies a delimited list of FILES
    //                               each containing a FileDescriptorSet (a
    //                               protocol buffer defined in descriptor.proto).
    //                               The FileDescriptor for each of the PROTO_FILES
    //                               provided will be loaded from these
    //                               FileDescriptorSets. If a FileDescriptor
    //                               appears multiple times, the first occurrence
    //                               will be used.
    maybeAdd(args, "--descriptor_set_in", protoc.fileDescriptorSetIn);
    //   -oFILE,                     Writes a FileDescriptorSet (a protocol buffer,
    //     --descriptor_set_out=FILE defined in descriptor.proto) containing all of
    //                               the input files to FILE.
    maybeAdd(args, "--descriptor_set_out", protoc.fileDescriptorSetOut);
    //   --include_imports           When using --descriptor_set_out, also include
    //                               all dependencies of the input files in the
    //                               set, so that the set is self-contained.
    maybeAddFlag(args, "--includeImports", protoc.includeImports);
    //   --include_source_info       When using --descriptor_set_out, do not strip
    //                               SourceCodeInfo from the FileDescriptorProto.
    //                               This results in vastly larger descriptors that
    //                               include information about the original
    //                               location of each decl in the source file as
    //                               well as surrounding comments.
    maybeAddFlag(args, "--include_source_info", protoc.includeSourceInfo);
    //   --dependency_out=FILE       Write a dependency output file in the format
    //                               expected by make. This writes the transitive
    //                               set of input file paths to FILE
    maybeAdd(args, "--dependency_out", protoc.dependencyOut);
    //   --error_format=FORMAT       Set the format in which to print errors.
    //                               FORMAT may be 'gcc' (the default) or 'msvs'
    //                               (Microsoft Visual Studio format).
    if (protoc.errorFormat)
        args ~= "--error_format=" ~ protoc.errorFormat.to!string;
    //   --fatal_warnings            Make warnings be fatal (similar to -Werr in
    //                               gcc). This flag will make protoc return
    //                               with a non-zero exit code if any warnings
    //                               are generated.
    // maybeAddFlag(args, "--fatal_warnings", protoc.fatalWarin
    //   --print_free_field_numbers  Print the free field numbers of the messages
    //                               defined in the given proto files. Groups share
    //                               the same field number space with the parent
    //                               message. Extension ranges are counted as
    //                               occupied fields numbers.
    maybeAddFlag(args, "--print_free_field_numbers", protoc.printFreeFieldNumbers);
    //   --plugin=EXECUTABLE         Specifies a plugin executable to use.
    //                               Normally, protoc searches the PATH for
    //                               plugins, but you may specify additional
    //                               executables not in the path using this flag.
    //                               Additionally, EXECUTABLE may be of the form
    //                               NAME=PATH, in which case the given plugin name
    //                               is mapped to the given executable even if
    //                               the executable's own name differs.
    foreach (p; protoc.plugins)
    {
        string a = p.name;
        if (p.path != "")
        {
            // I think I can't quote the path, because then it interprets it wrong.
            // Don't know what's going on exactly, but quoting it here makes it complain
            // that the path does not exist.
            // I haven't tested it with spaces in the path while unqoted, but it might
            // not be possible with the current state of things to do that.
            a ~= "=" ~ (p.path);
        }
        args ~= "--plugin";
        args ~= a;
    }
    //   --cpp_out=OUT_DIR           Generate C++ header and source.
    maybeAdd(args, "--cpp_out", protoc.cppOut);
    //   --csharp_out=OUT_DIR        Generate C# source file.
    maybeAdd(args, "--csharp_out", protoc.csharpOut);
    //   --java_out=OUT_DIR          Generate Java source file.
    maybeAdd(args, "--java_out", protoc.javaOut);
    //   --kotlin_out=OUT_DIR        Generate Kotlin file.
    //   --objc_out=OUT_DIR          Generate Objective-C header and source.
    //   --php_out=OUT_DIR           Generate PHP source file.
    //   --pyi_out=OUT_DIR           Generate python pyi stub.
    //   --python_out=OUT_DIR        Generate Python source file.
    maybeAdd(args, "--python_out", protoc.pythonOut);
    //   --ruby_out=OUT_DIR          Generate Ruby source file.
    
    maybeAdd(args, "--grpc_out", protoc.grpcOut);

    //   @<filename>                 Read options and filenames from file. If a
    //                               relative file path is specified, the file
    //                               will be searched in the working directory.
    //                               The --proto_path option will not affect how
    //                               this argument file is searched. Content of
    //                               the file will be expanded in the position of
    //                               @<filename> as in the argument list. Note
    //                               that shell expansion is not applied to the
    //                               content of the file (i.e., you cannot use
    //                               quotes, wildcards, escapes, commands, etc.).
    //                               Each line corresponds to a single argument,
    //                               even if it contains spaces.
    args ~= protoc.protoFiles;
    
    toolEndBuildingArguments(protoc, args);
    return args;
}

mixin template Getter(parentField, field)
{
    mixin("ref typeof(field) ", __traits(identifier, field), "() inout return "
        // return parent.field;
        ~ "{ return __tratis(child, __traits(child, this, parentField), field); }");
}

mixin template Getters(field)
{
    static foreach (f; typeof(field).tupleof)
        mixin Getter!(field, f);
}

struct DotnetBuild
{
    Tool tool;
    alias tool this;
    
    string path;

    bool noRestore;
    string configuration;
    string[string] msbuildProperties;
}

DotnetBuild dotnetBuild()
{
    DotnetBuild result;
    result.toolPath = ["dotnet"];
    return result;
}

string[] buildArgs(in DotnetBuild build)
{
    string[] args;
    toolStartBuildingArguments(build.tool, args);
    
    args ~= "build";
    args ~= build.path;

    maybeAddFlag(args, "--no-restore", build.noRestore);
    maybeAdd(args, "--configuration", build.configuration);

    foreach (key, value; build.msbuildProperties)
        args ~= ("/p:" ~ key ~ "=" ~ value);

    toolEndBuildingArguments(build.tool, args);
    return args;
}

void maybeAdd_Join(ref string[] args, string option, const(string)[] values, string joinWith = ",")
{
    if (values.length > 0)
    {
        args ~= option;
        import std.string : join;
        args ~= values.join(joinWith);
    }
}

struct Kari
{
    Tool tool;
    alias tool this;

    string configurationFile;
    string inputFolder;
    string[] pluginPaths;
    string pluginConfigFilePath;
    string generatedNamespaceSuffix;
    string[] conditionalSymbols;
    string rootNamespace;
    string[] pluginNames;
    string generatedName;
    string commonProjectName;
    string rootProjectName;
    string[] ignoredNames;
    string[] ignoredFullPaths;
    string[] whitelistGeneratedCodeForProjects;
    string[] whitelistAnalyzedProjects;
    string[] additionalAnnotationAssemblyPaths;
    string[] additionalAnnotationAssemblyNames;
    string gitignoreTemplate;
}

void addDefault(T)(ref string[] args, in T tool)
{
    static foreach (field; T.tupleof)
    {{
        auto value = __traits(child, tool, field); 
        enum option = "-" ~ __traits(identifier, field);

        static if (is(typeof(field) : string))
            maybeAdd(args, option, value);
        else static if (is(typeof(field) : string[]))
            maybeAdd_Join(args, option, value, ",");
        else static if (is(typeof(field) : bool))
            maybeAddFlag(args, option, value);
    }}
}

string[] buildArgsDefault(T)(in T tool)
{
    string[] args;
    toolStartBuildingArguments(tool, args);
    addDefault(args, tool);
    toolEndBuildingArguments(tool, args);
    return args;
}

string[] buildArgs(in Kari kari)
{
    return buildArgsDefault(kari); 
}


struct NuGet
{
}

struct MagicOnion
{
    
    Tool tool;
    alias tool this;

    // Options:
    // -i, -input <String>                            Input path of analyze csproj or directory. (Required)
    string input;
    // -o, -output <String>                           Output path(file) or directory base(in separated mode). (Required)
    string output;
    // -u, -unuseUnityAttr <Boolean>                  Unuse UnityEngine's RuntimeInitializeOnLoadMethodAttribute on MagicOnionInitializer. (Default: False)
    bool unuseUnityAttr;
    // -n, -namespace <String>                        Set namespace root name. (Default: MagicOnion)
    string namespace;
    // -m, -messagePackGeneratedNamespace <String>    Set generated MessagePackFormatter namespace. (Default: MessagePack.Formatters)
    string messagePackGeneratedNamespace;
    // -c, -conditionalSymbol <String>                Conditional compiler symbols, split with ','. (Default: null)
    string[] conditionalSymbols;
}

MagicOnion magicOnion()
{
    MagicOnion result;
    result.toolPath = ["dotnet", "tool", "run", "dotnet-moc"];
    return result;
}

import std.array;


string[] buildArgs(in MagicOnion mo)
{
    return buildArgsDefault(mo); 
}
// string[] buildArgs(in MagicOnion mc)
// {
//     string[] args;
//     toolStartBuildingArguments(mc, args);

//     maybeAdd(args, "-input", mc.input);
//     maybeAdd(args, "-output", mc.output);
//     maybeAddFlag(args, "-unuseUnityAttr", mc.unuseUnityAttr);
//     maybeAdd(args, "-namespace", mc.namespace);
//     maybeAdd(args, "-messagePackGeneratedNamespace", mc.messagePackGeneratedNamespace);
//     maybeAdd_Join(args, "-conditionalSymbol", mc.conditionalSymbols, ",");

//     toolEndBuildingArguments(mc, args);
//     return args;
// }


struct MessagePack
{
    
    Tool tool;
    alias tool this;

    // D:\projects\Zayats>dotnet tool run mpc -- --help
    // Usage: mpc [options...]

    // Options:
    // -i, -input <String>                                Input path to MSBuild project file or the directory containing Unity source files. (Required)
    string input;
    // -o, -output <String>                               Output file path(.cs) or directory (multiple generate file). (Required)
    string output;
    // -c, -conditionalSymbol <String>                    Conditional compiler symbols, split with ','. Ignored if a project file is specified for input. (Default: null)
    string[] conditionalSymbols;
    // -r, -resolverName <String>                         Set resolver name. (Default: GeneratedResolver)
    string resolverName;
    // -n, -namespace <String>                            Set namespace root name. (Default: MessagePack)
    string namespace;
    // -m, -useMapMode                                    Force use map mode serialization. (Optional)
    bool useMapMode;
    // -ms, -multipleIfDirectiveOutputSymbols <String>    Generate #if-- files by symbols, split with ','. (Default: null)
    string[] multipleIfDirectiveOutputSymbols;
    // -ei, -externalIgnoreTypeNames <String[]>           Ignore type names. (Default: null)
    string[] externalIgnoreTypeNames;

    // Commands:
    // help          Display help.
    // version       Display version.
}

MessagePack messagePack()
{
    MessagePack result;
    result.toolPath = ["dotnet", "tool", "run", "dotnet-moc"];
    return result;
}

import std.array;

string[] buildArgs(in MessagePack mp)
{
    return buildArgsDefault(mp);
}