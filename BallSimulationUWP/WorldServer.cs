using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;

namespace BallSimulationUWP
{
    public class WorldServer
    {
        private readonly Simulator _simulator;
        private readonly int _port;
        private readonly TcpListener _listener;
        private readonly List<WorldServerClient> _clients = new List<WorldServerClient>();

        private bool _running = false;

        public WorldServer(Simulator simulator, int port)
        {
            _simulator = simulator;
            _port = port;
            _listener = new TcpListener(IPAddress.Any, port);

            _simulator.OnTickCallback = OnSimulatorTick;
        }

        public async Task Start()
        {
            Stop();
            _running = true;
            _listener.Start();

            while (_running)
            {
                var tcpClient = await _listener.AcceptTcpClientAsync();
                Debug.WriteLine("Client Connected");
                var client = new WorldServerClient(this, tcpClient);
                _clients.Add(client);
                client.Handle();
            }
        }

        public void OnSimulatorTick()
        {
            foreach (var entity in _simulator.World.Entities)
            {
                if (entity.GetUpdateFlag())
                {
                    SendBallUpdate(entity.GetHashCode(), entity);
                }
            }
        }

        public void Stop()
        {
            _listener.Stop();
            _running = false;
        }

        public void HandleCommand(WorldServerClient client, string line)
        {
            Debug.WriteLine($"Client sent '{line}'");

            var parts = line.Split(' ');
            var cmd = parts[0];

            if (cmd == "A")
            {
                var mass = float.Parse(parts[1]);
                var radius = float.Parse(parts[2]);
                var x = float.Parse(parts[3]);
                var y = float.Parse(parts[4]);
                var velocityX = float.Parse(parts[5]);
                var velocityY = float.Parse(parts[6]);

                var ball = new BallEntity(mass, radius, new Vector2(x, y))
                {
                    Velocity = new Vector2(velocityX, velocityY)
                };

                _simulator.AddBall(ball);
                SendBallAdded(ball.GetHashCode(), ball);
            }
            else if (cmd == "S")
            {
                _simulator.World.Scatter();    
            }
            else if (cmd == "T")
            {
                _simulator.Toggle();
            }
            else if (cmd == "Z")
            {
                _simulator.ZeroVelocity();
            }
            else if (cmd == "G")
            {
                World.Gravity = Math.Abs(World.Gravity) < World.Epsilon ? 3.33333f : 0;
            }
            else if (cmd == "C")
            {
                World.EnableCollisions = !World.EnableCollisions;
            }
        }

        public void BroadcastMessage(string msg)
        {
            foreach (var client in _clients)
            {
                client.Send(msg);
            }
        }

        public void SendBallUpdate(int ballId, BallEntity entity)
        {
            BroadcastMessage($"U {ballId} {entity.Position.X} {entity.Position.Y} {entity.Velocity.X} {entity.Velocity.Y}");
        }

        public void SendBallDelete(int ballId)
        {
            BroadcastMessage($"D {ballId}");
        }

        public void SendBallAdded(int ballId, BallEntity entity)
        {
            BroadcastMessage($"A {ballId} {entity.Mass} {entity.Radius} {entity.Position.X} {entity.Position.Y} {entity.Velocity.X} {entity.Velocity.Y}");
        }

        public void SendInitialState(WorldServerClient client)
        {
            foreach (var entity in _simulator.Entities())
            {
                client.Send($"A {entity.GetHashCode()} {entity.Mass} {entity.Radius} {entity.Position.X} {entity.Position.Y} {entity.Velocity.X} {entity.Velocity.Y}");
            }
        }

        public void RemoveClient(WorldServerClient client)
        {
            _clients.Remove(client);
        }
    }

    public class WorldServerClient
    {
        private readonly WorldServer _server;
        private readonly TcpClient _client;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;

        public WorldServerClient(WorldServer server, TcpClient client)
        {
            _server = server;
            _client = client;

            var stream = client.GetStream();
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream)
            {
                AutoFlush = true
            };

            _server.SendInitialState(this);
        }

        public async Task Handle()
        {
            while (_client.Connected)
            {
                var line = await _reader.ReadLineAsync();
                _server.HandleCommand(this, line);
            }
            _server.RemoveClient(this);
        }

        public void Send(string msg)
        {
            if (!msg.EndsWith("\n"))
            {
                msg += "\n";
            }
            _writer.Write(msg.ToCharArray());
        }
    }

    public class WorldClient
    {
        public readonly Action UpdateHandler;
        private readonly TcpClient _client;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;

        public List<BallEntity> Entities = new List<BallEntity>();
        private Dictionary<int, int> _remoteToIndexes = new Dictionary<int, int>();

        public WorldClient(TcpClient client, Action updateHandler)
        {
            UpdateHandler = updateHandler;

            _client = client;

            var stream = _client.GetStream();
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream)
            {
                AutoFlush = true
            };
        }

        public async Task Handle()
        {
            while (_client.Connected)
            {
                var line = await _reader.ReadLineAsync();
                HandleCommand(line);
            }
        }

        public void HandleCommand(string msg)
        {
            var parts = msg.Split(' ');
            var cmd = parts[0];

            if (cmd == "A")
            {
                var remoteId = int.Parse(parts[1]);
                var mass = float.Parse(parts[2]);
                var radius = float.Parse(parts[3]);
                var x = float.Parse(parts[4]);
                var y = float.Parse(parts[5]);
                var velocityX = float.Parse(parts[6]);
                var velocityY = float.Parse(parts[7]);

                var ball = new BallEntity(mass, radius, new Vector2(x, y))
                {
                    Velocity = new Vector2(velocityX, velocityY),
                    RemoteId = remoteId
                };

                Entities.Add(ball);
                _remoteToIndexes[ball.RemoteId] = Entities.Count - 1;

                UpdateHandler();
            }
            else if (cmd == "U")
            {
                var remoteId = int.Parse(parts[1]);
                var x = float.Parse(parts[2]);
                var y = float.Parse(parts[3]);
                var velocityX = float.Parse(parts[4]);
                var velocityY = float.Parse(parts[5]);

                if (!_remoteToIndexes.ContainsKey(remoteId))
                {
                    return;
                }

                var entity = Entities[_remoteToIndexes[remoteId]];
                entity.Position = new Vector2(x, y);
                entity.Velocity = new Vector2(velocityX, velocityY);
                UpdateHandler();
            }
        }

        public void Close()
        {
            _client.Dispose();
        }

        public void SendCommand(string cmd)
        {
            if (!cmd.EndsWith("\n"))
            {
                cmd += "\n";
            }
            _writer.Write(cmd.ToCharArray());
        }
    }
}
