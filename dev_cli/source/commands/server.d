module commands.server;

import jcli;

import commands.context;
import common.tools;
import common;

import std.path;
import std.stdio;
import std.process : wait;

@Command("server", "Server commands.")
struct ServerContext
{
}
