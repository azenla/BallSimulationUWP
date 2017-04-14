using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Numerics;

namespace BallSimulationUWP
{
    public class WorldServer
    {
        private readonly Simulator _simulator;
        private readonly TcpListener _listener;
        private readonly List<WorldServerClient> _clients = new List<WorldServerClient>();

        private readonly Queue<WorldServerClient> _newClients = new Queue<WorldServerClient>();

        private bool _running;

        public WorldServer(Simulator simulator, int port)
        {
            _simulator = simulator;
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
                _newClients.Enqueue(client);
                client.Handle();
            }
        }

        public void OnSimulatorTick()
        {
            while (_newClients.Count > 0)
            {
                var client = _newClients.Dequeue();
                SendInitialState(client);
            }

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
                World.Gravity = Math.Abs(World.Gravity) > World.Epsilon ? 0.0f : World.DefaultGravity;
            }
            else if (cmd == "C")
            {
                World.EnableCollisions = !World.EnableCollisions;
            }
            else if (cmd == "E")
            {
                World.Restitution = Math.Abs(World.Restitution) < World.Epsilon ? 0.85f : 0.0f;
            }
            else if (cmd == "D")
            {
                var id = int.Parse(parts[1]);
                foreach (var entity in _simulator.Entities.Where(entity => entity.GetHashCode() == id).ToList())
                {
                    _simulator.RemoveBall(entity);
                    SendBallDelete(id);
                }
            }
        }

        public void BroadcastMessage(string msg)
        {
            var clients = new List<WorldServerClient>(_clients);
            foreach (var client in clients)
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
            foreach (var entity in _simulator.Entities)
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

        private bool _shouldHandle = true;

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
        }

        public async Task Handle()
        {
            try
            {
                while (_client.Connected && _shouldHandle)
                {
                    var line = await _reader.ReadLineAsync();

                    if (line.Length == 0)
                    {
                        break;
                    }

                    try
                    {
                        _server.HandleCommand(this, line);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Server failed to handle message '{line}': {e}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Server failed to handle client: {e}");
            }
            finally
            {
                _shouldHandle = false;
                _server.RemoveClient(this);
            }
        }

        public void Send(string msg)
        {
            if (!msg.EndsWith("\n"))
            {
                msg += "\n";
            }

            try
            {
                _writer.Write(msg.ToCharArray());
            }
            catch
            {
                Debug.WriteLine("Failed to send a message to a client. Considering it disconnected.");
                _shouldHandle = false;
                _server.RemoveClient(this);
            }
        }
    }

    public class WorldClient
    {
        public readonly Action<BallEntity> UpdateHandler;
        private readonly TcpClient _client;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly Dictionary<int, BallEntity> _entities = new Dictionary<int, BallEntity>();

        private bool _shouldHandle;

        public WorldClient(TcpClient client, Action<BallEntity> updateHandler)
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
            _shouldHandle = true;
            while (_client.Connected && _shouldHandle)
            {
                var line = await _reader.ReadLineAsync();
                HandleCommand(line);
            }
        }

        public bool DoesEntityExist(int remoteId)
        {
            return _entities.ContainsKey(remoteId);
        }

        public void HandleCommand(string msg)
        {
            if (msg.Length == 0)
            {
                return;
            }

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

                var entity = new BallEntity(mass, radius, new Vector2(x, y))
                {
                    Velocity = new Vector2(velocityX, velocityY),
                    RemoteId = remoteId
                };

                _entities[remoteId] = entity;

                UpdateHandler(entity);
            }
            else if (cmd == "U")
            {
                var remoteId = int.Parse(parts[1]);
                var x = float.Parse(parts[2]);
                var y = float.Parse(parts[3]);
                var velocityX = float.Parse(parts[4]);
                var velocityY = float.Parse(parts[5]);

                if (!_entities.ContainsKey(remoteId))
                {
                    Debug.WriteLine($"Server tried to update entity with ID {remoteId}, but the client never knew this entity existed.");
                    return;
                }

                var entity = _entities[remoteId];
                entity.Position = new Vector2(x, y);
                entity.Velocity = new Vector2(velocityX, velocityY);
                UpdateHandler(entity);
            }
            else if (cmd == "D")
            {
                var remoteId = int.Parse(parts[1]);

                if (!_entities.ContainsKey(remoteId)) return;
                var entity = _entities[remoteId];
                _entities.Remove(remoteId);
                UpdateHandler(entity);
            }
        }

        public void Close()
        {
            try
            {
                _shouldHandle = false;
                _client.Client.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        public void SendCommand(string cmd)
        {
            if (!cmd.EndsWith("\n"))
            {
                cmd += "\n";
            }

            try
            {
                _writer.Write(cmd.ToCharArray());
            }
            catch
            {
                Close();
            }
        }
    }
}
