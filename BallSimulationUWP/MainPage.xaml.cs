using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace BallSimulationUWP
{
    public sealed partial class MainPage
    {
        private WorldClient _client;
        private readonly Dictionary<BallEntity, Ellipse> _entityShapes;

        public MainPage()
        {
            _entityShapes = new Dictionary<BallEntity, Ellipse>();

            InitializeComponent();

            SetupClient(IPAddress.Loopback);

            PointerPressed += (sender, args) =>
            {
                var point = args.GetCurrentPoint(MainCanvas);
                var position = ScaleClientToServerPosition(point.Position.ToVector2());
                AddBallAtPosition(position.X, position.Y);
            };

            SizeChanged += (sender, args) =>
            {
                RefreshAllEntities();
            };
        }

        public void AddBallAtPosition(float x, float y)
        {
            var mass = new Random().NextDouble();
            _client.SendCommand($"A {mass} 20.0 {x} {y} 20.0 20.0");
        }

        public async Task SetupClient(IPAddress address)
        {
            try
            {
                _client?.Close();
                ClearEntities();

                var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(address, 9020);
                Debug.WriteLine("Client is now connected to the server.");
                _client = new WorldClient(tcpClient, entity => UpdateWorldEntity(entity));
                await _client.Handle();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to connect to server: {e}");
            }
        }

        public void UpdateWorldEntity(BallEntity entity, bool forceUpdate = false)
        {
            Ellipse ellipse;

            if (!_client.DoesEntityExist(entity.RemoteId))
            {
                if (!_entityShapes.ContainsKey(entity)) return;
                ellipse = _entityShapes[entity];
                MainCanvas.Children.Remove(ellipse);
            }

            if (_entityShapes.ContainsKey(entity))
            {
                ellipse = _entityShapes[entity];
            }
            else
            {
                ellipse = new Ellipse();
                MainCanvas.Children.Add(ellipse);
                ellipse.Fill = new SolidColorBrush(Colors.CornflowerBlue);
                ellipse.Stroke = new SolidColorBrush(Colors.Black);
                ellipse.StrokeThickness = 2.0;
                ellipse.DataContext = entity;

                ellipse.AddHandler(PointerPressedEvent, new PointerEventHandler(HandleEntityPointerClick), true);

                _entityShapes[entity] = ellipse;

                forceUpdate = true;
            }

            if (forceUpdate)
            {
                var size = ScaleServerToClientPosition(new Vector2(entity.Radius * 2, entity.Radius * 2));
                ellipse.Width = size.X;
                ellipse.Height = size.Y;
            }

            var position = ScaleServerToClientPosition(entity.Position);
            Canvas.SetLeft(ellipse, position.X);
            Canvas.SetTop(ellipse, position.Y);

            ellipse.Fill = new SolidColorBrush(GetEntityColor(entity));
        }

        public void RefreshAllEntities()
        {
            foreach (var entity in _entityShapes.Keys)
            {
                UpdateWorldEntity(entity, true);
            }
        }

        public void HandleEntityPointerClick(object sender, PointerRoutedEventArgs args)
        {
            var ellipse = (Ellipse) sender;
            var entity = (BallEntity) ellipse.DataContext;
            var point = args.GetCurrentPoint(MainCanvas);

            if (point.Properties.IsRightButtonPressed)
            {
                _client.SendCommand($"D {entity.RemoteId}");
            }
        }

        public Color GetEntityColor(BallEntity entity)
        {
            var approximateSpeed = entity.Velocity.Length();

            if (approximateSpeed <= World.Epsilon)
            {
                return Colors.Blue;
            }

            return approximateSpeed <= 50.0 ? Colors.Green : Colors.Red;
        }

        public Vector2 ScaleServerToClientPosition(Vector2 position)
        {
            var scaleX = MainCanvas.ActualWidth / World.DefaultWidth; // TODO: Have server and client negotiate at connection start, to get world information
            var scaleY = MainCanvas.ActualHeight / World.DefaultHeight;

            return new Vector2((float) (position.X * scaleX), (float) (position.Y * scaleY));
        }

        public Vector2 ScaleClientToServerPosition(Vector2 position)
        {
            var scaleX = World.DefaultWidth / MainCanvas.ActualWidth; // TODO: Have server and client negotiate at connection start, to get world information
            var scaleY = World.DefaultHeight / MainCanvas.ActualHeight;

            return new Vector2((float) (position.X * scaleX), (float) (position.Y * scaleY));
        }

        private void ScatterButton_Click(object sender, RoutedEventArgs e)
        {
            _client.SendCommand("S");
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _client.SendCommand("T");
        }

        private void ToggleGravityButton_Click(object sender, RoutedEventArgs e)
        {
            _client.SendCommand("G");
        }

        private void ZeroVelocityButton_OnClick(object sender, RoutedEventArgs e)
        {
            _client.SendCommand("Z");
        }

        private void ToggleCollisions_OnClick(object sender, RoutedEventArgs e)
        {
            _client.SendCommand("C");
        }

        private void ToggleElasticity_OnClick(object sender, RoutedEventArgs e)
        {
            _client.SendCommand("E");
        }

        private void ClearEntities()
        {
            foreach (var shape in _entityShapes.Values)
            {
                MainCanvas.Children.Remove(shape);
            }

            _entityShapes.Clear();
        }

        private void ClientConnect_OnClick(object sender, RoutedEventArgs e)
        {
            HandleClientConnect();
        }

        private async void HandleClientConnect()
        {
            var ip = await ShowAddressDialog();

            if (ip.Length == 0) return;
            var addresses = await Dns.GetHostAddressesAsync(ip);
            if (addresses.Length == 0)
            {
                Debug.WriteLine($"Failed to resolve host {ip}... falling back to local server.");
                addresses = new[] { IPAddress.Loopback };
            }
            var address = addresses[0];
            Debug.WriteLine($"Connecting to {address}");
            SetupClient(address);
        }

        private static async Task<string> ShowAddressDialog()
        {
            var box = new TextBox
            {
                AcceptsReturn = false,
                Height = 32
            };

            var dialog = new ContentDialog
            {
                Content = box,
                Title = "Simulation Server Address",
                IsSecondaryButtonEnabled = true,
                PrimaryButtonText = "Connect",
                SecondaryButtonText = "Cancel"
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? box.Text : "";
        }
    }
}
