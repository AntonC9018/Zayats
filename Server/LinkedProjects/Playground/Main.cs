using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Zayats.Core;
using Zayats.Serialization;

var game = Initialization.CreateGame(cellCountNotIncludingStartAndFinish: 8, playerCount: 2);
game.State.Components = Components.CreateComponentStorages();
var sb = new StringBuilder();
var sw = new StringWriter(sb);
using var jsonWriter = new JsonTextWriter(sw);
Serialization.SerializeJson(game.State, jsonWriter);

var str = sb.ToString();
Console.WriteLine(str);

var textReader = new StringReader(str);
var jsonReader = new JsonTextReader(textReader);
var g = Serialization.Deserialize(jsonReader);
g.GetComponentStorage(Components.CurrencyId).Add(0).Value = 6;

sb.Clear();
Serialization.SerializeJson(g, jsonWriter);
Console.WriteLine(sb.ToString());
