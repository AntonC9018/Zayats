# Zayats


## Introduction

**Zayats** (russian for rabbit) is a board game.

It's your typical roll your dice, then move that many spaces sort of game with special mechanics:
- the ability to jump over players;
- items, which grant different passive effects or add activated abilities;
- mines that explode and kill you when you step on them;
- other stuff.


### Game architecture

The logic is completely separate from the view, such that the code can be used, unchanged, both in the Unity game and in the game server, without it having any Unity references.
Hence, it has a tiny component storage implementation, an events system, and other barebones stuff necessary for game development.
You can find the most relevant code in [Zayats\Assets\Source\Core\Runtime](Zayats\Assets\Source\Core\Runtime).

I tried to stay away from OOP as much as possible, keeping all state separate from behavior, centralized.
All state of an active game can be found in `GameContext.State`.
The view follows a similar principle.
Interfaces are still used to encompass polymorphic behavior though.


### Project structure

The folders are as follows:

- `3dmodels` contains the sources for the models, a blender file with all of the models dumped in a single scene, and an exported FBX's for each of the model.

- `build_folder` contains build artefacts, cached tools and local configuration.

- `dev_cli` is the `dev` tool, used to do the initial setup for the project and manage devop tasks, like running the code generator, opening the unity editor, running the server, etc.
  Each new command becomes part of the `dev` tool, which can be conveniently invoked from the command line.
  The tool is written in the D programming language and can use its rich type system and the standard libraries.
  > I thought about replacing it with Nuke, but I did not like it very much.

- `kari_stuff` contains my code generator, Kari, as a git submodule, and a bunch of plugin projects for it.
  Think of the plugins as individual source generators.
  > I know Unity now has support for source generators, but my own thing is IMO way easier to use at this point.

- `Server` contains:
  * the GameServer (aka matchmaking and gameplay server, currently unimplemented);
  * projects that import the shared source code from the Unity folder;
  * protobuf source files, which define GRPC functions and the packet types for both the client and the server;
  * a web application for viewing statistics, creating accounts, setting up the profile, and storing all data in the database (currently unimplemented).

- `Zayats` is the Unity game.


### Getting started

Follow the steps below to set up the project:
1. Install a [D compiler](https://dlang.org/download.html)
2. Install [.NET 6](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
3. Install Unity version 2021.3.4f1 from [the archive](https://unity3d.com/get-unity/download/archive)
4. Install [Mono](https://www.mono-project.com/docs/getting-started/install/) if you're not on Windows.
5. Run the setup script in the root of the project.

The `dev` cli tool will be written to `build_folder/dev_cli/dev`, but I recommend you define an alias for this command (this is done automatically on Windows, see [dosmacros.doskey](dosmacros.doskey), which gets automatically applied by the setup script.
Also, I recommend you set up a terminal profile, which gets those commands automatically defined, because the `dev` tool will be used a lot.

After the setup script has run, you should be good to open Unity.
For that, call `dev unity open`.


### The `dev` tool

Most important dev commands:
- `dev setup` runs the setup script;
- `dev kari run` runs the code generator on the unity project;
- `dev unity open` opens the Unity Editor;
- `dev protoc` runs the protoc compiler on the proto files;
- `dev models package` copies the exported 3d models to the unity folder;
- others.

The help messages are janky at the moment, because I didn't bother implementing proper help messages in the [CLI framework](https://github.com/AntonC9018/jcli) that I'm using, while the original author doesn't have time to do them properly, so **the best way to see what arguments the commands take at the moment is to browse the code**.
Any fields marked with attributes are valid arguments to the commands.


I'll add more commands in the future, as I see fit.
Perhaps there will be a build command for the game, a publish command for a git release, etc.


### How to open files in VSCode when double-clicking

*Edit -> Preferences -> External Tools -> Editor Script Editor Args*, change the string to
```
"$(ProjectPath)/.." --reuse-window --goto "$(File)":$(Line):$(Column)
```.