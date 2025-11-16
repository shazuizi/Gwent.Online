using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Gwent.Core;

TcpListener server = new(IPAddress.Any, 9000);
server.Start();

Console.WriteLine("SERVER: Waiting for players...");

TcpClient? player1 = await server.AcceptTcpClientAsync();
Console.WriteLine("Player 1 joined.");

TcpClient? player2 = await server.AcceptTcpClientAsync();
Console.WriteLine("Player 2 joined.");

var stream1 = player1.GetStream();
var stream2 = player2.GetStream();

var state = new GameState();

PlayerState? player1state = state.Player1;
PlayerState? player2state = state.Player2;

async Task Send(NetworkStream s, NetMessage msg)
{
	var json = JsonSerializer.Serialize(msg);
	var bytes = Encoding.UTF8.GetBytes(json);
	await s.WriteAsync(bytes);
}

async Task<NetMessage?> Receive(NetworkStream s)
{
	var buffer = new byte[4096];
	int bytes = await s.ReadAsync(buffer);
	if (bytes == 0) return null;
	var json = Encoding.UTF8.GetString(buffer, 0, bytes);
	return JsonSerializer.Deserialize<NetMessage>(json);
}

async Task WaitForJoins()
{
	var j1 = await Receive(stream1);
	var j2 = await Receive(stream2);

	player1state.PlayerId = "P1";
	player2state.PlayerId = "P2";
	player1state.Name = j1!.Name!;
	player2state.Name = j2!.Name!;
}

await WaitForJoins();
GameLogic.InitGame(state);

async Task Broadcast()
{
	await Send(stream1, new NetMessage { Type = "state", GameState = state });
	await Send(stream2, new NetMessage { Type = "state", GameState = state });
}

await Broadcast();

while (true)
{
	var msgP1 = Receive(stream1);
	var msgP2 = Receive(stream2);

	var completed = await Task.WhenAny(msgP1, msgP2);
	var msg = completed.Result;

	if (msg == null) break;

	if (msg.Type == "playCard")
	{
		PlayerState p = msg.PlayerId == "P1" ? player1state : player2state;
		GameLogic.PlayCard(state, p, msg.CardId!);
	}

	await Broadcast();
}
