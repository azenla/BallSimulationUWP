using System;
using System.Collections.Generic;
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
    public sealed partial class MainPage : Page
    {
        private WorldClient _client;
        private readonly Dictionary<BallEntity, Ellipse> _entityShapes;

        public MainPage()
        {
            _entityShapes = new Dictionary<BallEntity, Ellipse>();

            InitializeComponent();

            SetupClient();

            PointerPressed += (sender, args) =>
            {
                var point = args.GetCurrentPoint(this);
                AddBallAtPosition((float) point.Position.X, (float) point.Position.Y);
            };
        }

        public void AddBallAtPosition(float x, float y)
        {
            _client.SendCommand($"A 0.1 20.0 {x} {y} 1.0 1.0");
        }

        public async Task SetupClient()
        {
            _client?.Close();

            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(IPAddress.Loopback, 9020);
            _client = new WorldClient(tcpClient, UpdateWorldEntity);
            await _client.Handle();
        }

        public void UpdateWorldEntity(BallEntity entity)
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
                ellipse.Width = ellipse.Height = entity.Radius * 2;
                ellipse.DataContext = entity;

                ellipse.AddHandler(PointerPressedEvent, new PointerEventHandler(HandleEntityPointerClick), true);

                _entityShapes[entity] = ellipse;
            }

            ellipse.Margin = new Thickness(
                entity.Position.X,
                entity.Position.Y,
                ellipse.Margin.Right,
                ellipse.Margin.Bottom
            );

            ellipse.Fill = new SolidColorBrush(GetEntityColor(entity));
        }

        public void HandleEntityPointerClick(object sender, PointerRoutedEventArgs args)
        {
            var ellipse = (Ellipse) sender;
            var entity = (BallEntity) ellipse.DataContext;
            var point = args.GetCurrentPoint(this);

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
    }
}
