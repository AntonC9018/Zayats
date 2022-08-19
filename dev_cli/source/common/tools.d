module common.tools;
import std.conv : to;

struct Tool
{
    string toolPath;
    string workingDirectory;
    string[] moreArguments;
}

struct Curl
{
    Tool tool;
    alias tool this;
    
    string url;

    string httpPostData;
    bool failFast;
    bool help;
    string[] includeHeaders;
    string outputFile;
    string remoteName;
    bool silent;
    string uploadFile;

    string user;
    string password;

    string userAgent;
    bool verbose;

    bool followRedirects;
    //  -d, --data <data>          HTTP POST data
    // -f, --fail                 Fail fast with no output on HTTP errors
    // -h, --help <category>      Get help for commands
    // -i, --include              Include protocol response headers in the output
    // -o, --output <file>        Write to file instead of stdout
    // -O, --remote-name          Write output to a file named as the remote file
    // -s, --silent               Silent mode
    // -T, --upload-file <file>   Transfer local FILE to destination
    // -u, --user <user:password> Server user and password
    // -A, --user-agent <name>    Send User-Agent <name> to server
    // -v, --verbose              Make the operation more talkative
    // -V, --version              Show version number and quit
}

Curl curlTool()
{
    Curl c;
    c.toolPath = "curl";
    c.workingDirectory = ".";
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

void toolStartExecute(in Tool tool, ref string[] args)
{
    assert(tool.toolPath);
    args ~= tool.toolPath;
}

auto toolEndExecute(in Tool tool, ref string[] args)
{
    args ~= tool.moreArguments;

    import common.convenience;
    return execute2(args, tool.workingDirectory);
}

auto execute(in Curl curl)
{
    string[] args;
    toolStartExecute(curl, args);

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

    return toolEndExecute(curl, args);
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
    string[] plugins;
    string cppOut;
    string javaOut;
    string pythonOut;
    string csharpOut;
}

Protoc protocTool()
{
    Protoc p;
    p.toolPath = "protoc";
    p.workingDirectory = ".";
    return p;
}

auto execute(in Protoc protoc)
{
    string[] args;
    toolStartExecute(protoc, args);
    
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
    maybeAdd_Equal(args, "--encode", protoc.encode);
    //   --deterministic_output      When using --encode, ensure map fields are
    //                               deterministically ordered. Note that this order
    //                               is not canonical, and changes across builds or
    //                               releases of protoc.
    maybeAddFlag(args, "--deterministic_output", protoc.deterministicInput);
    //   --decode=MESSAGE_TYPE       Read a binary message of the given type from
    //                               standard input and write it in text format
    //                               to standard output.  The message type must
    //                               be defined in PROTO_FILES or their imports.
    maybeAdd_Equal(args, "--decode", protoc.decode);
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
    maybeAdd_Equal(args, "--descriptor_set_in", protoc.fileDescriptorSetIn);
    //   -oFILE,                     Writes a FileDescriptorSet (a protocol buffer,
    //     --descriptor_set_out=FILE defined in descriptor.proto) containing all of
    //                               the input files to FILE.
    maybeAdd_Equal(args, "--descriptor_set_out", protoc.fileDescriptorSetOut);
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
    maybeAdd_Equal(args, "--dependency_out", protoc.dependencyOut);
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
    maybeAddMany_Equal(args, "--plugin", protoc.plugins);
    //   --cpp_out=OUT_DIR           Generate C++ header and source.
    maybeAdd_Equal(args, "--cpp_out", protoc.cppOut);
    //   --csharp_out=OUT_DIR        Generate C# source file.
    maybeAdd_Equal(args, "--csharp_out", protoc.csharpOut);
    //   --java_out=OUT_DIR          Generate Java source file.
    maybeAdd_Equal(args, "--java_out", protoc.javaOut);
    //   --kotlin_out=OUT_DIR        Generate Kotlin file.
    //   --objc_out=OUT_DIR          Generate Objective-C header and source.
    //   --php_out=OUT_DIR           Generate PHP source file.
    //   --pyi_out=OUT_DIR           Generate python pyi stub.
    //   --python_out=OUT_DIR        Generate Python source file.
    maybeAdd_Equal(args, "--python_out", protoc.pythonOut);
    //   --ruby_out=OUT_DIR          Generate Ruby source file.
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
    
    return toolEndExecute(protoc, args);
}