using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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
        }

        public async Task SetupClient()
        {
            _client?.Close();

            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(IPAddress.Loopback, 9020);
            _client = new WorldClient(tcpClient, OnTick);
            _client.Handle();
        }

        public void OnTick()
        {
            foreach (var entity in _client.Entities)
            {
                UpdateWorldEntity(entity);
            }
        }

        public void UpdateWorldEntity(BallEntity entity)
        {
            Ellipse ellipse;
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

        public Color GetEntityColor(BallEntity entity)
        {
            var approximateSpeed = Math.Abs(entity.Velocity.X) + Math.Abs(entity.Velocity.Y);

            if (approximateSpeed <= 0)
            {
                return Colors.Blue;
            }
            else if (approximateSpeed <= 2)
            {
                return Colors.Green;
            }
            else
            {
                return Colors.Red;
            }
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
    }
}
