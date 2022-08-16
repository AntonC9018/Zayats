using System;
using Zayats.Core;
using Zayats.Serialization;

var game = Initialization.CreateGame(cellCountNotIncludingStartAndFinish: 8, playerCount: 2);
var s = Serialization.SerializeJson(game.State);

Console.WriteLine(s);