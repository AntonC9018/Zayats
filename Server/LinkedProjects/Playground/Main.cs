using System;
using Zayats.Core;
using Zayats.Serialization;


var game = Initialization.CreateGame(cellCountNotIncludingStartAndFinish: 8, playerCount: 2);
game.State.Components = Components.CreateComponentStorages();
var s = Serialization.SerializeJson(game.State);
Console.WriteLine(s);
var g = Serialization.Deserialize(s);
Console.WriteLine(g.Components.Storages is null 
    ? "Components null" 
    : (g.Components.Storages[0] is null 
        ? "Zero null"
        : "Good"));
g.GetComponentStorage(Components.CurrencyId).Add(0).Value = 6;
s = Serialization.SerializeJson(g);
Console.WriteLine(s);

struct A
{
    public int a;
    public Events.Storage events;
    public int b { get; set; }
    public int[] c;
}

